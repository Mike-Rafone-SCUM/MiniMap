using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace ScumMiniMap {
    internal sealed class SpawnItem {
        internal string Id { get; private set; }
        internal string Name { get; private set; }
        internal string DisplayName { get { return Localization.GetSpawnItemName(Id,Name); } }
        internal string Category { get; private set; }
        internal string SourceUrl { get; private set; }
        internal string Subcategory { get; private set; }

        internal SpawnItem(string id,string name,string category,string sourceUrl,string subcategory="") {
            if(!SpawnItemCatalog.IsValidId(id) || !ValidText(name,256) || !ValidText(category,80) ||
                sourceUrl!="https://scum-db.com/items/"+id || !ValidSubcategory(subcategory))
                throw new FormatException("Invalid spawn item record.");
            Id=id;Name=name;Category=category;SourceUrl=sourceUrl;Subcategory=subcategory;
        }

        static bool ValidSubcategory(string value) {
            if(value==null || value.Length>120 || value.Trim()!=value)return false;
            foreach(char c in value)if(char.IsControl(c))return false;
            return true;
        }

        static bool ValidText(string value,int maximum) {
            if(string.IsNullOrWhiteSpace(value) || value.Length>maximum || value.Trim()!=value)return false;
            foreach(char c in value)if(char.IsControl(c))return false;
            return true;
        }
    }

    internal sealed class SpawnItemSearchResult {
        internal SpawnItem Item { get; private set; }
        internal bool IsRelated { get; private set; }
        internal bool RequiresAdapter { get; private set; }
        internal IList<SpawnItem> RelatedWeapons { get; private set; }
        internal SpawnItemSearchResult(SpawnItem item,bool related,bool requiresAdapter,List<SpawnItem> weapons) {
            Item=item;IsRelated=related;RequiresAdapter=requiresAdapter;RelatedWeapons=weapons.AsReadOnly();
        }
    }

    // The catalogue is embedded in the executable. Opening the selector never contacts a website.
    internal sealed class SpawnItemCatalog {
        readonly Dictionary<string,SpawnItem> byId;
        readonly Dictionary<string,string[]> searchText=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string,IList<Compatibility>> compatibility=new Dictionary<string,IList<Compatibility>>(StringComparer.OrdinalIgnoreCase);
        sealed class Compatibility {
            internal SpawnItem Item;
            internal string Relation;
        }
        internal IList<SpawnItem> Items { get; private set; }
        internal IList<string> Categories { get; private set; }
        internal int CompatibilityCount { get; private set; }

        SpawnItemCatalog(List<SpawnItem> items) {
            byId=new Dictionary<string,SpawnItem>(StringComparer.OrdinalIgnoreCase);
            var categories=new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in items) {
                if(byId.ContainsKey(item.Id))throw new FormatException("Duplicate spawn item ID: "+item.Id);
                byId.Add(item.Id,item);categories.Add(item.Category);
                searchText.Add(item.Id,new[]{Compact(item.Id),Compact(item.Name),Compact(item.Category),Compact(item.Subcategory)});
            }
            items.Sort((left,right)=> {
                int order=StringComparer.OrdinalIgnoreCase.Compare(left.Name,right.Name);
                return order!=0?order:StringComparer.Ordinal.Compare(left.Id,right.Id);
            });
            Items=items.AsReadOnly();Categories=new List<string>(categories).AsReadOnly();
        }

        internal static bool IsValidId(string id) {
            if(string.IsNullOrEmpty(id) || id.Length>128)return false;
            for(int i=0;i<id.Length;i++) {
                char c=id[i];
                if((c>='a' && c<='z') || (c>='A' && c<='Z') || (c>='0' && c<='9') || c=='_' || (c=='-' && i>0))continue;
                return false;
            }
            return true;
        }

        internal static SpawnItemCatalog LoadBundled() {
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("spawn-items.tsv")) {
                if(stream==null)throw new InvalidOperationException("The bundled spawn item catalogue is missing.");
                SpawnItemCatalog catalog;
                using(var reader=new StreamReader(stream,new UTF8Encoding(false,true)))catalog=Load(reader);
                using(Stream links=Assembly.GetExecutingAssembly().GetManifestResourceStream("spawn-compatibility.tsv")) {
                    if(links==null)throw new InvalidOperationException("The bundled weapon compatibility catalogue is missing.");
                    using(var reader=new StreamReader(links,new UTF8Encoding(false,true)))catalog.LoadCompatibility(reader);
                }
                return catalog;
            }
        }

        internal static SpawnItemCatalog Load(TextReader reader) {
            if(reader==null)throw new ArgumentNullException("reader");
            var items=new List<SpawnItem>();string line;int number=0;
            while((line=reader.ReadLine())!=null) {
                number++;
                if(line.Length==0 || line.StartsWith("#",StringComparison.Ordinal))continue;
                string[] fields=line.Split('\t');
                if(fields.Length!=4 && fields.Length!=5)throw new FormatException("Invalid spawn item catalogue line "+number+".");
                try { items.Add(new SpawnItem(fields[0],fields[1],fields[2],fields[3],fields.Length==5?fields[4]:"")); }
                catch(FormatException error) { throw new FormatException("Invalid spawn item catalogue line "+number+".",error); }
            }
            if(items.Count==0)throw new FormatException("The spawn item catalogue is empty.");
            return new SpawnItemCatalog(items);
        }

        internal SpawnItem Find(string id) {
            SpawnItem item;
            return id!=null && byId.TryGetValue(id,out item)?item:null;
        }

        internal void LoadCompatibility(TextReader reader) {
            if(reader==null)throw new ArgumentNullException("reader");
            var loaded=new Dictionary<string,List<Compatibility>>(StringComparer.OrdinalIgnoreCase);
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);string line;int number=0,count=0;
            while((line=reader.ReadLine())!=null) {
                number++;if(line.Length==0 || line.StartsWith("#",StringComparison.Ordinal))continue;
                string[] fields=line.Split('\t');
                if(fields.Length!=4 || line.Length>1024)throw new FormatException("Invalid weapon compatibility line "+number+".");
                SpawnItem weapon=Find(fields[0]),item=Find(fields[1]);string relation=fields[2];
                if(weapon==null || item==null || weapon==item || weapon.Category!="weapon_ranged" || fields[0]!=weapon.Id || fields[1]!=item.Id ||
                    fields[3]!="https://scum-db.com/api/items/"+weapon.Id+"/compatibility" ||
                    (relation!="magazine" && relation!="ammunition" && relation!="attachment:direct" && relation!="attachment:requires_adapter" && relation!="required_adapter") ||
                    !seen.Add(weapon.Id+"\t"+item.Id+"\t"+relation) || ++count>50000)
                    throw new FormatException("Invalid weapon compatibility line "+number+".");
                List<Compatibility> edges;
                if(!loaded.TryGetValue(weapon.Id,out edges)) { edges=new List<Compatibility>();loaded.Add(weapon.Id,edges); }
                edges.Add(new Compatibility { Item=item,Relation=relation });
            }
            var snapshot=new Dictionary<string,IList<Compatibility>>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in loaded)snapshot.Add(entry.Key,entry.Value.AsReadOnly());
            compatibility=snapshot;CompatibilityCount=count;
        }

        static string Compact(string value) {
            var compact=new StringBuilder();
            foreach(char c in value.Normalize(NormalizationForm.FormD)) {
                if(System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)==System.Globalization.UnicodeCategory.NonSpacingMark)continue;
                if(char.IsLetterOrDigit(c))compact.Append(c=='ł'||c=='Ł'?'L':char.ToUpperInvariant(c));
            }
            return compact.ToString();
        }

        bool Matches(SpawnItem item,string term) {
            foreach(string field in searchText[item.Id])if(field.IndexOf(term,StringComparison.Ordinal)>=0)return true;
            if(Compact(item.DisplayName).IndexOf(term,StringComparison.Ordinal)>=0)return true;
            if(Compact(Localization.GetSpawnCategoryName(item.Category,item.Category)).IndexOf(term,StringComparison.Ordinal)>=0)return true;
            if(Compact(Localization.GetSpawnCategoryName(item.Subcategory,item.Subcategory)).IndexOf(term,StringComparison.Ordinal)>=0)return true;
            return false;
        }

        internal IList<SpawnItem> Search(string query,string category) {
            Func<SpawnItem,bool> filter=item=>string.IsNullOrEmpty(category) || string.Equals(item.Category,category,StringComparison.OrdinalIgnoreCase);
            var items=new List<SpawnItem>();
            foreach(var match in SearchWithRelations(query,filter))if(filter(match.Item))items.Add(match.Item);
            return items.AsReadOnly();
        }

        // Category membership determines the gun seeds. Their verified compatible items may cross categories.
        internal IList<SpawnItemSearchResult> SearchWithRelations(string query,Func<SpawnItem,bool> filter) {
            var terms=new List<string>();
            foreach(string word in (query??"").Split((char[])null,StringSplitOptions.RemoveEmptyEntries)) {
                string term=Compact(word);if(term.Length>0 && !terms.Contains(term))terms.Add(term);
            }
            var direct=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var related=new Dictionary<string,List<SpawnItem>>(StringComparer.OrdinalIgnoreCase);
            var adapters=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in Items) {
                if(filter!=null && !filter(item))continue;
                bool include=true;
                foreach(string term in terms) {
                    if(!Matches(item,term)) { include=false;break; }
                }
                if(include)direct.Add(item.Id);
                IList<Compatibility> edges;
                if(terms.Count==0 || !compatibility.TryGetValue(item.Id,out edges))continue;
                var remaining=new List<string>();bool weaponTerm=false;
                foreach(string term in terms) { if(Matches(item,term))weaponTerm=true;else remaining.Add(term); }
                if(!weaponTerm)continue;
                foreach(var edge in edges) {
                    bool compatibleMatch=true;
                    foreach(string term in remaining)if(!Matches(edge.Item,term)) { compatibleMatch=false;break; }
                    if(!compatibleMatch)continue;
                    List<SpawnItem> weapons;
                    if(!related.TryGetValue(edge.Item.Id,out weapons)) { weapons=new List<SpawnItem>();related.Add(edge.Item.Id,weapons); }
                    if(!weapons.Contains(item))weapons.Add(item);
                    if(edge.Relation=="attachment:requires_adapter")adapters.Add(edge.Item.Id);
                }
            }
            var matches=new List<SpawnItemSearchResult>();
            foreach(var item in Items) {
                List<SpawnItem> weapons;related.TryGetValue(item.Id,out weapons);
                if(direct.Contains(item.Id) || weapons!=null)matches.Add(new SpawnItemSearchResult(item,!direct.Contains(item.Id),adapters.Contains(item.Id),weapons??new List<SpawnItem>()));
            }
            if(terms.Count>0)matches.Sort((left,right)=> {
                int leftOrder=left.IsRelated?2:left.Item.Category=="weapon_ranged"?0:1;
                int rightOrder=right.IsRelated?2:right.Item.Category=="weapon_ranged"?0:1;
                int order=leftOrder.CompareTo(rightOrder);
                if(order==0)order=StringComparer.CurrentCultureIgnoreCase.Compare(left.Item.DisplayName,right.Item.DisplayName);
                return order!=0?order:StringComparer.Ordinal.Compare(left.Item.Id,right.Item.Id);
            });
            return matches.AsReadOnly();
        }
    }
}
