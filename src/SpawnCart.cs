using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace ScumMiniMap {
    internal sealed class SpawnCartEntry {
        internal readonly SpawnItem Item;
        internal int Quantity { get; private set; }
        internal string ModifiersText { get; private set; }
        internal SpawnCartEntry(SpawnItem item,int quantity,string modifiers="") {
            if(item==null)throw new ArgumentNullException("item");
            Item=item;string normalized=SpawnModifiers.Normalize(modifiers);ValidateCommand(quantity,normalized);Quantity=quantity;ModifiersText=normalized;
        }
        internal void SetQuantity(int quantity) { ValidateCommand(quantity,ModifiersText);Quantity=quantity; }
        internal void SetModifiers(string modifiers) {
            string normalized=SpawnModifiers.Normalize(modifiers);ValidateCommand(Quantity,normalized);ModifiersText=normalized;
        }
        void ValidateCommand(int quantity,string modifiers) {
            if(quantity<1 || quantity>SpawnCart.MaximumQuantity)throw new FormatException(Localization.Get("SpawnCartInvalid"));
            if(BuildCommand(quantity,modifiers).Length>512)throw new FormatException(Localization.Get("SpawnCartModifierLength"));
        }
        string BuildCommand(int quantity,string modifiers) {
            return "#SpawnItem "+Item.Id+" "+quantity.ToString(CultureInfo.InvariantCulture)+(modifiers.Length==0?"":" "+modifiers);
        }
        internal string Command { get { return BuildCommand(Quantity,ModifiersText); } }
    }

    internal sealed class SpawnCart {
        internal const int MaximumLines=100,MaximumQuantity=100,MaximumTotal=1000;
        readonly SpawnItemCatalog catalog;
        readonly List<SpawnCartEntry> entries=new List<SpawnCartEntry>();
        internal IList<SpawnCartEntry> Entries { get { return entries.AsReadOnly(); } }
        internal int TotalQuantity { get { int total=0;foreach(var entry in entries)total+=entry.Quantity;return total; } }
        internal SpawnCart(SpawnItemCatalog catalog) {
            if(catalog==null)throw new ArgumentNullException("catalog");
            this.catalog=catalog;
        }
        SpawnCartEntry Find(string id) {
            return entries.Find(entry=>string.Equals(entry.Item.Id,id,StringComparison.OrdinalIgnoreCase));
        }
        static void ValidateQuantity(int quantity) {
            if(quantity<1 || quantity>MaximumQuantity)throw new FormatException(Localization.Get("SpawnCartInvalid"));
        }
        internal void Add(string id,int quantity) {
            AddMany(new[]{id},quantity);
        }
        // Validate the entire selection first so a failed multi-select never leaves a partial cart.
        internal void AddMany(IEnumerable<string> ids,int quantity) {
            ValidateQuantity(quantity);
            var copy=new SpawnCart(catalog);
            foreach(var entry in entries)copy.entries.Add(new SpawnCartEntry(entry.Item,entry.Quantity,entry.ModifiersText));
            foreach(string id in ids) {
                var item=catalog.Find(id);
                if(item==null)throw new FormatException(Localization.Get("SpawnCartInvalid"));
                var existing=copy.Find(item.Id);
                long next=(long)quantity+(existing==null?0:existing.Quantity);
                if(next>MaximumQuantity || copy.TotalQuantity+quantity>MaximumTotal
                    || (existing==null && copy.entries.Count>=MaximumLines))throw new FormatException(Localization.Get("SpawnCartLimitExceeded"));
                if(existing==null)copy.entries.Add(new SpawnCartEntry(item,quantity));else existing.SetQuantity((int)next);
            }
            entries.Clear();entries.AddRange(copy.entries);
        }
        internal void SetQuantity(string id,int quantity) {
            ValidateQuantity(quantity);
            var entry=Find(id);
            if(entry==null)throw new FormatException(Localization.Get("SpawnCartInvalid"));
            if(TotalQuantity-entry.Quantity+quantity>MaximumTotal)throw new FormatException(Localization.Get("SpawnCartLimitExceeded"));
            entry.SetQuantity(quantity);
        }
        internal void SetModifiers(string id,string modifiers) {
            var entry=Find(id);
            if(entry==null)throw new FormatException(Localization.Get("SpawnCartInvalid"));
            entry.SetModifiers(modifiers);
        }
        // Apply only touched properties. Build every replacement before committing any cart change.
        internal void PatchModifiers(IEnumerable<string> ids,IDictionary<string,string> changes) {
            if(ids==null)throw new ArgumentNullException("ids");
            if(changes==null)throw new ArgumentNullException("changes");
            var patch=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            foreach(var change in changes) {
                string property=null;
                foreach(string known in SpawnModifiers.PropertyNames)
                    if(string.Equals(known,change.Key,StringComparison.OrdinalIgnoreCase)) { property=known;break; }
                if(property==null)throw new FormatException(Localization.T("SpawnCartModifierUnknown",change.Key??""));
                if(patch.ContainsKey(property))throw new FormatException(Localization.T("SpawnCartModifierDuplicate",property));
                if(change.Value==null) { patch.Add(property,null);continue; }
                var parsed=SpawnModifiers.Parse(property+" "+change.Value);
                if(parsed.Count!=1 || !parsed.ContainsKey(property))throw new FormatException(Localization.Get("SpawnCartModifierPairs"));
                patch.Add(property,parsed[property]);
            }
            var selected=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string id in ids) {
                var entry=Find(id);
                if(entry==null)throw new FormatException(Localization.Get("SpawnCartInvalid"));
                selected.Add(entry.Item.Id);
            }
            if(selected.Count==0 || patch.Count==0)return;
            var replacements=new List<SpawnCartEntry>();
            foreach(var entry in entries) {
                if(!selected.Contains(entry.Item.Id)) { replacements.Add(entry);continue; }
                var values=new Dictionary<string,string>(SpawnModifiers.Parse(entry.ModifiersText),StringComparer.OrdinalIgnoreCase);
                foreach(var change in patch) {
                    if(change.Value==null)values.Remove(change.Key);
                    else values[change.Key]=change.Value;
                }
                var tokens=new List<string>();
                foreach(string property in SpawnModifiers.PropertyNames) {
                    string value;
                    if(values.TryGetValue(property,out value)) { tokens.Add(property);tokens.Add(value); }
                }
                replacements.Add(new SpawnCartEntry(entry.Item,entry.Quantity,string.Join(" ",tokens)));
            }
            entries.Clear();entries.AddRange(replacements);
        }
        internal void Remove(string id) { entries.RemoveAll(entry=>string.Equals(entry.Item.Id,id,StringComparison.OrdinalIgnoreCase)); }
        internal void Clear() { entries.Clear(); }
        internal void ReplaceWith(SpawnCart source) {
            if(source==null)throw new ArgumentNullException("source");
            // Resolve through this cart's catalogue and validate every row before changing it.
            var replacement=new SpawnCart(catalog);
            foreach(var entry in source.Entries) {
                replacement.Add(entry.Item.Id,entry.Quantity);
                replacement.SetModifiers(entry.Item.Id,entry.ModifiersText);
            }
            entries.Clear();entries.AddRange(replacement.entries);
        }
        internal List<string> BuildCommands() {
            var result=new List<string>();foreach(var entry in entries)result.Add(entry.Command);return result;
        }
        // Called at Enter-down, including if Escape cleanup subsequently fails.
        internal void ConsumeSubmitted(int count) {
            if(count<0 || count>entries.Count)throw new ArgumentOutOfRangeException("count");
            entries.RemoveRange(0,count);
        }
        internal void Save(string path) {
            string fullPath=Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string temporary=fullPath+"."+Guid.NewGuid().ToString("N")+".tmp";
            try {
                var lines=new List<string> { "# SCUM MiniMap spawn kit v2", "# Item IDs: https://scum-db.com/", "# id\tquantity\tmodifiers" };
                foreach(var entry in entries)lines.Add(entry.Item.Id+"\t"+entry.Quantity.ToString(CultureInfo.InvariantCulture)+"\t"+entry.ModifiersText);
                File.WriteAllLines(temporary,lines,new UTF8Encoding(false));
                if(File.Exists(fullPath))File.Replace(temporary,fullPath,null);else File.Move(temporary,fullPath);
            } finally { if(File.Exists(temporary))File.Delete(temporary); }
        }
        internal void Load(string path) {
            if(new FileInfo(path).Length>65536)throw new FormatException(Localization.Get("SpawnCartInvalid"));
            var loaded=new SpawnCart(catalog);
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string line in File.ReadAllLines(path,Encoding.UTF8)) {
                if(line.Length==0 || line.StartsWith("#",StringComparison.Ordinal))continue;
                string[] parts=line.Split('\t');int quantity;
                if((parts.Length!=2 && parts.Length!=3) || !seen.Add(parts[0]) || !int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out quantity))
                    throw new FormatException(Localization.Get("SpawnCartInvalid"));
                loaded.Add(parts[0],quantity);
                if(parts.Length==3)loaded.SetModifiers(parts[0],parts[2]);
            }
            entries.Clear();entries.AddRange(loaded.entries);
        }
    }
}
