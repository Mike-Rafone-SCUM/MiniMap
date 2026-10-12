using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ScumMiniMap;

static class AdminPlayerTests {
    static int count;
    const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    static readonly DateTime Now=new DateTime(2026,10,8,9,0,0,DateTimeKind.Utc);
    static string Block(int number,string id,string name,string platform,double x) {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture," {0}. {1}\nPlatform Name: {2}\nPlatform ID: {3}\nFame: 50  \nAccount balance: -500\nGold balance: 4\nLocation: X={4:0.000} Y=-164771.844 Z=96.770\n\n",number,name,platform,id,x);
    }
    static string First=Block(1,"76561198000000001","Test O'Neil","ツ 🐻💘",428762.750);
    static string Second=Block(2,"76561198000000002","Another Player","Platform Name",-772127.750);
    static void Check(bool condition,string message) { if(!condition)throw new Exception(message);count++;Console.WriteLine("PASS: "+message); }
    static void Reject(Action action,string message) { try { action(); }catch(FormatException) { Check(true,message);return; }throw new Exception(message); }
    static T Field<T>(object target,string name) { return (T)target.GetType().GetField(name,Hidden).GetValue(target); }
    static void Set(object target,string name,object value) { target.GetType().GetField(name,Hidden).SetValue(target,value); }
    static T AwaitUi<T>(Task<T> task) {
        var timeout=System.Diagnostics.Stopwatch.StartNew();
        while(!task.IsCompleted) {
            if(timeout.ElapsedMilliseconds>15000)throw new Exception("Asynchronous UI operation timed out");
            Application.DoEvents();Thread.Sleep(1);
        }
        return task.GetAwaiter().GetResult();
    }
    static IEnumerable<Control> Controls(Control parent) { foreach(Control child in parent.Controls) { yield return child;foreach(Control nested in Controls(child))yield return nested; } }
    static void ParseTests() {
        var players=PlayerList.Parse("[09:08:49]\n"+First+Second,Now);
        Check(players.Count==2 && players[0].Name=="Test O'Neil" && players[0].PlatformName=="ツ 🐻💘" && players[1].Name=="Another Player","Parsing preserves character/platform distinctions, spaces, apostrophes and Unicode");
        Check(players[0].Location.X==428762.750 && players[0].Location.Y==-164771.844 && players[0].Location.Z==96.770
            && players[1].Location.X==-772127.750 && players[0].Balance==-500 && players[0].Gold==4 && players[0].SeenUtc==Now,"Player snapshot coordinates, negative balances and capture time are preserved");
        Check(players[0].TeleportTo=="#TeleportTo 76561198000000001" && players[0].TeleportHere=="#TeleportToMe 76561198000000001","Teleport targets use stable IDs instead of display names");
        Check(PlayerList.Parse((First+Second).Replace("\n","\r\n"),Now).Count==2,"Windows line endings and optional timestamps are supported");
        foreach(string bad in new[]{"",null,"#ListPlayers true","No results",First.Replace("Location:","Position:"),First.Replace("Z=96.770","Z=NaN"),
            First.Replace("428762.750","2000000.001"),First.Replace("428762.750","1e10"),First.Replace("Platform ID: 76561198000000001","Platform ID: 76561198000000001 #Teleport"),
            First+Second.Replace("76561198000000002","76561198000000001"),First+Second.Replace(" 2."," 3."),First.Replace("Gold balance: 4\n",""),
            First+"Location: X=1 Y=2 Z=3",First.Replace("Fame: 50","Fame: 99999999999999999999999"),new string('x',PlayerList.MaximumLength+1)})
            Reject(()=>PlayerList.Parse(bad,Now),"Malformed, duplicate, oversized or incomplete player text fails closed");
        var before=Thread.CurrentThread.CurrentCulture;
        try { Thread.CurrentThread.CurrentCulture=new System.Globalization.CultureInfo("fr-FR");Check(PlayerList.Parse(First,Now)[0].Location.X==428762.750,"SCUM decimal points parse independently of the app locale"); }
        finally { Thread.CurrentThread.CurrentCulture=before; }
        Reject(()=>PlayerDatabase.ServerName(" "),"A server identity is mandatory before importing");
        Reject(()=>PlayerDatabase.ServerName("bad\nserver"),"Server identities reject embedded controls");
    }
    static void DatabaseTests(string root) {
        string path=Path.Combine(root,"players.db");var db=new PlayerDatabase(path);
        db.SelectServer("  Server A  ");db.Import("Server A",First+Second,Now);db.Import("Server B",First.Replace("428762.750","123.000"),Now.AddSeconds(1));
        Check(System.Text.Encoding.ASCII.GetString(File.ReadAllBytes(path),0,15)=="SQLite format 3","The player directory is stored in a real local SQLite file");
        Check(new PlayerDatabase(path).ActiveServer()=="Server A" && db.Servers().SequenceEqual(new[]{"Server A","Server B"}),"Server selection and directory names persist across database instances");
        Check(db.Load("server a").Count==2 && db.Load("Server B").Single().Location.X==123,"Identical player IDs remain isolated between servers");
        db.Import("SERVER A",First.Replace("Test O'Neil","Changed Name").Replace("428762.750","12.250"),Now.AddMinutes(1));
        var saved=db.Load("Server A");
        Check(saved.Count==2 && saved.Single(player=>player.Id=="76561198000000001").Name=="Changed Name" && saved.Single(player=>player.Id=="76561198000000001").Location.X==12.25,"Refreshing updates identity/coordinates without duplicating the player");
        Check(saved.Count(player=>player.InSnapshot)==1 && saved.Single(player=>!player.InSnapshot).SeenUtc==Now,"Missing players move to saved history while their last position/time remain available");
        Reject(()=>db.Import("Server A",First.Replace("Platform ID:","Broken ID:"),Now.AddHours(1)),"Invalid imports cannot commit a replacement snapshot");
        Check(db.Load("Server A").Single(player=>player.InSnapshot).SeenUtc==Now.AddMinutes(1),"Invalid refreshes preserve the previous population and capture time");
        db.SelectServer("Quoted ' server; DROP TABLE players; --");db.Import("Quoted ' server; DROP TABLE players; --",First,Now);
        Check(db.Load("Server A").Count==2 && db.Load("Quoted ' server; DROP TABLE players; --").Count==1,"Server names and Unicode player data are bound values, never SQL text");
        using(var raw=new LocalSqliteDatabase(path,connection=>{}))raw.Execute("PRAGMA user_version=999");
        bool failed=false;try { db.Load("Server A"); }catch(IOException) { failed=true; }Check(failed,"Unsupported database schemas are rejected before directory reads or writes");
    }
    static void ProfileTests(string root) {
        const string owner="76561198000000001",alias="76561198000000002";
        string vehicle="#42: BPC_Laika   2026-10-08T13:00:00.000Z   X=-10.25 Y=20.5 Z=-54.082   STEAMID:"+alias+"   VID:42   43   "+owner;
        string offline=vehicle.Replace("#42:","#44:").Replace("VID:42","VID:44").Replace("43   "+owner,"45   Owner not online, database id = 49");
        string noOwner=vehicle.Replace("#42:","#46:").Replace("VID:42","VID:46").Replace("43   "+owner,"47   No owner");
        string plain="#48: BPC_Dirtbike 2026-10-08T13:00:00.000Z X=1 Y=2 Z=3 0 "+owner;
        string squad="[13:01:56] Flags:\nFlag ID: 51 | Owner: ["+owner+"] Test Character (49) | Location: X=-1 Y=2 Z=3\n\nSquad: TEST SQUAD (95)\n    Platform ID                    Platform Name                  Character Name                 Member Rank                    Status\n"+owner+"              Platform One                   Test Character                         4 Offline\n"+alias+"              ツ                   Character Two                          3 Online\n";
        var parsed=AdminProfileParser.Parse("-----\n"+vehicle+"\n"+offline+"\n"+noOwner+"\n"+plain+"\n-----",Now);
        Check(parsed.Assets.Count==4 && parsed.Assets[0].OwnerId==owner && parsed.Assets[0].AliasId==alias,"Vehicle aliases never override explicit ownership");
        Check(parsed.Assets[1].OwnerId=="" && parsed.Assets[1].OwnerDatabaseId=="49","Offline database owners stay unresolved instead of being assigned to alias IDs");
        Check(parsed.Assets[2].OwnerId=="" && parsed.Assets[2].AliasId==alias && parsed.Assets[2].OwnerDatabaseId=="","No owner remains unowned even when a STEAMID alias exists");
        Check(parsed.Assets[3].OwnerId==owner && parsed.Assets[3].AliasId=="" && parsed.Assets[0].Location.Z==-54.082,"Owner-only rows, zero auxiliary IDs and negative boat elevations parse correctly");
        var response=Native.WaitForPlayerClipboard(1,()=>2,()=>true,()=>vehicle,()=>true,milliseconds=>Task.FromResult(0),text=> {
            if(AdminProfileParser.Parse(text,Now).Kind!="Vehicles")throw new FormatException();
        }).GetAwaiter().GetResult();
        Check(response!=null && response.Text==vehicle,"Fresh game-owned vehicle clipboard responses use the validated report capture path");
        response=Native.WaitForPlayerClipboard(1,()=>2,()=>false,()=>vehicle,()=>true,milliseconds=>Task.FromResult(0),text=>AdminProfileParser.Parse(text,Now)).GetAwaiter().GetResult();
        Check(response==null,"Another application cannot supply an automatic vehicle response");
        MapWindow.ValidateSquadResponse(squad,owner);
        Reject(()=>MapWindow.ValidateSquadResponse(squad,"76561198010000004"),"Automatic squad refresh rejects reports that omit the requested player");
        Reject(()=>MapWindow.ValidateSquadResponse(vehicle,owner),"Automatic squad refresh rejects a vehicle response");
        var squadResponse=Native.WaitForPlayerClipboard(1,()=>2,()=>true,()=>squad,()=>true,milliseconds=>Task.FromResult(0),text=>MapWindow.ValidateSquadResponse(text,owner)).GetAwaiter().GetResult();
        Check(squadResponse!=null,"Fresh squad responses pass through the game-owned clipboard capture validator");
        Check(MapWindow.AssetTeleportCommand(AdminProfileParser.Parse(vehicle,Now).Assets[0])=="#Teleport -10.25 20.5 -54.082","Asset teleport uses the reported XYZ including negative altitude");
        var markerLocation=new Position { X=1,Y=2,Z=3 };var markerPoint=MapWindow.ToMap(markerLocation);
        Check(MapWindow.MarkerDistance(markerPoint,markerLocation,800)==0 && MapWindow.MarkerDistance(new PointF(markerPoint.X+0.02f,markerPoint.Y),markerLocation,800)>12,"Map marker hit testing uses screen pixels and respects zoom-scaled distance");
        Check(MapWindow.AssetTeleportCommand(null)==null && MapWindow.AssetTeleportCommand(new AdminAsset { Location=new Position { X=double.NaN } })==null,"Invalid asset coordinates never generate a teleport command");
        parsed=AdminProfileParser.Parse(squad,Now);
        Check(parsed.SquadId=="95" && parsed.SquadName=="TEST SQUAD" && parsed.Members.Count==2 && parsed.Members[1].PlatformName=="ツ","Squad parsing retains Unicode names, membership and squad identity");
        Check(parsed.Assets.Single().Kind=="Flag" && parsed.Assets[0].OwnerId==owner && parsed.Assets[0].OwnerDatabaseId=="49","Flags link through explicit player IDs and retain their database ID");
        foreach(string bad in new[]{vehicle+"\n"+vehicle,vehicle.Replace("VID:42","VID:41"),vehicle.Replace("X=-10.25","X=2000001"),vehicle.Replace("2026-10-08","2026-19-08"),vehicle+"\ntruncated garbage",squad.Replace("Z=3","Z=NaN"),squad+owner+"              Platform One                   Test Character                         4 Offline\n","SquadInfo <Player ID>",new string('x',PlayerList.MaximumLength+1)})
            Reject(()=>AdminProfileParser.Parse(bad,Now),"Malformed or partial asset/squad reports fail before database changes");
        string saints="[14:31:16] Flags:\nFlag ID: 1 | Owner: [76561198010000005] Test Admin (1) | Location: X=100 Y=200 Z=300\nFlag ID: 2 | Owner: [76561198010000005] Test Admin (1) | Location: X=200 Y=200 Z=300\n\nSquad: Sample Squad (1)\n    Platform ID                    Platform Name                  Character Name                 Member Rank                    Status\n76561198010000005              Account 0                      Player 0                       4 Offline\n76561198010000012              Account 1                      Player 1                       3 Online\n76561198010000013              Account 2                      Player 2                       3 Offline\n76561198010000010              Account 3                      Player 3                       3 Online\n76561198010000008              Account 4                      Player 4                       3 Offline\n76561198010000007              Account 5                      Player 5                       3 Online\n76561198010000006              Account 6                      Player 6                       3 Offline";
        MapWindow.ValidateSquadResponse(saints,"76561198010000008");var actual=AdminProfileParser.Parse(saints,Now);
        Check(actual.Members.Count==7 && actual.Assets.Count==2 && actual.Members.Single(member=>member.Id=="76561198010000008").Name=="Player 4","Synthetic squad reports validate the target and preserve column spacing");
        var db=new AdminProfileDatabase(Path.Combine(root,"profiles.db"));db.Import("Server A",vehicle+"\n"+offline+"\n"+noOwner,Now);db.Import("Server B",plain,Now);db.Import("Server A",squad,Now);
        Check(db.SquadSizes("Server A")[owner]==2 && db.SquadSizes("Server B").Count==0,"Squad size includes imported members and stays scoped to the selected server");
        Check(MapWindow.ReportCoordinates(new Position { X=-10.25,Y=20.5,Z=-54.082 })=="X=-10.25 Y=20.5 Z=-54.082" && MapWindow.ReportCoordinates(new Position { X=double.NaN })==null,"Copied coordinates retain invariant decimals and reject invalid values");
        Check(new AdminProfileDatabase(Path.Combine(root,"profiles.db")).Assets("server a").Count==4 && db.Assets("Server B").Count==1,"Asset observations persist in SQLite and remain isolated per server");
        Check(db.Assets("Server A").Count(asset=>asset.Kind=="Vehicle" && asset.OwnerId==alias)==2 && db.Assets("Server A").Single(asset=>asset.Id=="46").OwnerId=="","Registered vehicles stay with their STEAMID rather than another squad member, while no-owner rows remain unowned");
        var fallback=new AdminProfileDatabase(Path.Combine(root,"fallback-profiles.db"));
        fallback.Import("Server A",offline.Replace("STEAMID:"+alias+"   VID:44   ",""),Now);fallback.Import("Server A",squad,Now);
        Check(fallback.Assets("Server A").Single(asset=>asset.Kind=="Vehicle").OwnerId==owner,"Vehicles without registration tags still resolve through unambiguous flag-owner database IDs");
        Check(db.Squad("Server A",alias).Contains("TEST SQUAD") && db.Squad("Server B",alias).StartsWith("No imported"),"Squad membership is scoped to the selected server");
        Reject(()=>db.Import("Server A",vehicle+"\ngarbage",Now),"Invalid imports cannot replace the saved vehicle snapshot");
        Check(db.Assets("Server A").Count==4,"Failed profile imports preserve previous observations atomically");
        db.Import("Server A",plain,Now.AddMinutes(1));
        Check(db.Assets("Server A").Count==2 && db.Assets("Server A").Any(asset=>asset.Kind=="Flag") && db.Assets("Server B").Count==1,"A new vehicle snapshot removes missing vehicles without clearing flags or another server");
        db.Import("Server A",squad.Replace("Flag ID: 51","Flag ID: 52"),Now.AddMinutes(2));
        Check(db.Assets("Server A").Single(asset=>asset.Kind=="Flag").Id=="52","Refreshing a squad replaces its previously imported flag locations");
        Reject(()=>db.Import("",vehicle,Now),"Profile imports require a selected server");
        using(var future=new LocalSqliteDatabase(Path.Combine(root,"future-profiles.db"),connection=>connection.Execute("PRAGMA user_version=99"))) {}
        try { new AdminProfileDatabase(Path.Combine(root,"future-profiles.db")).Assets("Server A");throw new Exception("Future profile schema accepted"); }
        catch(IOException) { Check(true,"Unsupported profile databases fail before reading or replacing saved data"); }
    }
    static void BulkReportTests(string root,string fixtures) {
        string squadText=File.ReadAllText(Path.Combine(fixtures,"squads.txt"));
        string playerText=File.ReadAllText(Path.Combine(fixtures,"players.txt"));
        string vehicleText=File.ReadAllText(Path.Combine(fixtures,"vehicles.txt"));
        string flagText=File.ReadAllText(Path.Combine(fixtures,"flags-page-1.txt"));
        var squads=AdminProfileParser.Parse(squadText,Now);var vehicles=AdminProfileParser.Parse(vehicleText,Now);var flags=AdminProfileParser.Parse(flagText,Now);var players=PlayerList.Parse(playerText,Now);
        Check(squads.Kind=="Squads" && squads.Squads.Count>1 && squads.Squads.SelectMany(squad=>squad.Members).All(member=>member.Status=="Unknown"),"The supplied all-squad dump parses every squad without inventing online status");
        Check(squads.Squads.Single(squad=>squad.SquadName=="Sample Group").Members.Single(member=>member.Id=="76561198010000011").Name=="Sample Player","Bulk members preserve stable IDs, platform names and character names");
        Check(vehicles.Kind=="Vehicles" && vehicles.Assets.Count>0 && players.Count>0,"The supplied vehicle and population clipboard samples remain compatible");
        Check(flags.Kind=="Flags" && flags.Page==1 && flags.TotalPages==2 && flags.Assets.Count>0,"Flag pagination comes from the copied Page 1/2 header");
        string second="Page 2/2\nFlag ID: 55056 | Owner: [76561198010000009] Player Two (383) | Location: X=400 Y=500 Z=600\nFlag ID: 55927 | Owner: [76561198010000014] Player Three (393) | Location: X=700 Y=800 Z=900";
        var page2=AdminProfileParser.Parse(second,Now);
        var combined=AdminProfileDatabase.CompleteFlagPages(new[]{flags,page2});
        Check(combined.Count==flags.Assets.Count+2,"Complete flag pages combine in numeric order without dropping earlier pages");
        Reject(()=>AdminProfileDatabase.CompleteFlagPages(new[]{flags}),"A missing flag page cannot replace the complete saved flag list");
        Reject(()=>AdminProfileDatabase.CompleteFlagPages(new[]{page2,flags}),"Out-of-order or repeated flag pages are rejected");
        Reject(()=>AdminProfileDatabase.CompleteFlagPages(new[]{flags,AdminProfileParser.Parse(flagText.Replace("Page 1/2","Page 2/2"),Now)}),"Duplicate flag IDs across pages cannot commit a replacement");
        Reject(()=>AdminProfileParser.Parse(second.Replace("Page 2/2","Page 2/101"),Now),"Flag page counts have a bounded maximum");
        Reject(()=>AdminProfileParser.ValidateBulkResponse(second,"#ListFlags 1 true"),"The capture validator rejects a copied report for the wrong page");
        Reject(()=>AdminProfileParser.ValidateBulkResponse(vehicleText,"#DumpAllSquadsInfoList true"),"Bulk squad capture cannot accept a vehicle report");
        Reject(()=>AdminProfileParser.Parse(squadText+"\n"+squadText,Now),"Repeated squads or members fail closed instead of merging ambiguous membership");
        Reject(()=>AdminProfileParser.Parse("[SquadId: 1 SquadName: Test]\nPlatform ID: broken Platform Name: A Character Name: B Member Rank: 4",Now),"Malformed bulk member IDs fail closed");
        var store=new AdminProfileDatabase(Path.Combine(root,"bulk.db"));store.Import("Other",squadText,Now);store.Import("Bulk",squadText,Now,new HashSet<string>(players.Select(player=>player.Id)));
        Check(store.Players("Bulk").Count==squads.Squads.Sum(squad=>squad.Members.Count) && store.Players("Other").Count==store.Players("Bulk").Count,"All squad members persist once with server isolation");
        var online=players.FirstOrDefault(player=>store.SquadMembers("Bulk",player.Id).Any(member=>member.Id==player.Id));
        Check(online!=null && store.SquadMembers("Bulk",online.Id).Single(member=>member.Id==online.Id).Status=="Online","Bulk membership uses the fresh population report to mark online players");
        Check(store.SquadMembers("Other","76561198010000011").All(member=>member.Status=="Unknown"),"Standalone bulk imports keep unreported status unknown");
        store.Import("Bulk",flagText,Now);int previous=store.Assets("Bulk").Count;
        Reject(()=>store.ImportFlagPages("Bulk",new[]{flags}),"Incomplete pagination preserves existing flags");
        Check(store.Assets("Bulk").Count==previous,"An incomplete flag refresh cannot delete previous observations");
        store.ImportFlagPages("Bulk",new[]{flags,page2});Check(store.Assets("Bulk").Count==combined.Count,"Only a complete validated page set replaces the flag snapshot");
        store.Import("Bulk",vehicleText,Now);
        var registered=store.Assets("Bulk").Where(asset=>asset.Kind=="Vehicle").ToList();
        Check(registered.Single(asset=>asset.Id=="12737589").OwnerId=="76561198010000002" && registered.Single(asset=>asset.Id=="12737589").OwnerDatabaseId=="53","Test player's supplied Rager registration takes precedence over Test player's trailing database ID");
        Check(registered.Single(asset=>asset.Id=="12669481").OwnerId=="76561198010000001" && registered.Count(asset=>asset.Name=="BPC_Rager" && asset.OwnerId=="76561198010000001")==1,"Test player retains her one registered Rager rather than inheriting Test player's");
        Check(registered.Count(asset=>asset.Name=="BPC_Rager" && asset.OwnerId=="76561198010000003")==2,"Test player retains both distinct registered Ragers");
        Check(registered.Single(asset=>asset.Id=="55361").OwnerId=="76561198010000002" && registered.Single(asset=>asset.Id=="12094805").OwnerId=="76561198010000002","Test player's other supplied vehicle registrations are not reassigned to the squad's flag owner");
        store.Import("Bulk",vehicleText,Now.AddMinutes(1));
        Check(store.Assets("Bulk").Count(asset=>asset.Kind=="Vehicle")==registered.Count,"Repeated vehicle refreshes replace observations without duplicating legitimate registrations");
        previous=store.Assets("Bulk").Count;
        store.Import("Bulk",second,Now);Check(store.Assets("Bulk").Count==previous,"Manual page imports merge without clearing other flag pages or vehicles");
        store.Import("Bulk","[SquadId: 999 SquadName: Replacement]\nPlatform ID: 76561198000000001 Platform Name: A Character Name: B Member Rank: 4",Now);
        Check(store.Players("Bulk").Count==1 && store.Players("Other").Count>1 && store.Assets("Bulk").Count==previous,"Bulk squad replacement removes stale membership while retaining assets and other servers");
        Directory.CreateDirectory(Path.Combine(root,"bulk-ui"));
        using(var bitmap=new Bitmap(32,32))bitmap.Save(Path.Combine(root,"bulk-ui","map.png"));
        using(var window=new MapWindow(Path.Combine(root,"bulk-ui"),true))using(var panel=new Form()) {
            var requests=new List<string>();string status=null;
            window.profileReportSender=(command,id,owner)=> {
                requests.Add(command);string report=command=="#ListPlayers true"?playerText:command=="#ListSpawnedVehicles true"?vehicleText:command=="#DumpAllSquadsInfoList true"?squadText:command=="#ListFlags 1 true"?flagText:second;
                return Task.FromResult(new SpawnBatchResult { Completed=true,SubmittedCount=1,ChatClosed=true,ClipboardResponse=report });
            };
            Check(AwaitUi(window.RefreshAdminProfileReports("Bulk",new AdminPlayer[0],panel,text=>status=text,true)),"A full profile refresh automatically imports population, vehicles, squads and flags");
            Check(requests.SequenceEqual(new[]{"#ListPlayers true","#ListSpawnedVehicles true","#DumpAllSquadsInfoList true","#ListFlags 1 true","#ListFlags 2 true"}),"A two-page server refresh uses exactly five commands, independent of player count");
            var saved=new AdminProfileDatabase(Path.Combine(root,"bulk-ui","admin-profiles.db"));int savedFlags=saved.Assets("Bulk").Count(asset=>asset.Kind=="Flag");requests.Clear();
            window.profileReportSender=(command,id,owner)=> { requests.Add(command);return Task.FromResult(command=="#ListFlags 2 true"?new SpawnBatchResult { StopReason="report-response-timeout",Error="Missing page" }:new SpawnBatchResult { Completed=true,SubmittedCount=1,ChatClosed=true,ClipboardResponse=command=="#DumpAllSquadsInfoList true"?squadText:flagText }); };
            Check(!AwaitUi(window.RefreshAdminSquadsAndFlags("Bulk",panel,text=>status=text)) && status=="Missing page" && saved.Assets("Bulk").Count(asset=>asset.Kind=="Flag")==savedFlags,"A later page timeout reports the failure and retains the previous complete flags");
        }
    }
    static void ClipboardTests() {
        Check(MapWindow.CanRestoreAdminTool(new IntPtr(7),new IntPtr(7),false) && MapWindow.CanRestoreAdminTool(new IntPtr(8),new IntPtr(7),true),"Admin tools return from the original game or another MiniMap window");
        Check(!MapWindow.CanRestoreAdminTool(new IntPtr(8),new IntPtr(7),false) && !MapWindow.CanRestoreAdminTool(IntPtr.Zero,IntPtr.Zero,false),"Admin tools never take focus from an unrelated application or missing game");
        var alpha=new AdminPlayer { Id="76561198000000001",Name="Alpha",PlatformName="One" };
        var beta=new AdminPlayer { Id="76561198000000002",Name="Beta",PlatformName="Two" };
        var gamma=new AdminPlayer { Id="76561198000000003",Name="Gamma",PlatformName="Three" };
        var candidates=new[]{gamma,beta,alpha};
        var squadCounts=new Dictionary<string,int> { {alpha.Id,4},{beta.Id,2} };
        var flagCounts=new Dictionary<string,int> { {alpha.Id,1},{beta.Id,5},{gamma.Id,1} };
        var vehicleCounts=new Dictionary<string,int> { {alpha.Id,2},{beta.Id,0},{gamma.Id,3} };
        Check(PlayerDirectoryQuery.Apply(candidates,"",0,squadCounts,flagCounts,vehicleCounts).SequenceEqual(new[]{alpha,beta,gamma}),"Directory names sort A–Z with deterministic ordering");
        Check(PlayerDirectoryQuery.Apply(candidates,"",1,squadCounts,flagCounts,vehicleCounts).SequenceEqual(new[]{gamma,beta,alpha}),"Directory names sort Z–A");
        Check(PlayerDirectoryQuery.Apply(candidates,"",2,squadCounts,flagCounts,vehicleCounts).SequenceEqual(new[]{alpha,beta,gamma}),"Largest squad sorting uses imported member counts");
        Check(PlayerDirectoryQuery.Apply(candidates,"",4,squadCounts,flagCounts,vehicleCounts).SequenceEqual(new[]{beta,alpha,gamma}),"Flag sorting uses count first and names to break ties");
        Check(PlayerDirectoryQuery.Apply(candidates,"",6,squadCounts,flagCounts,vehicleCounts).SequenceEqual(new[]{gamma,alpha,beta}),"Vehicle sorting orders the entire directory by imported vehicle counts");
        Check(PlayerDirectoryQuery.Apply(candidates,"One",3,squadCounts,flagCounts,vehicleCounts).Single()==alpha,"Search and squad sorting compose without altering source players");
        const string steamId="76561198000000001";
        string steamXml="<profile><steamID64>"+steamId+"</steamID64><steamID><![CDATA[Current ツ & Name]]></steamID></profile>";
        Check(SteamProfiles.Url(steamId)=="https://steamcommunity.com/profiles/"+steamId && SteamProfiles.Url("../elsewhere")==null,"Steam links use validated IDs and only the Steam Community profile endpoint");
        Check(SteamProfiles.ParseName(steamXml,steamId)=="Current ツ & Name","Public Steam username parsing preserves Unicode and CDATA");
        Check(SteamProfiles.ParseName(steamXml,"76561198000000002")==null,"Steam names cannot be assigned to a different player's ID");
        Check(SteamProfiles.ParseName(new string('x',65537),steamId)==null,"Oversized Steam profile responses are rejected");
        try { SteamProfiles.ParseName("<!DOCTYPE profile [<!ENTITY name SYSTEM 'file:///private'>]><profile/>",steamId);throw new Exception("Unsafe Steam XML accepted"); }
        catch(System.Xml.XmlException) { Check(true,"Steam XML cannot resolve external entities or local files"); }
        int reads=0,waits=0;uint sequence=5;
        var response=Native.WaitForPlayerClipboard(4,()=>sequence,()=>true,()=>{reads++;return First;},()=>true,ms=>{waits++;return Task.FromResult(0);}).GetAwaiter().GetResult();
        Check(response!=null && response.Sequence==5 && response.Text==First && reads==1 && waits==0,"A fresh valid SCUM-owned player response is captured once");
        reads=waits=0;
        response=Native.WaitForPlayerClipboard(5,()=>5,()=>true,()=>{reads++;return First;},()=>true,ms=>{waits++;return Task.FromResult(0);}).GetAwaiter().GetResult();
        Check(response==null && reads==0 && waits==30,"The old clipboard cannot be reused as a new player refresh");
        reads=0;
        response=Native.WaitForPlayerClipboard(4,()=>5,()=>false,()=>{reads++;return First;},()=>true,ms=>Task.FromResult(0)).GetAwaiter().GetResult();
        Check(response==null && reads==0,"Another application's clipboard is never imported as a game response");
        reads=0;sequence=5;
        response=Native.WaitForPlayerClipboard(4,()=>sequence,()=>true,()=>{reads++;sequence++;return First;},()=>true,ms=>Task.FromResult(0)).GetAwaiter().GetResult();
        Check(response==null && reads==31,"A clipboard changing during capture is rejected");
        reads=0;waits=0;
        response=Native.WaitForPlayerClipboard(4,()=>5,()=>true,()=>{reads++;return First;},()=>false,ms=>{waits++;return Task.FromResult(0);}).GetAwaiter().GetResult();
        Check(response==null && reads==0 && waits==0,"Focus/input cancellation stops response capture immediately");
        reads=0;
        response=Native.WaitForPlayerClipboard(4,()=>5,()=>true,()=>{reads++;return reads==1?"partial":First;},()=>true,ms=>Task.FromResult(0)).GetAwaiter().GetResult();
        Check(response!=null && reads==2,"Incomplete response text can settle without repeating the ListPlayers command");
        int elapsed=0;
        response=Native.WaitForPlayerClipboard(4,()=>elapsed>=4200?5u:4u,()=>true,()=>First,()=>true,ms=>{elapsed+=ms;return Task.FromResult(0);},null,15000).GetAwaiter().GetResult();
        Check(response!=null && elapsed==4200,"Delayed squad clipboard reports remain capturable beyond the population timeout without resending");
        elapsed=0;
        response=Native.WaitForPlayerClipboard(4,()=>4u,()=>true,()=>First,()=>elapsed<4100,ms=>{elapsed+=ms;return Task.FromResult(0);},null,15000).GetAwaiter().GetResult();
        Check(response==null && elapsed==4100,"The longer report wait still stops immediately on user input or focus cancellation");
        int enterCount=0;
        var dispatch=Native.AdminChatBatchSequence(1,index=>true,(index,submitted)=>Native.AdminChatSequence(
            (key,up)=>key!=0x1B,ms=>Task.FromResult(0),()=>true,()=>true,()=>{},0x54,0,
            ()=>{enterCount++;submitted();}),ms=>Task.FromResult(0),()=>true).GetAwaiter().GetResult();
        Check(dispatch.SubmittedCount==1 && !dispatch.ChatClosed && enterCount==1,"An interrupted Escape keeps the single submitted command recorded");
        response=Native.WaitForSubmittedAdminReport(dispatch,4,()=>5u,()=>true,()=>First,()=>true,ms=>Task.FromResult(0)).GetAwaiter().GetResult();
        Check(response!=null && enterCount==1,"A submitted report is captured automatically despite chat cleanup failure without another Enter");
        response=Native.WaitForSubmittedAdminReport(new SpawnBatchResult(),4,()=>5u,()=>true,()=>First,()=>true,ms=>Task.FromResult(0)).GetAwaiter().GetResult();
        Check(response==null,"An unsent command cannot capture an unrelated report");
        response=Native.WaitForSubmittedAdminReport(dispatch,4,()=>5u,()=>false,()=>First,()=>true,ms=>Task.FromResult(0)).GetAwaiter().GetResult();
        Check(response==null,"Read-only capture after interrupted cleanup still requires SCUM clipboard ownership");
    }
    static void PalettePlayerButtonTests(string root) {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        string profile=Path.Combine(root,"palette-player-button");Directory.CreateDirectory(profile);
        using(var bitmap=new Bitmap(32,32))bitmap.Save(Path.Combine(profile,"map.png"));
        var db=new PlayerDatabase(Path.Combine(profile,"admin-players.db"));db.SelectServer("Server A");db.Import("Server A",First+Second,Now);
        using(var window=new MapWindow(profile,true)) {
            Set(window,"adminToolsEnabled",true);Set(window,"fullMapActive",true);
            var overlay=Field<OverlayWindow>(window,"overlay");overlay.FullMapMode=true;overlay.Show();
            window.GetType().GetMethod("ShowAdminCommands",Hidden).Invoke(window,null);
            var palette=Field<Form>(window,"adminCommandsPanel");
            var players=Controls(palette).OfType<Button>().Single(button=>button.Name=="OpenAdminPlayers");
            var click=typeof(Button).GetMethod("OnClick",Hidden);
            click.Invoke(players,new object[]{EventArgs.Empty});click.Invoke(players,new object[]{EventArgs.Empty});
            Check(Field<Form>(window,"adminPlayersPanel")==null && palette.Visible,"Players button returns before hiding its active palette or constructing the directory");
            WaitFor(()=>Field<Form>(window,"adminPlayersPanel")!=null && Field<Form>(window,"adminPlayersPanel").Visible);
            var directory=Field<Form>(window,"adminPlayersPanel");
            var list=Controls(directory).OfType<ListView>().Single(control=>control.Name=="PlayerDirectory");WaitFor(()=>list.Items.Count==2);
            Check(!palette.Visible && directory.Visible && !Field<bool>(window,"playerDirectoryOpenQueued"),"Queued player handoff opens and loads the directory, hides the palette and releases its pending flag");
            Application.DoEvents();Check(ReferenceEquals(directory,Field<Form>(window,"adminPlayersPanel")),"Repeated Players clicks cannot create duplicate directories");
            directory.Close();
        }
    }
    static void UiTests(string root) {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        string profile=Path.Combine(root,"profile");Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"settings.ini"),"Welcomed=True");
        using(var bitmap=new Bitmap(32,32))bitmap.Save(Path.Combine(profile,"map.png"));
        var db=new PlayerDatabase(Path.Combine(profile,"admin-players.db"));db.SelectServer("Server A");db.Import("Server A",First+Second,Now);
        using(var window=new MapWindow(profile,true)) {
            Check(!Field<bool>(window,"adminToolsEnabled"),"Admin tools are disabled in a fresh profile by default");
            window.ShowAdminPlayers();Check(Field<Form>(window,"adminPlayersPanel")==null,"Disabled admin tools cannot open the player directory");
            Set(window,"adminToolsEnabled",true);
            Set(window,"adminPlayerToolOpening",1);
            window.GetType().GetMethod("UpdateIntensiveWorkPause",Hidden).Invoke(window,null);
            Check(Field<bool>(window,"intensiveWorkWasPaused") && Field<System.Windows.Forms.Timer>(window,"timer").Interval==250,
                "Player tools pause intensive work before directory construction begins");
            Set(window,"adminPlayerToolOpening",0);
            var initialMap=Field<OverlayWindow>(window,"overlay");initialMap.Show();
            IntPtr foreground=Native.GetForegroundWindow();window.ShowAdminPlayers();Application.DoEvents();
            Form form=Field<Form>(window,"adminPlayersPanel");var controls=Controls(form).ToList();var list=(ListView)controls.Single(control=>control.Name=="PlayerDirectory");
            WaitFor(()=>list.Items.Count==2);
            Check(form.Visible && list.Items.Count==2,"Opening the diagnostic player directory loads saved data");
            Check(Field<bool>(window,"intensiveWorkWasPaused") && Field<System.Windows.Forms.Timer>(window,"timer").Interval==250,
                "Visible player directory retains the paused tracking and animation cadence");
            Set(window,"copyInProgress",false);
            window.GetType().GetMethod("PerformCopyAsync",Hidden).Invoke(window,null);
            Check(!Field<bool>(window,"copyInProgress"),"Player directory blocks coordinate copy before clipboard capture");
            Check(((FlowLayoutPanel)controls.Single(control=>control.Name=="PlayerTiles")).Controls.Count==0,
                "Table view does not construct hidden player cards");
            Check(Native.GetForegroundWindow()==foreground,"Opening the diagnostic player directory does not take desktop focus");
            Check(form.TopMost,"Player directory remains above the topmost map");
            Check(!initialMap.Visible,"Opening player tools suspends the map window so it cannot cover input");
            var overlay=Field<OverlayWindow>(window,"overlay");overlay.Show();window.GetType().GetMethod("SetFullMap",Hidden).Invoke(window,new object[]{true});
            Check(!overlay.Visible,"Map restoration cannot cover an open player directory");
            window.SyncFullMapAdminControls(true,false);
            Check(Field<Form>(window,"adminCommandsPanel")==null || !Field<Form>(window,"adminCommandsPanel").Visible,
                "Attached admin controls stay hidden while the player directory is open");
            var refresh=(Button)controls.Single(control=>control.Name=="RefreshPlayers");var import=(Button)controls.Single(control=>control.Name=="ImportPlayers");
            var server=(ComboBox)controls.Single(control=>control.Name=="PlayerServer");var go=(Button)controls.Single(control=>control.Name=="TeleportToPlayer");
            Check(refresh.Enabled && import.Enabled && !go.Enabled,"Refresh/import are available for the selected server; teleport requires selection");
            server.Text="Unselected server";
            Check(!refresh.Enabled && !import.Enabled,"Editing a server name requires explicit selection before importing a population");
            server.Text="Server A";var search=(TextBox)controls.Single(control=>control.Name=="SearchPlayers");search.Text="ツ";
            Check(list.Items.Count==2,"Typing a search returns before the debounced directory rebuild");
            WaitFor(()=>list.Items.Count==1);
            Check(list.Items.Count==1,"Player search includes Unicode platform names");search.Text="76561198000000002";
            WaitFor(()=>list.Items.Count==1 && ((AdminPlayer)list.Items[0].Tag).Name=="Another Player");
            Check(list.Items.Count==1 && ((AdminPlayer)list.Items[0].Tag).Name=="Another Player","Player search also matches stable IDs");
            Set(window,"showAdminPlayers",true);Check(window.AdminPlayerMarkers(Now.AddSeconds(59)).Count==2,"Current snapshot players can be displayed on the map");
            Check(window.AdminPlayerMarkers(Now.AddMinutes(6)).Count==0 && window.AdminPlayerMarkers(Now.AddSeconds(-1)).Count==0,"Stale and future-dated snapshots never appear as current map positions");
            db.Import("Server A",First,Now.AddMinutes(1));window.ReloadAdminPlayers("Server A");
            Check(window.AdminPlayerMarkers(Now.AddMinutes(2)).Count==1,"Players absent from the latest snapshot are excluded from the map layer");
            var marker=Field<IList<AdminPlayer>>(window,"adminPlayers").Single(player=>player.InSnapshot);
            marker.SeenUtc=DateTime.UtcNow;PointF markerPoint=MapWindow.ToMap(marker.Location);
            var overlayKey=window.GetType().GetMethod("OverlayKey",Hidden);
            string freshKey=(string)overlayKey.Invoke(window,new object[0]);
            using(var bitmap=new Bitmap(200,200))using(var graphics=Graphics.FromImage(bitmap)) {
                graphics.Clear(Color.Magenta);
                window.GetType().GetMethod("DrawAdminPlayers",Hidden).Invoke(window,new object[]{graphics,new Rectangle(0,0,200,200),0f,0f,200f});
                Check(bitmap.GetPixel((int)(markerPoint.X*200),(int)(markerPoint.Y*200)).ToArgb()==Color.DeepSkyBlue.ToArgb(),
                    "The enabled player layer paints fresh positions using the map's actual coordinate transform");
            }
            marker.SeenUtc=DateTime.UtcNow.AddMinutes(-6);
            Check((string)overlayKey.Invoke(window,new object[0])!=freshKey,"The overlay cache changes when player snapshots expire even if the admin stands still");
            Set(window,"showAdminPlayers",false);Check(window.AdminPlayerMarkers(Now.AddMinutes(2)).Count==0,"The admin player map layer is opt-in");
            window.ShowAdminPlayers();Check(Object.ReferenceEquals(form,Field<Form>(window,"adminPlayersPanel")),"Repeated player-directory access reuses the existing window");
            var choose=(Button)controls.Single(control=>control.Name=="ChoosePlayerServer");
            server.Text="New server from UI";choose.PerformClick();
            WaitFor(()=>refresh.Enabled && Field<string>(window,"adminPlayerServer")=="New server from UI");
            Check(Field<string>(window,"adminPlayerServer")=="New server from UI" && import.Enabled && refresh.Enabled && list.Items.Count==0,
                "Typing a new server and clicking Select server enables import without mixing previous players");
            server.Text="Server A";
            typeof(ComboBox).GetMethod("OnSelectionChangeCommitted",Hidden).Invoke(server,new object[]{EventArgs.Empty});
            WaitFor(()=>import.Enabled && Field<string>(window,"adminPlayerServer")=="Server A");
            Check(Field<string>(window,"adminPlayerServer")=="Server A" && import.Enabled,
                "Selecting a saved server commits it immediately for clipboard import");
            var tiles=(FlowLayoutPanel)Controls(form).Single(control=>control.Name=="PlayerTiles");
            search.Text="";
            WaitFor(()=>list.Items.Count==2);
            ((Button)controls.Single(control=>control.Name=="PlayerView")).PerformClick();
            WaitFor(()=>tiles.Controls.Count==2);
            Check(tiles.Visible && !list.Visible && tiles.Controls.Count==2,"Players can switch to a tile directory with one card per saved player");
            using(var tile=MapWindow.CreateAdminPlayerTile(new AdminPlayer { Id="76561198000000001",Name="Test",PlatformName="Platform",SeenUtc=Now },2,1,page=>tilePages.Add(page))) {
                tile.CreateControl();foreach(var button in Controls(tile).OfType<Button>()) { typeof(Button).GetMethod("OnClick",Hidden).Invoke(button,new object[]{EventArgs.Empty}); }
                Check(tilePages.SequenceEqual(new[]{"Overview","Vehicles","Flags","Squad"}),"All player-tile quick links route to their intended profile page");
                Check(Controls(tile).OfType<Button>().All(button=>button.BackColor==OverlayTheme.Surface),"Dynamically created tile buttons retain the map's dark styling");
            }
            var profiles=new AdminProfileDatabase(Path.Combine(profile,"admin-profiles.db"));
            var quickTeleports=new List<bool>();
            using(var quickTile=MapWindow.CreateAdminPlayerTile(new AdminPlayer { Id="76561198000000001",Name="Test" },0,0,page=>{},bring=>quickTeleports.Add(bring))) {
                quickTile.CreateControl();
                foreach(string buttonName in new[]{"TileTeleportTo","TileTeleportHere"})typeof(Button).GetMethod("OnClick",Hidden).Invoke(Controls(quickTile).OfType<Button>().Single(button=>button.Name==buttonName),new object[]{EventArgs.Empty});
                Check(quickTeleports.SequenceEqual(new[]{false,true}),"Player tiles expose direct Teleport to and Teleport to me buttons with distinct targets");
            }
            profiles.Import("Server A","#42: BPC_Laika 2026-10-08T13:00:00.000Z X=1 Y=2 Z=3 0 76561198000000001",Now);
            var known=Field<IList<AdminPlayer>>(window,"adminPlayers").First(player=>player.Id=="76561198000000001");
            window.ShowAdminProfile(known);
            var liveProfile=Field<List<Form>>(window,"adminProfileWindows").Single();
            Check(!liveProfile.Modal && liveProfile.Visible,"Profiles use persistent non-modal windows for async command refresh");
            Check(liveProfile.Owner==form && form.OwnedForms.Contains(liveProfile),"Player profiles are owned by the directory so they remain above the admin player menu");
            liveProfile.Owner=null;form.Hide();
            Check(liveProfile.Visible && Field<bool>(window,"intensiveWorkWasPaused"),"A visible profile keeps the loops paused while the directory is hidden");
            int pausedFrames=window.PresentedFrames;
            window.GetType().GetMethod("RenderOverlay",Hidden).Invoke(window,null);
            Check(window.PresentedFrames==pausedFrames,"Player profiles suppress overlay animation rendering");
            window.ShowAdminPlayers();liveProfile.Owner=form;
            string capturedReport=null;
            window.profileReportSender=async(command,requested,panel)=> {
                Check((command=="#DumpAllSquadsInfoList true" || command=="#ListFlags 1 true") && requested==null,"Squad refresh uses bulk squad and paginated flag commands without a player ID");
                panel.Hide();await Task.Delay(20);
                Check(!panel.IsDisposed,"Hiding a profile while focusing SCUM preserves its lifetime");
                string report=command=="#DumpAllSquadsInfoList true"?"[SquadId: 95 SquadName: TEST SQUAD]\nPlatform ID: "+known.Id+" Platform Name: Platform One Character Name: Test Character Member Rank: 4\nPlatform ID: 76561198000000002 Platform Name: Platform Two Character Name: Flag Owner Member Rank: 3":"Page 1/1\nFlag ID: 91 | Owner: [76561198000000002] Test (49) | Location: X=1 Y=2 Z=3";
                capturedReport=report;AdminProfileParser.ValidateBulkResponse(report,command);
                var interrupted=new SpawnBatchResult { Completed=command=="#DumpAllSquadsInfoList true",SubmittedCount=1,ChatClosed=command=="#DumpAllSquadsInfoList true",Error="Cleanup interrupted" };
                var reply=await Native.WaitForSubmittedAdminReport(interrupted,1,()=>2u,()=>true,()=>report,()=>true,ms=>Task.FromResult(0),text=>AdminProfileParser.ValidateBulkResponse(text,command));
                interrupted.ClipboardResponse=reply.Text;return interrupted;
            };
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            ((Button)Controls(liveProfile).Single(control=>control.Name=="RefreshProfileSquad")).PerformClick();
            var refreshWait=System.Diagnostics.Stopwatch.StartNew();
            while(refreshWait.ElapsedMilliseconds<15000 && ((Button)Controls(liveProfile).Single(control=>control.Name=="RefreshProfileSquad")).Enabled==false) { Application.DoEvents();System.Threading.Thread.Sleep(5); }
            Application.DoEvents();
            Check(liveProfile.Visible && !liveProfile.IsDisposed && profiles.Squad("Server A",known.Id).Contains("TEST SQUAD") && ((ListView)Controls(liveProfile).Single(control=>control.Name=="ProfileFlags")).Items.Count==1,"The real squad-refresh button automatically imports a captured reply after interrupted cleanup and displays squad flags without manual import");
            Check(profiles.SquadFlagCounts("Server A")[known.Id]==1,"Player tile flag counts include flags owned by another imported squad member");
            liveProfile.Hide();profiles.ImportFlagPages("Server A",new[]{AdminProfileParser.Parse(capturedReport.Replace("Flag ID: 91","Flag ID: 92"),Now)});
            window.ShowAdminProfile(known,"Squad");
            Check(Field<List<Form>>(window,"adminProfileWindows").Count==1 && ((TabControl)Controls(liveProfile).Single(control=>control.Name=="ProfilePages")).SelectedTab.Name=="Squad","Reopening a player reuses the surviving profile and selects the requested page");
            WaitFor(()=> { var refreshedFlags=(ListView)Controls(liveProfile).Single(control=>control.Name=="ProfileFlags");return refreshedFlags.Items.Count==1 && ((AdminAsset)refreshedFlags.Items[0].Tag).Id=="92"; });
            Check(((AdminAsset)((ListView)Controls(liveProfile).Single(control=>control.Name=="ProfileFlags")).Items[0].Tag).Id=="92","Reopening a cached profile reloads reports imported while it was hidden");
            liveProfile.Close();window.profileReportSender=null;
            using(var overview=window.CreateAdminProfileForm(known)) {
                Check(Controls(overview).OfType<Button>().Any(button=>button.Name=="RefreshProfileAll"),"Profiles offer a combined vehicles, squad and flags refresh");
                overview.Show();Application.DoEvents();var pages=(TabControl)Controls(overview).Single(control=>control.Name=="ProfilePages");
                Check(pages.TabPages.Count==4 && pages.SelectedTab.Name=="Overview","A full player profile opens with Overview, Vehicles, Flags and Squad pages");
                var fullRequests=new List<string>();window.profileReportSender=(command,id,panel)=> {
                    fullRequests.Add(command);
                    string report=command=="#ListPlayers true"?First.Replace("Test O'Neil","Fresh Character").Replace("Account balance: -500","Account balance: 123456"):
                        command=="#ListSpawnedVehicles true"?"#42: BPC_Laika 2026-10-08T13:00:00.000Z X=1 Y=2 Z=3 43 "+known.Id:
                        command=="#DumpAllSquadsInfoList true"?"[SquadId: 95 SquadName: TEST SQUAD]\nPlatform ID: "+known.Id+" Platform Name: One Character Name: Fresh Character Member Rank: 4\nPlatform ID: 76561198000000002 Platform Name: Two Character Name: Flag Owner Member Rank: 3":
                        "Page 1/1\nFlag ID: 92 | Owner: [76561198000000002] Test (49) | Location: X=1 Y=2 Z=3";
                    return Task.FromResult(new SpawnBatchResult { Completed=true,SubmittedCount=1,ChatClosed=true,ClipboardResponse=report });
                };
                var fullRefresh=(Button)Controls(overview).Single(control=>control.Name=="RefreshProfileAll");
                bool uiServiced=false;overview.BeginInvoke(new Action(()=>uiServiced=!fullRefresh.Enabled));
                fullRefresh.PerformClick();
                var fullWait=System.Diagnostics.Stopwatch.StartNew();
                while(!fullRefresh.Enabled && fullWait.ElapsedMilliseconds<15000) { Application.DoEvents();Thread.Sleep(1); }
                Check(uiServiced && fullRefresh.Enabled,"The UI processes queued work while report imports are running and restores refresh controls afterwards");
                Check(fullRequests.SequenceEqual(new[]{"#ListPlayers true","#ListSpawnedVehicles true","#DumpAllSquadsInfoList true","#ListFlags 1 true"}) && known.Balance==123456 && Controls(overview).Single(control=>control.Name=="ProfileIdentity").Text.Contains("Fresh Character"),"The real Refresh all button updates player identity/statistics and all profile reports in one sequence");
                window.profileReportSender=null;
                ((Button)Controls(overview).Single(control=>control.Name=="OpenVehicles")).PerformClick();
                Application.DoEvents();
                var vehicleList=(ListView)Controls(overview).Single(control=>control.Name=="ProfileVehicles");
                Check(pages.SelectedTab.Name=="Vehicles" && vehicleList.Items.Count==1,"Overview links open the matching page and show explicitly owned vehicle locations");
                Check(vehicleList.ContextMenuStrip!=null && Controls(overview).Single(control=>control.Name=="ProfileIdentity").ContextMenuStrip.Items.Cast<ToolStripItem>().Any(item=>item.Text==Localization.Get("AdminCopyDetails")),"Vehicles and profile identity expose teleport and copy context menus");
                vehicleList.Items[0].Selected=true;((Button)Controls(overview).Single(control=>control.Name=="ShowProfileAssets")).PerformClick();
                Check(Field<IList<AdminAsset>>(window,"adminProfileMarkers").Single().Id=="42" && !form.Visible,"Selecting an asset paints its snapshot on the map and hides the player tools");
            }
            window.ShowAdminPlayers();server.Text="Other server";choose.PerformClick();
            WaitFor(()=>Field<string>(window,"adminPlayerServer")=="Other server" && choose.Enabled);
            Check(Field<IList<AdminAsset>>(window,"adminProfileMarkers").Count==0,"Switching servers clears selected asset markers");
            Check(Native.GetForegroundWindow()==foreground,"Profile page and tile regressions never take desktop focus");
            var close=(Button)controls.Single(control=>control.Name=="CloseWindow");
            Check(!close.CausesValidation,"Player window Close does not depend on server-field validation");
            Set(window,"musicalScumbagOpen",true);
            close.PerformClick();Check(form.IsDisposed,"Clicking player window Close disposes the idle directory");
            Check(Field<bool>(window,"intensiveWorkWasPaused"),"Closing player tools retains the pause while MusicalScumbag is open");
            Check(!(bool)window.GetType().GetProperty("AdminPlayerWorkPaused",Hidden).GetValue(window,null),
                "Music alone does not activate the player-tool input and rendering block");
            // Detach the snapshot before replacing the clipboard: the OLE data
            // object returned by GetDataObject can become invalid after SetText.
            var previousClipboard=Native.CaptureClipboardAsync().GetAwaiter().GetResult();
            try {
                Clipboard.SetText("{X=315989.281 Y=-229276.188 Z=16060.481|P=-22.862701 Y=-178.932495 R=0.000000}");
                window.GetType().GetMethod("PollMusicPausedInput",Hidden).Invoke(window,null);
                Check(Field<Position>(window,"position")!=null && Field<Position>(window,"position").X==315989.281,
                    "Music pause still reads manually copied coordinates without injecting input");
            } finally { if(previousClipboard!=null)Clipboard.SetDataObject(previousClipboard,true); }
            window.GetType().GetMethod("SetFullMap",Hidden).Invoke(window,new object[]{true});
            Check(Field<bool>(window,"fullMapActive"),"Full map can open during music pause");
            overlay.Show();Set(window,"lastFrameKey",null);
            int musicFrames=window.PresentedFrames;
            window.GetType().GetMethod("RenderOverlay",Hidden).Invoke(window,null);
            Check(window.PresentedFrames>musicFrames,"Explicit map rendering remains available during music pause");
            window.GetType().GetMethod("SetFullMap",Hidden).Invoke(window,new object[]{false});
            Set(window,"musicalScumbagOpen",false);
            window.GetType().GetMethod("UpdateIntensiveWorkPause",Hidden).Invoke(window,null);
            Check(!Field<bool>(window,"intensiveWorkWasPaused") && Field<System.Windows.Forms.Timer>(window,"timer").Interval!=250,
                "Closing all player tools resumes the loop when MusicalScumbag is closed");
            Check(overlay.Visible,"Closing player tools restores the previously visible map");
        }
    }
    static readonly List<string> tilePages=new List<string>();
    static void CacheAndResponsivenessTests(string root) {
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        string profile=Path.Combine(root,"responsive");Directory.CreateDirectory(profile);
        using(var map=new Bitmap(100,100))map.Save(Path.Combine(profile,"map.png"),System.Drawing.Imaging.ImageFormat.Png);
        string playerPath=Path.Combine(profile,"admin-players.db");
        var db=new PlayerDatabase(playerPath);db.SelectServer("Large server");
        var population=new System.Text.StringBuilder();
        for(int index=0;index<1000;index++)population.Append(Block(index+1,(76561198000000000L+index).ToString(),"Player "+index.ToString("D4"),"Platform "+index,index));
        db.Import("Large server",population.ToString(),Now);
        var reports=new AdminProfileDatabase(Path.Combine(profile,"admin-profiles.db"));
        reports.Import("Large server","#42: BPC_Laika 2026-10-08T13:00:00.000Z X=1 Y=2 Z=3 0 76561198000000000",Now);
        using(var window=new MapWindow(profile,true)) {
            Set(window,"adminToolsEnabled",true);
            using(var locked=new LocalSqliteDatabase(playerPath,connection=>{})) {
                locked.Execute("BEGIN EXCLUSIVE");
                var opening=System.Diagnostics.Stopwatch.StartNew();window.ShowAdminPlayers();opening.Stop();
                var form=Field<Form>(window,"adminPlayersPanel");
                Check(form.Visible && opening.ElapsedMilliseconds<500,"Player directory opens without waiting on a locked SQLite database");
                bool dispatched=false;form.BeginInvoke(new Action(()=>dispatched=true));
                Application.DoEvents();Check(dispatched,"The player directory processes UI input while startup data is locked");
                locked.Execute("COMMIT");
            }
            var panel=Field<Form>(window,"adminPlayersPanel");
            var list=(ListView)Controls(panel).Single(control=>control.Name=="PlayerDirectory");
            WaitFor(()=>list.Items.Count==1000);
            var tiles=(FlowLayoutPanel)Controls(panel).Single(control=>control.Name=="PlayerTiles");
            var scroll=Controls(panel).OfType<SidebarScrollPanel>().Single();
            var cadence=System.Diagnostics.Stopwatch.StartNew();long previous=0,maxGap=0;int ticks=0;
            string measuredAction="Open cards",slowestAction="";
            using(var heartbeat=new System.Windows.Forms.Timer { Interval=10 }) {
                heartbeat.Tick+=(s,e)=> { long now=cadence.ElapsedMilliseconds;if(now-previous>maxGap) {maxGap=now-previous;slowestAction=measuredAction;}previous=now;ticks++; };
                heartbeat.Start();
                ((Button)Controls(panel).Single(control=>control.Name=="PlayerView")).PerformClick();
                WaitFor(()=>tiles.Controls.Count>0);
                Check(tiles.Controls.Count<=30 && tiles.Controls.Count<1000,"A 1,000-player directory creates only one viewport-sized page of cards");
                var firstTile=tiles.Controls[0];
                scroll.ScrollBy(0,int.MaxValue);Application.DoEvents();
                Check(!firstTile.IsDisposed && ReferenceEquals(firstTile,tiles.Controls[0]),"Changing the pixel scroll offset never rebuilds player cards");
                var page=(NumericUpDown)Controls(panel).Single(control=>control.Name=="PlayerPageNumber");
                measuredAction="Next page";
                ((Button)Controls(panel).Single(control=>control.Name=="NextPlayerPage")).PerformClick();
                Check(firstTile.IsDisposed && page.Value==2,"Next page replaces only the bounded page of visible cards");
                var wheel=typeof(Control).GetMethod("OnMouseWheel",BindingFlags.Instance|BindingFlags.NonPublic);
                var wheelBatch=(BatchedWheel)typeof(SidebarScrollPanel).GetField("wheel",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(scroll);
                Action<Control,int> turnWheel=(target,delta)=> {
                    measuredAction="Wheel "+delta;
                    wheel.Invoke(target,new object[]{new HandledMouseEventArgs(MouseButtons.None,0,10,10,delta)});
                    WaitFor(()=>!wheelBatch.Waiting);
                };
                turnWheel(scroll,-120);
                Check(page.Value==3 && tiles.Controls.Count<=24,"Wheel down advances one bounded player page");
                turnWheel(Controls(tiles.Controls[0]).OfType<Button>().First(),120);
                Check(page.Value==2,"Wheel up over a player-card button returns to the previous page");
                turnWheel(scroll,-60);Check(page.Value==2,"Partial wheel deltas wait for a complete notch");
                turnWheel(scroll,-60);Check(page.Value==3,"High-resolution wheel deltas accumulate to one page step");
                turnWheel(scroll,-240);Check(page.Value==5,"Multiple wheel notches navigate the matching number of pages");
                var burstTile=tiles.Controls[0];
                for(int notch=0;notch<12;notch++)wheel.Invoke(scroll,new object[]{new HandledMouseEventArgs(MouseButtons.None,0,10,10,-120)});
                Check(page.Value==5 && !burstTile.IsDisposed,"Rapid player wheel input returns without rebuilding each intermediate page");
                WaitFor(()=>page.Value==17);
                var settledTile=tiles.Controls[0];DateTime settleUntil=DateTime.UtcNow.AddMilliseconds(150);
                WaitFor(()=>DateTime.UtcNow>=settleUntil);
                Check(ReferenceEquals(settledTile,tiles.Controls[0]),"Player wheel burst settles once without trailing queued rebuilds");
                page.Value=1;var edgeTile=tiles.Controls[0];turnWheel(scroll,120);
                Check(page.Value==1 && ReferenceEquals(edgeTile,tiles.Controls[0]),"Wheel up at the first page keeps the existing cards");
                page.Value=page.Maximum;
                WaitFor(()=>tiles.Controls.Cast<Control>().Any(control=>control.Name=="PlayerTile_76561198000000999"));
                Check(tiles.Controls.Count<=30,"Page jumping reaches the final player without building 1,000 controls");
                var bottomTile=tiles.Controls[0];scroll.RefreshContentLayout();Application.DoEvents();
                turnWheel(scroll,-120);
                Check(page.Value==page.Maximum && ReferenceEquals(bottomTile,tiles.Controls[0]),"Wheel down at the final page stays put without wrapping or rebuilding cards");
                Check(ReferenceEquals(bottomTile,tiles.Controls[0]),"An unchanged viewport reuses its existing player cards");
                measuredAction="Teleport and reopen";
                window.FinishPlayerTeleport(panel,new[]{"#TeleportTo 76561198000000999"},0);
                Check(panel.Visible,"A teleport stopped before submission leaves the player directory available");
                window.FinishPlayerTeleport(panel,new[]{"#ListPlayers true"},1);
                Check(panel.Visible,"Read-only report refreshes keep the directory available");
                var lastPage=page.Value;
                window.FinishPlayerTeleport(panel,new[]{"#TeleportTo 76561198000000999"},1);
                window.GetType().GetMethod("RestoreAdminTool",Hidden).Invoke(window,new object[]{panel,IntPtr.Zero});
                Check(!panel.Visible,"Submitted player teleports keep the directory hidden through automatic tool restoration");
                window.ShowAdminPlayers();
                Check(panel.Visible && page.Value==lastPage && ReferenceEquals(bottomTile,tiles.Controls[0]),"Reopening players preserves the server, page and existing card controls");
                measuredAction="Resize";
                page.Value=1;Application.DoEvents();int smallPageCount=tiles.Controls.Count;
                var originalSize=panel.ClientSize;
                panel.ClientSize=new Size(originalSize.Width,originalSize.Height+300);
                WaitFor(()=>tiles.Controls.Count>smallPageCount);
                Application.DoEvents();
                Check(tiles.Controls.Count<=24,"Resizing only the window height increases the visible page without building unbounded cards");
                panel.ClientSize=originalSize;
                WaitFor(()=>tiles.Controls.Count==smallPageCount);
                Application.DoEvents();
                Check(page.Value==1,"Restoring the window size recalculates pages and retains the visible player range");
                measuredAction="Search";
                ((TextBox)Controls(panel).Single(control=>control.Name=="SearchPlayers")).Text="Player 0999";
                WaitFor(()=>list.Items.Count==1);
                Check(page.Value==1 && page.Maximum==1 && tiles.Controls.Count==1,"Searching from the last page starts at the first matching result");
                var pump=System.Diagnostics.Stopwatch.StartNew();while(pump.ElapsedMilliseconds<100) { Application.DoEvents();Thread.Sleep(1); }
                Console.WriteLine("Player directory responsiveness: max UI gap="+maxGap+" ms during "+slowestAction+"; live cards="+tiles.Controls.Count+" of 1000 players.");
                Check(ticks>0 && maxGap<500,"Large-directory paging and scrolling keep the UI heartbeat within 500 ms");
            }
            var load=window.GetType().GetMethod("LoadProfileView",Hidden);
            Func<string,object> snapshot=id=> { var task=(Task)load.Invoke(window,new object[]{"Large server",id});task.GetAwaiter().GetResult();return task.GetType().GetProperty("Result").GetValue(task,null); };
            var cached=snapshot("76561198000000000");
            using(var locked=new LocalSqliteDatabase(playerPath,connection=>{})) {
                locked.Execute("BEGIN EXCLUSIVE");var warm=System.Diagnostics.Stopwatch.StartNew();
                Check(ReferenceEquals(cached,snapshot("76561198000000000")) && warm.ElapsedMilliseconds<500,
                    "A warm profile reuses its snapshot without reading a locked database");
                locked.Execute("COMMIT");
            }
            reports.Import("Large server","#43: BPC_Laika 2026-10-08T13:00:00.000Z X=1 Y=2 Z=3 0 76561198000000000",Now.AddSeconds(1));
            var fresh=snapshot("76561198000000000");
            var assets=(IList<AdminAsset>)fresh.GetType().GetField("Assets",Hidden).GetValue(fresh);
            Check(!ReferenceEquals(cached,fresh) && assets.Single().Id=="43","Changed database content invalidates the cache and returns the new report");
            var profileOpening=System.Diagnostics.Stopwatch.StartNew();window.ShowAdminProfile(Field<IList<AdminPlayer>>(window,"adminPlayers").First());profileOpening.Stop();
            Check(profileOpening.ElapsedMilliseconds<500,"A cached player profile opens without a synchronous database reload");
            var playerForm=Field<List<Form>>(window,"adminProfileWindows").Single();
            WaitFor(()=>((ListView)Controls(playerForm).Single(control=>control.Name=="ProfileVehicles")).Items.Count==1);
            var vehicleList=(ListView)Controls(playerForm).Single(control=>control.Name=="ProfileVehicles");
            var retainedRow=vehicleList.Items[0];playerForm.Hide();window.ShowAdminProfile(Field<IList<AdminPlayer>>(window,"adminPlayers").First());
            var reopenPump=System.Diagnostics.Stopwatch.StartNew();while(reopenPump.ElapsedMilliseconds<100) { Application.DoEvents();Thread.Sleep(1); }
            Check(ReferenceEquals(retainedRow,vehicleList.Items[0]),"Reopening an unchanged profile retains its existing UI rows");
            playerForm.Close();panel.Close();
        }
    }
    static void WaitFor(Func<bool> ready) {
        var wait=System.Diagnostics.Stopwatch.StartNew();
        while(!ready() && wait.ElapsedMilliseconds<10000) { Application.DoEvents();Thread.Sleep(1); }
        if(!ready())throw new Exception("Timed out waiting for the player directory update.");
    }
    [STAThread] static int Main(string[] args) {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        Control.CheckForIllegalCrossThreadCalls=true;
        string root=Path.Combine(Path.GetTempPath(),"MiniMap-PlayerTests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try { ParseTests();DatabaseTests(root);ProfileTests(root);BulkReportTests(root,args.Length>0?args[0]:Path.Combine("tests","fixtures","admin-reports"));ClipboardTests();PalettePlayerButtonTests(root);UiTests(root);CacheAndResponsivenessTests(root);Console.WriteLine("Passed "+count+" admin player checks.");return 0; }
        catch(Exception error) { Console.Error.WriteLine(error);return 1; }
        finally { Directory.Delete(root,true); }
    }
}
