using System;
using Database=ScumMiniMap.LocalSqliteDatabase;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ScumMiniMap {
    internal sealed class SpawnSavedKit {
        internal long Id { get; private set; }
        internal string Name { get; private set; }
        internal int LineCount { get; private set; }
        internal int TotalQuantity { get; private set; }
        internal DateTime UpdatedUtc { get; private set; }
        internal SpawnSavedKit(long id,string name,int lines,int quantity,DateTime updated) {
            Id=id;Name=name;LineCount=lines;TotalQuantity=quantity;UpdatedUtc=updated;
        }
        public override string ToString() { return Name; }
    }

    // Windows 10 and later provide SQLite in System32. No separate runtime or writable DLL is loaded.
    // Prepared statements copy bound UTF-8 values (SQLITE_TRANSIENT); every replacement is transactional.
    // Native contracts: https://www.sqlite.org/c3ref/open.html and https://www.sqlite.org/c3ref/bind_blob.html
    internal sealed class SpawnKitDatabase {
        readonly SpawnItemCatalog catalog;
        internal string DatabasePath { get; private set; }
        internal SpawnKitDatabase(string path,SpawnItemCatalog catalog) {
            if(catalog==null)throw new ArgumentNullException("catalog");
            if(string.IsNullOrWhiteSpace(path))throw new ArgumentException("A database path is required.","path");
            this.catalog=catalog;DatabasePath=Path.GetFullPath(path);
        }
        static string KitName(string name) {
            string normalized;
            try { normalized=(name??"").Trim().Normalize(NormalizationForm.FormC); }
            catch(ArgumentException) { throw new FormatException(Localization.Get("SpawnCartKitNameInvalid")); }
            if(normalized.Length==0 || normalized.Length>80)throw new FormatException(Localization.Get("SpawnCartKitNameInvalid"));
            foreach(char character in normalized)if(char.IsControl(character))throw new FormatException(Localization.Get("SpawnCartKitNameInvalid"));
            return normalized;
        }
        Database Open() {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath));
            return new Database(DatabasePath,database=> {
                    long version=database.ScalarLong("PRAGMA user_version");
                    if(version!=0 && version!=1)throw new IOException(Localization.Get("SpawnCartDatabaseVersion"));
                    if(version==0)database.Transaction(()=> {
                        database.Execute("CREATE TABLE IF NOT EXISTS spawn_meta(key TEXT PRIMARY KEY,value TEXT NOT NULL)");
                        database.Execute("CREATE TABLE IF NOT EXISTS spawn_cart_entries(position INTEGER PRIMARY KEY CHECK(position>=0 AND position<100),item_id TEXT NOT NULL COLLATE NOCASE UNIQUE,quantity INTEGER NOT NULL CHECK(quantity>=1 AND quantity<=100),modifiers TEXT NOT NULL)");
                        database.Execute("CREATE TABLE IF NOT EXISTS spawn_kits(id INTEGER PRIMARY KEY,name TEXT NOT NULL,name_key TEXT NOT NULL UNIQUE,updated_utc TEXT NOT NULL)");
                        database.Execute("CREATE TABLE IF NOT EXISTS spawn_kit_entries(kit_id INTEGER NOT NULL REFERENCES spawn_kits(id) ON DELETE CASCADE,position INTEGER NOT NULL CHECK(position>=0 AND position<100),item_id TEXT NOT NULL COLLATE NOCASE,quantity INTEGER NOT NULL CHECK(quantity>=1 AND quantity<=100),modifiers TEXT NOT NULL,PRIMARY KEY(kit_id,position),UNIQUE(kit_id,item_id))");
                        database.Execute("PRAGMA user_version=1");
                    });
            });
        }
        static SpawnCart ValidatedCart(SpawnItemCatalog catalog,SpawnCart source) {
            if(source==null)throw new ArgumentNullException("cart");
            var copy=new SpawnCart(catalog);copy.ReplaceWith(source);return copy;
        }
        internal void LoadCart(SpawnCart cart,string legacyCartPath) {
            if(cart==null)throw new ArgumentNullException("cart");
            SpawnCart loaded=null;
            using(var database=Open()) {
                database.Transaction(()=> {
                    if(database.ScalarLong("SELECT COUNT(*) FROM spawn_meta WHERE key='cart_initialized'")==0) {
                        var migrated=new SpawnCart(catalog);
                        if(!string.IsNullOrEmpty(legacyCartPath) && File.Exists(legacyCartPath))migrated.Load(legacyCartPath);
                        WriteCart(database,migrated);
                    }
                    loaded=ReadCart(database,"SELECT position,item_id,quantity,modifiers FROM spawn_cart_entries ORDER BY position",null);
                });
            }
            cart.ReplaceWith(loaded);
        }
        internal void SaveCart(SpawnCart cart) {
            var validated=ValidatedCart(catalog,cart);
            using(var database=Open())database.Transaction(()=>WriteCart(database,validated));
        }
        static void WriteCart(Database database,SpawnCart cart) {
            database.Execute("DELETE FROM spawn_cart_entries");
            int position=0;
            foreach(var entry in cart.Entries)database.Execute("INSERT INTO spawn_cart_entries(position,item_id,quantity,modifiers) VALUES(?,?,?,?)",
                position++,entry.Item.Id,entry.Quantity,entry.ModifiersText);
            database.Execute("INSERT OR REPLACE INTO spawn_meta(key,value) VALUES('cart_initialized','1')");
        }
        SpawnCart ReadCart(Database database,string sql,long? id) {
            var loaded=new SpawnCart(catalog);var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);int position=0;
            using(var statement=database.Prepare(sql,id.HasValue?new object[]{id.Value}:new object[0])) {
                while(statement.Read()) {
                    long row=statement.Long(0),quantity=statement.Long(2);string item=statement.Text(1),modifiers=statement.Text(3);
                    if(row!=position++ || position>SpawnCart.MaximumLines || quantity<1 || quantity>SpawnCart.MaximumQuantity || !seen.Add(item))
                        throw new FormatException(Localization.Get("SpawnCartInvalid"));
                    loaded.Add(item,(int)quantity);loaded.SetModifiers(item,modifiers);
                }
            }
            return loaded;
        }
        internal IList<SpawnSavedKit> ListKits() {
            var result=new List<SpawnSavedKit>();
            using(var database=Open())using(var statement=database.Prepare(
                "SELECT k.id,k.name,k.updated_utc,COUNT(i.position),COALESCE(SUM(i.quantity),0) FROM spawn_kits k LEFT JOIN spawn_kit_entries i ON i.kit_id=k.id GROUP BY k.id ORDER BY k.name_key,k.id")) {
                while(statement.Read()) {
                    DateTime updated;
                    if(!DateTime.TryParseExact(statement.Text(2),"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out updated))
                        throw new FormatException(Localization.Get("SpawnCartInvalid"));
                    long lines=statement.Long(3),quantity=statement.Long(4);
                    if(lines<0 || lines>SpawnCart.MaximumLines || quantity<0 || quantity>SpawnCart.MaximumTotal)
                        throw new FormatException(Localization.Get("SpawnCartInvalid"));
                    result.Add(new SpawnSavedKit(statement.Long(0),statement.Text(1),(int)lines,(int)quantity,updated));
                }
            }
            return result.AsReadOnly();
        }
        internal long SaveKit(string name,SpawnCart cart,bool overwrite=false) {
            name=KitName(name);string key=name.ToUpperInvariant();var validated=ValidatedCart(catalog,cart);
            if(validated.Entries.Count==0)throw new FormatException(Localization.Get("SpawnCartEmpty"));
            long id=0;
            using(var database=Open())database.Transaction(()=> {
                id=database.ScalarLong("SELECT COALESCE((SELECT id FROM spawn_kits WHERE name_key=?),0)",key);
                if(id!=0 && !overwrite)throw new FormatException(Localization.T("SpawnCartKitExists",name));
                string updated=DateTime.UtcNow.ToString("o",CultureInfo.InvariantCulture);
                if(id==0) {
                    database.Execute("INSERT INTO spawn_kits(name,name_key,updated_utc) VALUES(?,?,?)",name,key,updated);
                    id=database.ScalarLong("SELECT last_insert_rowid()");
                } else {
                    database.Execute("UPDATE spawn_kits SET name=?,updated_utc=? WHERE id=?",name,updated,id);
                    database.Execute("DELETE FROM spawn_kit_entries WHERE kit_id=?",id);
                }
                int position=0;
                foreach(var entry in validated.Entries)database.Execute("INSERT INTO spawn_kit_entries(kit_id,position,item_id,quantity,modifiers) VALUES(?,?,?,?,?)",
                    id,position++,entry.Item.Id,entry.Quantity,entry.ModifiersText);
            });
            return id;
        }
        internal void LoadKit(long id,SpawnCart cart) {
            if(cart==null)throw new ArgumentNullException("cart");
            SpawnCart loaded=null;
            using(var database=Open())database.Transaction(()=> {
                if(id<=0 || database.ScalarLong("SELECT COUNT(*) FROM spawn_kits WHERE id=?",id)!=1)
                    throw new FormatException(Localization.Get("SpawnCartKitMissing"));
                loaded=ReadCart(database,"SELECT position,item_id,quantity,modifiers FROM spawn_kit_entries WHERE kit_id=? ORDER BY position",id);
                if(loaded.Entries.Count==0)throw new FormatException(Localization.Get("SpawnCartInvalid"));
            });
            cart.ReplaceWith(loaded);
        }
        internal long ImportKit(string name,string legacyPath,bool overwrite=false) {
            var imported=new SpawnCart(catalog);imported.Load(legacyPath);return SaveKit(name,imported,overwrite);
        }

    }
}
