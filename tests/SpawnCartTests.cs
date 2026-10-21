using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScumMiniMap;

static class SpawnCartTests {
    delegate IntPtr ActivationHook(int code,IntPtr first,IntPtr second);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id,ActivationHook callback,IntPtr module,uint thread);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr first,IntPtr second);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int WindowStyle(IntPtr window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetWindowStyle(IntPtr window,int index,int value);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]
    struct GroupState {
        internal uint cbSize,mask;
        internal IntPtr header;
        internal int headerLength;
        internal IntPtr footer;
        internal int footerLength,id;
        internal uint stateMask,state,align;
        internal IntPtr subtitle;
        internal uint subtitleLength;
        internal IntPtr task;
        internal uint taskLength;
        internal IntPtr descriptionTop;
        internal uint descriptionTopLength;
        internal IntPtr descriptionBottom;
        internal uint descriptionBottomLength;
        internal int titleImage,extendedImage,firstItem;
        internal uint itemCount;
        internal IntPtr subsetTitle;
        internal uint subsetTitleLength;
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr window,int message,IntPtr first,ref GroupState second);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr window,int message,IntPtr first,IntPtr second);
    static int NativeGroupId(ListView view,string key) {
        // Find the exact native heading independently of the production member-row mapping.
        // Windows may insert a default group, so managed and native collection indices differ.
        string heading=view.Groups[key].Header;
        int count=SendMessage(view.Handle,0x1000+152,IntPtr.Zero,IntPtr.Zero).ToInt32();
        IntPtr buffer=Marshal.AllocHGlobal(4096);
        try {
            for(int index=0;index<count;index++) {
                Marshal.WriteInt16(buffer,0,0);
                var group=new GroupState { cbSize=(uint)Marshal.SizeOf(typeof(GroupState)),mask=0x11,header=buffer,headerLength=2048 };
                if(SendMessage(view.Handle,0x1000+153,new IntPtr(index),ref group)!=IntPtr.Zero && Marshal.PtrToStringUni(buffer)==heading)
                    return group.id;
            }
        }finally { Marshal.FreeHGlobal(buffer); }
        throw new Exception("Could not find the actual native heading: "+heading);
    }
    static bool NativeGroupCollapsed(ListView view,string key) {
        var state=new GroupState { cbSize=(uint)Marshal.SizeOf(typeof(GroupState)),mask=0x04,stateMask=0x01 };
        if(SendMessage(view.Handle,0x1000+149,new IntPtr(NativeGroupId(view,key)),ref state)==new IntPtr(-1))
            throw new Exception("Could not read the actual native group state.");
        return (state.state&0x01)!=0;
    }
    [StructLayout(LayoutKind.Sequential)]
    struct GroupRectangle { internal int left,top,right,bottom; }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]
    static extern IntPtr SendMessage(IntPtr window,int message,IntPtr first,ref GroupRectangle second);
    static System.Drawing.Rectangle NativeGroupHeader(ListView view,string key) {
        var rectangle=new GroupRectangle { top=1 };
        if(SendMessage(view.Handle,0x1000+98,new IntPtr(NativeGroupId(view,key)),ref rectangle)==IntPtr.Zero)
            throw new Exception("Could not read the native group heading bounds.");
        return System.Drawing.Rectangle.FromLTRB(rectangle.left,rectangle.top,rectangle.right,rectangle.bottom);
    }
    static void FocusNativeGroup(ListView view,string key,bool focused) {
        int id=NativeGroupId(view,key);var state=new GroupState { cbSize=(uint)Marshal.SizeOf(typeof(GroupState)),mask=0x04,stateMask=0x10,state=focused?0x10u:0 };
        if(SendMessage(view.Handle,0x1000+147,new IntPtr(id),ref state)==new IntPtr(-1))
            throw new Exception("Could not set the owned test control's group focus state.");
    }

    sealed class InactiveUi:IDisposable {
        readonly ActivationHook callback;
        readonly IntPtr hook;
        internal InactiveUi() {
            // A thread-local CBT hook prevents this test's off-screen forms or child editors from activating.
            callback=(code,first,second)=>code==5?new IntPtr(1):CallNextHookEx(IntPtr.Zero,code,first,second);
            hook=SetWindowsHookEx(5,callback,IntPtr.Zero,GetCurrentThreadId());
            if(hook==IntPtr.Zero)throw new Exception("Could not prevent test-window activation.");
        }
        internal void Show(Form dialog) {
            dialog.TopMost=false;dialog.ShowInTaskbar=false;dialog.StartPosition=FormStartPosition.Manual;
            dialog.Location=new System.Drawing.Point(-32000,-32000);
            SetWindowStyle(dialog.Handle,-20,WindowStyle(dialog.Handle,-20)|0x08000000);
            dialog.Show();dialog.PerformLayout();Application.DoEvents();
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Off-screen test windows never take the foreground");
        }
        public void Dispose() { UnhookWindowsHookEx(hook);GC.KeepAlive(callback); }
    }
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static int checks;
    static string fixtureRoot;
    static void Check(bool condition,string message) {
        if(!condition)throw new Exception(message);
        checks++;Console.WriteLine("PASS: "+message);
    }
    static void Reject<T>(Action action,string message) where T:Exception {
        bool rejected=false;
        try { action(); }catch(T) { rejected=true; }
        Check(rejected,message);
    }
    static T Field<T>(object instance,string name) { return (T)instance.GetType().GetField(name,Hidden).GetValue(instance); }
    static void SetField(object instance,string name,object value) { instance.GetType().GetField(name,Hidden).SetValue(instance,value); }
    static void Invoke(object instance,string name) { instance.GetType().GetMethod(name,Hidden).Invoke(instance,null); }
    static string Snapshot(SpawnCart cart) { return string.Join("\n",cart.BuildCommands()); }
    static string ProfileCartPath(string directory,string name) {
        string profile=Path.Combine(directory,Path.GetFileNameWithoutExtension(name));Directory.CreateDirectory(profile);
        return Path.Combine(profile,"spawn-cart.tsv");
    }
    static string ProfileDatabasePath(string legacyPath) { return Path.Combine(Path.GetDirectoryName(legacyPath),"spawn-kits.db"); }
    static void ReloadProfileCart(SpawnCart cart,SpawnItemCatalog catalog,string legacyPath) {
        new SpawnKitDatabase(ProfileDatabasePath(legacyPath),catalog).LoadCart(cart,legacyPath);
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_open_v2")]
    static extern int OpenFixtureDatabase(byte[] path,out IntPtr database,int flags,IntPtr vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_exec")]
    static extern int ExecuteFixtureSql(IntPtr database,byte[] sql,IntPtr callback,IntPtr argument,out IntPtr error);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_free")]
    static extern void FreeFixtureError(IntPtr error);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl,EntryPoint="sqlite3_close")]
    static extern int CloseFixtureDatabase(IntPtr database);
    static byte[] Utf8Terminated(string value) { return System.Text.Encoding.UTF8.GetBytes(value+"\0"); }
    static string FixtureError(IntPtr message) {
        if(message==IntPtr.Zero)return "unknown SQLite fixture failure";
        int length=0;while(length<4096 && Marshal.ReadByte(message,length)!=0)length++;
        var bytes=new byte[length];Marshal.Copy(message,bytes,0,length);return System.Text.Encoding.UTF8.GetString(bytes);
    }
    static void FixtureSql(string databasePath,string sql) {
        string path=Path.GetFullPath(databasePath);
        if(string.IsNullOrEmpty(fixtureRoot) || !path.StartsWith(fixtureRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
            throw new Exception("SQLite fixtures must stay inside the owned disposable test profile.");
        IntPtr database;int opened=OpenFixtureDatabase(Utf8Terminated(path),out database,2,IntPtr.Zero);
        if(opened!=0) { if(database!=IntPtr.Zero)CloseFixtureDatabase(database);throw new Exception("Could not open owned SQLite test fixture."); }
        try {
            IntPtr error;int result=ExecuteFixtureSql(database,Utf8Terminated(sql),IntPtr.Zero,IntPtr.Zero,out error);
            try { if(result!=0)throw new Exception("Owned SQLite fixture setup failed: "+FixtureError(error)); }
            finally { if(error!=IntPtr.Zero)FreeFixtureError(error); }
        }finally { CloseFixtureDatabase(database); }
    }
    static string Row(string id,string name,string category) { return id+"\t"+name+"\t"+category+"\thttps://scum-db.com/items/"+id; }
    static SpawnItemCatalog Parse(string text) { using(var reader=new StringReader(text))return SpawnItemCatalog.Load(reader); }
    static string CompatibilityRow(string weapon,string item,string relation) { return weapon+"\t"+item+"\t"+relation+"\thttps://scum-db.com/api/items/"+weapon+"/compatibility"; }
    static void LoadCompatibility(SpawnItemCatalog catalog,string text) { using(var reader=new StringReader(text))catalog.LoadCompatibility(reader); }
    static IEnumerable<SpawnCategoryNode> CategoryNodes(IEnumerable<SpawnCategoryNode> nodes) {
        foreach(var node in nodes) { yield return node;foreach(var child in CategoryNodes(node.Children))yield return child; }
    }
    static IEnumerable<TreeNode> TreeNodes(TreeNodeCollection nodes) {
        foreach(TreeNode node in nodes) { yield return node;foreach(var child in TreeNodes(node.Nodes))yield return child; }
    }

    static void TestCategoryHierarchy(SpawnItemCatalog catalog) {
        Reject<ArgumentNullException>(()=>SpawnItemCategories.Build(null),"Category hierarchy requires a source catalogue");
        var groups=SpawnItemCategories.Build(catalog);var nodes=CategoryNodes(groups).ToList();
        Check(groups.IsReadOnly && groups.Count==6,"Bundled catalogue exposes six immutable broad item groups");
        Check(groups.Select(group=>group.Key).SequenceEqual(new[]{"group/weapons","group/clothing","group/survival","group/crafting","group/vehicles","group/other"}),
            "Broad categories use stable keys and an intentional browsing order");
        var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var leaves=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var node in nodes) {
            if(!keys.Add(node.Key) || string.IsNullOrWhiteSpace(node.Label) || !node.Items.IsReadOnly || !node.Children.IsReadOnly || node.Items.Count==0)
                throw new Exception("Invalid item category node: "+node.Key);
            if(node.Contains(null) || node.Items.Any(item=>!node.Contains(item)))throw new Exception("Category membership mismatch: "+node.Key);
            if(node.IsLeaf) {
                foreach(var item in node.Items)if(!leaves.Add(item.Id))throw new Exception("Item duplicated across category leaves: "+item.Id);
            } else {
                var children=node.Children.SelectMany(child=>child.Items).ToList();
                if(children.Count!=node.Items.Count || !new HashSet<string>(children.Select(item=>item.Id),StringComparer.OrdinalIgnoreCase)
                    .SetEquals(node.Items.Select(item=>item.Id)))throw new Exception("Parent does not cover exactly its children: "+node.Key);
            }
        }
        Check(leaves.Count==catalog.Items.Count && leaves.SetEquals(catalog.Items.Select(item=>item.Id)),
            "Every bundled item belongs to exactly one leaf, with complete membership retained at each parent");
        Check(keys.Count==nodes.Count,"All category and subcategory nodes have unique keys, readable labels and read-only contents");

        var fixture=Parse(string.Join("\n",new[]{
            Row("Rifle","Rifle","weapon_ranged")+"\tassault_rifle",
            Row("Pistol","Pistol","weapon_ranged")+"\tpistol",
            Row("Knife","Knife","weapon_melee")+"\tknife",
            Row("OldKnife","Legacy knife","melee_weapon")+"\tknife",
            Row("Jacket","Jacket","clothing")+"\tjacket",
            Row("Apple","Apple","consumable")+"\tfood",
            Row("Rope","Rope","material"),
            Row("Engine","Engine","vehicle_part")+"\tengine",
            Row("UnknownCategoryItem","Unknown category item","new_source_category")+"\tSpecial/Test"
        }));
        var fixtureGroups=SpawnItemCategories.Build(fixture);var fixtureNodes=CategoryNodes(fixtureGroups).ToDictionary(node=>node.Key);
        Check(fixtureGroups.Count==6,"Source metadata populates all broad categories including an unknown-category fallback");
        var rifles=fixtureNodes["subcategory/weapons/weapon_ranged/assault_rifle"];
        var pistols=fixtureNodes["subcategory/weapons/weapon_ranged/pistol"];
        Check(rifles.Contains(fixture.Find("Rifle")) && !rifles.Contains(fixture.Find("Pistol")) && pistols.Contains(fixture.Find("Pistol")),
            "Source gun subtypes keep rifles and pistols in separate leaf selectors");
        Check(fixtureNodes["category/weapons/weapon_melee"].Items.Count==2
            && fixtureNodes["subcategory/weapons/weapon_melee/knife"].Items.Count==2,
            "Legacy and current melee categories share one category and subtype without duplicating items");
        Check(fixtureNodes["subcategory/crafting/material/general"].Contains(fixture.Find("Rope")),
            "A legacy item without a source subtype appears in its category's General leaf");
        Check(fixtureNodes["group/other"].Contains(fixture.Find("UnknownCategoryItem"))
            && fixtureNodes.ContainsKey("subcategory/other/new_source_category/Special%2FTest"),
            "New source categories remain browsable under Other and escape subcategory separators in stable keys");
    }

    static void TestCatalogue(SpawnItemCatalog bundled) {
        Check(bundled.Items.Count>100,"Embedded SCUM Database catalogue supports a large item selector");
        Check(bundled.Items.IsReadOnly && bundled.Categories.IsReadOnly,"Catalogue items and category lists cannot be modified by UI callers");
        var ids=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in bundled.Items) {
            if(!ids.Add(item.Id) || !SpawnItemCatalog.IsValidId(item.Id) || string.IsNullOrWhiteSpace(item.Name)
                || string.IsNullOrWhiteSpace(item.Category) || item.SourceUrl!="https://scum-db.com/items/"+item.Id)
                throw new Exception("Invalid bundled item: "+item.Id);
        }
        Check(ids.Count==bundled.Items.Count,"Every bundled item has a unique safe ID, readable name/category and matching SCUM Database source");
        Check(bundled.Find(null)==null && bundled.Find("NoSuchItem_ForSpawnCartRegression")==null,"Unknown and null catalogue lookups return no item");
        var first=bundled.Items[0];
        Check(bundled.Find(first.Id.ToUpperInvariant())==first,"Catalogue lookup uses canonical items regardless of ID casing");
        Check(bundled.Search(first.Id.ToLowerInvariant(),null).Any(item=>item.Id==first.Id),"Bundled item IDs can be found through case-insensitive search");

        var fixture=Parse("# Test catalogue\n\n"+Row("AK_47","AK-47 rifle","Weapons")+"\n"
            +Row("Cal_30-06","Rifle cartridge","Ammunition")+"\n"
            +Row("CarBattery","Car battery","Vehicle parts")+"\n"
            +Row("CarRepairKit","Car repair kit","Tools")+"\n");
        Check(fixture.Items.Count==4 && fixture.Categories.Count==4,"Catalogue parser accepts comment headers and empty lines");
        Check(fixture.Items[0].Id=="AK_47" && fixture.Items[3].Id=="Cal_30-06","Catalogue results are sorted by readable item name");
        Check(fixture.Search("  CAR\tBATTERY  ",null).Single().Id=="CarBattery","Search requires all whitespace-separated words and ignores casing");
        Check(fixture.Search("cal AMMUNITION",null).Single().Id=="Cal_30-06","Search combines ID and category terms across fields");
        foreach(string query in new[]{"AK47","AK-47","AK 47","AK_47","ak  47 rifle"})
            Check(fixture.Search(query,null).Single().Id=="AK_47","Gun search treats common spaces and punctuation consistently: "+query);
        Check(fixture.Search("AK47 battery",null).Count==0 && fixture.Search("AK-47","Tools").Count==0,
            "Normalized gun search still requires every word and respects the requested category");
        Check(fixture.Search("car","tools").Single().Id=="CarRepairKit" && fixture.Search("car","Weapons").Count==0,
            "Category filtering and text search must both match");
        Check(fixture.Search(null,null).Count==4 && fixture.Search("","").Count==4,"Empty search and category expose the entire catalogue");
        Check(fixture.Search("impossible item",null).Count==0,"Unmatched search returns an empty result");
        Check(fixture.Items.All(item=>item.Subcategory==""),"Legacy four-column catalogue rows retain an empty optional subcategory");
        var metadata=Parse(Row("Weapon_AK47","AK-47 rifle","weapon_ranged")+"\tassault_rifle\n"
            +Row("CarBattery","Car battery","Vehicle parts")+"\t\n");
        Check(metadata.Find("Weapon_AK47").Subcategory=="assault_rifle" && metadata.Find("CarBattery").Subcategory=="",
            "Five-column catalogue rows preserve sourced subcategory metadata and accept an explicitly empty value");
        Check(metadata.Search("assault",null).Single().Id=="Weapon_AK47" && metadata.Search("AK ASSAULT","weapon_ranged").Count==1,
            "Search includes subcategory metadata and combines it with item and category terms");
        foreach(string invalid in new[]{" subcategory","subcategory ","\u0001subcategory",new string('A',121)})
            Reject<FormatException>(()=>Parse(Row("Weapon_AK47","AK-47 rifle","weapon_ranged")+"\t"+invalid),
                "Malformed optional catalogue subcategories are rejected");
        foreach(string id in new[]{"AK_47","Cal_30-06","_Item","123"})Check(SpawnItemCatalog.IsValidId(id),"Valid SCUM item ID accepted: "+id);
        foreach(string id in new[]{null,"","-Item","Item Name","Item;#Kick","Item\t1","Item\n#Kick","中文",new string('A',129)})
            Check(!SpawnItemCatalog.IsValidId(id),"Command-breaking or invalid item IDs are rejected");
        Reject<ArgumentNullException>(()=>SpawnItemCatalog.Load(null),"Catalogue load rejects a missing reader");
        Reject<FormatException>(()=>Parse("# Empty\n"),"An empty catalogue fails closed");
        Reject<FormatException>(()=>Parse(Row("AK_47","Rifle","Weapons")+"\n"+Row("ak_47","Other rifle","Weapons")),
            "Duplicate IDs are rejected even with different casing");
        foreach(string row in new[]{
            "AK_47\tRifle\tWeapons",
            Row("AK_47","Rifle","Weapons")+"\textra\tunexpected",
            Row("AK_47"," Rifle","Weapons"),
            Row("AK_47","Rifle","Weapons "),
            Row("AK_47","","Weapons"),
            Row("AK_47","Rifle","\u0001Weapons"),
            Row("AK_47",new string('A',257),"Weapons"),
            Row("AK_47","Rifle",new string('A',81)),
            Row("AK_47;Kick","Rifle","Weapons"),
            "AK_47\tRifle\tWeapons\thttps://example.com/items/AK_47",
            "AK_47\tRifle\tWeapons\thttps://scum-db.com/items/AnotherItem"
        })Reject<FormatException>(()=>Parse(row),"Malformed catalogue records fail before they become spawn commands");
    }

    static void TestCompatibilitySearch(SpawnItemCatalog bundled) {
        Check(bundled.CompatibilityCount>3000,"Bundled catalogue includes verified compatibility edges queried for every ranged weapon");
        var bundledMatches=bundled.SearchWithRelations("AK47",null);var ak=bundledMatches.Select(match=>match.Item.Id).ToList();
        foreach(string id in new[]{"Weapon_AK47","Weapon_AK47_Engraved","Magazine_AK47","Magazine_RPK","M70_Bayonet",
            "ImprovisedRail_Long","ImprovisedRail_Short","WeaponScope_ACOG_01","WeaponSuppressor_ImprovisedBottle_White","Cal_7_62x39mm"})
            Check(ak.Contains(id),"AK47 search includes a weapon or source-listed compatible item: "+id);
        Check(!ak.Contains("1H_N9_Black") && !ak.Contains("1H_N9_White") && !ak.Contains("WeaponScope_M82A1_Desert"),
            "AK47 compatibility does not infer another gun's exclusive bayonets or an attachment's unlisted skin variant");
        foreach(string query in new[]{"AK-47","AK 47","ak_47"})
            Check(bundled.SearchWithRelations(query,null).Select(match=>match.Item.Id).SequenceEqual(ak),
                "Bundled gun family and compatible search results stay consistent across punctuation and spaces: "+query);
        var acog=bundledMatches.Single(match=>match.Item.Id=="WeaponScope_ACOG_01");
        Check(acog.IsRelated && acog.RequiresAdapter && acog.RelatedWeapons.IsReadOnly
            && acog.RelatedWeapons.Select(item=>item.Id).OrderBy(id=>id).SequenceEqual(new[]{"Weapon_AK47","Weapon_AK47_Engraved"}),
            "A shared scope appears once with its two explicitly queried AK47 variants and its required-adapter status");
        Check(ak.Count==new HashSet<string>(ak,StringComparer.OrdinalIgnoreCase).Count
            && bundledMatches.Take(2).All(match=>!match.IsRelated && match.Item.Category=="weapon_ranged")
            && !bundledMatches.SkipWhile(match=>!match.IsRelated).Any(match=>!match.IsRelated),
            "Direct gun matches lead related results, and shared attachment or adapter edges cannot duplicate an item");
        var scopes=bundled.SearchWithRelations("AK47 scope",null);
        Check(scopes.Any(match=>match.Item.Id=="WeaponScope_ACOG_01" && match.IsRelated && match.RequiresAdapter)
            && scopes.Any(match=>match.Item.Id=="WeaponScope_M82A1") && !scopes.Any(match=>match.Item.Id=="Magazine_RPK"),
            "A gun-plus-scope query combines weapon and attachment terms and excludes compatible magazines that do not match the remaining term");
        Check(bundled.SearchWithRelations("scope",null).Any(match=>match.Item.Id=="WeaponScope_ACOG_01" && !match.IsRelated),
            "Searching only for scopes retains ordinary direct item matches without inventing a gun seed");
        Check(bundled.Search("AK47","weapon_ranged").All(item=>item.Category=="weapon_ranged")
            && bundled.Search("AK47","magazine").All(item=>item.Category=="magazine"),
            "Legacy category search retains strict category membership after compatibility expansion");

        var fixture=Parse(string.Join("\n",new[]{
            Row("Weapon_AK47","AK-47","weapon_ranged")+"\tassault_rifle",
            Row("Weapon_AK47_Engraved","AK47 Engraved","weapon_ranged")+"\tassault_rifle",
            Row("Weapon_AK47_Unsourced","AK47 Unsourced variant","weapon_ranged")+"\tassault_rifle",
            Row("Weapon_M16A4","M16A4","weapon_ranged")+"\tassault_rifle",
            Row("Magazine_AK47","AK47 Magazine","magazine")+"\tassault_rifle",
            Row("Magazine_RPK","RPK Magazine","magazine")+"\tlmg",
            Row("M70_Bayonet","M70 Bayonet","attachment")+"\tbayonet",
            Row("1H_N9_Black","N9 Bayonet","attachment")+"\tbayonet",
            Row("WeaponScope_ACOG_01","ACOG","attachment")+"\tscope",
            Row("WeaponScope_ACOG_01_Desert","ACOG Desert","attachment")+"\tscope",
            Row("ImprovisedRail_Long","Long rail","attachment")+"\trail",
            Row("Cal_7_62x39mm","7.62x39 cartridge","ammunition")+"\trifle"
        }));
        var edges=new List<string>();
        foreach(string weapon in new[]{"Weapon_AK47","Weapon_AK47_Engraved"}) {
            edges.Add(CompatibilityRow(weapon,"Magazine_AK47","magazine"));edges.Add(CompatibilityRow(weapon,"Magazine_RPK","magazine"));
            edges.Add(CompatibilityRow(weapon,"WeaponScope_ACOG_01","attachment:requires_adapter"));edges.Add(CompatibilityRow(weapon,"M70_Bayonet","attachment:direct"));
            edges.Add(CompatibilityRow(weapon,"ImprovisedRail_Long","attachment:direct"));edges.Add(CompatibilityRow(weapon,"ImprovisedRail_Long","required_adapter"));
            edges.Add(CompatibilityRow(weapon,"Cal_7_62x39mm","ammunition"));
        }
        edges.Add(CompatibilityRow("Weapon_M16A4","WeaponScope_ACOG_01","attachment:requires_adapter"));
        edges.Add(CompatibilityRow("Weapon_M16A4","1H_N9_Black","attachment:direct"));LoadCompatibility(fixture,"# Source fixture\n\n"+string.Join("\n",edges));
        Check(fixture.CompatibilityCount==16,"Compatibility loader accepts source comments and multiple relations for the same adapter");
        var results=fixture.SearchWithRelations("AK47",item=>item.Category=="weapon_ranged");var ids=results.Select(match=>match.Item.Id).ToList();
        Check(results.IsReadOnly && ids.Count==new HashSet<string>(ids).Count && results.Take(3).All(match=>!match.IsRelated && match.Item.Category=="weapon_ranged")
            && results.Skip(3).All(match=>match.IsRelated),"Filtered gun search returns direct guns before deduplicated compatible rows outside their category");
        Check(results.Single(match=>match.Item.Id=="WeaponScope_ACOG_01").RelatedWeapons.Select(item=>item.Id)
            .SequenceEqual(new[]{"Weapon_AK47","Weapon_AK47_Engraved"}) && !ids.Contains("1H_N9_Black") && !ids.Contains("WeaponScope_ACOG_01_Desert"),
            "Related provenance includes only matched compatible gun seeds and excludes exclusive or unlisted variant parts");
        var oneGun=fixture.SearchWithRelations("AK47 scope",item=>item.Id=="Weapon_AK47");
        Check(oneGun.Count==1 && oneGun[0].Item.Id=="WeaponScope_ACOG_01" && oneGun[0].RequiresAdapter
            && oneGun[0].RelatedWeapons.Single().Id=="Weapon_AK47",
            "An in-category gun seed can expose its compatible scope across a category boundary with only that seed as provenance");
        Check(fixture.SearchWithRelations("AK47 scope",item=>item.Id=="Weapon_M16A4").Count==0
            && fixture.SearchWithRelations("AK47 nonexistent",null).Count==0,
            "Out-of-filter guns cannot seed related rows, and every remaining attachment query term is still required");
        Check(fixture.SearchWithRelations("Unsourced",null).Single().Item.Id=="Weapon_AK47_Unsourced",
            "A weapon variant without its own source edges does not inherit a parent gun's compatibility by name");
        string before=string.Join("\n",fixture.SearchWithRelations("AK47",null).Select(match=>match.Item.Id));int count=fixture.CompatibilityCount;
        Reject<ArgumentNullException>(()=>fixture.LoadCompatibility(null),"Compatibility loader requires a reader");
        foreach(string invalid in new[]{
            "Weapon_AK47\tMagazine_AK47\tmagazine",
            CompatibilityRow("Weapon_AK47","Magazine_AK47","magazine")+"\textra",
            CompatibilityRow("UnknownWeapon","Magazine_AK47","magazine"),
            CompatibilityRow("Weapon_AK47","UnknownItem","attachment:direct"),
            CompatibilityRow("Magazine_AK47","WeaponScope_ACOG_01","attachment:direct"),
            CompatibilityRow("Weapon_AK47","Weapon_AK47","attachment:direct"),
            CompatibilityRow("Weapon_AK47","Magazine_AK47","unknown"),
            CompatibilityRow("weapon_ak47","Magazine_AK47","magazine"),
            "Weapon_AK47\tMagazine_AK47\tmagazine\thttps://example.com/api/items/Weapon_AK47/compatibility",
            "Weapon_AK47\tMagazine_AK47\tmagazine\thttps://scum-db.com/api/items/Weapon_M16A4/compatibility",
            CompatibilityRow("Weapon_AK47","Magazine_AK47","magazine")+"\n"+CompatibilityRow("Weapon_AK47","Magazine_AK47","magazine")
        }) {
            Reject<FormatException>(()=>LoadCompatibility(fixture,CompatibilityRow("Weapon_AK47","M70_Bayonet","attachment:direct")+"\n"+invalid),
                "Malformed or unverified compatibility relationships fail as a complete graph import");
            Check(fixture.CompatibilityCount==count && string.Join("\n",fixture.SearchWithRelations("AK47",null).Select(match=>match.Item.Id))==before,
                "A later invalid compatibility row preserves the previous graph and search results atomically");
        }
    }

    static void TestCart(SpawnItemCatalog catalog) {
        string first=catalog.Items[0].Id,second=catalog.Items[1].Id,third=catalog.Items[2].Id;
        Reject<ArgumentNullException>(()=>new SpawnCart(null),"Cart requires an authoritative item catalogue");
        var cart=new SpawnCart(catalog);
        cart.Add(first,2);cart.Add(second,3);cart.Add(first.ToUpperInvariant(),4);
        Check(cart.Entries.Count==2 && cart.Entries[0].Item.Id==first && cart.Entries[0].Quantity==6 && cart.TotalQuantity==9,
            "Repeated IDs merge quantities without changing insertion order or canonical casing");
        Check(cart.Entries.IsReadOnly,"UI callers cannot insert unvalidated cart entries");
        Check(cart.BuildCommands().SequenceEqual(new[]{"#SpawnItem "+first+" 6","#SpawnItem "+second+" 3"}),
            "Cart builds one ordered SpawnItem command per item type with its total quantity");
        string before=Snapshot(cart);
        foreach(int invalid in new[]{0,-1,SpawnCart.MaximumQuantity+1,int.MaxValue}) {
            Reject<FormatException>(()=>cart.Add(first,invalid),"Invalid add quantity is rejected");
            Check(Snapshot(cart)==before,"Rejected add preserves the complete cart");
            Reject<FormatException>(()=>cart.SetQuantity(first,invalid),"Invalid edited quantity is rejected");
            Check(Snapshot(cart)==before,"Rejected quantity edit preserves the complete cart");
        }
        Reject<FormatException>(()=>cart.Add("NoSuchItem_ForSpawnCartRegression",1),"Cart cannot enqueue an unknown item ID");
        Reject<FormatException>(()=>cart.Add(first+"\n#Kick",1),"Cart cannot enqueue a command-injection ID");
        Reject<FormatException>(()=>cart.SetQuantity(third,1),"Quantity editor cannot create an absent item");
        Check(Snapshot(cart)==before,"Invalid ID operations preserve the complete cart");
        Reject<FormatException>(()=>cart.AddMany(new[]{third,"NoSuchItem_ForSpawnCartRegression"},1),
            "A multi-selection with an unknown ID is rejected atomically");
        Check(Snapshot(cart)==before,"Valid earlier selections are rolled back when a later selection is invalid");
        cart.SetQuantity(first,99);before=Snapshot(cart);
        Reject<FormatException>(()=>cart.AddMany(new[]{third,first},2),"A multi-selection cannot exceed the per-item limit");
        Check(Snapshot(cart)==before,"Multi-selection limit failure does not leave an earlier item partially added");
        cart.SetQuantity(first,100);before=Snapshot(cart);
        Reject<FormatException>(()=>cart.Add(first,1),"An item already at the maximum cannot be incremented");
        Check(Snapshot(cart)==before,"Per-item limit failure leaves the maximum quantity intact");
        var commands=cart.BuildCommands();cart.SetQuantity(first,5);
        Check(commands[0]=="#SpawnItem "+first+" 100" && cart.BuildCommands()[0]=="#SpawnItem "+first+" 5",
            "Command snapshots remain stable when the editable cart later changes");
        cart.Remove(first.ToUpperInvariant());
        Check(cart.Entries.Count==1 && cart.Entries[0].Item.Id==second && cart.TotalQuantity==3,"Removing an item ignores casing and adjusts totals");
        cart.Remove("unknown");Check(cart.TotalQuantity==3,"Removing an unknown item leaves the cart intact");
        cart.Clear();Check(cart.Entries.Count==0 && cart.TotalQuantity==0 && cart.BuildCommands().Count==0,"Clear removes all cart entries and commands");

        var lineLimit=new SpawnCart(catalog);
        lineLimit.AddMany(catalog.Items.Take(SpawnCart.MaximumLines).Select(item=>item.Id),1);
        Check(lineLimit.Entries.Count==SpawnCart.MaximumLines,"Exactly the maximum number of item types is accepted");
        before=Snapshot(lineLimit);
        Reject<FormatException>(()=>lineLimit.Add(catalog.Items[SpawnCart.MaximumLines].Id,1),"The next distinct item type exceeds the cart limit");
        Check(Snapshot(lineLimit)==before,"Item-type limit failure is atomic");
        lineLimit.Add(catalog.Items[0].Id,1);
        Check(lineLimit.TotalQuantity==SpawnCart.MaximumLines+1,"An existing item can still be incremented at the item-type limit");
        var totalLimit=new SpawnCart(catalog);
        totalLimit.AddMany(catalog.Items.Take(10).Select(item=>item.Id),100);
        Check(totalLimit.TotalQuantity==SpawnCart.MaximumTotal,"Exactly 1,000 items is accepted");
        before=Snapshot(totalLimit);
        Reject<FormatException>(()=>totalLimit.Add(catalog.Items[10].Id,1),"The next item exceeds the total cart limit");
        Check(Snapshot(totalLimit)==before,"Total limit failure preserves all existing entries");
        totalLimit.SetQuantity(catalog.Items[0].Id,99);totalLimit.Add(catalog.Items[10].Id,1);before=Snapshot(totalLimit);
        Reject<FormatException>(()=>totalLimit.SetQuantity(catalog.Items[0].Id,100),"Editing a quantity cannot exceed the total limit");
        Check(Snapshot(totalLimit)==before,"Total limit failure during editing is atomic");

        cart.Add(first,1);cart.Add(second,2);cart.Add(third,3);before=Snapshot(cart);
        Reject<ArgumentOutOfRangeException>(()=>cart.ConsumeSubmitted(-1),"Negative submitted counts are rejected");
        Reject<ArgumentOutOfRangeException>(()=>cart.ConsumeSubmitted(4),"Submitted counts beyond the current cart are rejected");
        Check(Snapshot(cart)==before,"Invalid submitted counts cannot remove unsent entries");
        cart.ConsumeSubmitted(0);Check(Snapshot(cart)==before,"Consuming zero submissions preserves the cart");
        cart.ConsumeSubmitted(2);
        Check(cart.Entries.Count==1 && cart.Entries[0].Item.Id==third && cart.TotalQuantity==3,"Submitted commands remove only their ordered prefix");
        cart.ConsumeSubmitted(1);Check(cart.Entries.Count==0,"Consuming the final submitted command empties the cart");
    }

    static void TestPersistence(SpawnItemCatalog catalog,string directory) {
        string first=catalog.Items[0].Id,second=catalog.Items[1].Id;
        string path=Path.Combine(directory,"roundtrip.tsv"),badPath=Path.Combine(directory,"invalid.tsv");
        var cart=new SpawnCart(catalog);cart.Add(first,2);cart.Add(second,3);cart.Save(path);
        var loaded=new SpawnCart(catalog);loaded.Add(catalog.Items[2].Id,1);loaded.Load(path);
        Check(Snapshot(loaded)==Snapshot(cart),"Saved kits load canonical IDs, quantities and insertion order, replacing the previous cart");
        Check(File.ReadAllText(path).Contains("https://scum-db.com/"),"Saved kits retain item-ID provenance");
        cart.SetQuantity(first,8);cart.Save(path);loaded.Load(path);
        Check(Snapshot(loaded)==Snapshot(cart),"Saving over an existing kit atomically replaces its previous contents");
        string saved=File.ReadAllText(path),before=Snapshot(cart);
        using(var locked=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.None))
            Reject<IOException>(()=>cart.Save(path),"A locked kit reports save failure instead of claiming completion");
        Check(File.ReadAllText(path)==saved && Snapshot(cart)==before,"A failed replacement preserves the original kit and cart");
        Check(Directory.GetFiles(directory,"*.tmp").Length==0,"Failed and successful saves clean their temporary files");

        var broken=new List<string> {
            first+"\t1\nNoSuchItem_ForSpawnCartRegression\t2",
            first+"\t1\n"+first.ToUpperInvariant()+"\t2",
            first+"\t1\textra\tunexpected",
            first+"\n",
            first+"\t1\n"+second+"\t0",
            first+"\t1\n"+second+"\t-1",
            first+"\t1\n"+second+"\t101",
            first+"\t1\n"+second+"\t1.5",
            first+"\t1\n"+second+"\t+1",
            first+"\t1\n"+second+"\t1 ",
            first+"\t1\n"+second+"\t2147483648",
            new string('x',65537),
            string.Join("\n",catalog.Items.Take(101).Select(item=>item.Id+"\t1")),
            string.Join("\n",catalog.Items.Take(10).Select(item=>item.Id+"\t100"))+"\n"+catalog.Items[10].Id+"\t1"
        };
        before=Snapshot(loaded);
        foreach(string content in broken) {
            File.WriteAllText(badPath,content);
            Reject<FormatException>(()=>loaded.Load(badPath),"Malformed, unknown, duplicate, oversized or over-limit kits are rejected");
            Check(Snapshot(loaded)==before,"Failed kit imports preserve the previous cart without partially accepted entries");
        }
        cart.Clear();cart.Save(path);loaded.Load(path);
        Check(loaded.Entries.Count==0,"An explicitly saved empty kit reloads as an empty cart");
        File.WriteAllText(path,"# Hand-written kit\n\n"+first.ToUpperInvariant()+"\t4\n");loaded.Load(path);
        Check(loaded.Entries[0].Item.Id==first && loaded.TotalQuantity==4,"Imported kits accept comments and normalize known IDs to catalogue casing");
    }

    static void TestKitDatabase(SpawnItemCatalog catalog,string directory) {
        string legacy=ProfileCartPath(directory,"database-profile.tsv"),path=ProfileDatabasePath(legacy);
        var database=new SpawnKitDatabase(path,catalog);var cart=new SpawnCart(catalog);
        string first=catalog.Items[0].Id,second=catalog.Items[1].Id,third=catalog.Items[2].Id;
        cart.Add(first.ToUpperInvariant(),2);cart.SetModifiers(first,"Weight -00.750 Uses 0 Health 75 Location \"X=-1 Y=2 Z=3\"");
        cart.Add(second,4);cart.SetModifiers(second,"Radiation 0 KeyCardSector D4 StackCount 20 Dirtiness 25");cart.Add(third,1);
        string expected=Snapshot(cart);database.SaveCart(cart);
        Check(database.DatabasePath==Path.GetFullPath(path) && File.ReadAllBytes(path).Take(16).SequenceEqual(System.Text.Encoding.ASCII.GetBytes("SQLite format 3\0")),
            "Working carts are persisted in a real SQLite database inside their supplied profile");
        var loaded=new SpawnCart(catalog);loaded.Add(third,9);database.LoadCart(loaded,legacy);
        Check(Snapshot(loaded)==expected && loaded.Entries.Select(entry=>entry.Item.Id).SequenceEqual(new[]{first,second,third})
            && loaded.Entries.All(entry=>ReferenceEquals(entry.Item,catalog.Find(entry.Item.Id))),
            "Database cart roundtrip preserves catalogue identity, normalized modifiers, quantities and insertion order");
        Check(!File.Exists(legacy) && Directory.GetFiles(Path.GetDirectoryName(path),"*.tsv").Length==0,
            "Saving and loading the working cart creates no loose TSV file");
        Check(database.ListKits().Count==0,"The working cart does not become a named saved kit implicitly");
        string name="O'Brien; DROP TABLE spawn_kits; -- / \" Ελληνικά 日本語 🎒";
        long id=database.SaveKit("  "+name+"  ",cart);var listed=database.ListKits();var kit=listed.Single();
        Check(id>0 && kit.Id==id && kit.Name==name && kit.LineCount==3 && kit.TotalQuantity==7 && kit.UpdatedUtc.Kind==DateTimeKind.Utc,
            "Named kits bind Unicode, apostrophes and SQL-like punctuation literally and list useful UTC counts");
        loaded.Clear();database.LoadKit(id,loaded);Check(Snapshot(loaded)==expected,"Named-kit loading replaces the target cart with the complete saved order and modifiers");
        Reject<FormatException>(()=>database.SaveKit(name.ToLowerInvariant(),cart),"Case-equivalent kit names require explicit overwrite");
        loaded.Clear();database.LoadKit(id,loaded);Check(database.ListKits().Count==1 && Snapshot(loaded)==expected,
            "An unconfirmed duplicate name leaves its existing kit intact without adding another kit");
        cart.SetQuantity(first,8);cart.SetModifiers(second,"Uses 3 Weight 0.5");long overwritten=database.SaveKit(name,cart,true);
        loaded.Clear();database.LoadKit(id,loaded);
        Check(overwritten==id && Snapshot(loaded)==Snapshot(cart) && database.ListKits().Single().TotalQuantity==13,
            "Confirmed overwrite retains the stable kit ID and atomically replaces quantities and modifiers");
        database.LoadCart(loaded,legacy);Check(Snapshot(loaded)==expected,"Saving a named kit leaves the independently persisted working cart unchanged");
        foreach(string invalid in new[]{"", "  ",new string('a',81),"Line\nbreak","Zero\0byte"})
            Reject<FormatException>(()=>database.SaveKit(invalid,cart),"Invalid database kit names are rejected: "+invalid.Length);
        Reject<FormatException>(()=>database.SaveKit("Empty",new SpawnCart(catalog)),"An empty cart cannot create a named kit");
        Reject<FormatException>(()=>database.LoadKit(id+1000,loaded),"Loading a missing kit reports failure");
        Check(Snapshot(loaded)==expected,"A missing kit never replaces the target cart");
        long alphabetical=database.SaveKit("A Loadout",cart),last=database.SaveKit("Zulu",cart);
        Check(database.ListKits().Select(saved=>saved.Name).SequenceEqual(new[]{"A Loadout",name,"Zulu"}) && listed.Count==1,
            "Saved kits list alphabetically and previous list snapshots remain stable");
        string decomposed="Cafe\u0301 kit";long unicode=database.SaveKit(decomposed,cart);
        Reject<FormatException>(()=>database.SaveKit("CAFÉ KIT",cart),"Canonically equivalent Unicode names resolve to the same saved kit");
        Check(database.ListKits().Single(saved=>saved.Id==unicode).Name=="Café kit","Unicode kit display names are normalized without losing characters");
        var isolated=new SpawnKitDatabase(ProfileDatabasePath(ProfileCartPath(directory,"isolated-database-profile.tsv")),catalog);
        var isolatedCart=new SpawnCart(catalog);isolated.LoadCart(isolatedCart,null);
        Check(isolated.ListKits().Count==0 && isolatedCart.Entries.Count==0,"Separate AppData-style profiles keep saved kits and working carts isolated");

        string oldKit=Path.Combine(Path.GetDirectoryName(path),"legacy-import.tsv");
        File.WriteAllText(oldKit,"# SCUM MiniMap spawn kit v1\n"+third+"\t3\n"+first+"\t2\n");byte[] oldBytes=File.ReadAllBytes(oldKit);
        long imported=database.ImportKit("Legacy v1",oldKit);database.LoadKit(imported,loaded);
        Check(loaded.BuildCommands().SequenceEqual(new[]{"#SpawnItem "+third+" 3","#SpawnItem "+first+" 2"}) && File.ReadAllBytes(oldKit).SequenceEqual(oldBytes),
            "Legacy TSV import stores a named database kit while preserving the source file and item order");
        cart.Save(oldKit);imported=database.ImportKit("Legacy v1",oldKit,true);database.LoadKit(imported,loaded);
        Check(Snapshot(loaded)==Snapshot(cart),"Legacy v2 import and confirmed overwrite retain all selectable modifiers");
        File.WriteAllText(oldKit,first+"\t1\nUnknown_SCUM_Item\t2\n");
        Reject<FormatException>(()=>database.ImportKit("Legacy v1",oldKit,true),"An invalid later legacy row rejects the entire database import");
        database.LoadKit(imported,loaded);Check(Snapshot(loaded)==Snapshot(cart),"A failed legacy overwrite preserves the previous database kit");

        FixtureSql(path,"CREATE TRIGGER test_kit_abort BEFORE INSERT ON spawn_kit_entries WHEN NEW.position=1 BEGIN SELECT RAISE(ABORT,'fixture kit interruption'); END");
        cart.SetQuantity(first,9);string before=database.ListKits().Single(saved=>saved.Id==id).UpdatedUtc.ToString("o",CultureInfo.InvariantCulture);
        Reject<IOException>(()=>database.SaveKit(name,cart,true),"A database interruption during the second kit row reports save failure");
        Reject<IOException>(()=>database.SaveKit("Interrupted new kit",cart),"A database interruption during a new kit save reports failure");
        database.LoadKit(id,loaded);
        Check(loaded.Entries[0].Quantity==8 && database.ListKits().Single(saved=>saved.Id==id).UpdatedUtc.ToString("o",CultureInfo.InvariantCulture)==before
            && !database.ListKits().Any(saved=>saved.Name=="Interrupted new kit"),
            "Failed kit transactions roll back deleted rows, name timestamps and newly created kit records");
        FixtureSql(path,"DROP TRIGGER test_kit_abort; CREATE TRIGGER test_cart_abort BEFORE INSERT ON spawn_cart_entries WHEN NEW.position=1 BEGIN SELECT RAISE(ABORT,'fixture cart interruption'); END");
        Reject<IOException>(()=>database.SaveCart(cart),"A working-cart row interruption reports failure");database.LoadCart(loaded,legacy);
        Check(Snapshot(loaded)==expected,"A failed working-cart replacement restores every previously persisted row");
        FixtureSql(path,"DROP TRIGGER test_cart_abort; UPDATE spawn_kit_entries SET modifiers='Uses -1' WHERE kit_id="+id.ToString(CultureInfo.InvariantCulture)+" AND position=1");
        Reject<FormatException>(()=>database.LoadKit(id,loaded),"Invalid modifiers in a later database row reject the complete kit load");
        Check(Snapshot(loaded)==expected,"A corrupt database kit cannot partially replace the target cart");
        FixtureSql(path,"UPDATE spawn_kit_entries SET modifiers='',item_id='Unknown_SCUM_Item' WHERE kit_id="+id.ToString(CultureInfo.InvariantCulture)+" AND position=1");
        Reject<FormatException>(()=>database.LoadKit(id,loaded),"Database rows are revalidated against current canonical SCUM item IDs");
        Check(Snapshot(loaded)==expected,"A missing catalogue ID leaves the target cart unchanged");
        FixtureSql(path,"UPDATE spawn_cart_entries SET position=9 WHERE position=1");
        Reject<FormatException>(()=>database.LoadCart(loaded,legacy),"Noncontiguous stored cart positions are rejected instead of silently reordering entries");
        Check(Snapshot(loaded)==expected,"An invalid saved working cart leaves the current target cart intact");
        FixtureSql(path,"PRAGMA user_version=99");byte[] future=File.ReadAllBytes(path);
        Reject<IOException>(()=>database.SaveCart(cart),"An unsupported future database version stops saving");
        Check(File.ReadAllBytes(path).SequenceEqual(future),"A future-version database remains byte-for-byte unchanged after rejected save");

        string migrationPath=ProfileCartPath(directory,"database-migration.tsv");cart.Save(migrationPath);byte[] originalLegacy=File.ReadAllBytes(migrationPath);
        var migration=new SpawnKitDatabase(ProfileDatabasePath(migrationPath),catalog);var migrated=new SpawnCart(catalog);migration.LoadCart(migrated,migrationPath);
        Check(Snapshot(migrated)==Snapshot(cart) && File.ReadAllBytes(migrationPath).SequenceEqual(originalLegacy),
            "First database use migrates the legacy profile cart without removing or rewriting its source TSV");
        migrated.Clear();migration.SaveCart(migrated);migration.LoadCart(migrated,migrationPath);
        Check(migrated.Entries.Count==0,"An intentionally emptied database cart cannot resurrect entries from its retained legacy TSV");
        string invalidLegacy=ProfileCartPath(directory,"database-invalid-migration.tsv");
        File.WriteAllText(invalidLegacy,first+"\t1\nUnknown_SCUM_Item\t2\n");var invalidMigration=new SpawnKitDatabase(ProfileDatabasePath(invalidLegacy),catalog);
        migrated.Add(third,9);string held=Snapshot(migrated);
        Reject<FormatException>(()=>invalidMigration.LoadCart(migrated,invalidLegacy),"Invalid legacy profile migration stops before initializing the database cart");
        Check(Snapshot(migrated)==held && File.Exists(invalidLegacy),"A failed migration preserves the active cart and legacy source for correction");
        File.WriteAllText(invalidLegacy,first+"\t2\n");invalidMigration.LoadCart(migrated,invalidLegacy);
        Check(migrated.BuildCommands().Single()=="#SpawnItem "+first+" 2","Corrected legacy migration can be retried because failure did not commit its initialization marker");

        string commitLegacy=ProfileCartPath(directory,"database-commit-migration.tsv");File.WriteAllText(commitLegacy,first+"\t2\n");
        var commitMigration=new SpawnKitDatabase(ProfileDatabasePath(commitLegacy),catalog);commitMigration.ListKits();
        FixtureSql(commitMigration.DatabasePath,"CREATE TABLE fixture_parent(id INTEGER PRIMARY KEY); CREATE TABLE fixture_child(parent_id INTEGER REFERENCES fixture_parent(id) DEFERRABLE INITIALLY DEFERRED); CREATE TRIGGER test_commit_abort AFTER INSERT ON spawn_meta WHEN NEW.key='cart_initialized' BEGIN INSERT INTO fixture_child(parent_id) VALUES(999); END");
        migrated.Clear();migrated.Add(third,9);held=Snapshot(migrated);
        Reject<IOException>(()=>commitMigration.LoadCart(migrated,commitLegacy),"A deferred constraint can stop a migration at transaction commit");
        Check(Snapshot(migrated)==held,"Even a commit-time migration failure cannot replace the in-memory cart before persistence succeeds");
        FixtureSql(commitMigration.DatabasePath,"DROP TRIGGER test_commit_abort");commitMigration.LoadCart(migrated,commitLegacy);
        Check(migrated.BuildCommands().Single()=="#SpawnItem "+first+" 2","A commit-time rollback also leaves legacy migration available for retry");
    }

    static void TestModifiers(SpawnItemCatalog catalog,string directory) {
        Check(SpawnModifiers.PropertyNames.IsReadOnly && SpawnModifiers.PropertyNames.SequenceEqual(new[]{"Health","Uses","Dirtiness","Location","KeyCardSector","StackCount","Radiation","Weight"}),
            "Spawn modifiers expose the eight verified property names in a stable read-only order");
        Check(SpawnModifiers.Normalize(null)=="" && SpawnModifiers.Normalize("")=="" && SpawnModifiers.Normalize("   ")=="",
            "Blank modifier text uses all game defaults");
        string mixed="weight +00.500 uses 0 StackCount 01 radiation -0.25 KeyCardSector d4 Health 100.00 dirtiness 0";
        string canonical="Health 100 Uses 0 Dirtiness 0 KeyCardSector D4 StackCount 1 Radiation -0.25 Weight 0.5";
        Check(SpawnModifiers.Normalize(mixed)==canonical,"Modifier names, values and ordering normalize without losing zero or signed numeric overrides");
        var values=SpawnModifiers.Parse(mixed);
        Check(values.IsReadOnly && values["Health"]=="100" && values["Uses"]=="0" && values["KeyCardSector"]=="D4" && values["Weight"]=="0.5",
            "Parsed modifiers return immutable canonical property/value pairs");
        foreach(string property in SpawnModifiers.PropertyNames)Check(!string.IsNullOrWhiteSpace(SpawnModifiers.GetHint(property)),"Verified modifier has a source hint: "+property);
        Reject<FormatException>(()=>SpawnModifiers.GetHint("Unknown"),"Unknown properties cannot acquire misleading source hints");
        foreach(string property in new[]{"Health","Uses","Dirtiness","Radiation","Weight"})
            Check(SpawnModifiers.Normalize(property+" 0")==property+" 0","Zero is retained as an explicit value for "+property);
        Check(SpawnModifiers.Normalize("StackCount 2147483647 Uses 2147483647")=="Uses 2147483647 StackCount 2147483647",
            "Integer modifiers support representable values without inventing a smaller item-specific maximum");
        Check(SpawnModifiers.Normalize("Health .5 Dirtiness 100.0 Radiation -12.5 Weight +12.5")=="Health 0.5 Dirtiness 100 Radiation -12.5 Weight 12.5",
            "Decimal modifiers use invariant values and only documented Health/Dirtiness ranges");
        foreach(char letter in new[]{'A','B','C','D'})for(int sector=1;sector<=4;sector++)
            Check(SpawnModifiers.Normalize("KeyCardSector "+char.ToLowerInvariant(letter)+sector)=="KeyCardSector "+letter+sector,
                "Documented keycard sector is accepted and normalized: "+letter+sector);
        var previousCulture=Thread.CurrentThread.CurrentCulture;
        try {
            Thread.CurrentThread.CurrentCulture=new CultureInfo("fr-FR");
            Check(SpawnModifiers.Normalize("Weight 0.5 Health 50.5")=="Health 50.5 Weight 0.5",
                "Modifier parsing and commands retain decimal points under a comma-decimal locale");
        }finally { Thread.CurrentThread.CurrentCulture=previousCulture; }
        foreach(string invalid in new[]{
            "Health","Weight 0.5 Uses","Unknown 1","Location 1","Health 1 health 2","Health -0.1","Health 100.1","Dirtiness -1","Dirtiness 101",
            "Uses -1","Uses +1","Uses 1.5","Uses 2147483648","StackCount 0","StackCount -1","StackCount 1.5","StackCount 2147483648",
            "Weight NaN","Weight Infinity","Weight 1e3","Weight 1,5","Weight .","Weight +","Weight 1..2","Weight １２","Weight 999999999999999999999999999999999",
            "Radiation NaN","Radiation 1e-3","KeyCardSector A0","KeyCardSector A5","KeyCardSector E1","KeyCardSector A11",
            "Uses 0;#Kick","Weight 1 #Kick","Weight 1\n#Kick","Health\t100","Health 100\r","Weight 1\u0001",new string(' ',513)
        })Reject<FormatException>(()=>SpawnModifiers.Normalize(invalid),"Invalid, unknown, repeated or command-breaking modifiers are rejected");

        string first=catalog.Items[0].Id,second=catalog.Items[1].Id;
        var cart=new SpawnCart(catalog);cart.Add(first,2);cart.Add(second,1);cart.SetModifiers(first.ToUpperInvariant(),mixed);
        Check(cart.Entries[0].ModifiersText==canonical && cart.BuildCommands()[0]=="#SpawnItem "+first+" 2 "+canonical,
            "Per-item overrides append canonical property/value pairs after the spawn quantity");
        cart.SetQuantity(first,4);cart.Add(first,1);cart.AddMany(new[]{second},1);
        Check(cart.Entries[0].Quantity==5 && cart.Entries[0].ModifiersText==canonical && cart.Entries[1].ModifiersText=="",
            "Quantity edits and merged additions preserve one item's modifiers without leaking them into another item");
        string before=Snapshot(cart);
        Reject<FormatException>(()=>cart.SetModifiers(first,"Health 101"),"An invalid modifier edit fails closed");
        Reject<FormatException>(()=>cart.SetModifiers("UnknownItem","Weight 1"),"Modifier editing cannot create an unknown cart item");
        Reject<FormatException>(()=>cart.AddMany(new[]{first,"UnknownItem"},1),"A failed multi-add still rolls back after modifiers exist");
        Check(Snapshot(cart)==before,"Invalid modifier edits and failed multi-adds preserve quantities and overrides atomically");
        var commands=cart.BuildCommands();cart.SetModifiers(first,"Uses 0 Weight 0");
        Check(commands[0].EndsWith(canonical) && cart.Entries[0].ModifiersText=="Uses 0 Weight 0","Submitted command snapshots retain their original modifiers while the editable cart changes");
        cart.SetModifiers(first,"");Check(cart.Entries[0].ModifiersText=="" && cart.BuildCommands()[0]=="#SpawnItem "+first+" 5",
            "Resetting modifiers removes their command arguments and restores game defaults");
        cart.SetModifiers(first,mixed);
        string path=Path.Combine(directory,"modifier-kit.tsv"),badPath=Path.Combine(directory,"invalid-modifier-kit.tsv");cart.Save(path);
        var reloaded=new SpawnCart(catalog);reloaded.Load(path);
        Check(Snapshot(reloaded)==Snapshot(cart) && File.ReadAllText(path).Contains("spawn kit v2"),"Version-two kits round-trip normalized modifiers with IDs and quantities");
        File.WriteAllText(path,"# SCUM MiniMap spawn kit v1\n"+first+"\t3\n"+second+"\t4\n");reloaded.Load(path);
        Check(reloaded.TotalQuantity==7 && reloaded.Entries.All(entry=>entry.ModifiersText==""),"Legacy two-column kits remain compatible and use game-default modifiers");
        File.WriteAllText(path,first+"\t2\tWeight +00.500 Uses 0\n"+second+"\t1\t\n");reloaded.Load(path);
        Check(reloaded.Entries[0].ModifiersText=="Uses 0 Weight 0.5" && reloaded.Entries[1].ModifiersText=="",
            "Imported three-column kits normalize property values and retain blank defaults");
        before=Snapshot(reloaded);
        foreach(string content in new[]{
            first+"\t1\tHealth 100\n"+second+"\t1\tUses -1",
            first+"\t1\tHealth 100\n"+second+"\t1\tStackCount 0",
            first+"\t1\tHealth 100\nUnknownItem\t1\tWeight 1",
            first+"\t1\tHealth 100\n"+second+"\t1\tUses 1 Uses 2",
            first+"\t1\tHealth 100\n"+second+"\t1\tUnknown 1",
            first+"\t1\tHealth 100\n"+second+"\t1\tWeight 1;#Kick",
            first+"\t1\tHealth 100\textra"
        }) {
            File.WriteAllText(badPath,content);Reject<FormatException>(()=>reloaded.Load(badPath),"Malformed or invalid modifier kits are rejected as a complete import");
            Check(Snapshot(reloaded)==before,"A modifier error in a later kit row preserves the entire previous cart");
        }
        cart.ConsumeSubmitted(1);Check(cart.Entries.Count==1 && cart.Entries[0].Item.Id==second && cart.Entries[0].ModifiersText=="",
            "Consuming a modified submitted item leaves the next item's own defaults intact");
    }

    static void TestLocationModifiers(SpawnItemCatalog catalog,string directory) {
        string[] input={
            "\"x=-001.50 y=+02.000 z=.5\"",
            "\"{x=-001.50 y=+02.000 z=.5|p=-00.25 y=+90.000 r=0.0}\"",
            "76561198000000000",
            "-001.50 +02.000 .5"
        };
        string[] canonical={
            "\"X=-1.5 Y=2 Z=0.5\"",
            "\"{X=-1.5 Y=2 Z=0.5|P=-0.25 Y=90 R=0}\"",
            "76561198000000000",
            "-1.5 2 0.5"
        };
        for(int i=0;i<input.Length;i++) {
            var parsed=SpawnModifiers.Parse("Weight +00.500 Location "+input[i]+" Uses 0");
            string expected="Uses 0 Location "+canonical[i]+" Weight 0.5";
            Check(parsed.Count==3 && parsed["Location"]==canonical[i] && parsed["Uses"]=="0" && parsed["Weight"]=="0.5",
                "Location format retains its whole value and following properties: "+i);
            Check(SpawnModifiers.Normalize("Weight +00.500 Location "+input[i]+" Uses 0")==expected
                && SpawnModifiers.Normalize(expected)==expected,"Each documented Location format normalizes and reparses without losing quotes: "+i);
        }
        Check(SpawnModifiers.NormalizeLocation("x=-001.50 y=+02.000 z=.5")==canonical[0]
            && SpawnModifiers.NormalizeLocation("{x=-001.50 y=+02.000 z=.5|p=-00.25 y=+90.000 r=0.0}")==canonical[1],
            "The Location editor can quote a pasted keyed vector or full copied transform safely");
        Check(SpawnModifiers.Normalize("Location 0 0 0 Dirtiness 0")=="Dirtiness 0 Location 0 0 0",
            "Zero coordinates remain explicit and Location follows Dirtiness in canonical commands");
        Check(SpawnModifiers.Normalize("Location 11111111111111111 0 0 Weight 1")=="Location 11111111111111111 0 0 Weight 1"
            && SpawnModifiers.Normalize("Location 76561198000000000 Weight 1")=="Location 76561198000000000 Weight 1",
            "A seventeen-digit X coordinate with two numeric axes stays a triple while a standalone SteamID leaves following properties intact");
        var previousCulture=Thread.CurrentThread.CurrentCulture;
        try {
            Thread.CurrentThread.CurrentCulture=new CultureInfo("fr-FR");
            Check(SpawnModifiers.Normalize("Location "+input[1]+" Weight 0.5")=="Location "+canonical[1]+" Weight 0.5",
                "Location axes and scalar modifiers keep decimal points under a comma-decimal locale");
        }finally { Thread.CurrentThread.CurrentCulture=previousCulture; }
        foreach(string invalid in new[]{
            "Location","Location 1","Location 1 2","Location 1 2 3 4","Location 1 2 Uses 0",
            "Location X=1 Y=2 Z=3","Location {X=1 Y=2 Z=3|P=0 Y=0 R=0}",
            "Location \"76561198000000000\"","Location \"1 2 3\"","Location 7656119800000000","Location 765611980000000000",
            "Location ７６５６１１９８０００００００００","Location +76561198000000000",
            "Location \"X=1 Y=2 Z=3","Location \"X=1 Y=2 Z=3\"Weight 1","Location \"X=1 Y=2 Z=3\"\"",
            "Location \"Y=2 X=1 Z=3\"","Location \"X=1 Y=2\"","Location \"X=1 Y=2 Z=3 W=4\"",
            "Location \"{X=1 Y=2 Z=3 P=0 Y=0 R=0}\"","Location \"{X=1 Y=2 Z=3|P=0 Y=0}\"",
            "Location \"X=1e3 Y=2 Z=3\"","Location \"X=NaN Y=2 Z=3\"","Location \"X=1 Y=Infinity Z=3\"",
            "Location \"X=1,5 Y=2 Z=3\"","Location 1 2 NaN","Location 1e3 2 3",
            "Location \"X=1 Y=2 Z=3;#Kick\"","Location \"X=1 Y=2 Z=3\\\"",
            "Location \"X=1 Y=2\nZ=3\"","Location 1\t2 3","Location 1 2 3 Location 4 5 6",
            "\"Location\" 1 2 3","Weight \"0.5\"","Uses \"0\""
        })Reject<FormatException>(()=>SpawnModifiers.Normalize(invalid),"Malformed or command-breaking Location/property quoting fails closed");
        foreach(string invalid in new[]{null,"","1 2","\"1 2 3\"","\"76561198000000000\"","X=1 Y=2 Z=3;#Kick","X=1\tY=2 Z=3"})
            Reject<FormatException>(()=>SpawnModifiers.NormalizeLocation(invalid),"The Location field rejects an incomplete or unsafe pasted value");

        var cart=new SpawnCart(catalog);
        for(int i=0;i<canonical.Length;i++) {
            string id=catalog.Items[i].Id;cart.Add(id,i+1);cart.SetModifiers(id,"Uses 0 Location "+input[i]+" Weight 0.5");
        }
        string path=Path.Combine(directory,"location-kit.tsv"),badPath=Path.Combine(directory,"invalid-location-kit.tsv");
        cart.Save(path);var reloaded=new SpawnCart(catalog);reloaded.Load(path);
        Check(Snapshot(reloaded)==Snapshot(cart) && reloaded.Entries.Select(entry=>SpawnModifiers.Parse(entry.ModifiersText)["Location"]).SequenceEqual(canonical),
            "All four Location formats round-trip in one kit with quoted spaces and later modifiers intact");
        string first=catalog.Items[0].Id;string preserved=cart.Entries[0].ModifiersText;
        cart.SetQuantity(first,7);cart.AddMany(new[]{first,catalog.Items[1].Id},1);
        Check(cart.Entries[0].Quantity==8 && cart.Entries[0].ModifiersText==preserved
            && cart.Entries[1].ModifiersText==reloaded.Entries[1].ModifiersText,
            "Quantity edits and multi-add merges preserve each item's own quoted Location");
        string before=Snapshot(reloaded);
        foreach(string malformed in new[]{"Location \"X=1 Y=2 Z=3","Location \"X=1 Y=2 Z=3;#Kick\"","Location 1 2","Location X=1 Y=2 Z=3"}) {
            File.WriteAllText(badPath,first+"\t1\tLocation "+canonical[2]+"\n"+catalog.Items[1].Id+"\t1\t"+malformed);
            Reject<FormatException>(()=>reloaded.Load(badPath),"A malformed Location after a valid kit row rejects the whole import");
            Check(Snapshot(reloaded)==before,"Failed Location kit import preserves the complete previously loaded cart");
        }
        Reject<FormatException>(()=>cart.SetModifiers(first,"Location \"X=1 Y=2 Z=3;#Kick\""),"An unsafe Location edit cannot replace a valid cart entry");
        Check(cart.Entries[0].ModifiersText==preserved,"Rejected Location edits preserve the previous coordinates and other modifiers");

        string longId=new string('A',128),large="-79228162514264337593543950335";
        var lengthCart=new SpawnCart(Parse(Row(longId,"Long command fixture","test")));lengthCart.Add(longId,100);
        string longModifiers="Health 100 Uses 2147483647 Dirtiness 100 Location \"{X="+large+" Y="+large+" Z="+large+
            "|P="+large+" Y="+large+" R="+large+"}\" KeyCardSector D4 StackCount 2147483647 Radiation "+large+" Weight "+large;
        Check(SpawnModifiers.Normalize(longModifiers).Length<=512,"A valid large copied transform can fit within modifier-text capacity");
        Reject<FormatException>(()=>lengthCart.SetModifiers(longId,longModifiers),"The full spawn command length includes the item ID, quantity and copied transform");
        Check(lengthCart.Entries[0].ModifiersText=="" && lengthCart.BuildCommands()[0]=="#SpawnItem "+longId+" 100",
            "A command-length failure preserves the previous quantity and defaults atomically");
        lengthCart.SetQuantity(longId,1);lengthCart.SetModifiers(longId,longModifiers);string lengthBefore=Snapshot(lengthCart);
        Reject<FormatException>(()=>lengthCart.SetQuantity(longId,100),"Increasing quantity cannot make a valid long Location command exceed the chat limit");
        Check(Snapshot(lengthCart)==lengthBefore && lengthCart.Entries[0].Quantity==1,
            "A quantity edit that exceeds command length preserves the whole valid Location entry");
    }

    static void TestModifierPatches(SpawnItemCatalog catalog,string directory) {
        string first=catalog.Items[0].Id,second=catalog.Items[1].Id,third=catalog.Items[2].Id;
        var cart=new SpawnCart(catalog);cart.Add(first,2);cart.Add(second,3);cart.Add(third,4);
        cart.SetModifiers(first,"Health 100 Uses 5 Location \"X=-1.5 Y=2 Z=0.5\" Weight 0.5");
        cart.SetModifiers(second,"Health 100 Uses 0 Radiation 1 Weight 2");
        cart.SetModifiers(third,"Health 50 Location \"{X=-1 Y=2 Z=3|P=0 Y=90 R=0}\" Weight -1");
        string unselected=cart.Entries[2].Command;var order=cart.Entries.Select(entry=>entry.Item.Id).ToList();
        cart.PatchModifiers(new[]{first.ToUpperInvariant(),first,second},new Dictionary<string,string> { {"weight","1"},{"dirtiness","0"} });
        Check(cart.Entries[0].ModifiersText=="Health 100 Uses 5 Dirtiness 0 Location \"X=-1.5 Y=2 Z=0.5\" Weight 1"
            && cart.Entries[1].ModifiersText=="Health 100 Uses 0 Dirtiness 0 Radiation 1 Weight 1",
            "A bulk patch changes selected properties and preserves each selected item's distinct untouched modifiers");
        Check(cart.Entries[2].Command==unselected && cart.Entries.Select(entry=>entry.Item.Id).SequenceEqual(order)
            && cart.Entries.Select(entry=>entry.Quantity).SequenceEqual(new[]{2,3,4}),
            "Case-insensitive duplicate selection leaves quantities, insertion order and unselected items intact");
        cart.PatchModifiers(new[]{first,second},new Dictionary<string,string> {
            {"Uses",null},{"Location","\"{X=-1.5 Y=2 Z=0.5|P=-0.25 Y=90 R=0}\""}
        });
        Check(cart.Entries.Take(2).All(entry=>!SpawnModifiers.Parse(entry.ModifiersText).ContainsKey("Uses")
            && SpawnModifiers.Parse(entry.ModifiersText)["Location"]=="\"{X=-1.5 Y=2 Z=0.5|P=-0.25 Y=90 R=0}\"")
            && cart.Entries[0].ModifiersText.Contains("Dirtiness 0") && cart.Entries[1].ModifiersText.Contains("Radiation 1"),
            "Null bulk values remove a property while a quoted Location is applied to every selected item without losing their other overrides");
        string path=Path.Combine(directory,"bulk-modifier-kit.tsv");cart.Save(path);var persisted=new SpawnCart(catalog);persisted.Load(path);
        Check(Snapshot(persisted)==Snapshot(cart),"Bulk edits persist as complete per-item modifier values and reopen without losing quoted Location");
        string before=Snapshot(cart);
        Reject<ArgumentNullException>(()=>cart.PatchModifiers(null,new Dictionary<string,string>()),"Bulk modifier patch requires a selected-ID collection");
        Reject<ArgumentNullException>(()=>cart.PatchModifiers(new[]{first},null),"Bulk modifier patch requires a change collection");
        foreach(string missing in new[]{"UnknownItem",catalog.Items[3].Id}) {
            Reject<FormatException>(()=>cart.PatchModifiers(new[]{first,missing},new Dictionary<string,string> { {"Weight","0"} }),
                "A bulk selection containing an unknown or absent cart item fails atomically");
            Check(Snapshot(cart)==before,"A later invalid selected ID does not change an earlier valid row");
        }
        foreach(var invalid in new[]{
            new Dictionary<string,string> { {"Unknown","1"} },
            new Dictionary<string,string> { {"Unknown",null} },
            new Dictionary<string,string> { {"Weight","1"},{"Health","101"} },
            new Dictionary<string,string> { {"Uses",""} },
            new Dictionary<string,string> { {"Weight","1 Uses 0"} },
            new Dictionary<string,string> { {"Location","1 2 3 Uses 0"} },
            new Dictionary<string,string> { {"Location","X=1 Y=2 Z=3"} },
            new Dictionary<string,string> { {"Location","\"X=1 Y=2 Z=3;#Kick\""} },
            new Dictionary<string,string> { {"Uses","0\n#Kick"} },
            new Dictionary<string,string>(StringComparer.Ordinal) { {"Weight","1"},{"weight","2"} }
        }) {
            Reject<FormatException>(()=>cart.PatchModifiers(new[]{first,second},invalid),"Bulk patches reject invalid values, duplicate names and extra property or command injection");
            Check(Snapshot(cart)==before,"Rejected bulk modifier changes preserve every selected and unselected entry");
        }
        var reset=SpawnModifiers.PropertyNames.ToDictionary(property=>property,property=>(string)null);
        cart.PatchModifiers(new[]{first,second},reset);
        Check(cart.Entries.Take(2).All(entry=>entry.ModifiersText=="") && cart.Entries[2].Command==unselected
            && cart.Entries.Select(entry=>entry.Quantity).SequenceEqual(new[]{2,3,4}),
            "Bulk reset restores game defaults only for selected items and preserves all quantities");

        string longId=new string('A',128),large="-79228162514264337593543950335";
        var lengthCart=new SpawnCart(Parse(Row("ShortItem","A short item","test")+"\n"+Row(longId,"A long item","test")+"\n"+Row("UntouchedItem","An untouched item","test")));
        lengthCart.Add("ShortItem",1);lengthCart.Add(longId,100);lengthCart.Add("UntouchedItem",2);
        lengthCart.SetModifiers("ShortItem","Weight 1");lengthCart.SetModifiers("UntouchedItem","Uses 0");before=Snapshot(lengthCart);
        string oversized="Health 100 Uses 2147483647 Dirtiness 100 Location \"{X="+large+" Y="+large+" Z="+large+
            "|P="+large+" Y="+large+" R="+large+"}\" KeyCardSector D4 StackCount 2147483647 Radiation "+large+" Weight "+large;
        Reject<FormatException>(()=>lengthCart.PatchModifiers(new[]{"ShortItem",longId},SpawnModifiers.Parse(oversized)),
            "A later selected item whose full patched command exceeds 512 characters rejects the whole bulk edit");
        Check(Snapshot(lengthCart)==before,"A later command-length failure preserves the earlier item and every other cart entry atomically");
    }

    static void TestBatch() {
        var events=new List<string>();var progress=new List<int>();
        var result=Native.AdminChatBatchSequence(3,index=> { events.Add("prepare"+index);return true; },
            (index,submitted)=> { events.Add("send"+index);submitted();submitted();return Task.FromResult(true); },
            milliseconds=> { events.Add("delay"+milliseconds);return Task.FromResult(0); },()=>true,
            count=> { progress.Add(count);events.Add("submit"+count); }).GetAwaiter().GetResult();
        Check(result.Completed && result.ChatClosed && result.SubmittedCount==3 && string.IsNullOrEmpty(result.Error),
            "An ordered batch completes only after every command is dispatched and chat cleanup succeeds");
        Check(events.SequenceEqual(new[]{"prepare0","send0","submit1","delay60","prepare1","send1","submit2","delay60","prepare2","send2","submit3"}),
            "Each command prepares its clipboard, sends once and leaves a 60 ms gap before the next command");
        Check(progress.SequenceEqual(new[]{1,2,3}),"Repeated submission callbacks cannot count or consume the same command twice");

        int prepared=0,sent=0;
        foreach(int empty in new[]{0,-1}) {
            result=Native.AdminChatBatchSequence(empty,index=> { prepared++;return true; },
                (index,submitted)=> { sent++;submitted();return Task.FromResult(true); },milliseconds=>Task.FromResult(0),()=>true).GetAwaiter().GetResult();
            Check(!result.Completed && result.SubmittedCount==0 && prepared==0 && sent==0 && !string.IsNullOrEmpty(result.Error),
                "Empty or invalid batches stop before clipboard preparation and input dispatch");
        }
        result=Native.AdminChatBatchSequence(3,index=> { prepared++;return true; },
            (index,submitted)=> { sent++;submitted();return Task.FromResult(true); },milliseconds=>Task.FromResult(0),()=>false).GetAwaiter().GetResult();
        Check(prepared==0 && sent==0 && result.SubmittedCount==0 && !result.Completed,"An initial input or focus gate prevents every clipboard and key action");
        result=Native.AdminChatBatchSequence(3,index=>false,
            (index,submitted)=> { sent++;submitted();return Task.FromResult(true); },milliseconds=>Task.FromResult(0),()=>true).GetAwaiter().GetResult();
        Check(sent==0 && result.SubmittedCount==0 && !result.Completed,"Failed clipboard ownership blocks command submission");

        bool allowed=true;prepared=sent=0;
        result=Native.AdminChatBatchSequence(3,index=> { prepared++;return true; },
            (index,submitted)=> { sent++;submitted();return Task.FromResult(true); },
            milliseconds=> { allowed=false;return Task.FromResult(0); },()=>allowed).GetAwaiter().GetResult();
        Check(result.SubmittedCount==1 && !result.Completed && result.ChatClosed && prepared==1 && sent==1,
            "Input or focus changes during pacing stop before preparing the next command");
        prepared=sent=0;
        result=Native.AdminChatBatchSequence(3,index=> { prepared++;return index==0; },
            (index,submitted)=> { sent++;submitted();return Task.FromResult(true); },milliseconds=>Task.FromResult(0),()=>true).GetAwaiter().GetResult();
        Check(result.SubmittedCount==1 && !result.Completed && prepared==2 && sent==1,"Clipboard replacement between commands retains only the submitted prefix");
        sent=0;
        result=Native.AdminChatBatchSequence(3,index=>true,
            (index,submitted)=> { sent++;return Task.FromResult(true); },milliseconds=>Task.FromResult(0),()=>true).GetAwaiter().GetResult();
        Check(result.SubmittedCount==0 && !result.Completed && sent==1,"A sender cannot report batch completion without an accepted Enter dispatch");
        sent=0;
        result=Native.AdminChatBatchSequence(3,index=>true,
            (index,submitted)=> { sent++;return Task.FromResult(false); },milliseconds=>Task.FromResult(0),()=>true).GetAwaiter().GetResult();
        Check(result.SubmittedCount==0 && !result.Completed && !result.ChatClosed && sent==1,"Interruption before Enter leaves the current command unsent and stops the batch");
        sent=0;
        result=Native.AdminChatBatchSequence(3,index=>true,
            (index,submitted)=> { sent++;submitted();return Task.FromResult(false); },milliseconds=>Task.FromResult(0),()=>true).GetAwaiter().GetResult();
        Check(result.SubmittedCount==1 && !result.Completed && !result.ChatClosed && sent==1,
            "Enter followed by failed Escape cleanup counts the command once and stops without a retry");
        prepared=sent=0;int callbacks=0;
        result=Native.AdminChatBatchSequence(3,index=> { prepared++;return true; },
            (index,submitted)=> { sent++;submitted();return Task.FromResult(true); },milliseconds=>Task.FromResult(0),()=>true,
            count=> { callbacks++;throw new IOException("Simulated cart save failure after Enter"); }).GetAwaiter().GetResult();
        Check(result.SubmittedCount==1 && !result.Completed && prepared==1 && sent==1 && callbacks==1 && !string.IsNullOrEmpty(result.Error),
            "A persistence callback failure after Enter preserves its submission count and stops before the next command");
    }

    static void TestFocusReadiness() {
        int transitionTime=0,retries=0;
        string transitionGate=Native.AdminFocusReadiness(()=>true,()=>transitionTime>=225,()=>false,()=>false,()=>null,
            ms=> { transitionTime+=ms;return Task.FromResult(0); },()=>transitionTime<225,()=>retries++).GetAwaiter().GetResult();
        Check(transitionGate==null && retries==1,"A temporary empty foreground during owned handoff settles with one pre-dispatch focus retry");
        retries=0;
        transitionGate=Native.AdminFocusReadiness(()=>true,()=>false,()=>false,()=>false,()=>null,ms=>Task.FromResult(0),()=>false,()=>retries++).GetAwaiter().GetResult();
        Check(transitionGate=="unrelated-foreground" && retries==0,"Focus retry never runs after an unrelated app becomes foreground");
        var delays=new List<int>();
        Func<int,Task> record=milliseconds=> { delays.Add(milliseconds);return Task.FromResult(0); };
        string gate=Native.AdminFocusReadiness(()=>true,()=>true,()=>false,()=>false,()=>null,record).GetAwaiter().GetResult();
        Check(gate==null && delays.SequenceEqual(new[]{150}),"Focus readiness accepts the exact target with released keys after the initial settling delay");
        int elapsed=0;delays.Clear();
        Func<int,Task> advance=milliseconds=> { elapsed+=milliseconds;delays.Add(milliseconds);return Task.FromResult(0); };
        gate=Native.AdminFocusReadiness(()=>true,()=>elapsed>=225,()=>true,()=>false,()=>null,advance).GetAwaiter().GetResult();
        Check(gate==null && delays.SequenceEqual(new[]{150,25,25,25}),"A delayed focus handoff is allowed to settle while MiniMap remains foreground");
        elapsed=0;delays.Clear();
        gate=Native.AdminFocusReadiness(()=>true,()=>true,()=>false,()=>elapsed<250,()=>null,advance).GetAwaiter().GetResult();
        Check(gate==null && elapsed==250,"Readiness waits for held keys to be released before accepting the focused target");
        delays.Clear();bool inspected=false;
        gate=Native.AdminFocusReadiness(()=>false,()=> { inspected=true;return true; },()=>true,()=>false,()=>null,record).GetAwaiter().GetResult();
        Check(gate=="target-unavailable" && delays.Count==0 && !inspected,"An invalid original game target stops before waiting or checking focus");
        foreach(string blocked in new[]{"chat","inventory","closing","monitor-unavailable"}) {
            delays.Clear();gate=Native.AdminFocusReadiness(()=>true,()=>true,()=>false,()=>false,()=>blocked,record).GetAwaiter().GetResult();
            Check(gate==blocked && delays.Count==0,"A blocking game/input gate stops focus readiness immediately: "+blocked);
        }
        delays.Clear();
        gate=Native.AdminFocusReadiness(()=>true,()=>false,()=>false,()=>false,()=>null,record).GetAwaiter().GetResult();
        Check(gate=="unrelated-foreground" && delays.SequenceEqual(new[]{150}),"An unrelated foreground application stops readiness without additional waits");
        elapsed=0;delays.Clear();
        gate=Native.AdminFocusReadiness(()=>true,()=>false,()=>elapsed<200,()=>false,()=>null,advance).GetAwaiter().GetResult();
        Check(gate=="unrelated-foreground" && elapsed==200,"Switching away during a pending handoff stops readiness at the first observation");
        elapsed=0;delays.Clear();
        gate=Native.AdminFocusReadiness(()=>elapsed<150,()=>false,()=>true,()=>false,()=>null,advance).GetAwaiter().GetResult();
        Check(gate=="target-unavailable" && elapsed==150,"A disappearing original target is rejected after the initial settling delay");
        elapsed=0;delays.Clear();
        gate=Native.AdminFocusReadiness(()=>true,()=>false,()=>true,()=>false,()=>elapsed>=200?"chat":null,advance).GetAwaiter().GetResult();
        Check(gate=="chat" && elapsed==200,"Chat opening during a pending focus handoff stops readiness before dispatch");
        elapsed=0;delays.Clear();
        gate=Native.AdminFocusReadiness(()=>true,()=>false,()=>true,()=>false,()=>null,advance).GetAwaiter().GetResult();
        Check(gate=="focus-handoff" && elapsed==650 && delays.Count==21 && delays.Skip(1).All(milliseconds=>milliseconds==25),
            "A stalled handoff has a bounded 150 ms settling delay plus 500 ms retry window");
        elapsed=0;delays.Clear();
        gate=Native.AdminFocusReadiness(()=>true,()=>true,()=>false,()=>true,()=>null,advance).GetAwaiter().GetResult();
        Check(gate=="held-keys" && elapsed==650,"Persistently held keys stop after the bounded readiness window");
    }

    static void TestClipboardPublication() {
        bool locked=false;uint current=40;var events=new List<string>();
        Func<bool> open=()=> { locked=true;events.Add("open");return true; };
        Func<bool> close=()=> { events.Add("close");locked=false;current=44;return true; };
        Func<uint> readSequence=()=> { events.Add("sequence:"+current);return current; };
        Func<bool> verify=()=> { Check(locked,"Clipboard text verification runs inside the simulated lock");events.Add("verify");if(current<43)current=43;return true; };
        var result=Native.AdminClipboardPublication(40,open,close,readSequence,
            ()=> { Check(locked,"Clipboard publication empties only while the simulated lock is held");events.Add("empty");current=41;return true; },
            ()=> { Check(locked,"Clipboard publication writes only while the simulated lock is held");events.Add("write");current=42;return true; },
            verify,()=>true);
        Check(result.Ready && result.Sequence==43 && current==44,"Publication retains its locked verification result until an explicit post-close finalization");
        result=Native.AdminClipboardFinalization(result,open,close,readSequence,()=>true,verify,()=>null,milliseconds=>Task.FromResult(0)).GetAwaiter().GetResult();
        Check(result.Ready && result.Sequence==44 && result.StopReason==null && !locked && current==44
            && events.LastIndexOf("sequence:44")>events.IndexOf("close") && events.Count(entry=>entry=="empty")==1 && events.Count(entry=>entry=="write")==1,
            "Clipboard publication validates the final lease after CloseClipboard advances its sequence without republishing the command");
        foreach(uint expected in new uint[]{0,39}) {
            int emptied=0,written=0,closed=0;
            result=Native.AdminClipboardPublication(expected,()=>true,()=> { closed++;return true; },()=>40,
                ()=> { emptied++;return true; },()=> { written++;return true; },()=>true,()=>true);
            Check(!result.Ready && result.StopReason=="clipboard-changed" && result.Sequence==0 && emptied==0 && written==0 && closed==1,
                "A missing or replaced expected clipboard sequence blocks mutation but still closes an acquired lock");
        }
        int acquiredClose=0,mutations=0;
        result=Native.AdminClipboardPublication(40,()=>false,()=> { acquiredClose++;return true; },()=>40,
            ()=> { mutations++;return true; },()=>true,()=>true,()=>true);
        Check(!result.Ready && result.StopReason=="clipboard-busy" && acquiredClose==0 && mutations==0,
            "A contended clipboard returns before mutation without closing a lock it never acquired");
        result=Native.AdminClipboardPublication(40,()=>true,()=> { acquiredClose++;return true; },()=>40,
            ()=> { mutations++;return true; },()=>true,()=>true,()=>false,true);
        Check(!result.Ready && result.StopReason=="clipboard-owner" && result.Sequence==0 && mutations==0 && acquiredClose==1,
            "Replacing a command clipboard requires the original owner before any empty or write operation");
        foreach(string failure in new[]{"empty","write","verify","owner","exception","close","zero"}) {
            uint sequence=30;bool owned=true;int closed=0,emptied=0,written=0,verified=0;
            result=Native.AdminClipboardPublication(30,()=>true,()=> { closed++;return failure!="close"; },()=>sequence,
                ()=> { emptied++;if(failure=="empty")return false;sequence=31;return true; },
                ()=> { written++;if(failure=="write")return false;sequence=32;return true; },
                ()=> { verified++;if(failure=="exception")throw new InvalidOperationException("Simulated verification error");
                    if(failure=="verify")return false;if(failure=="owner")owned=false;sequence=failure=="zero"?0u:33u;return true; },()=>owned);
            uint expectedSequence=failure=="empty" || failure=="owner" || failure=="zero"?0u:failure=="write"?31u:failure=="verify" || failure=="exception"?32u:33u;
            string expectedReason=failure=="empty" || failure=="write" || failure=="close"?"clipboard-publication":failure=="owner"?"clipboard-owner":"clipboard-verification";
            Check(!result.Ready && result.Sequence==expectedSequence && result.StopReason==expectedReason && closed==1,
                "Failed clipboard publication retains only a sequence still owned for safe restoration, and always closes: "+failure);
            if(failure=="empty")Check(emptied==1 && written==0 && verified==0,"A failed EmptyClipboard never advances to a write or verification");
            if(failure=="write")Check(written==1 && verified==0,"A failed clipboard write cannot advance to command verification");
            if(failure=="exception")Check(result.NativeError!=0,"Verification exceptions retain diagnostic error metadata without claiming readiness");
        }
    }

    static void TestClipboardAfterClose() {
        Check(Native.AdminClipboardTextSupported("#SpawnItem Weapon_AK47 2 Health 100 Location \"X=-1.5 Y=2 Z=0.5\"")
            && Native.AdminClipboardTextSupported("#"+new string('A',511)),
            "Clipboard commands retain exact text across Unicode, ANSI and OEM formats within the 512-character capacity");
        foreach(string unsupported in new[]{null,"","#SpawnItem Weapon_AK47 1\0","#SpawnItem é 1","#SpawnItem 😀 1","#"+new string('A',512)})
            Check(!Native.AdminClipboardTextSupported(unsupported),"Clipboard publication rejects text that cannot preserve its exact command across materialized formats");
        foreach(string replacement in new[]{"owner","text","final-owner","final-text"}) {
            uint sequence=20;bool owned=true,locked=false;string text="original";int opened=0,closed=0,emptied=0,written=0,verified=0;
            Func<bool> open=()=> { opened++;locked=true;return true; };
            Func<bool> close=()=> {
                closed++;locked=false;if(closed==1)sequence++;
                if(closed==(replacement.StartsWith("final-",StringComparison.Ordinal)?2:1)) {
                    sequence++;if(replacement.EndsWith("owner",StringComparison.Ordinal))owned=false;else text="external replacement";
                }return true;
            };
            Func<bool> verify=()=> { verified++;if(!locked)throw new Exception("Verification must hold the clipboard lock.");return text=="command"; };
            var publication=Native.AdminClipboardPublication(20,open,close,()=>sequence,()=> { emptied++;sequence++;text="";return true; },
                ()=> { written++;sequence++;text="command";return true; },verify,()=>owned);
            var result=Native.AdminClipboardFinalization(publication,open,close,()=>sequence,()=>owned,verify,()=>null,milliseconds=>Task.FromResult(0)).GetAwaiter().GetResult();
            Check(!result.Ready && result.Sequence==0 && result.StopReason!=null && !locked && emptied==1 && written==1 && closed>=1,
                "An owner or exact-data replacement after CloseClipboard is rejected without republishing: "+replacement);
            if(replacement.EndsWith("text",StringComparison.Ordinal))Check(opened>=2 && verified>=2,"A same-owner sequence change requires reopening and verifying the command text after close");
        }
        {
            uint sequence=60;bool locked=false;int opened=0,closed=0,emptied=0,written=0;
            Func<bool> open=()=> { opened++;if(opened>1)return false;locked=true;return true; };
            Func<bool> close=()=> { closed++;locked=false;sequence++;return true; };
            var publication=Native.AdminClipboardPublication(60,open,close,()=>sequence,()=> { emptied++;sequence++;return true; },
                ()=> { written++;sequence++;return true; },()=>locked,()=>true);
            var result=Native.AdminClipboardFinalization(publication,open,close,()=>sequence,()=>true,()=>locked,()=>null,milliseconds=>Task.FromResult(0)).GetAwaiter().GetResult();
            Check(!result.Ready && result.StopReason=="clipboard-finalization" && !locked && emptied==1 && written==1 && closed==1,
                "Contended post-close revalidation stops with a finalization error and never repeats Empty or Set");
            int publications=0,waits=0;
            var stopped=Native.AdminClipboardPreparation(()=> { publications++;return result; },()=>null,
                milliseconds=> { waits++;return Task.FromResult(0); }).GetAwaiter().GetResult();
            Check(!stopped.Ready && publications==1 && waits==0 && stopped.Sequence==result.Sequence,
                "A post-close failure with an owned sequence cannot cause preparation to republish the command");
        }
        foreach(bool ownershipLost in new[]{false,true}) {
            uint sequence=70;bool owned=true;int closed=0,emptied=0,written=0;
            Func<bool> close=()=> { closed++;if(closed==1) { sequence++;if(ownershipLost)owned=false; }return true; };
            var publication=Native.AdminClipboardPublication(70,()=>true,close,()=>sequence,
                ()=> { emptied++;sequence++;return true; },()=> { written++;return false; },()=>true,()=>owned);
            var result=Native.AdminClipboardFinalization(publication,()=>true,close,()=>sequence,()=>owned,()=>false,()=>null,milliseconds=>Task.FromResult(0)).GetAwaiter().GetResult();
            Check(!result.Ready && result.StopReason!=null && result.Sequence==(ownershipLost?0u:sequence)
                && emptied==1 && written==1 && closed>=1,
                "A partial publication records the post-close sequence only while ownership remains available for restoration: "+ownershipLost);
        }
        {
            uint sequence=80;bool locked=false;int reads=0,closed=0,elapsed=0;
            var result=Native.AdminClipboardFinalization(new AdminClipboardResult { Ready=true,Sequence=79 },()=> { reads++;locked=true;return true; },
                ()=> { closed++;locked=false;sequence++;return true; },()=>sequence,()=>true,()=>locked,()=>null,
                milliseconds=> { elapsed+=milliseconds;return Task.FromResult(0); }).GetAwaiter().GetResult();
            Check(!result.Ready && result.StopReason=="clipboard-finalization" && reads==3 && closed==3 && !locked && elapsed==0,
                "Repeated same-owner close-time sequence changes stop after three read-only passes without claiming a stable lease");
        }
        {
            uint sequence=90;bool locked=false;int attempts=0,verified=0,closed=0,elapsed=0;
            var result=Native.AdminClipboardFinalization(new AdminClipboardResult { Ready=true,Sequence=89 },()=> { attempts++;if(elapsed<50)return false;locked=true;return true; },
                ()=> { closed++;locked=false;return true; },()=>sequence,()=>true,()=> { verified++;return locked; },()=>null,
                milliseconds=> { elapsed+=milliseconds;return Task.FromResult(0); }).GetAwaiter().GetResult();
            Check(result.Ready && result.Sequence==sequence && attempts==3 && verified==1 && closed==1 && elapsed==50 && !locked,
                "Finalization can wait for read-lock contention without rewriting the already published command");
            attempts=closed=elapsed=0;
            result=Native.AdminClipboardFinalization(new AdminClipboardResult { Ready=true,Sequence=89 },()=> { attempts++;return false; },
                ()=> { closed++;return true; },()=>sequence,()=>true,()=>true,()=>null,
                milliseconds=> { elapsed+=milliseconds;return Task.FromResult(0); }).GetAwaiter().GetResult();
            Check(!result.Ready && result.StopReason=="clipboard-finalization" && attempts==5 && closed==0 && elapsed==100,
                "Finalization acquisition has a bounded 100 ms wait and never closes an unacquired lock");
            attempts=closed=elapsed=0;
            result=Native.AdminClipboardFinalization(new AdminClipboardResult { Ready=true,Sequence=90 },()=> { attempts++;return false; },
                ()=> { closed++;return true; },()=>sequence,()=>true,()=>true,()=>elapsed>=25?"physical-input":null,
                milliseconds=> { elapsed+=milliseconds;return Task.FromResult(0); }).GetAwaiter().GetResult();
            Check(!result.Ready && result.StopReason!=null && attempts==1 && closed==0 && elapsed==25,
                "Physical input while finalization waits for a read lock stops before another acquisition attempt");
        }
    }

    static void TestClipboardPreparation() {
        int attempts=0,elapsed=0;var delays=new List<int>();
        Func<int,Task> advance=milliseconds=> { elapsed+=milliseconds;delays.Add(milliseconds);return Task.FromResult(0); };
        var result=Native.AdminClipboardPreparation(()=> { attempts++;return attempts<3?new AdminClipboardResult { StopReason="clipboard-busy" }:
            new AdminClipboardResult { Ready=true,Sequence=51 }; },()=>null,advance).GetAwaiter().GetResult();
        Check(result.Ready && result.Sequence==51 && attempts==3 && elapsed==50 && delays.SequenceEqual(new[]{25,25}),
            "Temporary clipboard lock contention can settle before the first command without repeating a successful publication");
        attempts=elapsed=0;delays.Clear();
        result=Native.AdminClipboardPreparation(()=> { attempts++;return new AdminClipboardResult { StopReason="clipboard-busy" }; },()=>null,advance).GetAwaiter().GetResult();
        Check(!result.Ready && attempts==5 && elapsed==100 && delays.Count==4,"Clipboard acquisition retries have a bounded 100 ms window");
        foreach(string reason in new[]{"clipboard-changed","clipboard-publication","clipboard-verification","clipboard-owner"}) {
            attempts=elapsed=0;delays.Clear();
            result=Native.AdminClipboardPreparation(()=> { attempts++;return new AdminClipboardResult { StopReason=reason }; },()=>null,advance).GetAwaiter().GetResult();
            Check(result.StopReason==reason && attempts==1 && elapsed==0,"A clipboard failure beyond lock acquisition is never retried: "+reason);
        }
        attempts=elapsed=0;delays.Clear();
        result=Native.AdminClipboardPreparation(()=> { attempts++;return new AdminClipboardResult { StopReason="clipboard-busy",Sequence=52 }; },()=>null,advance).GetAwaiter().GetResult();
        Check(result.Sequence==52 && attempts==1 && elapsed==0,"A partial owned clipboard mutation blocks acquisition retries and retains its restoration sequence");
        attempts=elapsed=0;delays.Clear();
        result=Native.AdminClipboardPreparation(()=> { attempts++;return new AdminClipboardResult { StopReason="clipboard-busy" }; },
            ()=>elapsed>=25?"physical-input":null,advance).GetAwaiter().GetResult();
        Check(result.StopReason=="physical-input" && attempts==1 && elapsed==25,"Physical input during clipboard contention stops before a second acquisition attempt");
    }

    static void TestMenuCloseReadiness() {
        int elapsed=0,probes=0;var delays=new List<int>();
        Func<int,Task> advance=milliseconds=> { elapsed+=milliseconds;delays.Add(milliseconds);return Task.FromResult(0); };
        string gate=Native.AdminMenuCloseReadiness(true,()=>null,()=>false,()=> { probes++;return elapsed<175; },advance).GetAwaiter().GetResult();
        Check(gate==null && elapsed==175 && probes==8 && delays.All(milliseconds=>milliseconds==25),
            "A single owned map-close transition can settle across menu samples before chat opens");
        elapsed=probes=0;delays.Clear();
        gate=Native.AdminMenuCloseReadiness(false,()=>null,()=>false,()=> { probes++;return true; },advance).GetAwaiter().GetResult();
        Check(gate=="inventory-open" && elapsed==0 && probes==1,"A visible menu without an owned map-close request blocks immediately");
        elapsed=probes=0;delays.Clear();
        gate=Native.AdminMenuCloseReadiness(true,()=>null,()=>false,()=> { probes++;return true; },advance).GetAwaiter().GetResult();
        Check(gate=="inventory-open" && elapsed==500 && probes==21 && delays.Count==20,"Map-close settling times out after 500 ms without assuming that the menu closed");
        foreach(string blocked in new[]{"physical-input","focus-changed","clipboard-changed"}) {
            elapsed=probes=0;delays.Clear();
            gate=Native.AdminMenuCloseReadiness(true,()=>elapsed>=50?blocked:null,()=>false,()=> { probes++;return true; },advance).GetAwaiter().GetResult();
            Check(gate==blocked && elapsed==50 && probes==2,"A changed safety gate stops menu settling before another probe: "+blocked);
        }
        elapsed=probes=0;delays.Clear();
        gate=Native.AdminMenuCloseReadiness(true,()=>null,()=>true,()=> { probes++;return true; },advance).GetAwaiter().GetResult();
        Check(gate=="chat-open" && elapsed==0 && probes==0,"An already open chat blocks settling before probing or waiting on a map menu");
    }

    static void TestAsyncMapDispatch(SpawnItemCatalog catalog) {
        foreach(bool allowedAfterPreparation in new[]{false,true}) {
            bool fullMap=true,allowed=true;int mapAccepted=0,sent=0;var ready=new TaskCompletionSource<bool>();
            var task=Native.AdminChatBatchSequenceAsync(1,index=>ready.Task,(index,submitted)=>Native.AdminChatSequence((key,up)=> { if(!up)sent++;return true; },
                milliseconds=>Task.FromResult(0),()=>allowed,()=>true,()=>{},0x54,0x4D,submitted,null,()=> { mapAccepted++;fullMap=false; }),
                milliseconds=>Task.FromResult(0),()=>allowed);
            Check(!task.IsCompleted && fullMap && sent==0,"Awaiting clipboard preparation leaves the open-map state and all game input untouched");
            allowed=allowedAfterPreparation;ready.SetResult(false);var result=task.GetAwaiter().GetResult();
            Check(result.SubmittedCount==0 && !result.Completed && fullMap && mapAccepted==0 && sent==0,
                "Failed asynchronous preparation never clears the map flag or opens chat, even when focus/input changes");
        }
        {
            bool fullMap=true,allowed=true;int mapAccepted=0,sent=0;var ready=new TaskCompletionSource<bool>();
            var task=Native.AdminChatBatchSequenceAsync(1,index=>ready.Task,(index,submitted)=>Native.AdminChatSequence((key,up)=> { if(!up)sent++;return true; },
                milliseconds=>Task.FromResult(0),()=>allowed,()=>true,()=>{},0x54,0x4D,submitted,null,()=> { mapAccepted++;fullMap=false; }),
                milliseconds=>Task.FromResult(0),()=>allowed);
            allowed=false;ready.SetResult(true);var result=task.GetAwaiter().GetResult();
            Check(result.SubmittedCount==0 && fullMap && mapAccepted==0 && sent==0,"A late gate change after successful clipboard preparation still preserves the unsent open-map state");
        }
        foreach(bool acceptMap in new[]{false,true}) {
            bool fullMap=true;int mapAccepted=0,opened=0,submitted=0,menuTime=0;var keys=new List<uint>();var held=new HashSet<uint>();var events=new List<string>();
            bool sent=Native.AdminChatSequence((key,up)=> {
                if(!up) { keys.Add(key);events.Add("down:"+key);if(key==0x4D && !acceptMap)return false;held.Add(key); }else held.Remove(key);return true;
            },milliseconds=> { events.Add("delay:"+milliseconds);return Task.FromResult(0); },()=>true,()=>true,()=>opened++,0x54,0x4D,()=>submitted++,null,
                ()=> { mapAccepted++;fullMap=false;events.Add("map-accepted"); },async()=> {
                    events.Add("settle-start");string gate=await Native.AdminMenuCloseReadiness(mapAccepted==1,()=>null,()=>false,()=>menuTime<100,
                        milliseconds=> { menuTime+=milliseconds;return Task.FromResult(0); });events.Add("settle-end");return gate==null;
                }).GetAwaiter().GetResult();
            if(!acceptMap)Check(!sent && fullMap && mapAccepted==0 && opened==0 && submitted==0 && keys.SequenceEqual(new uint[]{0x4D}) && held.Count==0,
                "Rejected map key-down preserves the map flag and cannot call the close callback or open chat");
            else Check(sent && !fullMap && mapAccepted==1 && opened==1 && submitted==1 && menuTime==100 && keys.Count(key=>key==0x4D)==1 && held.Count==0
                && events.IndexOf("down:77")<events.IndexOf("map-accepted") && events.IndexOf("map-accepted")<events.IndexOf("settle-start")
                && events.IndexOf("settle-end")<events.IndexOf("down:84"),
                "Accepted map key-down clears the flag once, then async menu settling completes before a single chat opener and submission");
        }
        {
            int maps=0,opened=0,submitted=0,elapsed=0;var keys=new List<uint>();
            bool sent=Native.AdminChatSequence((key,up)=> { if(!up)keys.Add(key);return true; },milliseconds=>Task.FromResult(0),()=>true,()=>true,()=>opened++,0x54,0x4D,
                ()=>submitted++,null,()=>maps++,async()=>await Native.AdminMenuCloseReadiness(maps==1,()=>null,()=>false,()=>true,
                    milliseconds=> { elapsed+=milliseconds;return Task.FromResult(0); })==null).GetAwaiter().GetResult();
            Check(!sent && maps==1 && elapsed==500 && opened==0 && submitted==0 && keys.SequenceEqual(new uint[]{0x4D}),
                "A map-close timeout sends no chat/paste/Enter and never retries the map toggle automatically");
        }
        {
            var cart=new SpawnCart(catalog);cart.AddMany(catalog.Items.Take(3).Select(item=>item.Id),1);var original=cart.BuildCommands();
            var ready=new TaskCompletionSource<bool>();int maps=0,opened=0;var keys=new List<uint>();
            var task=Native.AdminChatBatchSequenceAsync(3,index=>index==0?Task.FromResult(true):ready.Task,
                (index,submitted)=>Native.AdminChatSequence((key,up)=> { if(!up)keys.Add(key);return true; },milliseconds=>Task.FromResult(0),()=>true,()=>true,
                    ()=>opened++,0x54,index==0?0x4D:0,submitted,null,()=>maps++),milliseconds=>Task.FromResult(0),()=>true,count=>cart.ConsumeSubmitted(1));
            Check(!task.IsCompleted && maps==1 && opened==1 && cart.BuildCommands().SequenceEqual(original.Skip(1)),
                "Awaiting a later clipboard preparation retains the dispatched prefix and the remaining ordered cart after one map close");
            ready.SetResult(false);var result=task.GetAwaiter().GetResult();
            Check(!result.Completed && result.SubmittedCount==1 && result.ChatClosed && maps==1 && opened==1 && keys.Count(key=>key==0x0D)==1
                && cart.BuildCommands().SequenceEqual(original.Skip(1)),"A later asynchronous preparation failure preserves only unsent entries without reopening chat or repeating the map toggle");
        }
    }

    static void TestDispatchGateErrors() {
        var mappings=new Dictionary<string,string> {
            { "clipboard-changed","SpawnCartClipboardChanged" },{ "clipboard-verification","SpawnCartClipboardChanged" },
            { "clipboard-busy","SpawnCartClipboardChanged" },{ "clipboard-publication","SpawnCartClipboardChanged" },
            { "clipboard-owner","SpawnCartClipboardChanged" },{ "clipboard-finalization","SpawnCartClipboardChanged" },
            { "physical-input","SpawnCartFocusInputChanged" },{ "focus-changed","SpawnCartFocusInputChanged" },{ "target-unavailable","SpawnCartFocusInputChanged" },
            { "inventory-open","SpawnCartCloseChatInventory" },{ "chat-open","SpawnCartCloseChatInventory" },
            { "keyboard-monitor","SpawnCartMonitorUnavailable" },{ "closing","SpawnCartBusy" },{ "admin-busy","SpawnCartBusy" },
            { "unrelated-foreground","SpawnCartForegroundBlocked" },{ "held-keys","SpawnCartFocusUnavailable" },{ "focus-handoff","SpawnCartFocusUnavailable" }
        };
        foreach(AppLanguage language in Enum.GetValues(typeof(AppLanguage))) {
            Localization.Current=language;
            foreach(var mapping in mappings) {
                if(Native.AdminDispatchGateError(mapping.Key)!=Localization.Get(mapping.Value))
                    throw new Exception("Dispatch stop reason differs: "+language+" / "+mapping.Key);
            }
        }
        Localization.Current=AppLanguage.English;
        Check(true,"Every known dispatch gate retains its specific localized failure reason in all ten languages");
        foreach(string gate in new[]{null,"","none","new-unknown-gate"})
            Check(Native.AdminDispatchGateError(gate)==null,"Unknown or successful dispatch gates preserve the generic sequence result");
    }

    static void TestAfterChatOpenAbort(SpawnItemCatalog catalog) {
        foreach(bool replacedClipboard in new[]{false,true}) {
            var cart=new SpawnCart(catalog);cart.Add(catalog.Items[0].Id,1);string original=Snapshot(cart);
            var downs=new List<uint>();var held=new HashSet<uint>();bool allowed=true,clipboard=true;int opened=0;
            var result=Native.AdminChatBatchSequence(1,index=>true,(index,submitted)=>Native.AdminChatSequence((key,up)=> {
                if(up)held.Remove(key);else { downs.Add(key);held.Add(key); }return true;
            },milliseconds=> {
                if(milliseconds==160) { if(replacedClipboard)clipboard=false;else allowed=false; }
                return Task.FromResult(0);
            },()=>allowed,()=>clipboard,()=>opened++,0x54,0,submitted),milliseconds=>Task.FromResult(0),()=>allowed,count=>cart.ConsumeSubmitted(1)).GetAwaiter().GetResult();
            Check(opened==1 && downs.SequenceEqual(new uint[]{0x54}) && held.Count==0 && result.SubmittedCount==0 && !result.Completed && !result.ChatClosed,
                "A gate change during chat opening stops before Ctrl/select-all/paste/Enter and releases injected keys: "+(replacedClipboard?"clipboard":"physical or focus input"));
            Check(Snapshot(cart)==original,"An abort after opening chat preserves every unsent cart entry");
        }
        bool typing=true;var keys=new List<uint>();int submittedCount=0;
        var sent=Native.AdminChatSequence((key,up)=> { if(!up)keys.Add(key);return true; },milliseconds=>Task.FromResult(0),
            ()=>typing,()=>true,()=>{},0x54,0,()=>submittedCount++).GetAwaiter().GetResult();
        Check(sent && submittedCount==1 && keys.Contains(0xA2) && keys.Contains(0x41) && keys.Contains(0x56),
            "The pure command sequence continues through its own Ctrl/select-all/paste key presses while the caller's input gate remains valid");
    }

    static void TestBeforeChatGate() {
        var events=new List<string>();int opened=0,submitted=0;
        bool sent=Native.AdminChatSequence((key,up)=> { if(!up)events.Add("key:"+key);return true; },
            milliseconds=> { events.Add("delay:"+milliseconds);return Task.FromResult(0); },()=>true,()=>true,()=>opened++,0x54,0x4D,
            ()=>submitted++,()=> { events.Add("preflight");return true; }).GetAwaiter().GetResult();
        int map=events.IndexOf("key:77"),settled=events.IndexOf("delay:120"),preflight=events.IndexOf("preflight"),chat=events.IndexOf("key:84");
        Check(sent && opened==1 && submitted==1 && map<settled && settled<preflight && preflight<chat,
            "The one-time inventory/chat preflight runs after map closure settles and before chat opens");
        foreach(int mapKey in new[]{0,0x4D}) {
            events.Clear();opened=submitted=0;int probed=0;
            sent=Native.AdminChatSequence((key,up)=> { if(!up)events.Add("key:"+key);return true; },milliseconds=>Task.FromResult(0),
                ()=>true,()=>true,()=>opened++,0x54,mapKey,()=>submitted++,()=> { probed++;return false; }).GetAwaiter().GetResult();
            Check(!sent && probed==1 && opened==0 && submitted==0 && !events.Contains("key:84") && !events.Contains("key:162"),
                "An authoritative pre-chat menu gate blocks chat and paste after any required map closure: "+mapKey);
        }
        events.Clear();bool allowed=true;int lateProbe=0;opened=submitted=0;
        sent=Native.AdminChatSequence((key,up)=> { if(!up)events.Add("key:"+key);return true; },milliseconds=> {
            if(milliseconds==120)allowed=false;return Task.FromResult(0);
        },()=>allowed,()=>true,()=>opened++,0x54,0x4D,()=>submitted++,()=> { lateProbe++;return true; }).GetAwaiter().GetResult();
        Check(!sent && lateProbe==0 && opened==0 && submitted==0 && !events.Contains("key:84"),
            "Input or focus changes while closing the map stop before the menu probe or chat key");
        events.Clear();int probes=0;bool chatClosed=false;
        var result=Native.AdminChatBatchSequence(2,index=>true,(index,notify)=>Native.AdminChatSequence((key,up)=> {
            if(!up)events.Add(index+":"+key);return true;
        },milliseconds=>Task.FromResult(0),()=>true,()=>true,()=>{},0x54,index==0?0x4D:0,notify,()=> {
            probes++;if(index==1)chatClosed=events.Contains("0:27");return true;
        }),milliseconds=>Task.FromResult(0),()=>true).GetAwaiter().GetResult();
        Check(result.Completed && result.SubmittedCount==2 && probes==2 && chatClosed,
            "Every batch entry gets a fresh menu preflight after the preceding command's Escape cleanup");
    }

    static void TestInventoryInputOwnership() {
        // This fixture does not construct a Form, install input hooks, create a handle or start timers.
        var fixture=(MapWindow)FormatterServices.GetUninitializedObject(typeof(MapWindow));
        DateTime now=new DateTime(2026,10,7,12,0,0,DateTimeKind.Utc),next=now.AddSeconds(-1);int probes=0;
        SetField(fixture,"adminCommandBusy",true);SetField(fixture,"inventoryInputLocked",false);
        SetField(fixture,"inventoryProbeMisses",1);SetField(fixture,"nextInventoryProbe",next);
        fixture.UpdateInventoryInputGate(now,true,()=> { probes++;return true; });
        Check(probes==0 && !Field<bool>(fixture,"inventoryInputLocked") && Field<int>(fixture,"inventoryProbeMisses")==1
            && Field<DateTime>(fixture,"nextInventoryProbe")==next,
            "Automatic inventory detection cannot inspect or mutate a ready input gate while an admin batch owns the game UI");
        SetField(fixture,"inventoryInputLocked",true);SetField(fixture,"fullMapActive",true);
        fixture.UpdateInventoryInputGate(now,false,()=> { probes++;return false; });
        Check(probes==0 && Field<bool>(fixture,"inventoryInputLocked") && Field<int>(fixture,"inventoryProbeMisses")==1,
            "A busy admin batch preserves a previously locked inventory gate even during focus loss or map-state changes");
        SetField(fixture,"adminCommandBusy",false);SetField(fixture,"fullMapActive",false);
        SetField(fixture,"inventoryProbeMisses",0);SetField(fixture,"nextInventoryProbe",DateTime.MinValue);
        fixture.UpdateInventoryInputGate(now,true,()=> { probes++;return false; });
        Check(probes==1 && Field<bool>(fixture,"inventoryInputLocked") && Field<int>(fixture,"inventoryProbeMisses")==1,
            "Idle inventory detection retains its lock after one missing navigation highlight");
        fixture.UpdateInventoryInputGate(now.AddMilliseconds(119),true,()=> { probes++;return false; });
        Check(probes==1 && Field<bool>(fixture,"inventoryInputLocked"),"Idle inventory probes keep the existing 120 ms cadence");
        fixture.UpdateInventoryInputGate(now.AddMilliseconds(120),true,()=> { probes++;return false; });
        Check(probes==2 && !Field<bool>(fixture,"inventoryInputLocked") && Field<int>(fixture,"inventoryProbeMisses")==0,
            "Two consecutive idle inventory misses release the text-input lock");
        fixture.UpdateInventoryInputGate(now.AddMilliseconds(240),true,()=> { probes++;return true; });
        Check(probes==3 && Field<bool>(fixture,"inventoryInputLocked") && Field<int>(fixture,"inventoryProbeMisses")==0,
            "An idle selected navigation tab still locks inventory input");
        fixture.UpdateInventoryInputGate(now.AddMilliseconds(250),false,()=> { probes++;return true; });
        Check(probes==3 && !Field<bool>(fixture,"inventoryInputLocked") && Field<int>(fixture,"inventoryProbeMisses")==0,
            "Idle focus loss clears the inventory gate without querying the screen");
        SetField(fixture,"inventoryInputLocked",true);SetField(fixture,"inventoryProbeMisses",1);SetField(fixture,"fullMapActive",true);
        fixture.UpdateInventoryInputGate(now.AddMilliseconds(400),true,()=> { probes++;return true; });
        Check(probes==3 && !Field<bool>(fixture,"inventoryInputLocked") && Field<int>(fixture,"inventoryProbeMisses")==0,
            "Idle full-map mode clears the inventory gate without querying the screen");
        GC.SuppressFinalize(fixture);
    }

    static void TestKeySequence() {
        var downs=new List<uint>();var delays=new List<int>();var held=new HashSet<uint>();int submitted=0,opened=0;
        bool sent=Native.AdminChatSequence((key,up)=> { if(up)held.Remove(key);else { downs.Add(key);held.Add(key); }return true; },
            milliseconds=> { delays.Add(milliseconds);return Task.FromResult(0); },()=>true,()=>true,()=>opened++,0x59,0x4D,()=>submitted++).GetAwaiter().GetResult();
        Check(sent && submitted==1 && opened==1 && held.Count==0,"Configured chat/map keys dispatch one command and release every injected key");
        Check(downs.SequenceEqual(new uint[]{0x4D,0x59,0xA2,0x41,0x56,0x0D,0x1B}),
            "Admin dispatch follows configured map/chat, select-all, paste, Enter and Escape order");
        Check(delays.SequenceEqual(new[]{35,120,35,160,25,35,35,100,35,80,35}),"Admin dispatch preserves chat-open, paste and close pacing");
        downs.Clear();submitted=0;
        sent=Native.AdminChatSequence((key,up)=> { if(!up)downs.Add(key);return true; },milliseconds=>Task.FromResult(0),
            ()=>false,()=>true,()=>{},0x54,0,()=>submitted++).GetAwaiter().GetResult();
        Check(!sent && downs.Count==0 && submitted==0,"Held-key or focus gates reject input before opening chat");

        foreach(uint interruption in new uint[]{0x59,0x41,0x56,0x0D}) {
            bool allowed=true;downs.Clear();held.Clear();submitted=0;
            sent=Native.AdminChatSequence((key,up)=> {
                if(up)held.Remove(key);else { downs.Add(key);held.Add(key);if(key==interruption)allowed=false; }return true;
            },milliseconds=>Task.FromResult(0),()=>allowed,()=>true,()=>{},0x59,0,()=>submitted++).GetAwaiter().GetResult();
            Check(!sent && held.Count==0 && downs.Last()==interruption && submitted==(interruption==0x0D?1:0),
                "Mid-command input/focus interruption stops new key-downs and releases injected keys at VK "+interruption);
        }
        foreach(uint replacement in new uint[]{0x41,0x56}) {
            bool clipboardOwned=true;downs.Clear();held.Clear();submitted=0;
            sent=Native.AdminChatSequence((key,up)=> {
                if(up)held.Remove(key);else { downs.Add(key);held.Add(key);if(key==replacement)clipboardOwned=false; }return true;
            },milliseconds=>Task.FromResult(0),()=>true,()=>clipboardOwned,()=>{},0x54,0,()=>submitted++).GetAwaiter().GetResult();
            Check(!sent && submitted==0 && !downs.Contains(0x0D) && held.Count==0,
                "Clipboard replacement during selection/paste prevents Enter and releases injected modifiers");
        }
        downs.Clear();held.Clear();submitted=0;
        sent=Native.AdminChatSequence((key,up)=> {
            if(!up && key==0x1B)return false;
            if(up)held.Remove(key);else { downs.Add(key);held.Add(key); }return true;
        },milliseconds=>Task.FromResult(0),()=>true,()=>true,()=>{},0x54,0,()=>submitted++).GetAwaiter().GetResult();
        Check(!sent && submitted==1 && downs.Count(key=>key==0x0D)==1 && held.Count==0,
            "An accepted Enter is recorded even if Escape fails, without repeating the submitted command");
    }

    static void TestConsumption(SpawnItemCatalog catalog) {
        var cart=new SpawnCart(catalog);cart.AddMany(catalog.Items.Take(3).Select(item=>item.Id),2);
        var original=cart.BuildCommands();var sent=new List<string>();
        var result=Native.AdminChatBatchSequence(original.Count,index=>true,
            (index,submitted)=> { sent.Add(original[index]);submitted();return Task.FromResult(index==0); },
            milliseconds=>Task.FromResult(0),()=>true,count=>cart.ConsumeSubmitted(1)).GetAwaiter().GetResult();
        Check(result.SubmittedCount==2 && cart.Entries.Count==1 && cart.BuildCommands()[0]==original[2],
            "Cart consumes an Enter-dispatched command even when its cleanup fails, retaining only unsent items");
        var retry=cart.BuildCommands();
        result=Native.AdminChatBatchSequence(retry.Count,index=>true,
            (index,submitted)=> { sent.Add(retry[index]);submitted();return Task.FromResult(true); },
            milliseconds=>Task.FromResult(0),()=>true,count=>cart.ConsumeSubmitted(1)).GetAwaiter().GetResult();
        Check(result.Completed && cart.Entries.Count==0 && sent.SequenceEqual(original),"Resuming the remainder never duplicates earlier submitted commands");
    }

    static void AwaitUi(Task task) {
        DateTime deadline=DateTime.UtcNow.AddSeconds(5);
        while(!task.IsCompleted && DateTime.UtcNow<deadline) { Application.DoEvents();Thread.Sleep(1); }
        if(!task.IsCompleted)throw new Exception("Timed out waiting for simulated spawn-cart UI submission.");
        task.GetAwaiter().GetResult();
    }
    static void TestResultGroups(SpawnItemCatalog catalog) {
        var search=catalog.SearchWithRelations("AK47",null);var original=search.Select(match=>match.Item.Id).ToList();
        var groups=SpawnResultGroups.Build(search);var keys=groups.Select(group=>group.Key).ToList();
        Check(groups.IsReadOnly && groups.All(group=>group.Matches.IsReadOnly && group.Matches.Count>0 && !string.IsNullOrWhiteSpace(group.Label)),
            "Search-result groups expose immutable populated groups with readable headings");
        Check(keys.Count==new HashSet<string>(keys,StringComparer.OrdinalIgnoreCase).Count
            && groups.SelectMany(group=>group.Matches).Select(match=>match.Item.Id).OrderBy(id=>id).SequenceEqual(original.OrderBy(id=>id))
            && search.Select(match=>match.Item.Id).SequenceEqual(original),
            "Grouping assigns each matching canonical item once without changing logical search results or order");
        var scopes=groups.Single(group=>group.Key=="attachment/scope");var rails=groups.Single(group=>group.Key=="attachment/rail");
        var ammunition=groups.Single(group=>group.Key=="ammunition/7.62x39mm");
        Check(scopes.Matches.Any(match=>match.Item.Id=="WeaponScope_ACOG_01") && scopes.Matches.Any(match=>match.Item.Id=="ScopeRail_AK47")
            && scopes.Matches.All(match=>match.Item.Category=="attachment" && match.Item.Subcategory=="scope"),
            "Scopes and the RIS Adapter follow their SCUM Database scope subtype without guessed attachment categories");
        Check(rails.Matches.Any(match=>match.Item.Id=="ImprovisedRail_Long") && rails.Matches.Any(match=>match.Item.Id=="ImprovisedRail_Short")
            && rails.Matches.All(match=>match.Item.Category=="attachment" && match.Item.Subcategory=="rail"),
            "Source rail attachments have a separate collapsible result group");
        Check(ammunition.Matches.Count>=8 && ammunition.Matches.All(match=>match.Item.Category=="ammunition" && match.Item.Subcategory=="7.62x39mm")
            && ammunition.Matches.Any(match=>match.Item.Id=="Cal_7_62x39mm") && ammunition.Matches.Any(match=>match.Item.Id=="Cal_7_62x39mm_Ammobox"),
            "Loose rounds, crafted variants and ammunition boxes share their source calibre group");
        Check(groups.SelectMany(group=>group.Matches).All(match=>ReferenceEquals(match,search.Single(originalMatch=>originalMatch.Item.Id==match.Item.Id))
                && match.Item.SourceUrl=="https://scum-db.com/items/"+match.Item.Id),
            "Result grouping retains original source records, compatibility provenance and distinct variants");
        var fixture=Parse(string.Join("\n",new[]{Row("OldKnife","Old knife","melee_weapon")+"\tknife",
            Row("Knife","Knife","weapon_melee")+"\tknife",Row("Untyped","Untyped attachment","attachment"),
            Row("NewSubtype","New subtype","new_source_category")+"\tX/Y"}));
        var fixtureGroups=SpawnResultGroups.Build(fixture.SearchWithRelations("",null));
        Check(fixtureGroups.Single(group=>group.Key=="weapon_melee/knife").Matches.Count==2,
            "Current and legacy melee category names share one result heading without losing either item");
        Check(fixtureGroups.Any(group=>group.Key=="attachment/general" && group.Matches.Single().Item.Id=="Untyped")
            && fixtureGroups.Any(group=>group.Key=="new_source_category/x%2Fy" && group.Matches.Single().Item.Id=="NewSubtype"),
            "Missing source subtypes and future categories remain grouped with stable escaped keys");
        Check(SpawnResultGroups.Build(fixture.SearchWithRelations("does not exist",null)).Count==0,
            "An empty search produces no empty or selectable result headings");
    }
    static void TestDialog(SpawnItemCatalog catalog,string directory) {
        string path=ProfileCartPath(directory,"dialog-cart.tsv");
        var pending=new TaskCompletionSource<SpawnBatchResult>();Action<int> report=null;IList<string> submittedCommands=null;int calls=0;
        using(var dialog=new SpawnCartDialog(catalog,path,(commands,owner,progress)=> {
            calls++;submittedCommands=new List<string>(commands);report=progress;return pending.Task;
        })) {
            // Create hidden controls only. No game focus changes, clipboard calls or native input are used.
            IntPtr hiddenDialog=dialog.Handle;
            var items=Field<ListView>(dialog,"items");IntPtr hiddenItems=items.Handle;
            var grid=Field<DataGridView>(dialog,"grid");IntPtr hiddenGrid=grid.Handle;
            var search=Field<TextBox>(dialog,"search");var category=Field<TreeView>(dialog,"categoryTree");IntPtr hiddenCategories=category.Handle;
            var quantity=Field<NumericUpDown>(dialog,"quantity");var cart=Field<SpawnCart>(dialog,"cart");
            dialog.SetChatBinding("Y");
            Check(Field<Label>(dialog,"help").Text.Contains(Localization.T("SpawnCartChatBinding","Y")),
                "Selector help names the configured SCUM chat key instead of assuming a default binding");
            Check(!items.VirtualMode && items.ShowGroups && items.Items.Count==catalog.Items.Count && grid.Rows.Count==0,
                "Selector groups the embedded catalogue and starts with an empty cart");
            var treeNodes=TreeNodes(category.Nodes).ToList();
            var meleeChoices=treeNodes.Where(node=>node.Tag is SpawnCategoryNode && ((SpawnCategoryNode)node.Tag).Key=="category/weapons/weapon_melee").ToList();
            Check(meleeChoices.Count==1,"Both SCUM Database melee category names appear as one readable UI category");
            category.SelectedNode=meleeChoices[0];
            Check(Field<IList<SpawnItem>>(dialog,"filtered").Select(item=>item.Id).SequenceEqual(catalog.Items.Where(item=>item.Category=="weapon_melee" || item.Category=="melee_weapon").Select(item=>item.Id)),
                "The combined melee category includes every item from both source categories in catalogue order");
            TreeNode selectedCategory=treeNodes.First(node=>node.Tag is SpawnCategoryNode && ((SpawnCategoryNode)node.Tag).Key.StartsWith("category/")
                && ((SpawnCategoryNode)node.Tag).Key!="category/weapons/weapon_melee");
            var definition=(SpawnCategoryNode)selectedCategory.Tag;
            category.SelectedNode=selectedCategory;
            Check(items.Items.Count==definition.Items.Count,"UI category filter agrees with the source hierarchy membership");
            TreeNode selectedLeaf=selectedCategory.Nodes[0];category.SelectedNode=selectedLeaf;
            var leaf=(SpawnCategoryNode)selectedLeaf.Tag;
            Check(items.Items.Count==leaf.Items.Count && Field<IList<SpawnItem>>(dialog,"filtered").All(item=>leaf.Contains(item)),
                "Selecting a category subtype limits results to that source leaf");
            category.SelectedNode=selectedCategory;
            string selectedId=definition.Items[0].Id;search.Text=selectedId;
            var filtered=Field<IList<SpawnItem>>(dialog,"filtered");
            Check(filtered.Any(item=>item.Id==selectedId) && filtered.All(item=>definition.Contains(item)),
                "UI combines typed ID search with the selected tree category");
            int selectedIndex=filtered.ToList().FindIndex(item=>item.Id==selectedId);
            items.SelectedIndices.Add(selectedIndex);quantity.Value=2;Invoke(dialog,"AddSelected");
            Check(cart.Entries.Count==1 && cart.TotalQuantity==2 && grid.Rows.Count==1,"Selected catalogue item and chosen quantity are added through the UI");
            var persisted=new SpawnCart(catalog);ReloadProfileCart(persisted,catalog,path);
            Check(Snapshot(persisted)==Snapshot(cart),"Adding through the selector persists the cart immediately");
            grid.CurrentCell=grid.Rows[0].Cells[1];
            Check(grid.BeginEdit(false),"Quantity cell enters edit mode");
            ((TextBox)grid.EditingControl).Text="5";
            Check(grid.EndEdit() && cart.TotalQuantity==5,"A valid quantity edit updates the model");
            ReloadProfileCart(persisted,catalog,path);Check(persisted.TotalQuantity==5,"A committed quantity edit persists and refreshes the preview");
            Check(Field<TextBox>(dialog,"preview").Text=="#SpawnItem "+selectedId+" 5","Command preview reflects the edited quantity");
            foreach(string invalid in new[]{"0","101","not-a-number"}) {
                grid.CurrentCell=grid.Rows[0].Cells[1];grid.BeginEdit(false);((TextBox)grid.EditingControl).Text=invalid;
                Task blocked=dialog.SendCart();
                Check(blocked.IsCompleted && calls==0 && cart.TotalQuantity==5 && grid.Rows[0].ErrorText.Length>0,
                    "Sending while a quantity edit is invalid never dispatches the previous quantity: "+invalid);
                grid.CancelEdit();
            }
            category.SelectedNode=category.Nodes[0];search.Text="";
            filtered=Field<IList<SpawnItem>>(dialog,"filtered");
            int secondIndex=filtered.ToList().FindIndex(item=>item.Id!=selectedId);
            items.SelectedIndices.Add(secondIndex);quantity.Value=3;Invoke(dialog,"AddSelected");
            Check(cart.Entries.Count==2 && cart.TotalQuantity==8,"A second selection appends another item without losing the edited quantity");
            using(var locked=File.Open(ProfileDatabasePath(path),FileMode.Open,FileAccess.Read,FileShare.None)) {
                Task blocked=dialog.SendCart();
                Check(blocked.IsCompleted && calls==0,"Cart save failure before submission prevents the sender from starting");
            }
            grid.CurrentCell=grid.Rows[0].Cells[1];grid.BeginEdit(false);((TextBox)grid.EditingControl).Text="7";
            var original=new List<string> { "#SpawnItem "+selectedId+" 7",cart.BuildCommands()[1] };
            Task sending=dialog.SendCart();
            Check(cart.Entries[0].Quantity==7,"Sending commits a valid active quantity edit before constructing commands");
            Check(!sending.IsCompleted && calls==1 && submittedCommands.SequenceEqual(original),"UI sends a stable ordered command snapshot to the batch transport");
            Check(!search.Enabled && !category.Enabled && !grid.Enabled && !items.Enabled,"Cart edits and catalogue selection are disabled while a batch is in progress");
            Check(!dialog.Controls.Find("ClearSpawnItemSearch",true).Single().Enabled
                && !dialog.Controls.Find("ExpandSpawnResultGroups",true).Single().Enabled && !dialog.Controls.Find("CollapseSpawnResultGroups",true).Single().Enabled,
                "Search reset and group controls are also disabled while command submission is in progress");
            AwaitUi(dialog.SendCart());Check(calls==1,"Repeated send attempts cannot start a second concurrent batch");
            report(1);report(1);ReloadProfileCart(persisted,catalog,path);
            Check(cart.Entries.Count==1 && Snapshot(persisted)==original[1],"Submission progress removes and persists only the dispatched prefix, including duplicate progress reports");
            // Dispose before completing the fake send so the production restore-focus branch cannot activate any window.
            dialog.Dispose();pending.SetResult(new SpawnBatchResult { SubmittedCount=1,Completed=false,ChatClosed=false,Error="Simulated interruption" });AwaitUi(sending);
            ReloadProfileCart(persisted,catalog,path);Check(Snapshot(persisted)==original[1],"Interrupted UI sends retain the unsent remainder on disk for reopening");
        }
        using(var reopened=new SpawnCartDialog(catalog,path,(commands,owner,progress)=>Task.FromResult(new SpawnBatchResult())))
            Check(Field<SpawnCart>(reopened,"cart").Entries.Count==1,"Reopening the selector reloads only the unsent items");
    }

    static void TestCompatibilityDialog(SpawnItemCatalog catalog,string directory) {
        string path=ProfileCartPath(directory,"compatible-search-cart.tsv");
        using(var inactive=new InactiveUi())using(var dialog=new SpawnCartDialog(catalog,path,(commands,owner,progress)=> { throw new Exception("Compatibility search must not dispatch game commands."); })) {
            inactive.Show(dialog);var tree=Field<TreeView>(dialog,"categoryTree");var search=Field<TextBox>(dialog,"search");var items=Field<ListView>(dialog,"items");
            var assault=TreeNodes(tree.Nodes).Single(node=>node.Tag is SpawnCategoryNode
                && ((SpawnCategoryNode)node.Tag).Key=="subcategory/weapons/weapon_ranged/assault_rifle");
            var leaf=(SpawnCategoryNode)assault.Tag;tree.SelectedNode=assault;search.Text="AK47";Application.DoEvents();
            var filtered=Field<IList<SpawnItem>>(dialog,"filtered");var matches=Field<Dictionary<string,SpawnItemSearchResult>>(dialog,"searchMatches");
            Check(filtered.Select(item=>item.Id).SequenceEqual(catalog.SearchWithRelations("AK47",leaf.Contains).Select(match=>match.Item.Id))
                && filtered.Any(item=>item.Id=="WeaponScope_ACOG_01") && filtered.Any(item=>item.Id=="Magazine_RPK"),
                "Typing a gun name in the assault-rifle leaf includes verified compatible scopes and magazines across their category boundary");
            Check(filtered.All(item=>leaf.Contains(item) || matches[item.Id].IsRelated) && !filtered.Any(item=>item.Id=="1H_N9_Black"),
                "Only related rows from in-leaf gun seeds cross the selected category, while unrelated gun parts remain absent");
            int scopeIndex=filtered.ToList().FindIndex(item=>item.Id=="WeaponScope_ACOG_01");var scopeRow=items.Items[scopeIndex];var scopeMatch=matches["WeaponScope_ACOG_01"];
            string adapterLabel=Localization.T("SpawnCartCompatibilityAdapter",string.Join(", ",scopeMatch.RelatedWeapons.Select(item=>item.Name)));
            Check(items.Columns.Count==3 && items.ShowItemToolTips && scopeRow.SubItems[1].Text==adapterLabel
                && scopeRow.SubItems[2].Text==scopeMatch.Item.Id && scopeRow.ToolTipText.Contains(adapterLabel) && scopeRow.ToolTipText.Contains(scopeMatch.Item.Id),
                "Grouped selector rows and tooltips retain canonical IDs, source compatibility and required-adapter information");
            var original=filtered.Select(item=>item.Id).ToList();search.Text="AK-47";Application.DoEvents();
            Check(Field<IList<SpawnItem>>(dialog,"filtered").Select(item=>item.Id).SequenceEqual(original),"Typed gun punctuation produces the same results inside the selected leaf");
            search.Text="AK 47 scope";Application.DoEvents();filtered=Field<IList<SpawnItem>>(dialog,"filtered");
            Check(filtered.Any(item=>item.Id=="WeaponScope_ACOG_01") && !filtered.Any(item=>item.Id=="Magazine_RPK"),
                "The actual selector combines a spaced gun name with a scope term and removes nonmatching compatible magazines");
            search.Text="";Application.DoEvents();
            Check(Field<IList<SpawnItem>>(dialog,"filtered").All(leaf.Contains) && items.Items.Count==leaf.Items.Count,
                "Clearing the gun query restores the original strict category view without attachment expansion");
            tree.SelectedNode=TreeNodes(tree.Nodes).Single(node=>node.Tag is SpawnCategoryNode && ((SpawnCategoryNode)node.Tag).Key=="group/clothing");
            search.Text="AK47";Application.DoEvents();
            Check(!Field<IList<SpawnItem>>(dialog,"filtered").Any(item=>item.Id=="WeaponScope_ACOG_01"),
                "Searching another broad category cannot use an excluded gun to pull compatible attachments into its results");
            tree.SelectedNode=tree.Nodes[0];search.Text="scope";Application.DoEvents();
            Check(Field<Dictionary<string,SpawnItemSearchResult>>(dialog,"searchMatches")["WeaponScope_ACOG_01"].IsRelated==false,
                "The all-items selector still treats a scope-only query as ordinary direct item search");
            tree.SelectedNode=assault;search.Text="AK47";Application.DoEvents();filtered=Field<IList<SpawnItem>>(dialog,"filtered");
            items.SelectedIndices.Add(filtered.ToList().FindIndex(item=>item.Id=="Weapon_AK47"));items.SelectedIndices.Add(filtered.ToList().FindIndex(item=>item.Id=="WeaponScope_ACOG_01"));
            Field<NumericUpDown>(dialog,"quantity").Value=2;Field<Button>(dialog,"add").PerformClick();Application.DoEvents();
            var cart=Field<SpawnCart>(dialog,"cart");
            Check(cart.BuildCommands().SequenceEqual(new[]{"#SpawnItem Weapon_AK47 2","#SpawnItem WeaponScope_ACOG_01 2"}),
                "The actual Add button puts only the selected gun and compatible scope in the cart with canonical IDs and chosen quantity");
            var persisted=new SpawnCart(catalog);ReloadProfileCart(persisted,catalog,path);
            Check(Snapshot(persisted)==Snapshot(cart),"Gun and compatible attachment selections persist without automatically adding an adapter loadout");
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Compatible-item selector regressions never activate test windows or send game input");
        }
    }

    static void TestGroupedResultDialog(SpawnItemCatalog catalog,string directory) {
        string path=ProfileCartPath(directory,"grouped-search-cart.tsv");
        using(var inactive=new InactiveUi())using(var dialog=new SpawnCartDialog(catalog,path,(commands,owner,progress)=> {
            throw new Exception("Grouped search must not dispatch game commands.");
        })) {
            inactive.Show(dialog);var tree=Field<TreeView>(dialog,"categoryTree");var search=Field<TextBox>(dialog,"search");
            var items=Field<SpawnResultsListView>(dialog,"items");var add=Field<Button>(dialog,"add");var cart=Field<SpawnCart>(dialog,"cart");
            search.Text="AK47";Application.DoEvents();var filtered=Field<IList<SpawnItem>>(dialog,"filtered");
            var ids=filtered.Select(item=>item.Id).ToList();
            Check(items.Items.Count==filtered.Count && items.Groups.Count==items.GroupKeys.Count
                && items.Items.Cast<ListViewItem>().All(row=>row.Tag is SpawnItemSearchResult)
                && items.Items.Cast<ListViewItem>().Select(row=>((SpawnItemSearchResult)row.Tag).Item.Id).SequenceEqual(ids),
                "Native group headings remain separate from selectable item rows with stable canonical identity and order");
            Check(items.GroupKeys.Contains("attachment/scope") && items.GroupKeys.Contains("attachment/rail")
                && items.GroupKeys.Contains("ammunition/7.62x39mm") && items.GroupKeys.Contains("magazine/assault_rifle"),
                "The actual gun selector exposes separate scope, rail, calibre and magazine result sections");
            int gunIndex=ids.IndexOf("Weapon_AK47"),scopeIndex=ids.IndexOf("WeaponScope_ACOG_01");
            items.Items[gunIndex].Selected=true;items.Items[scopeIndex].Selected=true;Application.DoEvents();
            Check(items.SelectedIndices.Count==2 && add.Enabled,"Gun and compatible scope rows can be selected across native groups");
            var header=NativeGroupHeader(items,"weapon_ranged/assault_rifle");int headerX=header.Left+8,headerY=header.Top+header.Height/2;
            Check(header.Width>0 && header.Height>0 && items.GetItemAt(headerX,headerY)==null,
                "Actual native result headings have bounds without becoming selectable item rows");
            items.GetType().GetMethod("OnMouseDoubleClick",Hidden).Invoke(items,new object[]{new MouseEventArgs(MouseButtons.Left,2,headerX,headerY,0)});
            Check(cart.Entries.Count==0 && items.SelectedIndices.Count==2,
                "Double-clicking a result heading never adds an existing selected gun or attachment to the cart");
            FocusNativeGroup(items,"attachment/rail",true);var enter=new KeyEventArgs(Keys.Enter);
            items.GetType().GetMethod("OnKeyDown",Hidden).Invoke(items,new object[]{enter});
            Check(enter.Handled && enter.SuppressKeyPress && NativeGroupCollapsed(items,"attachment/rail") && cart.Entries.Count==0,
                "Enter on a focused native heading collapses it without adding earlier item selections");
            var space=new KeyEventArgs(Keys.Space);items.GetType().GetMethod("OnKeyDown",Hidden).Invoke(items,new object[]{space});
            Check(space.Handled && space.SuppressKeyPress && !NativeGroupCollapsed(items,"attachment/rail") && cart.Entries.Count==0,
                "Space on a focused native heading expands it without treating the heading as an item");
            FocusNativeGroup(items,"attachment/rail",false);
            items.SetGroupCollapsed("attachment/scope",true);Application.DoEvents();
            Check(items.IsGroupCollapsed("attachment/scope") && !items.Items[scopeIndex].Selected && items.Items[gunIndex].Selected
                && items.Items.Cast<ListViewItem>().Select(row=>((SpawnItemSearchResult)row.Tag).Item.Id).SequenceEqual(ids),
                "Collapsing scopes clears hidden selection, retains other-group selection and preserves every result ID");
            Check(NativeGroupCollapsed(items,"attachment/scope") && !NativeGroupCollapsed(items,"attachment/rail")
                && !NativeGroupCollapsed(items,"ammunition/7.62x39mm") && !NativeGroupCollapsed(items,"weapon_ranged/assault_rifle"),
                "The Windows ListView actually collapses only the requested heading under visual styles");
            // Native select-all or stale selection notifications must not turn a collapsed row into a spawn request.
            items.Items[scopeIndex].Selected=true;Invoke(dialog,"AddSelected");Application.DoEvents();
            Check(cart.BuildCommands().SequenceEqual(new[]{"#SpawnItem Weapon_AK47 1"}),
                "Adding with a stale hidden selection spawns only visible selected items and cannot spawn a group heading");
            items.SetGroupCollapsed("attachment/scope",false);Application.DoEvents();
            Check(!items.IsGroupCollapsed("attachment/scope") && items.Items.Cast<ListViewItem>().Select(row=>((SpawnItemSearchResult)row.Tag).Item.Id).SequenceEqual(ids),
                "Expanding a result section restores the same source item rows without dropping or duplicating variants");
            Check(!NativeGroupCollapsed(items,"attachment/scope"),"Expanding a section also restores its actual native visibility state");
            items.SelectedIndices.Clear();items.Items[gunIndex].Selected=true;items.Items[scopeIndex].Selected=true;
            Field<NumericUpDown>(dialog,"quantity").Value=2;add.PerformClick();Application.DoEvents();
            Check(cart.BuildCommands().SequenceEqual(new[]{"#SpawnItem Weapon_AK47 3","#SpawnItem WeaponScope_ACOG_01 2"}),
                "Cross-group multi-selection uses the chosen quantity and merges existing cart entries by canonical ID");
            var persisted=new SpawnCart(catalog);ReloadProfileCart(persisted,catalog,path);
            Check(Snapshot(persisted)==Snapshot(cart),"Adding from grouped results preserves immediate cart persistence");
            items.SetGroupsCollapsed(true);Application.DoEvents();
            Check(items.GroupKeys.All(key=>items.IsGroupCollapsed(key)) && items.SelectedIndices.Count==0 && !add.Enabled
                && items.Items.Count==ids.Count,"Collapse-all closes every populated section and disables adding hidden selections");
            items.SetGroupsCollapsed(false);Application.DoEvents();
            Check(items.GroupKeys.All(key=>!items.IsGroupCollapsed(key)) && items.Items.Count==ids.Count,
                "Expand-all restores all populated search sections without altering the matching catalogue");
            items.SetGroupCollapsed("attachment/scope",true);search.Text="AK-47";Application.DoEvents();
            Check(items.IsGroupCollapsed("attachment/scope") && Field<IList<SpawnItem>>(dialog,"filtered").Select(item=>item.Id).SequenceEqual(ids),
                "Equivalent typed gun searches preserve each section's collapse preference and source matches");
            search.Text="AK47 scope";Application.DoEvents();
            Check(items.IsGroupCollapsed("attachment/scope") && items.SelectedIndices.Count==0 && !add.Enabled
                && items.Items.Cast<ListViewItem>().All(row=>((SpawnItemSearchResult)row.Tag).Item.Subcategory=="scope"),
                "Refining a query retains the matching section's state and clears stale selection from earlier results");
            search.Text="AK47";Application.DoEvents();
            Check(items.IsGroupCollapsed("attachment/scope"),"A temporarily filtered-out group retains its explicit collapse preference");
            var collapse=(Button)dialog.Controls.Find("CollapseSpawnResultGroups",true).Single();
            var expand=(Button)dialog.Controls.Find("ExpandSpawnResultGroups",true).Single();
            collapse.PerformClick();Application.DoEvents();
            Check(items.GroupKeys.All(key=>items.IsGroupCollapsed(key)),"The visible Collapse-all action uses the real result-group state");
            expand.PerformClick();Application.DoEvents();
            Check(items.GroupKeys.All(key=>!items.IsGroupCollapsed(key)),"The visible Expand-all action reopens all search-result groups");
            var assault=TreeNodes(tree.Nodes).Single(node=>node.Tag is SpawnCategoryNode
                && ((SpawnCategoryNode)node.Tag).Key=="subcategory/weapons/weapon_ranged/assault_rifle");
            tree.SelectedNode=assault;search.Text="AK47";Application.DoEvents();
            var clearSearch=(Button)dialog.Controls.Find("ClearSpawnItemSearch",true).Single();clearSearch.PerformClick();Application.DoEvents();
            Check(search.Text.Length==0 && Field<IList<SpawnItem>>(dialog,"filtered").All(item=>((SpawnCategoryNode)assault.Tag).Contains(item))
                && items.SelectedIndices.Count==0 && !add.Enabled,
                "Clear-search resets typed filtering while retaining the selected category and clearing stale selection");
            search.Text="no matching item exists";Application.DoEvents();
            Check(items.Items.Count==0 && items.Groups.Count==0 && !add.Enabled,
                "An unmatched query has no selectable placeholder rows or empty result groups");
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Grouped-result UI regressions never activate test windows or send game input");
        }
    }

    static void TestSelectorLayout(SpawnItemCatalog catalog,string directory) {
        using(var inactive=new InactiveUi())using(var dialog=new SpawnCartDialog(catalog,ProfileCartPath(directory,"layout-cart.tsv"),(commands,owner,progress)=> {
            throw new Exception("Layout checks must not dispatch game commands.");
        })) {
            inactive.Show(dialog);Field<TextBox>(dialog,"search").Text="AK47";Application.DoEvents();
            foreach(var size in new[]{new System.Drawing.Size(1000,660),new System.Drawing.Size(1280,760)}) {
                dialog.Size=size;dialog.PerformLayout();Application.DoEvents();
                foreach(string name in new[]{"SpawnItemCategories","SpawnItemResults","SpawnItemSearch","SpawnItemQuantity","SpawnCartEntries",
                    "SpawnCartPreview","ClearSpawnItemSearch","ExpandSpawnResultGroups","CollapseSpawnResultGroups","EditSpawnModifiers","SendSpawnCart",
                    "SpawnItemSelection","SpawnCartStatus"}) {
                    var control=dialog.Controls.Find(name,true).Single();
                    if(!control.Visible || !control.Parent.ClientRectangle.Contains(control.Bounds))
                        Console.WriteLine("LAYOUT: "+name+" Visible="+control.Visible+" Bounds="+control.Bounds+" Parent="+control.Parent.ClientRectangle);
                    Check(control.Visible && control.Width>0 && control.Height>0 && control.Parent.ClientRectangle.Contains(control.Bounds),
                        "Polished selector keeps "+name+" visible inside its parent at "+size.Width+"x"+size.Height);
                }
                var actions=new[]{"add","editModifiers","remove","clear","save","load","submit"}.Select(name=>Field<Button>(dialog,name)).ToList();
                Check(actions.All(button=>button.Parent.ClientRectangle.Contains(button.Bounds)),
                    "Every cart action stays reachable without clipping at "+size.Width+"x"+size.Height);
                var search=Field<TextBox>(dialog,"search");var clear=(Button)dialog.Controls.Find("ClearSpawnItemSearch",true).Single();
                Check(!search.Bounds.IntersectsWith(clear.Bounds),"Search entry and Clear action do not overlap at "+size.Width+"x"+size.Height);
                var tree=Field<TreeView>(dialog,"categoryTree");var items=Field<ListView>(dialog,"items");
                Check(!tree.Bounds.IntersectsWith(items.Bounds),"Categories and grouped results occupy distinct visible panes at "+size.Width+"x"+size.Height);
            }
            var searchBox=Field<TextBox>(dialog,"search");searchBox.Text="unmatched item query";Application.DoEvents();
            var empty=dialog.Controls.Find("SpawnItemEmptyResults",true).Single();
            Check(empty.Visible && empty.Parent.ClientRectangle.Contains(empty.Bounds) && !Field<ListView>(dialog,"items").Visible
                && !dialog.Controls.Find("ExpandSpawnResultGroups",true).Single().Enabled && !dialog.Controls.Find("CollapseSpawnResultGroups",true).Single().Enabled,
                "An empty query result displays its explanation and disables unavailable group actions");
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Selector layout regressions never activate off-screen test windows");
        }
    }

    static void TestDialogSaveFailure(SpawnItemCatalog catalog,string directory) {
        string path=ProfileCartPath(directory,"dialog-save-failure.tsv");
        var seed=new SpawnCart(catalog);seed.AddMany(catalog.Items.Take(2).Select(item=>item.Id),1);seed.Save(path);
        int sent=0;SpawnBatchResult result=null;
        using(var dialog=new SpawnCartDialog(catalog,path,(commands,owner,progress)=> {
            using(var locked=File.Open(ProfileDatabasePath(path),FileMode.Open,FileAccess.Read,FileShare.None)) {
                result=Native.AdminChatBatchSequence(commands.Count,index=>true,
                    (index,submitted)=> { sent++;submitted();return Task.FromResult(true); },
                    milliseconds=>Task.FromResult(0),()=>true,progress).GetAwaiter().GetResult();
            }
            owner.Dispose();return Task.FromResult(result);
        })) {
            var cart=Field<SpawnCart>(dialog,"cart");AwaitUi(dialog.SendCart());
            Check(result!=null && result.SubmittedCount==1 && !result.Completed && sent==1 && cart.Entries.Count==1,
                "A post-Enter cart-save failure stops the UI batch after one dispatch and removes it from in-memory retries");
            Check(result.Error.Contains(Localization.Get("SpawnCartSaveStopped")),"Post-Enter persistence failure reports that the saved kit needs review before reuse");
            var saved=new SpawnCart(catalog);ReloadProfileCart(saved,catalog,path);
            Check(saved.Entries.Count==2,"A failed post-Enter disk replacement leaves the previous kit intact for explicit review");
        }
    }

    static void TestButtonClicks(SpawnItemCatalog catalog,string directory) {
        using(var inactive=new InactiveUi()) {
            foreach(string editorValue in new[]{"none","7","0"}) {
                string path=ProfileCartPath(directory,"click-"+editorValue+".tsv");
                int calls=0;IList<string> commands=null;
                var pending=new TaskCompletionSource<SpawnBatchResult>();
                using(var dialog=new SpawnCartDialog(catalog,path,(batch,owner,progress)=> {
                    calls++;commands=new List<string>(batch);return pending.Task;
                })) {
                    inactive.Show(dialog);
                    var items=Field<ListView>(dialog,"items");var add=Field<Button>(dialog,"add");
                    var grid=Field<DataGridView>(dialog,"grid");var cart=Field<SpawnCart>(dialog,"cart");
                    var submit=Field<Button>(dialog,"submit");var category=Field<TreeView>(dialog,"categoryTree");
                    System.Drawing.Rectangle actual=category.RectangleToScreen(category.ClientRectangle);
                    Console.WriteLine("CATEGORY: Visible="+category.Visible+" Bounds="+category.Bounds+" Screen="+actual+" Parent="+category.Parent.ClientRectangle);
                    Check(category.Visible && category.Width>20 && category.Height>10 && category.Parent.ClientRectangle.Contains(category.Bounds),
                        "Category tree has visible bounds inside its parent on a shown form");
                    items.SelectedIndices.Add(0);Field<NumericUpDown>(dialog,"quantity").Value=2;
                    add.PerformClick();Application.DoEvents();
                    Check(cart.Entries.Count==1 && cart.TotalQuantity==2 && submit.Enabled,"The real Add selected button populates and enables the cart");
                    dialog.ActiveControl=grid;grid.CurrentCell=grid.Rows[0].Cells[1];
                    if(editorValue!="none") {
                        Check(grid.BeginEdit(false),"The click regression enters the quantity editor");
                        ((TextBox)grid.EditingControl).Text=editorValue;
                    }
                    submit.PerformClick();Application.DoEvents();
                    Console.WriteLine("CLICK: Editor="+editorValue+" SenderCalls="+calls+" CartQuantity="+cart.TotalQuantity+" Status="+Field<Label>(dialog,"status").Text);
                    if(editorValue=="0")Check(calls==0 && cart.TotalQuantity==2 && Field<Label>(dialog,"status").Text==Localization.Get("SpawnCartInvalid"),
                        "The real Send cart button blocks an invalid active quantity edit and explains it");
                    else Check(calls==1 && commands.Count==1 && commands[0]=="#SpawnItem "+catalog.Items[0].Id+" "+(editorValue=="none"?"2":"7"),
                        "The real Send cart button invokes the sender with the valid current quantity: "+editorValue);
                    dialog.Dispose();pending.SetResult(new SpawnBatchResult { Error="Simulated preflight rejection" });Application.DoEvents();
                    Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Button clicks and fake submission never bring test windows to the foreground");
                }
            }
            string failurePath=ProfileCartPath(directory,"click-preflight-failure.tsv"),reason="Simulated preflight rejection: close SCUM chat and inventory before sending.";
            int failures=0;
            using(var dialog=new SpawnCartDialog(catalog,failurePath,(commands,owner,progress)=> {
                failures++;return Task.FromResult(new SpawnBatchResult { Error=reason });
            })) {
                inactive.Show(dialog);Field<ListView>(dialog,"items").SelectedIndices.Add(0);Field<Button>(dialog,"add").PerformClick();
                Field<Button>(dialog,"submit").PerformClick();Application.DoEvents();
                var status=Field<Label>(dialog,"status");
                Console.WriteLine("FAILURE STATUS: Visible="+status.Visible+" Bounds="+status.Bounds+" Parent="+status.Parent.ClientRectangle+" Text="+status.Text);
                int textHeight=TextRenderer.MeasureText(status.Text,status.Font,new System.Drawing.Size(status.Width-status.Padding.Horizontal,int.MaxValue),TextFormatFlags.WordBreak).Height;
                Check(failures==1 && status.Text==Localization.T("SpawnCartNotSent",reason) && status.Visible && status.Width>100
                    && status.Height>=textHeight+status.Padding.Vertical && !status.AutoEllipsis,
                    "A real Send cart click exposes a fake preflight rejection in the visible status label");
                Check(Field<SpawnCart>(dialog,"cart").Entries.Count==1 && !Native.IsOurWindow(Native.GetForegroundWindow()),
                    "A rejected button submission preserves the cart without bringing test windows to the foreground");
            }
        }
    }

    static void TestSavedKitControls(SpawnItemCatalog catalog,string directory) {
        string legacy=ProfileCartPath(directory,"saved-kit-controls.tsv");int editors=0,confirmations=0;
        using(var inactive=new InactiveUi())using(var dialog=new SpawnCartDialog(catalog,legacy,(commands,owner,progress)=> {
            throw new Exception("Saved-kit controls must never dispatch game commands.");
        })) {
            inactive.Show(dialog);var items=Field<ListView>(dialog,"items");items.SelectedIndices.Add(0);items.SelectedIndices.Add(1);
            Field<NumericUpDown>(dialog,"quantity").Value=2;Field<Button>(dialog,"add").PerformClick();Application.DoEvents();
            var cart=Field<SpawnCart>(dialog,"cart");var grid=Field<DataGridView>(dialog,"grid");var database=Field<SpawnKitDatabase>(dialog,"kitDatabase");
            string name="Rifle kit · O'Brien 日本語";cart.SetModifiers(cart.Entries[0].Item.Id,"Uses 0 Weight -0.5");Invoke(dialog,"RefreshCart");
            dialog.SetKitEditor(editor=> {
                editors++;inactive.Show(editor);Check(Field<bool>(editor,"saving"),"The actual Save button opens the named database-kit editor");
                var names=(TextBox)editor.Controls.Find("SpawnKitName",true).Single();names.Text=name;
                editor.Controls.Find("ApplySpawnKit",true).OfType<Button>().Single().PerformClick();
                Check(editor.KitName==name && editor.DialogResult==DialogResult.OK,"The kit name control and real Save action return a literal Unicode name");
                return editor.DialogResult;
            });
            grid.CurrentCell=grid.Rows[0].Cells[1];Check(grid.BeginEdit(false),"Named-kit save regression enters a pending quantity edit");((TextBox)grid.EditingControl).Text="7";
            Field<Button>(dialog,"save").PerformClick();Application.DoEvents();
            var saved=new SpawnCart(catalog);var kit=database.ListKits().Single();database.LoadKit(kit.Id,saved);
            Check(editors==1 && cart.Entries[0].Quantity==7 && Snapshot(saved)==Snapshot(cart) && Field<Label>(dialog,"status").Text==Localization.T("SpawnCartKitSaved",name),
                "Saving from the actual cart commits its active editor and stores the complete named kit with visible feedback");
            Check(!File.Exists(legacy) && Directory.GetFiles(Path.GetDirectoryName(legacy),"*.tsv").Length==0,
                "The named-kit UI uses its AppData-style database without opening or writing loose TSV kits");
            string kitSnapshot=Snapshot(saved);cart.SetQuantity(cart.Entries[0].Item.Id,9);Invoke(dialog,"RefreshCart");Invoke(dialog,"Persist");
            dialog.SetKitEditor(editor=> {
                editors++;inactive.Show(editor);var list=(ListBox)editor.Controls.Find("SpawnSavedKits",true).Single();
                Check(list.Items.Count==1 && list.GetItemText(list.Items[0])==name,"The saved-kit list displays the human kit name instead of its CLR class name");
                list.SelectedIndex=0;
                Check(((TextBox)editor.Controls.Find("SpawnKitName",true).Single()).Text==name && editor.Controls.Find("SpawnKitDetails",true).Single().Text.Length>0,
                    "Selecting a saved kit fills its editable name and shows item and quantity details");
                Check(editor.Apply(),"The saved-kit editor validates a selected overwrite name");return DialogResult.OK;
            });
            dialog.SetKitOverwriteConfirmation(selected=> { confirmations++;Check(selected==name,"Overwrite confirmation names the exact saved kit");return false; });
            Field<Button>(dialog,"save").PerformClick();Application.DoEvents();database.LoadKit(kit.Id,saved);
            Check(confirmations==1 && Snapshot(saved)==kitSnapshot && cart.Entries[0].Quantity==9,"Declining overwrite preserves the existing kit and the user's current cart");
            dialog.SetKitOverwriteConfirmation(selected=> { confirmations++;return true; });Field<Button>(dialog,"save").PerformClick();Application.DoEvents();database.LoadKit(kit.Id,saved);
            Check(confirmations==2 && database.ListKits().Single().Id==kit.Id && Snapshot(saved)==Snapshot(cart),"Confirming the actual Save action replaces the selected kit under its stable ID");
            dialog.SetKitEditor(editor=> { editors++;return DialogResult.Cancel; });cart.SetQuantity(cart.Entries[0].Item.Id,11);Invoke(dialog,"RefreshCart");Invoke(dialog,"Persist");
            Field<Button>(dialog,"save").PerformClick();Application.DoEvents();database.LoadKit(kit.Id,saved);
            Check(saved.Entries[0].Quantity==9,"Canceling the kit-name dialog does not replace the stored kit");
            dialog.SetKitEditor(editor=> {
                editors++;inactive.Show(editor);Check(!Field<bool>(editor,"saving"),"The actual Load button opens the database-kit selector");
                var list=(ListBox)editor.Controls.Find("SpawnSavedKits",true).Single();list.SelectedIndex=list.Items.Cast<SpawnSavedKit>().ToList().FindIndex(item=>item.Id==kit.Id);
                editor.Controls.Find("ApplySpawnKit",true).OfType<Button>().Single().PerformClick();
                Check(editor.KitId==kit.Id && editor.KitName==name,"The real kit Load action uses the selected stable database ID");return editor.DialogResult;
            });
            FixtureSql(database.DatabasePath,"CREATE TRIGGER test_ui_load_abort BEFORE INSERT ON spawn_cart_entries WHEN NEW.position=1 BEGIN SELECT RAISE(ABORT,'fixture load interruption'); END");
            string current=Snapshot(cart);Field<Button>(dialog,"load").PerformClick();Application.DoEvents();var persisted=new SpawnCart(catalog);ReloadProfileCart(persisted,catalog,legacy);
            Check(Snapshot(cart)==current && Snapshot(persisted)==current && Field<Label>(dialog,"status").Text.StartsWith(Localization.T("SpawnCartFileError",""),StringComparison.Ordinal),
                "If persisting a selected kit fails, Load preserves both the active cart and previously saved working cart");
            FixtureSql(database.DatabasePath,"DROP TRIGGER test_ui_load_abort");Field<Button>(dialog,"load").PerformClick();Application.DoEvents();ReloadProfileCart(persisted,catalog,legacy);
            Check(Snapshot(cart)==Snapshot(saved) && Snapshot(persisted)==Snapshot(saved) && Field<Label>(dialog,"status").Text==Localization.T("SpawnCartKitLoaded",name),
                "The actual Load action replaces and persists the working cart only after the saved kit is valid");
            string external=Path.Combine(directory,"external-kit-ui.tsv");var imported=new SpawnCart(catalog);imported.Add(catalog.Items[2].Id,3);imported.SetModifiers(catalog.Items[2].Id,"Health 75 StackCount 5");imported.Save(external);
            byte[] original=File.ReadAllBytes(external);string cartBeforeImport=Snapshot(cart);int importEditors=0,pickers=0;
            dialog.SetLegacyKitPicker(()=> { pickers++;return external; });dialog.SetKitEditor(editor=> {
                importEditors++;inactive.Show(editor);
                if(!Field<bool>(editor,"saving")) { editor.Controls.Find("ImportLegacySpawnKit",true).OfType<Button>().Single().PerformClick();Check(editor.ImportRequested,"The explicit legacy-import action is available from the saved-kit selector"); }
                else { ((TextBox)editor.Controls.Find("SpawnKitName",true).Single()).Text="Imported legacy kit";Check(editor.Apply(),"Imported loose kits use the same validated database-kit naming editor"); }
                return editor.DialogResult;
            });
            Field<Button>(dialog,"load").PerformClick();Application.DoEvents();var importedKit=database.ListKits().Single(item=>item.Name=="Imported legacy kit");database.LoadKit(importedKit.Id,persisted);
            Check(pickers==1 && importEditors==2 && Snapshot(persisted)==Snapshot(imported) && Snapshot(cart)==cartBeforeImport && File.ReadAllBytes(external).SequenceEqual(original),
                "The actual legacy-import workflow stores a named database kit while preserving the open cart and source TSV");
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Saved-kit editor, load and import regressions never activate test windows or send game input");
        }
        using(var inactive=new InactiveUi())using(var editor=new SpawnKitDialog(false,new List<SpawnSavedKit>(),null)) {
            inactive.Show(editor);Check(!editor.Controls.Find("ApplySpawnKit",true).Single().Enabled && editor.Controls.Find("ImportLegacySpawnKit",true).Single().Visible
                && editor.Controls.Find("SpawnKitDetails",true).Single().Text==Localization.Get("SpawnCartNoSavedKits"),"An empty saved-kit selector explains its state and still exposes legacy import");
        }
        using(var editor=new SpawnKitDialog(true,new List<SpawnSavedKit>(),null)) {
            var names=(TextBox)editor.Controls.Find("SpawnKitName",true).Single();names.Text="Bad\nName";
            Check(!editor.Apply() && editor.KitName==null && editor.Controls.Find("SpawnKitError",true).Single().Text.Length>0,"The named-kit editor rejects control characters before accepting a save");
            names.Text="  Cafe\u0301  ";Check(editor.Apply() && editor.KitName=="Café","The named-kit editor trims and normalizes Unicode names consistently with the database");
        }
    }

    static void TestModifierDialog() {
        using(var dialog=new SpawnModifierDialog("Test rifle","Health 75 Uses 0 Weight 0.5")) {
            var fields=Field<Dictionary<string,Control>>(dialog,"fields");
            Check(fields.Count==8 && fields["Health"].Text=="75" && fields["Uses"].Text=="0" && fields["Weight"].Text=="0.5" && fields["Location"].Text=="",
                "Per-item modifier popup restores every explicit saved field including zero");
            var sectors=(ComboBox)fields["KeyCardSector"];
            Check(sectors.Items.Count==17 && sectors.Items[0].ToString()=="" && sectors.Items[1].ToString()=="A1" && sectors.Items[16].ToString()=="D4",
                "Keycard modifier offers defaults plus exactly the sixteen source sectors");
            fields["Health"].Text="0";fields["Dirtiness"].Text="0";fields["StackCount"].Text="3";fields["Radiation"].Text="0";sectors.SelectedItem="C2";
            Check(dialog.Apply() && dialog.Result=="Health 0 Uses 0 Dirtiness 0 KeyCardSector C2 StackCount 3 Radiation 0 Weight 0.5",
                "Applying modifier popup fields produces one validated canonical property string");
        }
        using(var dialog=new SpawnModifierDialog("Test rifle","")) {
            var fields=Field<Dictionary<string,Control>>(dialog,"fields");fields["Uses"].Text="-1";
            Check(!dialog.Apply() && dialog.Result==null && Field<Label>(dialog,"error").Text.Length>0,
                "Invalid popup values keep the editor open and display their validation error");
            fields["Uses"].Text="0";fields["StackCount"].Text="0";
            Check(!dialog.Apply() && dialog.Result==null,"Popup distinguishes zero remaining uses from an invalid zero stack count");
        }
        using(var inactive=new InactiveUi())using(var dialog=new SpawnModifierDialog("Test rifle","Health 50 Uses 0 Location \"X=-1 Y=2 Z=3\" KeyCardSector B3 Weight 2")) {
            inactive.Show(dialog);
            var fields=Field<Dictionary<string,Control>>(dialog,"fields");
            var reset=dialog.Controls.Find("ResetSpawnModifiers",true).OfType<Button>().Single();reset.PerformClick();Application.DoEvents();
            Check(fields.Values.All(field=>field.Text=="") && Field<Dictionary<string,CheckBox>>(dialog,"selected").Values.All(option=>!option.Checked)
                && dialog.Apply() && dialog.Result=="",
                "Real reset button clears every modifier field including the sector dropdown and restores game defaults");
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Modifier popup validation and reset never bring test windows to the foreground");
        }
    }

    static void TestModifierSelections() {
        using(var inactive=new InactiveUi()) {
            using(var dialog=new SpawnModifierDialog("Selectable modifiers","")) {
                inactive.Show(dialog);var fields=Field<Dictionary<string,Control>>(dialog,"fields");
                var selected=Field<Dictionary<string,CheckBox>>(dialog,"selected");
                Check(fields.Count==8 && fields.Values.All(field=>field is ComboBox && field.Enabled)
                    && selected.Count==8 && selected.Values.All(option=>!option.Checked),
                    "All eight optional modifiers start at game defaults with directly selectable value controls");
                Check(((ComboBox)fields["Health"]).DropDownStyle==ComboBoxStyle.DropDown
                    && ((ComboBox)fields["KeyCardSector"]).DropDownStyle==ComboBoxStyle.DropDownList,
                    "Numeric presets permit custom values while keycard sectors remain constrained to documented choices");
                var location=(ComboBox)fields["Location"];
                Check(location.Items.Count==1 && location.Items[0].ToString()=="" && location.MaxLength==512,
                    "Location offers the game default without inventing coordinate or player targets");
                var chosen=new Dictionary<string,string> { {"Health","100"},{"Uses","0"},{"Dirtiness","25"},{"KeyCardSector","D4"},
                    {"StackCount","5"},{"Radiation","0"},{"Weight","0.5"} };
                foreach(var choice in chosen) {
                    var control=(ComboBox)fields[choice.Key];
                    Check(control.Items.Contains(choice.Value),"A useful spawn modifier value is selectable: "+choice.Key);
                    control.SelectedItem=choice.Value;
                }
                Application.DoEvents();
                Check(chosen.Keys.All(property=>selected[property].Checked) && !selected["Location"].Checked,
                    "Choosing dropdown values selects just their corresponding modifiers, including explicit zero values");
                selected["Health"].Checked=false;Application.DoEvents();
                Check(fields["Health"].Text=="" && !selected["Health"].Checked && selected["Uses"].Checked,
                    "Deselecting a modifier clears its override and preserves the other selected modifiers");
                ((ComboBox)fields["Health"]).SelectedItem="75";Application.DoEvents();
                Check(selected["Health"].Checked && fields["Health"].Text=="75","A later dropdown choice reselects a modifier without typing property syntax");
                dialog.Controls.Find("ApplySpawnModifiers",true).OfType<Button>().Single().PerformClick();Application.DoEvents();
                Check(dialog.Result=="Health 75 Uses 0 Dirtiness 25 KeyCardSector D4 StackCount 5 Radiation 0 Weight 0.5",
                    "The actual Apply button builds canonical modifiers entirely from dropdown choices");
            }
            using(var dialog=new SpawnModifierDialog("Selectable modifiers","")) {
                inactive.Show(dialog);var fields=Field<Dictionary<string,Control>>(dialog,"fields");
                var selected=Field<Dictionary<string,CheckBox>>(dialog,"selected");selected["Location"].Checked=true;
                dialog.Controls.Find("ApplySpawnModifiers",true).OfType<Button>().Single().PerformClick();Application.DoEvents();
                Check(dialog.Result==null && Field<Label>(dialog,"error").Text.Length>0,
                    "Selecting an optional modifier without a value displays an error instead of silently using defaults");
                fields["Location"].Text="X=-1.5 Y=2 Z=0.5";
                ((ComboBox)fields["Uses"]).SelectedItem="0";
                ((ComboBox)fields["Uses"]).SelectedItem="";Application.DoEvents();
                Check(!selected["Uses"].Checked && selected["Location"].Checked,
                    "Choosing the blank dropdown entry restores defaults while a custom Location remains selected");
                fields["Weight"].Text="-0.75";
                dialog.Controls.Find("ApplySpawnModifiers",true).OfType<Button>().Single().PerformClick();Application.DoEvents();
                Check(dialog.Result=="Location \"X=-1.5 Y=2 Z=0.5\" Weight -0.75",
                    "Selectable controls retain custom Location and signed numeric values through the actual Apply button");
            }
            using(var dialog=new SpawnModifierDialog("Saved selections","Uses 2147483647 Location \"{X=-1 Y=2 Z=3|P=0 Y=90 R=0}\" Weight -0.75")) {
                inactive.Show(dialog);var fields=Field<Dictionary<string,Control>>(dialog,"fields");var selected=Field<Dictionary<string,CheckBox>>(dialog,"selected");
                Check(new[]{"Uses","Location","Weight"}.All(property=>selected[property].Checked
                    && ((ComboBox)fields[property]).Items.Contains(fields[property].Text)) && !selected["Health"].Checked,
                    "Saved custom values appear as selected dropdown entries without changing unrelated game defaults");
                Check(((ComboBox)fields["Location"]).Items.Count==2,"A saved Location dropdown contains only the default and its own current target");
                dialog.Controls.Find("ResetSpawnModifiers",true).OfType<Button>().Single().PerformClick();Application.DoEvents();
                Check(fields.Values.All(field=>field.Text=="") && selected.Values.All(option=>!option.Checked)
                    && dialog.Apply() && dialog.Result=="","Reset clears preset and custom selections for all eight modifiers");
            }
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Selectable modifier controls and button tests never activate test windows");
        }
    }

    static void TestLocationDialog() {
        string[] saved={"\"X=-1.5 Y=2 Z=0.5\"","\"{X=-1.5 Y=2 Z=0.5|P=-0.25 Y=90 R=0}\"","76561198000000000","-1.5 2 0.5"};
        string[] pasted={"x=-001.50 y=+02.000 z=.5","{x=-001.50 y=+02.000 z=.5|p=-00.25 y=+90.000 r=0.0}",saved[2],"-001.50 +02.000 .5"};
        using(var inactive=new InactiveUi()) {
            for(int i=0;i<saved.Length;i++)using(var dialog=new SpawnModifierDialog("Location fixture","Health 75 Location "+saved[i]+" Uses 0 Weight -0.5")) {
                inactive.Show(dialog);var fields=Field<Dictionary<string,Control>>(dialog,"fields");
                Check(fields["Location"].Text==saved[i] && fields["Uses"].Text=="0" && fields["Weight"].Text=="-0.5",
                    "Reopening the modifier popup restores a complete Location and following fields: "+i);
                Check(fields.Values.All(field=>field.Visible && field.Height>=20 && field.Parent.ClientRectangle.Contains(field.Bounds)),
                    "All eight modifier controls have readable visible bounds inside the popup");
                fields["Location"].Text=pasted[i];fields["Weight"].Text="-00.500";
                Check(dialog.Apply() && dialog.Result=="Health 75 Uses 0 Location "+saved[i]+" Weight -0.5",
                    "Location popup accepts a pasted source value and preserves negative scalar values: "+i);
                using(var reopened=new SpawnModifierDialog("Location fixture",dialog.Result))
                    Check(Field<Dictionary<string,Control>>(reopened,"fields")["Location"].Text==saved[i],
                        "Applying and reopening a Location field retains its serialized format: "+i);
            }
            using(var dialog=new SpawnModifierDialog("Location fixture","Location \"X=-1 Y=2 Z=3\" Uses 0")) {
                inactive.Show(dialog);var fields=Field<Dictionary<string,Control>>(dialog,"fields");
                fields["Location"].Text="X=1 Y=2 Z=3;#Kick";
                Check(!dialog.Apply() && dialog.Result==null && Field<Label>(dialog,"error").Text.Length>0,
                    "An unsafe custom Location keeps the popup open with a visible validation error");
                fields["Location"].Text="";
                Check(dialog.Apply() && dialog.Result=="Uses 0","Clearing Location restores the default spawn position without removing another selected modifier");
            }
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Location popup regressions never activate test windows");
        }
    }

    static void TestBulkModifierDialog() {
        string[] modifiers={"Health 100 Uses 5 Location \"X=-1.5 Y=2 Z=0.5\" Weight 0.5","Health 100 Uses 0 Radiation 1 Weight 2"};
        string mixed=Localization.Get("SpawnCartModifierMixed");
        using(var inactive=new InactiveUi()) {
            using(var dialog=new SpawnModifierDialog("Two selected items",modifiers,true)) {
                inactive.Show(dialog);var fields=Field<Dictionary<string,Control>>(dialog,"fields");var selected=Field<Dictionary<string,CheckBox>>(dialog,"selected");
                Check(selected["Health"].CheckState==CheckState.Checked && fields["Health"].Text=="100"
                    && selected["Dirtiness"].CheckState==CheckState.Unchecked && fields["Dirtiness"].Text=="",
                    "Bulk editor displays common explicit values separately from a common game default");
                Check(new[]{"Uses","Weight","Location","Radiation"}.All(property=>selected[property].CheckState==CheckState.Indeterminate && fields[property].Text==mixed),
                    "Different values and overrides missing from one selected item are shown as mixed, including Location");
                Check(((ComboBox)fields["Location"]).Items.Contains("\"X=-1.5 Y=2 Z=0.5\"")
                    && ((ComboBox)fields["Weight"]).Items.Contains("0.5") && ((ComboBox)fields["Weight"]).Items.Contains("2"),
                    "Bulk value selectors offer existing per-item values without replacing a mixed selection automatically");
                Check(fields.Values.All(field=>field.Visible && field.Height>=20 && field.Parent.ClientRectangle.Contains(field.Bounds)),
                    "All eight bulk modifier controls remain readable inside the larger popup");
                dialog.Controls.Find("ApplySpawnModifiers",true).OfType<Button>().Single().PerformClick();Application.DoEvents();
                Check(dialog.Changes!=null && dialog.Changes.IsReadOnly && dialog.Changes.Count==0,
                    "Applying an untouched bulk editor preserves all common, mixed and missing properties through an empty patch");
                Reject<NotSupportedException>(()=>dialog.Changes["Weight"]="1","Applied bulk patch cannot be mutated after validation");
            }
            using(var dialog=new SpawnModifierDialog("Two selected items",modifiers,true)) {
                inactive.Show(dialog);var fields=Field<Dictionary<string,Control>>(dialog,"fields");var selected=Field<Dictionary<string,CheckBox>>(dialog,"selected");
                ((ComboBox)fields["Weight"]).SelectedItem="1";
                fields["Location"].Text="x=-001.50 y=+02.000 z=.5";
                typeof(CheckBox).GetMethod("OnClick",Hidden).Invoke(selected["Health"],new object[]{EventArgs.Empty});
                ((ComboBox)fields["Uses"]).SelectedItem="0";((ComboBox)fields["Uses"]).SelectedItem=mixed;Application.DoEvents();
                Check(selected["Health"].CheckState==CheckState.Unchecked && fields["Health"].Text==""
                    && selected["Uses"].CheckState==CheckState.Indeterminate,
                    "Bulk checkbox click removes a common override, while choosing Mixed restores individual values");
                Check(dialog.Apply() && dialog.Changes.Count==3 && dialog.Changes["Weight"]=="1" && dialog.Changes["Health"]==null
                    && dialog.Changes["Location"]=="\"X=-1.5 Y=2 Z=0.5\"" && !dialog.Changes.ContainsKey("Uses") && !dialog.Changes.ContainsKey("Radiation"),
                    "Bulk editor emits only touched properties, normalizes pasted Location and leaves untouched mixed overrides out of its patch");
            }
            using(var dialog=new SpawnModifierDialog("Two selected items",modifiers,true)) {
                inactive.Show(dialog);var selected=Field<Dictionary<string,CheckBox>>(dialog,"selected");
                typeof(CheckBox).GetMethod("OnClick",Hidden).Invoke(selected["Uses"],new object[]{EventArgs.Empty});
                Check(selected["Uses"].CheckState==CheckState.Checked && !dialog.Apply() && dialog.Changes==null,
                    "Selecting a mixed modifier without a replacement value blocks Apply instead of clearing individual values");
                typeof(CheckBox).GetMethod("OnClick",Hidden).Invoke(selected["Uses"],new object[]{EventArgs.Empty});
                Check(dialog.Apply() && dialog.Changes.Count==1 && dialog.Changes["Uses"]==null,
                    "Deselecting a mixed modifier produces one explicit removal for the selected items");
            }
            using(var dialog=new SpawnModifierDialog("Two selected items",modifiers,true)) {
                inactive.Show(dialog);var fields=Field<Dictionary<string,Control>>(dialog,"fields");
                fields["Weight"].Text="1 Uses 0";
                Check(!dialog.Apply() && dialog.Changes==null && Field<Label>(dialog,"error").Text.Length>0,
                    "A bulk value field cannot inject another valid modifier property");
                fields["Weight"].Text="";fields["Location"].Text="X=1 Y=2 Z=3;#Kick";
                Check(!dialog.Apply() && dialog.Changes==null,"An unsafe Location cannot escape a bulk property field");
                dialog.Controls.Find("ResetSpawnModifiers",true).OfType<Button>().Single().PerformClick();Application.DoEvents();
                Check(fields.Values.All(field=>field.Text=="") && Field<Dictionary<string,CheckBox>>(dialog,"selected").Values.All(option=>option.CheckState==CheckState.Unchecked)
                    && dialog.Apply() && dialog.Changes.Count==SpawnModifiers.PropertyNames.Count && dialog.Changes.Values.All(value=>value==null),
                    "Real bulk Reset clears every field and emits explicit removal of all eight modifiers");
            }
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Bulk editor regressions never activate test windows");
        }
    }

    static void TestBulkModifierButtonClicks(SpawnItemCatalog catalog,string directory) {
        string path=ProfileCartPath(directory,"bulk-editor-click.tsv");var seed=new SpawnCart(catalog);
        string first=catalog.Items[0].Id,second=catalog.Items[1].Id,third=catalog.Items[2].Id;
        seed.Add(first,2);seed.Add(second,3);seed.Add(third,4);
        seed.SetModifiers(first,"Health 100 Uses 5 Location \"X=-1.5 Y=2 Z=0.5\" Weight 0.5");
        seed.SetModifiers(second,"Health 100 Uses 0 Radiation 1 Weight 2");seed.SetModifiers(third,"Weight -1");seed.Save(path);
        using(var inactive=new InactiveUi())using(var dialog=new SpawnCartDialog(catalog,path,(commands,owner,progress)=> { throw new Exception("Bulk editing must not dispatch game commands."); })) {
            inactive.Show(dialog);var grid=Field<DataGridView>(dialog,"grid");var cart=Field<SpawnCart>(dialog,"cart");var edit=Field<Button>(dialog,"editModifiers");
            grid.ClearSelection();Application.DoEvents();
            Check(grid.MultiSelect && !edit.Enabled,"Bulk modifier action is disabled until at least one cart row is selected");
            grid.Rows[0].Selected=true;grid.Rows[1].Selected=true;Application.DoEvents();
            Check(edit.Enabled && grid.SelectedRows.Count==2 && !edit.CausesValidation,"The actual modifier action accepts multiple selected cart rows and commits its own active editor");
            int calls=0;string before=Snapshot(cart),savedBefore=File.ReadAllText(path),unselected=cart.Entries[2].Command;
            dialog.SetModifierEditor(editor=> {
                calls++;var fields=Field<Dictionary<string,Control>>(editor,"fields");var selected=Field<Dictionary<string,CheckBox>>(editor,"selected");
                Check(selected["Health"].CheckState==CheckState.Checked && selected["Uses"].CheckState==CheckState.Indeterminate
                    && fields["Location"].Text==Localization.Get("SpawnCartModifierMixed"),
                    "The actual Edit button initializes a bulk popup from only its selected cart rows");
                ((ComboBox)fields["Weight"]).SelectedItem="5";return DialogResult.Cancel;
            });
            edit.PerformClick();Application.DoEvents();
            Check(calls==1 && Snapshot(cart)==before && File.ReadAllText(path)==savedBefore && grid.SelectedRows.Count==2,
                "Cancelling a changed bulk editor preserves the cart, saved kit and row selection");
            dialog.SetModifierEditor(editor=> { calls++;Check(editor.Apply() && editor.Changes.Count==0,"Untouched actual bulk popup produces an empty patch");return DialogResult.OK; });
            edit.PerformClick();Application.DoEvents();
            Check(calls==2 && Snapshot(cart)==before && File.ReadAllText(path)==savedBefore,"Applying an untouched bulk popup performs no cart or kit mutation");
            dialog.SetModifierEditor(editor=> {
                calls++;var fields=Field<Dictionary<string,Control>>(editor,"fields");((ComboBox)fields["Weight"]).SelectedItem="1";
                Field<Dictionary<string,CheckBox>>(editor,"selected")["Health"].CheckState=CheckState.Unchecked;
                Check(editor.Apply(),"Actual bulk popup validates the selected preset and removal");return DialogResult.OK;
            });
            edit.PerformClick();Application.DoEvents();
            Check(calls==3 && cart.Entries[0].ModifiersText=="Uses 5 Location \"X=-1.5 Y=2 Z=0.5\" Weight 1"
                && cart.Entries[1].ModifiersText=="Uses 0 Radiation 1 Weight 1" && cart.Entries[2].Command==unselected,
                "The real Edit button patches every selected item and preserves differing untouched modifiers plus unselected rows");
            Check(grid.SelectedRows.Count==2 && grid.Rows[0].Selected && grid.Rows[1].Selected && !grid.Rows[2].Selected
                && cart.Entries.Select(entry=>entry.Quantity).SequenceEqual(new[]{2,3,4}),
                "Applied bulk editing preserves selected rows, cart order and every item quantity");
            var persisted=new SpawnCart(catalog);ReloadProfileCart(persisted,catalog,path);
            Check(Snapshot(persisted)==Snapshot(cart) && Field<TextBox>(dialog,"preview").Text==string.Join(Environment.NewLine,cart.BuildCommands()),
                "The actual bulk action persists complete edited entries and refreshes the command preview");
            dialog.ActiveControl=grid;grid.CurrentCell=grid.Rows[0].Cells[1];grid.ClearSelection();grid.Rows[0].Selected=true;grid.Rows[1].Selected=true;
            Check(grid.BeginEdit(false),"Bulk modifier button regression opens an active quantity editor");((TextBox)grid.EditingControl).Text="7";
            dialog.SetModifierEditor(editor=> { calls++;((ComboBox)Field<Dictionary<string,Control>>(editor,"fields")["Dirtiness"]).SelectedItem="0";
                Check(editor.Apply(),"Bulk popup after quantity commit validates its selected zero preset");return DialogResult.OK; });
            edit.PerformClick();Application.DoEvents();
            Check(calls==4 && cart.Entries[0].Quantity==7 && cart.Entries.Take(2).All(entry=>SpawnModifiers.Parse(entry.ModifiersText)["Dirtiness"]=="0")
                && cart.Entries[2].Command==unselected,"The real bulk Edit button commits the active quantity before applying modifiers to the selected rows");
            grid.ClearSelection();grid.Rows[2].Selected=true;Application.DoEvents();
            // A single selected row continues to use the existing full-value editor behavior.
            dialog.SetModifierEditor(editor=> { calls++;var fields=Field<Dictionary<string,Control>>(editor,"fields");
                Check(fields["Weight"].Text=="-1" && !Field<Dictionary<string,CheckBox>>(editor,"selected")["Weight"].ThreeState,
                    "One selected cart row still opens the existing single-item modifier editor");
                ((ComboBox)fields["Weight"]).SelectedItem="0.5";Check(editor.Apply(),"Single-item preset remains valid after bulk editing");return DialogResult.OK; });
            edit.PerformClick();Application.DoEvents();
            Check(calls==5 && cart.Entries[2].ModifiersText=="Weight 0.5" && cart.Entries[0].Quantity==7 && cart.Entries[1].ModifiersText.Contains("Dirtiness 0"),
                "The actual single-row Edit action changes only that item after a bulk edit");
            Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Bulk editing button regressions never activate test windows or dispatch game input");
        }
    }

    static void TestModifierButtonClicks(SpawnItemCatalog catalog,string directory) {
        using(var inactive=new InactiveUi()) {
            foreach(string editor in new[]{"valid-modifier","invalid-modifier","quantity-with-modifier","valid-location","valid-location-triple","invalid-location","quantity-with-location"}) {
                string path=ProfileCartPath(directory,"modifier-click-"+editor+".tsv");int calls=0;
                var trace=new List<string>();IList<string> snapshot=null;
                var pending=new TaskCompletionSource<SpawnBatchResult>();
                using(var dialog=new SpawnCartDialog(catalog,path,(commands,owner,progress)=> {
                    calls++;snapshot=new List<string>(commands);return pending.Task;
                })) {
                    dialog.SetDiagnostics((stage,count)=>trace.Add(stage+":"+count));inactive.Show(dialog);
                    Field<ListView>(dialog,"items").SelectedIndices.Add(0);Field<Button>(dialog,"add").PerformClick();Application.DoEvents();
                    var grid=Field<DataGridView>(dialog,"grid");var cart=Field<SpawnCart>(dialog,"cart");var submit=Field<Button>(dialog,"submit");
                    Check(grid.Columns[3].Name=="Modifiers" && !grid.Columns[3].ReadOnly && !submit.CausesValidation,
                        "Modifiers are editable per cart row and Send delegates validation to the queued action");
                    bool quantityEdit=editor.StartsWith("quantity-with-",StringComparison.Ordinal);
                    string modifiers=editor=="quantity-with-location"?"Uses 0 Location \"{X=-1.5 Y=2 Z=0.5|P=-0.25 Y=90 R=0}\" Weight 0.5":
                        editor=="valid-location"?"Uses 0 Location \"X=-1.5 Y=2 Z=0.5\" Weight 0.5":
                        editor=="valid-location-triple"?"Uses 0 Location -1.5 2 0.5 Weight 0.5":"Uses 0 Weight 0.5";
                    if(quantityEdit) {
                        cart.SetModifiers(cart.Entries[0].Item.Id,modifiers);Invoke(dialog,"RefreshCart");
                    }
                    dialog.ActiveControl=grid;grid.CurrentCell=grid.Rows[0].Cells[quantityEdit?1:3];
                    Check(grid.BeginEdit(false),"Actual send regression opens the pending modifier or quantity editor");
                    ((TextBox)grid.EditingControl).Text=editor=="valid-modifier"?"Weight +00.500 Uses 0":editor=="invalid-modifier"?"Uses -1":
                        editor=="valid-location"?"Weight +00.500 Location \"x=-001.50 y=+02.000 z=.5\" Uses 0":
                        editor=="valid-location-triple"?"Weight +00.500 Location -001.50 +02.000 .5 Uses 0":
                        editor=="invalid-location"?"Location \"X=1 Y=2 Z=3":"7";
                    submit.PerformClick();submit.PerformClick();
                    Check(calls==0,"The actual send click queues submission until the current UI input event finishes");
                    Application.DoEvents();
                    if(editor.StartsWith("invalid-",StringComparison.Ordinal)) {
                        Check(calls==0 && cart.Entries[0].ModifiersText=="" && trace.Contains("invalid-edit:1") && !trace.Contains("transport-start:1"),
                            "Queued Send rejects an active invalid modifier editor without submitting the previous defaults: "+editor);
                        Check(grid.Rows[0].ErrorText.Length>0,"Invalid modifier editor explains why submission stopped");
                    } else {
                        string expected="#SpawnItem "+catalog.Items[0].Id+" "+(quantityEdit?"7":"1")+" "+modifiers;
                        Check(calls==1 && snapshot.Single()==expected && cart.Entries[0].ModifiersText==modifiers,
                            "Real queued Send commits the active editor and preserves validated modifiers in its command: "+editor);
                        Check(trace.Contains("clicked:1") && trace.Contains("start:1") && trace.Contains("transport-start:1") && trace.Contains("already-sending:1"),
                            "UI diagnostics distinguish clicks, queued dispatch and repeated send attempts without command text");
                        var persisted=new SpawnCart(catalog);ReloadProfileCart(persisted,catalog,path);
                        Check(persisted.BuildCommands().Single()==expected,"Current modifier and quantity edits persist before batch submission");
                    }
                    dialog.Dispose();pending.SetResult(new SpawnBatchResult { Error="Simulated interruption" });Application.DoEvents();
                    Check(!Native.IsOurWindow(Native.GetForegroundWindow()),"Modifier send regressions never bring test windows to the foreground");
                }
            }
        }
    }

    [STAThread]
    static int Main(string[] arguments) {
        string temporary=Path.Combine(Path.GetTempPath(),"MiniMap-spawn-cart-"+Guid.NewGuid().ToString("N"));
        string diagnosticSession=Guid.NewGuid().ToString("N");
        string diagnostic=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ScumMiniMap-ShortcutDiagnostics-"+diagnosticSession);
        string priorTrace=Environment.GetEnvironmentVariable("SCUM_MINIMAP_INPUT_TRACE"),priorSession=Environment.GetEnvironmentVariable("SCUM_MINIMAP_DIAGNOSTIC_SESSION");
        // Exception-path regressions log only to this test's disposable diagnostic profile.
        Environment.SetEnvironmentVariable("SCUM_MINIMAP_INPUT_TRACE","1");
        Environment.SetEnvironmentVariable("SCUM_MINIMAP_DIAGNOSTIC_SESSION",diagnosticSession);
        Directory.CreateDirectory(temporary);
        fixtureRoot=Path.GetFullPath(temporary);
        try {
            Localization.Current=AppLanguage.English;Localization.SelfTest();
            var catalog=SpawnItemCatalog.LoadBundled();
            if(arguments.Length>0 && arguments[0]=="--transport-only") {
                TestClipboardPublication();TestClipboardAfterClose();TestClipboardPreparation();TestMenuCloseReadiness();TestAsyncMapDispatch(catalog);TestDispatchGateErrors();TestAfterChatOpenAbort(catalog);TestBeforeChatGate();TestInventoryInputOwnership();TestBatch();TestFocusReadiness();TestKeySequence();TestConsumption(catalog);
                Console.WriteLine("Spawn cart transport: "+checks+" pure checks passed without forms, hooks or live game input.");return 0;
            }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            TestPolish(catalog,temporary);
            if(arguments.Length>0 && arguments[0]=="--polish-only") { Console.WriteLine("Polish catalogue and selector: "+checks+" checks passed.");return 0; }
            TestExpandedLocalization(catalog,temporary);
            if(arguments.Length>0 && arguments[0]=="--localization-only") { Console.WriteLine("Expanded localization: "+checks+" checks passed.");return 0; }
            TestCatalogue(catalog);TestCompatibilitySearch(catalog);TestCategoryHierarchy(catalog);TestResultGroups(catalog);TestCart(catalog);TestPersistence(catalog,temporary);TestKitDatabase(catalog,temporary);TestModifiers(catalog,temporary);TestLocationModifiers(catalog,temporary);TestModifierPatches(catalog,temporary);
            TestClipboardPublication();TestClipboardAfterClose();TestClipboardPreparation();TestMenuCloseReadiness();TestAsyncMapDispatch(catalog);TestDispatchGateErrors();TestAfterChatOpenAbort(catalog);TestBeforeChatGate();TestInventoryInputOwnership();TestBatch();TestFocusReadiness();TestKeySequence();TestConsumption(catalog);TestDialog(catalog,temporary);TestCompatibilityDialog(catalog,temporary);TestGroupedResultDialog(catalog,temporary);TestSelectorLayout(catalog,temporary);TestDialogSaveFailure(catalog,temporary);TestButtonClicks(catalog,temporary);
            TestSavedKitControls(catalog,temporary);TestModifierDialog();TestModifierSelections();TestLocationDialog();TestBulkModifierDialog();TestBulkModifierButtonClicks(catalog,temporary);TestModifierButtonClicks(catalog,temporary);
            Console.WriteLine("Spawn cart: "+checks+" checks passed without live game input.");return 0;
        }catch(Exception error) { Console.Error.WriteLine(error);return 1; }
        finally {
            Environment.SetEnvironmentVariable("SCUM_MINIMAP_INPUT_TRACE",priorTrace);
            Environment.SetEnvironmentVariable("SCUM_MINIMAP_DIAGNOSTIC_SESSION",priorSession);
            string resolved=Path.GetFullPath(temporary),root=Path.GetFullPath(Path.GetTempPath());
            if(resolved.StartsWith(root,StringComparison.OrdinalIgnoreCase) && Path.GetFileName(resolved).StartsWith("MiniMap-spawn-cart-",StringComparison.Ordinal))
                Directory.Delete(resolved,true);
            string diagnosticRoot=Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            if(Path.GetFullPath(diagnostic).StartsWith(diagnosticRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(diagnostic)=="ScumMiniMap-ShortcutDiagnostics-"+diagnosticSession && Directory.Exists(diagnostic))
                Directory.Delete(diagnostic,true);
        }
    }

    static void TestExpandedLocalization(SpawnItemCatalog catalog,string directory) {
        string[] languages={"es-AR","fr","de","nl","ru","zh-CN","tr","ar","pt-BR","pt-PT","ko","pl","th","zh-TW","ja","cs","it","uk","fi"};
        var boots=catalog.Find("Boots_06_01");
        try {
            foreach(string code in languages) {
                Localization.SetLanguage(code);
                var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                using(var reader=new StreamReader(typeof(Localization).Assembly.GetManifestResourceStream("spawn-item-names-"+code+".tsv"))) {
                    string line;while((line=reader.ReadLine())!=null)if(line.Length>0&&!line.StartsWith("#")) {
                        var fields=line.Split('\t');names.Add(fields[0],fields[1]);
                    }
                }
                Check(names.Count==catalog.Items.Count && catalog.Items.All(item=>names.ContainsKey(item.Id) && item.DisplayName==names[item.Id]),code+": exact runtime catalogue coverage with no missing IDs");
                Check(catalog.Search(boots.DisplayName,null).Contains(boots) && catalog.Search(boots.Name,null).Contains(boots) && catalog.Search(boots.Id,null).Contains(boots),code+": translated, English and ID searches resolve the same item");
                Check(catalog.Items.Select(item=>item.Category).Distinct().All(key=>Localization.GetSpawnCategoryName(key,"").Length>0),code+": every catalogue category has an explicit translation");
                Check(catalog.Items.Select(item=>item.Subcategory).Distinct().Where(key=>key.Length>0 && char.IsLetter(key[0])).All(key=>Localization.GetSpawnCategoryName(key,"").Length>0),code+": every named subtype has an explicit translation");
                Check(SpawnModifiers.PropertyNames.All(key=>Localization.GetSpawnModifierLabel(key)!=key),code+": every modifier explains its original property token");
            }
            Localization.SetLanguage("fi-FI");
            Check(Localization.Current==AppLanguage.Finnish && Localization.CurrentCode=="fi" && Localization.Get("Settings")=="Asetukset","Finnish regional culture selects its persisted language identity");
            Check(boots.DisplayName=="Työkengät" && catalog.Search("tyokengat",null).Contains(boots),"Finnish item search accepts missing ä and ö accents");
            var suppressor=catalog.Items.First(item=>item.Name==".308 Suppressor");
            Check(suppressor.DisplayName.Contains("äänenvaimennin") && catalog.Search("aanenvaimennin",null).Contains(suppressor),"Finnish weapon terminology and accent-free search resolve the same canonical item");
            Check(Localization.GetSpawnModifierLabel("Health")=="Kunto (Health)" && Localization.GetSpawnModifierLabel("Weight")=="Paino (kg) (Weight)","Finnish modifier labels explain SCUM property tokens without changing them");
            Localization.SetLanguage("pt-PT");Check(Localization.Current==AppLanguage.PortuguesePortugal && Localization.CurrentCode=="pt-PT","European Portuguese has its own persisted language identity");
            Check(Localization.Get("Settings")=="Definições","European Portuguese uses European UI terminology");
            Localization.SetLanguage("pt-BR");Check(Localization.Current==AppLanguage.Portuguese && Localization.Get("Settings")=="Configurações","Existing Brazilian Portuguese settings retain their language identity and wording");
            Localization.SetLanguage("en");
            using(var inactive=new InactiveUi())using(var dialog=new SpawnCartDialog(catalog,ProfileCartPath(directory,"extended-localization.tsv"),(commands,owner,progress)=>Task.FromResult(new SpawnBatchResult()))) {
                inactive.Show(dialog);
                var search=Field<TextBox>(dialog,"search");search.Text=boots.Id;
                var list=Field<ListView>(dialog,"items");list.SelectedIndices.Add(0);
                Field<Button>(dialog,"add").PerformClick();Application.DoEvents();
                var cart=Field<SpawnCart>(dialog,"cart");string command=cart.BuildCommands().Single();
                foreach(string code in languages) {
                    Localization.SetLanguage(code);Application.DoEvents();
                    Check(search.Text==boots.Id && list.SelectedIndices.Count==1 && cart.BuildCommands().Single()==command
                        && Field<TextBox>(dialog,"preview").Text.Contains(command),code+": live language change preserves selection, search and raw command bytes");
                    Check(Field<DataGridView>(dialog,"grid").Rows[0].Cells[0].Value.ToString()==boots.DisplayName,code+": existing cart row refreshes to the translated item name");
                    if(code=="ar") {
                        Check(dialog.RightToLeft==RightToLeft.Yes && search.RightToLeft==RightToLeft.No && Field<TextBox>(dialog,"preview").RightToLeft==RightToLeft.No,"Arabic prose uses RTL while command and ID input remains LTR");
                        Check(list.Items[0].SubItems[2].Text=="\u200e"+boots.Id+"\u200e" && boots.Id.IndexOf('\u200e')<0,"Arabic identifier direction marks are presentation-only");
                        Check(Localization.DisplayLocalizedText("7.62x39mm")=="\u200e7.62x39mm\u200e" && command.IndexOf('\u200e')<0,"Arabic calibre direction marks never enter commands");
                    }
                }
                Localization.SetLanguage("en");Application.DoEvents();
                Check(dialog.RightToLeft==RightToLeft.No && list.Items[0].SubItems[2].Text==boots.Id,"Leaving Arabic resets text direction and identifier presentation");
            }
        } finally { Localization.Current=AppLanguage.English; }
    }

    static void TestPolish(SpawnItemCatalog catalog,string directory) {
        var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        using(var reader=new StreamReader(typeof(Localization).Assembly.GetManifestResourceStream("spawn-item-names-pl.tsv"))) {
            string line;while((line=reader.ReadLine())!=null)if(line.Length>0&&!line.StartsWith("#")) { var fields=line.Split('\t');names.Add(fields[0],fields[1]); }
        }
        Check(names.Count==catalog.Items.Count && catalog.Items.All(item=>names.ContainsKey(item.Id)),"Polish resource covers every canonical bundled item exactly once");
        try {
            Localization.SetLanguage("pl-PL");
            Check(Localization.Current==AppLanguage.Polish && Localization.Get("Settings")!="Settings","Polish culture selects native UI strings");
            Check(catalog.Items.All(item=>item.DisplayName==names[item.Id]),"All item display names use the complete Polish resource");
            var boots=catalog.Find("Boots_06_01");
            Check(boots.DisplayName=="Buty robocze" && catalog.Search("buty robocze",null).Contains(boots),"Natural Polish item names can be searched");
            Check(catalog.Search("Construction Boots",null).Contains(boots)&&catalog.Search(boots.Id,null).Contains(boots),"English names and spawn IDs remain searchable in Polish");
            var arrow=catalog.Items.First(item=>item.DisplayName.IndexOf("Strza",StringComparison.OrdinalIgnoreCase)>=0);
            Check(catalog.Search("strzala",null).Any(item=>item.DisplayName.IndexOf("Strzała",StringComparison.OrdinalIgnoreCase)>=0),"Polish search accepts missing accents including Ł");
            Check(Localization.GetSpawnCategoryName("weapon_ranged","")=="Broń dystansowa","Polish category labels are translated");
            Check(Localization.GetSpawnModifierLabel("Health")=="Stan (Health)" && Localization.GetSpawnModifierLabel("Weight")=="Masa w kg (Weight)","Polish modifier labels explain the original SCUM property tokens");
            foreach(var category in catalog.Items.Select(item=>item.Category).Distinct())Check(Localization.GetSpawnCategoryName(category,"")!="","Every Polish item category is explicitly translated: "+category);
            foreach(var subtype in catalog.Items.Select(item=>item.Subcategory).Distinct().Where(value=>value.Length>0 && char.IsLower(value[0])))Check(Localization.GetSpawnCategoryName(subtype,"")!="","Every named Polish item subtype is explicitly translated: "+subtype);
            using(var inactive=new InactiveUi())using(var dialog=new SpawnCartDialog(catalog,ProfileCartPath(directory,"polish.tsv"),(commands,owner,progress)=>Task.FromResult(new SpawnBatchResult()))) {
                inactive.Show(dialog);
                dialog.SetChatBinding("T");
                var search=Field<TextBox>(dialog,"search");search.Text=boots.Id;
                var list=Field<ListView>(dialog,"items");list.SelectedIndices.Add(0);
                Field<Button>(dialog,"add").PerformClick();Application.DoEvents();
                var cart=Field<SpawnCart>(dialog,"cart");string command=cart.BuildCommands().Single();
                Check(Field<DataGridView>(dialog,"grid").Rows[0].Cells[0].Value.ToString()==boots.DisplayName,"Polish cart displays names and preserves canonical commands");
                Localization.Current=AppLanguage.English;Application.DoEvents();
                Check(dialog.Text==Localization.Get("SpawnCartTitle") && Field<DataGridView>(dialog,"grid").Rows[0].Cells[0].Value.ToString()==boots.Name,"Open selector refreshes after switching to English");
                Localization.Current=AppLanguage.Polish;Application.DoEvents();
                Check(search.Text==boots.Id && cart.BuildCommands().Single()==command && list.SelectedIndices.Count==1,"Language switching preserves search, selected item, quantity and command identity");
                Check(Field<Label>(dialog,"help").Text==Localization.Get("SpawnCartLimits")+"   "+Localization.T("SpawnCartChatBinding","T"),"Language switching retains the configured chat key in translated help");
                Check(Field<DataGridView>(dialog,"grid").Rows[0].Cells[0].Value.ToString()==boots.DisplayName,"Open cart switches back to Polish names");
            }
        }finally { Localization.Current=AppLanguage.English; }
    }
}
