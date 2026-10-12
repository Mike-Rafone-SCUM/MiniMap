using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal static class SteamProfiles {
        internal static string Url(string id) { return PlayerList.ValidId(id)?"https://steamcommunity.com/profiles/"+id:null; }
        internal static string ParseName(string xml,string id) {
            if(!PlayerList.ValidId(id) || xml==null || xml.Length>65536)return null;
            var settings=new System.Xml.XmlReaderSettings { DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536 };
            using(var reader=System.Xml.XmlReader.Create(new StringReader(xml),settings)) {
                var document=new System.Xml.XmlDocument { XmlResolver=null };document.Load(reader);
                if(document.SelectSingleNode("/profile/steamID64")==null || document.SelectSingleNode("/profile/steamID64").InnerText!=id)return null;
                var name=document.SelectSingleNode("/profile/steamID");return name==null || name.InnerText.Length==0 || name.InnerText.Length>128?null:name.InnerText;
            }
        }
        static readonly System.Threading.SemaphoreSlim slots=new System.Threading.SemaphoreSlim(2);
        static readonly Dictionary<string,Task<string>> names=new Dictionary<string,Task<string>>();
        internal static Task<string> Name(string id) {
            if(Url(id)==null)return Task.FromResult<string>(null);
            lock(names) { Task<string> found;if(names.TryGetValue(id,out found))return found;return names[id]=Task.Run(()=>FetchName(id)); }
        }
        static async Task<string> FetchName(string id) {
            await slots.WaitAsync();
            try {
                byte[] bytes=UpdateService.FetchBounded(new Uri(Url(id)+"/?xml=1"),65536,true);
                return ParseName(System.Text.Encoding.UTF8.GetString(bytes),id);
            }catch(Exception) { return null; }
            finally { slots.Release(); }
        }
    }
    internal sealed class AdminAsset {
        internal string Kind,Id,Name,OwnerId,OwnerDatabaseId,AliasId;
        internal Position Location;
        internal DateTime SeenUtc;
        internal string LastAccess;
    }
    internal sealed class SquadMember {
        internal string Id,PlatformName,Name,Rank,Status;
    }
    internal sealed class AdminProfileImport {
        internal string Kind,SquadId,SquadName;
        internal int Page,TotalPages;
        internal List<AdminProfileImport> Squads=new List<AdminProfileImport>();
        internal List<AdminAsset> Assets=new List<AdminAsset>();
        internal List<SquadMember> Members=new List<SquadMember>();
    }
    internal static class AdminProfileParser {
        static Regex Pattern(string value) { return new Regex(value,RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1)); }
        static readonly Regex Vehicle=Pattern(@"^#(?<id>\d{1,12}):\s+(?<type>[A-Za-z0-9_]{1,100})\s+(?<date>\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d\.\d{3}Z)\s+X=(?<x>-?\d+(?:\.\d+)?)\s+Y=(?<y>-?\d+(?:\.\d+)?)\s+Z=(?<z>-?\d+(?:\.\d+)?)\s+(?:STEAMID:(?<alias>\d{17})\s+VID:(?<vid>\d{1,12})\s+)?\d{1,12}\s+(?<owner>\d{17}|Owner not online, database id = \d{1,12}|No owner)$");
        static readonly Regex Flag=Pattern(@"^Flag ID: (?<id>\d{1,12}) \| Owner: (?:\[(?<owner>\d{17})\] (?<name>.+) \((?<database>\d{1,12})\)|No Owner \(0\)) \| Location: X=(?<x>-?\d+(?:\.\d+)?) Y=(?<y>-?\d+(?:\.\d+)?) Z=(?<z>-?\d+(?:\.\d+)?)$");
        static readonly Regex Squad=Pattern(@"^Squad: (?<name>.+) \((?<id>\d{1,12})\)$");
        static readonly Regex Member=Pattern(@"^(?<id>\d{17})\s{2,}(?<platform>.+?)\s{2,}(?<name>.+?)\s{2,}(?<rank>\d+)\s+(?<status>Online|Offline)$");
        static readonly Regex Clock=Pattern(@"^\[\d\d:\d\d:\d\d\]\s*");
        static readonly Regex DumpSquad=Pattern(@"^\[SquadId: (?<id>\d{1,12}) SquadName: (?<name>.+)\]$");
        static readonly Regex DumpMember=Pattern(@"^Platform ID: (?<id>\d{17}) Platform Name: (?<platform>.+?) Character Name: (?<name>.+?) Member Rank: (?<rank>\d{1,3})$");
        static readonly Regex FlagPage=Pattern(@"^Page (?<page>\d{1,3})/(?<total>\d{1,3})$");
        static IEnumerable<string> Lines(string text) { return text.Replace("\r\n","\n").Split('\n').Select(raw=>Clock.Replace(raw.Trim(),"")).Where(line=>line.Length>0 && !line.All(c=>c=='-')); }
        static AdminProfileImport ParseBulk(string text,DateTime seen,bool squads) {
            var result=new AdminProfileImport { Kind=squads?"Squads":"Flags" };AdminProfileImport current=null;
            var ids=new HashSet<string>();var squadIds=new HashSet<string>();
            foreach(string line in Lines(text)) {
                var header=squads?DumpSquad.Match(line):FlagPage.Match(line);
                if(header.Success) {
                    if(squads) {
                        if(current!=null && current.Members.Count==0)throw Invalid();
                        if(!squadIds.Add(header.Groups["id"].Value))throw Invalid();
                        current=new AdminProfileImport { Kind="Squad",SquadId=header.Groups["id"].Value,SquadName=Name(header.Groups["name"].Value.Trim()) };result.Squads.Add(current);
                    }else {
                        if(result.Page!=0)throw Invalid();result.Page=int.Parse(header.Groups["page"].Value,CultureInfo.InvariantCulture);result.TotalPages=int.Parse(header.Groups["total"].Value,CultureInfo.InvariantCulture);
                        if(result.Page<1 || result.TotalPages<1 || result.TotalPages>100 || result.Page>result.TotalPages)throw Invalid();
                    }
                    continue;
                }
                if(squads) {
                    var member=DumpMember.Match(line);if(current==null || !member.Success || !PlayerList.ValidId(member.Groups["id"].Value) || !ids.Add(member.Groups["id"].Value))throw Invalid();
                    current.Members.Add(new SquadMember { Id=member.Groups["id"].Value,PlatformName=Name(member.Groups["platform"].Value),Name=Name(member.Groups["name"].Value),Rank=member.Groups["rank"].Value,Status="Unknown" });
                    if(ids.Count>PlayerList.MaximumPlayers)throw Invalid();
                }else {
                    var flag=Flag.Match(line);if(result.Page==0 || !flag.Success || (flag.Groups["owner"].Success && !PlayerList.ValidId(flag.Groups["owner"].Value)) || !ids.Add(flag.Groups["id"].Value))throw Invalid();
                    var asset=Asset(flag,"Flag",seen);asset.OwnerId=flag.Groups["owner"].Value;asset.OwnerDatabaseId=flag.Groups["database"].Value;result.Assets.Add(asset);
                    if(result.Assets.Count>10000)throw Invalid();
                }
            }
            if(squads?(current==null || current.Members.Count==0):result.Page==0)throw Invalid();return result;
        }
        internal static void ValidateBulkResponse(string text,string command) {
            var report=Parse(text,DateTime.UtcNow);
            if(command=="#DumpAllSquadsInfoList true") { if(report.Kind!="Squads")throw Invalid();return; }
            var match=Pattern(@"^#ListFlags (?<page>\d{1,3}) true$").Match(command);
            if(!match.Success || report.Kind!="Flags" || report.Page!=int.Parse(match.Groups["page"].Value,CultureInfo.InvariantCulture))throw Invalid();
        }
        static double Coordinate(Match match,string field) {
            double value;
            if(!double.TryParse(match.Groups[field].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value) || double.IsInfinity(value) || double.IsNaN(value) || Math.Abs(value)>2000000)throw Invalid();
            return value;
        }
        static FormatException Invalid() { return new FormatException(Localization.Get("ProfileInvalid")); }
        static string Name(string value) { if(value.Length==0 || value.Length>256 || value.Any(char.IsControl))throw Invalid();return value; }
        static AdminAsset Asset(Match match,string kind,DateTime seen) {
            if(match.Groups["id"].Value.All(c=>c=='0'))throw Invalid();
            return new AdminAsset { Kind=kind,Id=match.Groups["id"].Value,Name=kind=="Flag" && !match.Groups["name"].Success?Localization.Get("ProfileNoOwner"):Name(match.Groups[kind=="Vehicle"?"type":"name"].Value),
                OwnerId="",OwnerDatabaseId="",AliasId="",LastAccess="",SeenUtc=seen.ToUniversalTime(),
                Location=new Position { X=Coordinate(match,"x"),Y=Coordinate(match,"y"),Z=Coordinate(match,"z") } };
        }
        internal static AdminProfileImport Parse(string text,DateTime seen) {
            if(string.IsNullOrWhiteSpace(text) || text.Length>PlayerList.MaximumLength)throw Invalid();
            string first=Lines(text).FirstOrDefault();
            if(first!=null && first.StartsWith("[SquadId:",StringComparison.Ordinal))return ParseBulk(text,seen,true);
            if(first!=null && first.StartsWith("Page ",StringComparison.Ordinal))return ParseBulk(text,seen,false);
            var result=new AdminProfileImport();var ids=new HashSet<string>();bool memberHeader=false;
            foreach(string raw in text.Replace("\r\n","\n").Split('\n')) {
                string line=Clock.Replace(raw.Trim(),"");if(line.Length==0 || line.All(c=>c=='-'))continue;
                Match match=Vehicle.Match(line);
                if(match.Success) {
                    if(result.Kind!=null && result.Kind!="Vehicles")throw Invalid();result.Kind="Vehicles";
                    if(!ids.Add(match.Groups["id"].Value) || (match.Groups["vid"].Success && match.Groups["vid"].Value!=match.Groups["id"].Value))throw Invalid();
                    DateTime date;if(!DateTime.TryParseExact(match.Groups["date"].Value,"yyyy-MM-dd'T'HH:mm:ss.fff'Z'",CultureInfo.InvariantCulture,DateTimeStyles.AssumeUniversal|DateTimeStyles.AdjustToUniversal,out date))throw Invalid();
                    var asset=Asset(match,"Vehicle",seen);asset.LastAccess=date.ToString("o",CultureInfo.InvariantCulture);asset.AliasId=match.Groups["alias"].Value;
                    string owner=match.Groups["owner"].Value;
                    if(PlayerList.ValidId(owner))asset.OwnerId=owner;
                    else if(owner.StartsWith("Owner not online, database id = ",StringComparison.Ordinal))asset.OwnerDatabaseId=owner.Substring("Owner not online, database id = ".Length);
                    result.Assets.Add(asset);continue;
                }
                if(result.Kind=="Vehicles")throw Invalid();result.Kind="Squad";
                if(line=="Flags:")continue;
                match=Flag.Match(line);
                if(match.Success) {
                    if(!ids.Add(match.Groups["id"].Value))throw Invalid();var asset=Asset(match,"Flag",seen);
                    asset.OwnerId=match.Groups["owner"].Value;asset.OwnerDatabaseId=match.Groups["database"].Value;result.Assets.Add(asset);continue;
                }
                match=Squad.Match(line);
                if(match.Success) { if(result.SquadId!=null)throw Invalid();result.SquadId=match.Groups["id"].Value;result.SquadName=Name(match.Groups["name"].Value);continue; }
                if(line.StartsWith("Platform ID",StringComparison.Ordinal) && line.Contains("Platform Name") && line.Contains("Character Name") && line.Contains("Member Rank") && line.Contains("Status")) {
                    if(memberHeader || result.SquadId==null)throw Invalid();memberHeader=true;continue;
                }
                match=Member.Match(line);
                if(!match.Success || !memberHeader || result.Members.Any(member=>member.Id==match.Groups["id"].Value))throw Invalid();
                if(match.Groups["rank"].Value.Length>3)throw Invalid();
                result.Members.Add(new SquadMember { Id=match.Groups["id"].Value,PlatformName=Name(match.Groups["platform"].Value),Name=Name(match.Groups["name"].Value),Rank=match.Groups["rank"].Value,Status=match.Groups["status"].Value });
                if(result.Members.Count>PlayerList.MaximumPlayers)throw Invalid();
            }
            if(result.Assets.Count>10000 || result.Kind==null || (result.Kind=="Vehicles" && result.Assets.Count==0) || (result.Kind=="Squad" && (result.SquadId==null || result.Members.Count==0)))throw Invalid();
            return result;
        }
    }
    // Separate local database: imported observations never modify server state.
    internal sealed class AdminProfileDatabase {
        readonly string path;
        internal AdminProfileDatabase(string path) { this.path=Path.GetFullPath(path); }
        LocalSqliteDatabase Open() {
            Directory.CreateDirectory(Path.GetDirectoryName(path));return new LocalSqliteDatabase(path,db=> {
                long version=db.ScalarLong("PRAGMA user_version");if(version!=0 && version!=1)throw new IOException(Localization.Get("ProfileDatabaseVersion"));
                if(version==0)db.Transaction(()=> {
                    db.Execute("CREATE TABLE assets(server TEXT NOT NULL,kind TEXT NOT NULL,id TEXT NOT NULL,name TEXT NOT NULL,owner TEXT NOT NULL,owner_db TEXT NOT NULL,alias TEXT NOT NULL,x TEXT NOT NULL,y TEXT NOT NULL,z TEXT NOT NULL,seen TEXT NOT NULL,last_access TEXT NOT NULL,PRIMARY KEY(server,kind,id))");
                    db.Execute("CREATE TABLE members(server TEXT NOT NULL,id TEXT NOT NULL,squad_id TEXT NOT NULL,squad_name TEXT NOT NULL,platform TEXT NOT NULL,name TEXT NOT NULL,rank TEXT NOT NULL,status TEXT NOT NULL,seen TEXT NOT NULL,PRIMARY KEY(server,id))");
                    db.Execute("PRAGMA user_version=1");
                });
            });
        }
        internal void Import(string server,string text,DateTime seen,ISet<string> onlineIds=null) {
            server=PlayerDatabase.ServerName(server).ToUpperInvariant();var parsed=AdminProfileParser.Parse(text,seen);
            using(var db=Open())db.Transaction(()=> {
                if(parsed.Kind=="Vehicles")db.Execute("DELETE FROM assets WHERE server=? AND kind='Vehicle'",server);
                else if(parsed.Kind=="Squads")db.Execute("DELETE FROM members WHERE server=?",server);
                else if(parsed.Kind=="Squad") {
                    // A squad report replaces only that squad's observations.
                    db.Execute("DELETE FROM assets WHERE server=? AND kind='Flag' AND owner IN (SELECT id FROM members WHERE server=? AND squad_id=?)",server,server,parsed.SquadId);
                    db.Execute("DELETE FROM members WHERE server=? AND squad_id=?",server,parsed.SquadId);
                }
                foreach(var asset in parsed.Assets)db.Execute("INSERT OR REPLACE INTO assets VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",server,asset.Kind,asset.Id,asset.Name,asset.OwnerId,asset.OwnerDatabaseId,asset.AliasId,
                    asset.Location.X.ToString("R",CultureInfo.InvariantCulture),asset.Location.Y.ToString("R",CultureInfo.InvariantCulture),asset.Location.Z.ToString("R",CultureInfo.InvariantCulture),asset.SeenUtc.ToString("o",CultureInfo.InvariantCulture),asset.LastAccess);
                foreach(var member in parsed.Members)db.Execute("INSERT OR REPLACE INTO members VALUES(?,?,?,?,?,?,?,?,?)",server,member.Id,parsed.SquadId,parsed.SquadName,member.PlatformName,member.Name,member.Rank,member.Status,seen.ToUniversalTime().ToString("o",CultureInfo.InvariantCulture));
                foreach(var squad in parsed.Squads)foreach(var member in squad.Members)db.Execute("INSERT OR REPLACE INTO members VALUES(?,?,?,?,?,?,?,?,?)",server,member.Id,squad.SquadId,squad.SquadName,member.PlatformName,member.Name,member.Rank,
                    onlineIds==null?"Unknown":onlineIds.Contains(member.Id)?"Online":"Offline",seen.ToUniversalTime().ToString("o",CultureInfo.InvariantCulture));
            });
        }
        internal static IList<AdminAsset> CompleteFlagPages(IList<AdminProfileImport> pages) {
            if(pages==null || pages.Count==0 || pages.Count>100)throw new FormatException(Localization.Get("ProfileInvalid"));
            var assets=new List<AdminAsset>();var ids=new HashSet<string>();
            for(int index=0;index<pages.Count;index++) {
                var page=pages[index];if(page==null || page.Kind!="Flags" || page.Page!=index+1 || page.TotalPages!=pages.Count)throw new FormatException(Localization.Get("ProfileInvalid"));
                foreach(var asset in page.Assets) { if(!ids.Add(asset.Id) || assets.Count>=10000)throw new FormatException(Localization.Get("ProfileInvalid"));assets.Add(asset); }
            }
            return assets.AsReadOnly();
        }
        internal void ImportFlagPages(string server,IList<AdminProfileImport> pages) {
            server=PlayerDatabase.ServerName(server).ToUpperInvariant();var assets=CompleteFlagPages(pages);
            using(var db=Open())db.Transaction(()=> {
                db.Execute("DELETE FROM assets WHERE server=? AND kind='Flag'",server);
                foreach(var asset in assets)db.Execute("INSERT INTO assets VALUES(?,?,?,?,?,?,?,?,?,?,?,?)",server,asset.Kind,asset.Id,asset.Name,asset.OwnerId,asset.OwnerDatabaseId,asset.AliasId,
                    asset.Location.X.ToString("R",CultureInfo.InvariantCulture),asset.Location.Y.ToString("R",CultureInfo.InvariantCulture),asset.Location.Z.ToString("R",CultureInfo.InvariantCulture),asset.SeenUtc.ToString("o",CultureInfo.InvariantCulture),asset.LastAccess);
            });
        }
        internal IList<AdminAsset> Assets(string server) {
            server=PlayerDatabase.ServerName(server).ToUpperInvariant();var result=new List<AdminAsset>();
            using(var db=Open())using(var query=db.Prepare("SELECT kind,id,name,owner,owner_db,alias,x,y,z,seen,last_access FROM assets WHERE server=? ORDER BY kind,name,id",server))while(query.Read()) {
                result.Add(new AdminAsset { Kind=query.Text(0),Id=query.Text(1),Name=query.Text(2),OwnerId=query.Text(3),OwnerDatabaseId=query.Text(4),AliasId=query.Text(5),
                    Location=new Position { X=double.Parse(query.Text(6),CultureInfo.InvariantCulture),Y=double.Parse(query.Text(7),CultureInfo.InvariantCulture),Z=double.Parse(query.Text(8),CultureInfo.InvariantCulture) },
                    SeenUtc=DateTime.ParseExact(query.Text(9),"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),LastAccess=query.Text(10) });
            }
            // Vehicle STEAMID tags identify the individual registration. The trailing
            // ownership field can point at another squad member; do not use that
            // relationship to overwrite a vehicle's registration. An explicit
            // "No owner" row remains unowned even if it retains a STEAMID tag.
            foreach(var asset in result.Where(asset=>asset.Kind=="Vehicle" && asset.AliasId.Length>0 && (asset.OwnerId.Length>0 || asset.OwnerDatabaseId.Length>0)))
                asset.OwnerId=asset.AliasId;
            // Flag reports provide a fallback for vehicles without a registration tag.
            // Resolve only unambiguous matches in this server's imported observations.
            var owners=result.Where(asset=>asset.Kind=="Flag" && asset.OwnerId.Length>0 && asset.OwnerDatabaseId.Length>0)
                .GroupBy(asset=>asset.OwnerDatabaseId).Where(group=>group.Select(asset=>asset.OwnerId).Distinct().Count()==1)
                .ToDictionary(group=>group.Key,group=>group.First().OwnerId);
            foreach(var asset in result.Where(asset=>asset.Kind=="Vehicle" && asset.OwnerId.Length==0 && asset.OwnerDatabaseId.Length>0)) {
                string owner;if(owners.TryGetValue(asset.OwnerDatabaseId,out owner))asset.OwnerId=owner;
            }
            return result.AsReadOnly();
        }
        internal IDictionary<string,int> SquadFlagCounts(string server) {
            server=PlayerDatabase.ServerName(server).ToUpperInvariant();var result=new Dictionary<string,int>();
            using(var db=Open())using(var query=db.Prepare("SELECT m.id,CAST(COUNT(DISTINCT a.id) AS TEXT) FROM members m JOIN members owner ON owner.server=m.server AND owner.squad_id=m.squad_id JOIN assets a ON a.server=owner.server AND a.owner=owner.id AND a.kind='Flag' WHERE m.server=? GROUP BY m.id",server))while(query.Read())result[query.Text(0)]=int.Parse(query.Text(1),CultureInfo.InvariantCulture);
            return result;
        }
        internal IDictionary<string,int> SquadSizes(string server) {
            server=PlayerDatabase.ServerName(server).ToUpperInvariant();var result=new Dictionary<string,int>();
            using(var db=Open())using(var query=db.Prepare("SELECT m.id,CAST(COUNT(other.id) AS TEXT) FROM members m JOIN members other ON other.server=m.server AND other.squad_id=m.squad_id WHERE m.server=? GROUP BY m.id",server))while(query.Read())result[query.Text(0)]=int.Parse(query.Text(1),CultureInfo.InvariantCulture);
            return result;
        }
        internal string Squad(string server,string id) {
            server=PlayerDatabase.ServerName(server).ToUpperInvariant();using(var db=Open())using(var query=db.Prepare("SELECT squad_name,squad_id,name,rank,status,seen FROM members WHERE server=? AND squad_id=(SELECT squad_id FROM members WHERE server=? AND id=?) ORDER BY name",server,server,id)) {
                var lines=new List<string>();while(query.Read()) { if(lines.Count==0)lines.Add(Localization.T("ProfileSquadHeader",query.Text(0),query.Text(1),DateTime.ParseExact(query.Text(5),"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind).ToLocalTime()));lines.Add(Localization.T("ProfileSquadMember",query.Text(2),query.Text(3),Localization.Get(query.Text(4)=="Online"?"ProfileOnline":query.Text(4)=="Offline"?"ProfileOffline":"ProfileUnknownStatus"))); }
                return lines.Count==0?Localization.Get("ProfileNoSquad"):string.Join(Environment.NewLine,lines);
            }
        }
        internal IList<SquadMember> SquadMembers(string server,string id) {
            server=PlayerDatabase.ServerName(server).ToUpperInvariant();var result=new List<SquadMember>();
            using(var db=Open())using(var query=db.Prepare("SELECT id,platform,name,rank,status FROM members WHERE server=? AND squad_id=(SELECT squad_id FROM members WHERE server=? AND id=?) ORDER BY name",server,server,id))while(query.Read())
                result.Add(new SquadMember { Id=query.Text(0),PlatformName=query.Text(1),Name=query.Text(2),Rank=query.Text(3),Status=query.Text(4) });
            return result.AsReadOnly();
        }
        internal IList<AdminPlayer> Players(string server) {
            server=PlayerDatabase.ServerName(server).ToUpperInvariant();var result=new List<AdminPlayer>();
            using(var db=Open())using(var query=db.Prepare("SELECT id,name,platform,seen FROM members WHERE server=? ORDER BY name",server))while(query.Read())
                result.Add(new AdminPlayer { Id=query.Text(0),Name=query.Text(1),PlatformName=query.Text(2),SeenUtc=DateTime.ParseExact(query.Text(3),"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind),InSnapshot=false });
            return result.AsReadOnly();
        }
    }
    public partial class MapWindow {
        static readonly Lazy<Bitmap> steamProfileIcon=new Lazy<Bitmap>(()=> {
            var icon=new Bitmap(22,22);
            using(var source=typeof(MapWindow).Assembly.GetManifestResourceStream("steam-logo.png"))using(var g=Graphics.FromImage(icon)) {
                g.Clear(Color.Transparent);
                if(source!=null)using(var image=Image.FromStream(source)) { g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;g.DrawImage(image,new Rectangle(0,0,22,22)); }
            }
            return icon;
        });
        static Button SteamProfileButton(string id,bool allowBrowser) {
            var icon=new Bitmap(steamProfileIcon.Value);
            var button=new Button { Name="SteamProfileId",Text=id,Dock=DockStyle.Top,Height=28,Image=icon,ImageAlign=ContentAlignment.MiddleLeft,TextImageRelation=TextImageRelation.ImageBeforeText,Enabled=SteamProfiles.Url(id)!=null };
            button.Click+=(s,e)=> { string url=SteamProfiles.Url(id);if(allowBrowser && url!=null) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute=true }); }catch(Exception error) { Program.LogException("SteamProfile",error); } } };
            button.Disposed+=(s,e)=>icon.Dispose();return button;
        }
        readonly List<Form> adminProfileWindows=new List<Form>();
        internal Func<string,string,Form,Task<SpawnBatchResult>> profileReportSender=null;
        IList<AdminAsset> adminProfileMarkers=new List<AdminAsset>().AsReadOnly();
        AdminProfileDatabase ProfileStore { get { return new AdminProfileDatabase(Path.Combine(dataFolder,"admin-profiles.db")); } }
        sealed class ProfileViewSnapshot {
            internal string Stamp,Squad;
            internal AdminPlayer Player;
            internal IList<AdminAsset> Assets;
            internal IList<SquadMember> Members;
        }
        readonly Dictionary<string,ProfileViewSnapshot> profileViewCache=new Dictionary<string,ProfileViewSnapshot>();
        sealed class AdminServerSnapshot {
            internal string Stamp;
            internal IList<AdminPlayer> Players;
            internal IList<AdminAsset> Assets;
            internal IDictionary<string,int> SquadFlags,SquadSizes;
        }
        readonly Dictionary<string,AdminServerSnapshot> adminServerCache=new Dictionary<string,AdminServerSnapshot>();
        AdminServerSnapshot LoadAdminServerSnapshot(string scope) {
            // Called by database workers; cached snapshots survive closing a tool window.
            lock(adminServerCache) {
                string key=scope.ToUpperInvariant(),stamp=AdminDatabaseStamp();
                AdminServerSnapshot snapshot;
                if(adminServerCache.TryGetValue(key,out snapshot) && snapshot.Stamp==stamp)return snapshot;
                snapshot=new AdminServerSnapshot { Stamp=stamp,Players=LoadAdminPlayers(scope),Assets=ProfileStore.Assets(scope),
                    SquadFlags=ProfileStore.SquadFlagCounts(scope),SquadSizes=ProfileStore.SquadSizes(scope) };
                if(adminServerCache.Count>=8)adminServerCache.Clear();
                adminServerCache[key]=snapshot;return snapshot;
            }
        }
        string AdminDatabaseStamp() {
            return string.Join("|",new[]{"admin-players.db","admin-profiles.db"}.Select(name=> {
                var file=new FileInfo(Path.Combine(dataFolder,name));
                return file.Exists?file.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)+":"+file.Length:"missing";
            }));
        }
        Task<ProfileViewSnapshot> LoadProfileView(string scope,string playerId) {
            return Task.Run(()=> {
                lock(profileViewCache) {
                    string key=scope.ToUpperInvariant()+"\n"+playerId,stamp=AdminDatabaseStamp();
                    ProfileViewSnapshot snapshot;
                    if(profileViewCache.TryGetValue(key,out snapshot) && snapshot.Stamp==stamp)return snapshot;
                    var server=LoadAdminServerSnapshot(scope);
                    snapshot=new ProfileViewSnapshot {
                        Player=server.Players.FirstOrDefault(item=>item.Id==playerId),
                        Squad=ProfileStore.Squad(scope,playerId).Split(new[]{Environment.NewLine},StringSplitOptions.None)[0],
                        Assets=server.Assets,Members=ProfileStore.SquadMembers(scope,playerId)
                    };
                    // A write during loading must invalidate this snapshot on the next request.
                    snapshot.Stamp=stamp;
                    if(profileViewCache.Count>=64)profileViewCache.Clear();
                    profileViewCache[key]=snapshot;return snapshot;
                }
            });
        }
        string ImportAdminClipboard(string scope,string text) {
            bool population=true;try { PlayerList.Parse(text,DateTime.UtcNow); }catch(FormatException) { population=false; }
            if(population) { PlayerStore.Import(scope,text,DateTime.UtcNow);return Localization.T("PlayersImported",PlayerStore.Load(scope).Count(player=>player.InSnapshot),scope); }
            var report=AdminProfileParser.Parse(text,DateTime.UtcNow);ProfileStore.Import(scope,text,DateTime.UtcNow);
            return report.Kind=="Flags"?Localization.T("ProfileFlagPageImported",report.Page,report.TotalPages):Localization.T("ProfileImportDone",scope);
        }
        void ClearAdminProfileMarkers() { adminProfileMarkers=new List<AdminAsset>().AsReadOnly();adminPlayerRevision++;canvas.Invalidate();overlay.Invalidate(); }
        internal static Panel CreateAdminPlayerTile(AdminPlayer player,int vehicles,int flags,Action<string> open,Action<bool> teleport=null,bool onlineNames=false) {
            var tile=new Panel { Name="PlayerTile_"+player.Id,Width=295,Height=260,Margin=new Padding(4),Padding=new Padding(10),BackColor=Color.FromArgb(38,43,49) };
            var title=new Label { Dock=DockStyle.Top,Height=28,Font=new Font("Segoe UI",12f,FontStyle.Bold),Text=player.Name,AutoEllipsis=true };
            var identity=new Label { Dock=DockStyle.Fill,Text=player.PlatformName+"\n"+Localization.Get(player.InSnapshot?"PlayersSnapshot":player.Location==null?"ProfileImportedMember":"PlayersHistory")+"\n"+Localization.T("ProfileObservation",player.SeenUtc.ToLocalTime().ToString("g")),AutoEllipsis=true };
            var steam=SteamProfileButton(player.Id,onlineNames);
            var heading=new Panel { Dock=DockStyle.Top,Height=56 };
            var avatar=new Panel { Dock=DockStyle.Left,Width=38 };
            avatar.Paint+=(s,e)=> { using(var brush=new SolidBrush(OverlayTheme.InkMuted)) { e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.FillEllipse(brush,9,9,16,16);e.Graphics.FillEllipse(brush,3,29,28,24); } };
            var headingText=new Panel { Dock=DockStyle.Fill };headingText.Controls.Add(steam);headingText.Controls.Add(title);heading.Controls.Add(headingText);heading.Controls.Add(avatar);
            var stats=new Label { Dock=DockStyle.Bottom,Height=42,AutoEllipsis=true,Text=player.Location==null?"":Localization.T("PlayersDetails",player.Fame.ToString("N0"),player.Balance.ToString("N0"),player.Gold.ToString("N0")),ForeColor=OverlayTheme.Accent };
            bool lookupStarted=false;tile.VisibleChanged+=async(s,e)=> { if(!onlineNames || lookupStarted || !tile.Visible)return;lookupStarted=true;string name=await SteamProfiles.Name(player.Id);if(name!=null && !tile.IsDisposed)identity.Text=name+identity.Text.Substring(identity.Text.IndexOf('\n')); };
            var links=new FlowLayoutPanel { Dock=DockStyle.Bottom,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true };
            foreach(string page in new[]{"Overview","Vehicles","Flags","Squad"}) {
                string label=Localization.Get(page=="Vehicles"?"ProfileVehicles":page=="Flags"?"ProfileFlags":page=="Squad"?"ProfileSquad":"ProfileOverview");
                var button=new Button { Name="Tile"+page,Width=118,Height=28,AutoEllipsis=true,Text=page=="Vehicles"?Localization.T("ProfileAssetCount",label,vehicles):page=="Flags"?Localization.T("ProfileAssetCount",label,flags):label };
                button.Click+=(s,e)=>open(page);links.Controls.Add(button);
            }
            if(teleport!=null)foreach(bool bring in new[]{false,true}) {
                var button=new Button { Name=bring?"TileTeleportHere":"TileTeleportTo",Width=118,Height=28,AutoEllipsis=true,Text=Localization.Get(bring?"PlayersHere":"PlayersGo") };
                button.Click+=(s,e)=>teleport(bring);links.Controls.Add(button);
            }
            tile.Controls.Add(identity);tile.Controls.Add(heading);tile.Controls.Add(stats);tile.Controls.Add(links);OverlayTheme.Style(tile);
            heading.BackColor=headingText.BackColor=avatar.BackColor=stats.BackColor=OverlayTheme.SurfaceCard;stats.ForeColor=OverlayTheme.Accent;
            tile.BackColor=identity.BackColor=title.BackColor=links.BackColor=OverlayTheme.SurfaceCard;title.ForeColor=OverlayTheme.Accent;
            tile.Paint+=(s,e)=> { using(var pen=new Pen(OverlayTheme.Border))e.Graphics.DrawRectangle(pen,0,0,tile.ClientSize.Width-1,tile.ClientSize.Height-1); };return tile;
        }
        internal static void StyleProfileList(ListView list) {
            list.BorderStyle=BorderStyle.None;list.BackColor=OverlayTheme.Surface;list.ForeColor=OverlayTheme.Ink;list.OwnerDraw=true;
            list.DrawColumnHeader+=(s,e)=> { using(var brush=new SolidBrush(OverlayTheme.SurfaceCard))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.Header.Text,list.Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),OverlayTheme.InkMuted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); };
            list.DrawItem+=(s,e)=> { if(list.View!=View.Details)e.DrawDefault=true; };
            list.DrawSubItem+=(s,e)=> { using(var brush=new SolidBrush(e.Item.Selected?Color.FromArgb(55,43,27):OverlayTheme.Surface))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,e.SubItem.Text,list.Font,new Rectangle(e.Bounds.X+8,e.Bounds.Y,e.Bounds.Width-12,e.Bounds.Height),e.Item.Selected?OverlayTheme.Accent:OverlayTheme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis); };
        }
        internal static double MarkerDistance(PointF click,Position location,float side) {
            if(location==null)return double.PositiveInfinity;var point=ToMap(location);
            double dx=(point.X-click.X)*side,dy=(point.Y-click.Y)*side;return Math.Sqrt(dx*dx+dy*dy);
        }
        internal static string ReportCoordinates(Position location) {
            if(location==null || new[]{location.X,location.Y,location.Z}.Any(value=>double.IsNaN(value) || double.IsInfinity(value)))return null;
            return string.Format(CultureInfo.InvariantCulture,"X={0:0.###} Y={1:0.###} Z={2:0.###}",location.X,location.Y,location.Z);
        }
        static string PlayerInformation(AdminPlayer player) { return player.Name+"\n"+player.PlatformName+"\n"+player.Id+"\n"+(player.Location==null?Localization.Get("ProfileNoPopulation"):ReportCoordinates(player.Location)+"\n"+Localization.T("PlayersDetails",player.Fame,player.Balance,player.Gold)+"\n"+player.SeenUtc.ToLocalTime().ToString("g")); }
        static string AssetInformation(AdminAsset asset) { return asset.Kind+" "+asset.Id+"\n"+asset.Name+"\nOwner: "+asset.OwnerId+"\nOwner database ID: "+asset.OwnerDatabaseId+"\nAlias ID: "+asset.AliasId+"\n"+ReportCoordinates(asset.Location)+"\n"+asset.SeenUtc.ToLocalTime().ToString("g"); }
        void AddAdminCopy(ToolStripDropDown menu,string label,Func<string> value,Action<string> status=null) {
            var item=new ToolStripMenuItem(Localization.Get(label));
            menu.Opening+=(s,e)=>item.Enabled=!adminCommandBusy && !relativeTeleportBusy && !string.IsNullOrEmpty(value());
            item.Click+=(s,e)=> { if(adminCommandBusy || relativeTeleportBusy)return;string text=value();if(string.IsNullOrEmpty(text))return;try { Clipboard.SetText(text);if(status!=null)status(Localization.Get("AdminCopied")); }catch(Exception error) { if(status!=null)status(error.Message);else Program.LogException("AdminCopy",error); } };menu.Items.Add(item);
        }
        void AddPlayerInformationMenu(ToolStripDropDown menu,Func<AdminPlayer> selected,Form panel,Action<string> status) {
            menu.Items.Add(new ToolStripSeparator());
            var profile=new ToolStripMenuItem(Localization.Get("ProfileOpenPlayer"));profile.Click+=(s,e)=> { var player=selected();if(player!=null)ShowAdminProfile(player); };menu.Items.Add(profile);
            var steam=new ToolStripMenuItem("Steam");steam.Click+=(s,e)=> { var player=selected();string url=player==null?null:SteamProfiles.Url(player.Id);if(url!=null && !diagnosticMode)try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute=true }); }catch(Exception error) { status(error.Message); } };menu.Items.Add(steam);
            AddAdminCopy(menu,"AdminCopyId",()=>selected()==null?null:selected().Id,status);
            AddAdminCopy(menu,"AdminCopyName",()=>selected()==null?null:selected().Name,status);
            AddAdminCopy(menu,"AdminCopyCoordinates",()=>selected()==null?null:ReportCoordinates(selected().Location),status);
            AddAdminCopy(menu,"AdminCopyDetails",()=>selected()==null?null:PlayerInformation(selected()),status);
            AddAdminCopy(menu,"AdminCopyCommand",()=>selected()==null?null:selected().TeleportTo,status);
        }
        void PopulateAdminMarkerContextMenu(ContextMenuStrip menu,Point click) {
            foreach(ToolStripItem old in menu.Items.Cast<ToolStripItem>().Where(item=>item.Name=="AdminMarkerTeleport").ToArray()) { menu.Items.Remove(old);old.Dispose(); }
            PointF mapPoint;if(!adminToolsEnabled || !TryGetFullMapPoint(click,out mapPoint))return;
            float side=overlay.Height*fullMapZoom;
            var asset=adminProfileMarkers.OrderBy(item=>MarkerDistance(mapPoint,item.Location,side)).FirstOrDefault();
            var player=AdminPlayerMarkers(DateTime.UtcNow).OrderBy(item=>MarkerDistance(mapPoint,item.Location,side)).FirstOrDefault();
            double assetDistance=asset==null?double.PositiveInfinity:MarkerDistance(mapPoint,asset.Location,side),playerDistance=player==null?double.PositiveInfinity:MarkerDistance(mapPoint,player.Location,side);
            if(Math.Min(assetDistance,playerDistance)>12)return;
            bool usePlayer=playerDistance<=assetDistance;string name=usePlayer?player.Name:asset.Name;
            var root=new ToolStripMenuItem(name) { Name="AdminMarkerTeleport" };
            Action<string,string> add=(label,command)=> {
                var item=new ToolStripMenuItem(label) { Enabled=command!=null && !adminCommandBusy && !relativeTeleportBusy };
                item.Click+=(s,e)=> { menu.Close();Native.ReleaseCapture();try { BeginInvoke(new Action(async()=>await RunAdminCommand(command,overlay))); }catch(InvalidOperationException) {} };root.DropDownItems.Add(item);
            };
            if(usePlayer) { add(Localization.Get("PlayersGo"),player.TeleportTo);add(Localization.Get("PlayersHere"),player.TeleportHere); }
            else add(Localization.Get(asset.Kind=="Flag"?"ProfileTeleportFlag":"ProfileTeleportVehicle"),AssetTeleportCommand(asset));
            if(usePlayer)AddPlayerInformationMenu(root.DropDown,()=>player,overlay,text=>note=text);
            else {
                root.DropDownItems.Add(new ToolStripSeparator());
                AddAdminCopy(root.DropDown,"AdminCopyId",()=>asset.Id);AddAdminCopy(root.DropDown,"AdminCopyName",()=>asset.Name);AddAdminCopy(root.DropDown,"AdminCopyCoordinates",()=>ReportCoordinates(asset.Location));AddAdminCopy(root.DropDown,"AdminCopyOwner",()=>asset.OwnerId);AddAdminCopy(root.DropDown,"AdminCopyDetails",()=>AssetInformation(asset));AddAdminCopy(root.DropDown,"AdminCopyCommand",()=>AssetTeleportCommand(asset));
            }
            menu.Items.Insert(0,root);OverlayTheme.Menu(menu);
        }
        internal static string AssetTeleportCommand(AdminAsset asset) {
            if(asset==null || asset.Location==null)return null;
            var p=asset.Location;
            if(new[]{p.X,p.Y,p.Z}.Any(value=>double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value)>2000000))return null;
            return string.Format(CultureInfo.InvariantCulture,"#Teleport {0:0.###} {1:0.###} {2:0.###}",p.X,p.Y,p.Z);
        }
        void AttachPlayerTeleportMenu(Control target,AdminPlayer player,Form panel,Action<string> status) {
            var menu=new ContextMenuStrip();
            foreach(bool bring in new[]{false,true}) {
                var item=new ToolStripMenuItem(Localization.Get(bring?"PlayersHere":"PlayersGo"));
                item.Click+=async(s,e)=> { if(adminCommandBusy || relativeTeleportBusy)return;bool sent=await RunAdminCommand(bring?player.TeleportHere:player.TeleportTo,panel);if(!panel.IsDisposed)status(Localization.Get(sent?"PlayersTeleportSent":"PlayersTeleportStopped")); };menu.Items.Add(item);
            }
            AddPlayerInformationMenu(menu,()=>player,panel,status);
            OverlayTheme.Menu(menu);Action<Control> attach=null;attach=control=> { control.ContextMenuStrip=menu;foreach(Control child in control.Controls)attach(child); };attach(target);
            target.Disposed+=(s,e)=>menu.Dispose();
        }
        void AttachAssetTeleportMenu(ListView list,Form panel,Action<string> status) {
            var menu=new ContextMenuStrip();var teleport=new ToolStripMenuItem();menu.Items.Add(teleport);OverlayTheme.Menu(menu);
            list.MouseDown+=(s,e)=> { if(e.Button!=MouseButtons.Right)return;var hit=list.GetItemAt(e.X,e.Y);foreach(ListViewItem row in list.Items)row.Selected=false;if(hit!=null) { hit.Selected=true;hit.Focused=true; } };
            menu.Opening+=(s,e)=> { var asset=list.SelectedItems.Count==1?list.SelectedItems[0].Tag as AdminAsset:null;e.Cancel=asset==null;teleport.Text=Localization.Get(asset!=null && asset.Kind=="Flag"?"ProfileTeleportFlag":"ProfileTeleportVehicle");teleport.Enabled=!adminCommandBusy && !relativeTeleportBusy && AssetTeleportCommand(asset)!=null; };
            teleport.Click+=async(s,e)=> { if(adminCommandBusy || relativeTeleportBusy || list.SelectedItems.Count!=1)return;string command=AssetTeleportCommand(list.SelectedItems[0].Tag as AdminAsset);if(command==null)return;bool sent=await RunAdminCommand(command,panel);if(!panel.IsDisposed)status(Localization.Get(sent?"PlayersTeleportSent":"PlayersTeleportStopped")); };
            Func<AdminAsset> selected=()=>list.SelectedItems.Count==1?list.SelectedItems[0].Tag as AdminAsset:null;
            menu.Items.Add(new ToolStripSeparator());
            AddAdminCopy(menu,"AdminCopyId",()=>selected()==null?null:selected().Id,status);AddAdminCopy(menu,"AdminCopyName",()=>selected()==null?null:selected().Name,status);AddAdminCopy(menu,"AdminCopyCoordinates",()=>selected()==null?null:ReportCoordinates(selected().Location),status);AddAdminCopy(menu,"AdminCopyOwner",()=>selected()==null?null:selected().OwnerId,status);AddAdminCopy(menu,"AdminCopyDetails",()=>selected()==null?null:AssetInformation(selected()),status);AddAdminCopy(menu,"AdminCopyCommand",()=>selected()==null?null:AssetTeleportCommand(selected()),status);
            OverlayTheme.Menu(menu);
            list.ContextMenuStrip=menu;list.Disposed+=(s,e)=>menu.Dispose();
        }
        Task<SpawnBatchResult> RequestAdminProfileReport(string command,string playerId,Form panel) {
            if(diagnosticMode && profileReportSender!=null)return profileReportSender(command,playerId,panel);
            if(command=="#ListPlayers true")return RunAdminCommands(new List<string> { command },panel,null,false,true);
            if(command=="#ListSpawnedVehicles true")return RunAdminCommands(new List<string> { command },panel,null,false,false,true);
            if(playerId!=null)return RunAdminCommands(new List<string> { command },panel,null,false,false,false,playerId);
            return RunAdminCommands(new List<string> { command },panel,null,false,false,false,null,text=>AdminProfileParser.ValidateBulkResponse(text,command));
        }
        internal async Task<bool> RefreshAdminSquadsAndFlags(string scope,Form panel,Action<string> status,ISet<string> onlineIds=null) {
            status(Localization.Get("ProfileRefreshingAllSquads"));
            const string command="#DumpAllSquadsInfoList true";
            var squads=await RequestAdminProfileReport(command,null,panel);
            if(squads.ClipboardResponse==null) { status(squads.Error??Localization.Get("ProfileReportTimeout"));return false; }
            DateTime squadSeen=DateTime.UtcNow;
            await Task.Run(()=> { AdminProfileParser.ValidateBulkResponse(squads.ClipboardResponse,command);ProfileStore.Import(scope,squads.ClipboardResponse,squadSeen,onlineIds); });
            if(closing || panel.IsDisposed)return false;
            if(!squads.ChatClosed) { if(string.Equals(adminPlayerServer,scope,StringComparison.OrdinalIgnoreCase))await ReloadAdminPlayersAsync(scope);status(squads.Error??Localization.Get("SpawnCartCleanupStopped"));return false; }
            var pages=new List<AdminProfileImport>();int total=1;SpawnBatchResult response=null;
            for(int page=1;page<=total;page++) {
                if(closing || panel.IsDisposed)return false;
                status(Localization.T("ProfileRefreshingFlags",page,total));
                string flagCommand="#ListFlags "+page.ToString(CultureInfo.InvariantCulture)+" true";
                response=await RequestAdminProfileReport(flagCommand,null,panel);
                if(response.ClipboardResponse==null) { status(response.Error??Localization.Get("ProfileReportTimeout"));return false; }
                string flagText=response.ClipboardResponse;DateTime flagSeen=DateTime.UtcNow;
                var report=await Task.Run(()=> { AdminProfileParser.ValidateBulkResponse(flagText,flagCommand);return AdminProfileParser.Parse(flagText,flagSeen); });
                if(closing || panel.IsDisposed)return false;
                if(page==1)total=report.TotalPages;
                else if(report.TotalPages!=total)throw new FormatException(Localization.Get("ProfileInvalid"));
                pages.Add(report);
                if(!response.ChatClosed && page<total) { status(response.Error??Localization.Get("SpawnCartCleanupStopped"));return false; }
            }
            await Task.Run(()=>ProfileStore.ImportFlagPages(scope,pages));
            if(closing || panel.IsDisposed)return false;
            ClearAdminProfileMarkers();
            if(string.Equals(adminPlayerServer,scope,StringComparison.OrdinalIgnoreCase))await ReloadAdminPlayersAsync(scope);
            status(Localization.T("ProfileImportDone",scope)+(response.ChatClosed?"":"\n"+(response.Error??Localization.Get("SpawnCartCleanupStopped"))));return response.ChatClosed;
        }
        internal async Task<bool> RefreshAdminProfileReports(string scope,IEnumerable<AdminPlayer> players,Form panel,Action<string> status,bool includePlayers=false) {
            ISet<string> onlineIds=new HashSet<string>(players.Where(player=>player.InSnapshot).Select(player=>player.Id));
            if(includePlayers) {
                status(Localization.Get("PlayersRefreshing"));var population=await RequestAdminProfileReport("#ListPlayers true",null,panel);
                if(population.ClipboardResponse==null) { status(population.Error??Localization.Get("PlayersNoResponse"));return false; }
                DateTime seen=DateTime.UtcNow;
                await Task.Run(()=>PlayerStore.Import(scope,population.ClipboardResponse,seen));
                if(closing || panel.IsDisposed)return false;
                await ReloadAdminPlayersAsync(scope);
                onlineIds=new HashSet<string>(adminPlayers.Where(player=>player.InSnapshot).Select(player=>player.Id));
                if(!population.ChatClosed) { status(population.Error??Localization.Get("SpawnCartCleanupStopped"));return false; }
            }
            status(Localization.Get("ProfileRefreshingVehicles"));
            var vehicles=await RequestAdminProfileReport("#ListSpawnedVehicles true",null,panel);
            if(vehicles.ClipboardResponse==null) { status(vehicles.Error??Localization.Get("ProfileVehicleTimeout"));return false; }
            DateTime vehicleSeen=DateTime.UtcNow;
            await Task.Run(()=>ProfileStore.Import(scope,vehicles.ClipboardResponse,vehicleSeen));
            if(closing || panel.IsDisposed)return false;
            ClearAdminProfileMarkers();
            if(!vehicles.ChatClosed) { if(string.Equals(adminPlayerServer,scope,StringComparison.OrdinalIgnoreCase))await ReloadAdminPlayersAsync(scope);status(vehicles.Error??Localization.Get("SpawnCartCleanupStopped"));return false; }
            return await RefreshAdminSquadsAndFlags(scope,panel,status,onlineIds);
        }
        internal static void ValidateSquadResponse(string text,string playerId) {
            var report=AdminProfileParser.Parse(text,DateTime.UtcNow);
            if(report.Kind!="Squad" || !report.Members.Any(member=>member.Id==playerId))throw new FormatException(Localization.Get("ProfileInvalid"));
        }
        internal void ShowAdminProfile(AdminPlayer player,string page="Overview") {
            if(!adminToolsEnabled || closing || adminCommandBusy || relativeTeleportBusy)return;
            adminPlayerToolOpening++;
            try { UpdateIntensiveWorkPause();ShowAdminProfileCore(player,page); }
            finally { adminPlayerToolOpening--;UpdateIntensiveWorkPause(); }
        }
        void ShowAdminProfileCore(AdminPlayer player,string page) {
            string key=adminPlayerServer+"\n"+player.Id;
            var form=adminProfileWindows.FirstOrDefault(candidate=>!candidate.IsDisposed && (string)candidate.Tag==key);
            if(form==null) {
                form=CreateAdminProfileForm(player,page);form.Tag=key;form.TopMost=true;
                adminProfileWindows.Add(form);var tracked=form;
                tracked.Disposed+=(s,e)=>adminProfileWindows.Remove(tracked);
                Disposed+=(s,e)=>tracked.Dispose();ProtectToolWindow(tracked);
            }
            var tabs=form.Controls.Find("ProfilePages",true).OfType<TabControl>().First();
            tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().FirstOrDefault(tab=>tab.Name==page)??tabs.TabPages[0];
            // Sending hides the tool temporarily. A modal ShowDialog would end here
            // and its using block would dispose the profile before the async import.
            PrepareUiInput();
            // Owned windows stay above the directory without depending on focus
            // changes or another topmost window's position in the z-order.
            if(adminPlayersPanel!=null && !adminPlayersPanel.IsDisposed)form.Owner=adminPlayersPanel;
            form.Show();form.BringToFront();if(!diagnosticMode) { form.Activate();Native.ForceForeground(form.Handle); }
        }
        internal Form CreateAdminProfileForm(AdminPlayer player,string page="Overview") {
            var uiContext=new WindowsFormsSynchronizationContext();
            string scope=adminPlayerServer;
            var form=new AdminPlayersWindow { SuppressActivation=diagnosticMode,Text=Localization.T("ProfileTitle",player.Name),ClientSize=new Size(980,700),MinimumSize=new Size(780,580),StartPosition=FormStartPosition.CenterScreen,Font=new Font("Segoe UI",9f),ShowInTaskbar=false };
            try {
                    var help=new FlowLayoutPanel { Dock=DockStyle.Top,Height=34,Padding=new Padding(12,0,12,0),WrapContents=false };
                    var steamLink=SteamProfileButton(player.Id,!diagnosticMode);steamLink.Dock=DockStyle.None;steamLink.Width=230;
                    var profileName=new Label { AutoSize=true,Padding=new Padding(8,7,0,0),Text=scope+"  ·  "+player.PlatformName };
                    help.Controls.Add(steamLink);help.Controls.Add(profileName);
                    form.Shown+=async(s,e)=> { if(diagnosticMode)return;string name=await SteamProfiles.Name(player.Id);if(name!=null && !form.IsDisposed)profileName.Text=scope+"  ·  Steam: "+name; };
                    var tabs=new TabControl { Name="ProfilePages",Dock=DockStyle.Fill,DrawMode=TabDrawMode.OwnerDrawFixed,SizeMode=TabSizeMode.Fixed,ItemSize=new Size(130,36) };
                    tabs.DrawItem+=(s,e)=> { bool selected=e.Index==tabs.SelectedIndex;using(var brush=new SolidBrush(selected?OverlayTheme.SurfaceCard:OverlayTheme.Surface))e.Graphics.FillRectangle(brush,e.Bounds);TextRenderer.DrawText(e.Graphics,tabs.TabPages[e.Index].Text,tabs.Font,e.Bounds,selected?OverlayTheme.Accent:OverlayTheme.Ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter); };
                    var overviewPage=new TabPage(Localization.Get("ProfileOverview")) { Name="Overview" };var vehiclePage=new TabPage(Localization.Get("ProfileVehicles")) { Name="Vehicles" };var flagPage=new TabPage(Localization.Get("ProfileFlags")) { Name="Flags" };var squadPage=new TabPage(Localization.Get("ProfileSquad")) { Name="Squad" };
                    tabs.TabPages.AddRange(new[]{overviewPage,vehiclePage,flagPage,squadPage});
                    // Clip the native tab chrome and use the same dark buttons as
                    // the rest of the admin tools for page navigation.
                    var pageHost=new Panel { Dock=DockStyle.Fill,BackColor=OverlayTheme.Background };
                    tabs.Dock=DockStyle.None;pageHost.Controls.Add(tabs);
                    Action fitPages=()=> { int top=tabs.DisplayRectangle.Top;tabs.SetBounds(-4,-top,pageHost.ClientSize.Width+8,pageHost.ClientSize.Height+top+4); };
                    pageHost.SizeChanged+=(s,e)=>fitPages();tabs.HandleCreated+=(s,e)=>fitPages();
                    var navigation=new FlowLayoutPanel { Name="ProfileNavigation",Dock=DockStyle.Top,Height=40,Padding=new Padding(12,0,12,0),WrapContents=false,BackColor=OverlayTheme.Surface };
                    foreach(TabPage pageTab in tabs.TabPages) {
                        var button=new Button { Name="Navigate"+pageTab.Name,Text=pageTab.Text,Width=130,Height=34 };
                        button.Click+=(s,e)=>tabs.SelectedTab=pageTab;navigation.Controls.Add(button);
                    }
                    var overview=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(18) };
                    var identity=new Label { Name="ProfileIdentity",Width=850,Height=155,Font=new Font("Segoe UI",11f),Text=Localization.T("ProfileIdentity",player.Name,player.PlatformName,player.Id,player.Location==null?Localization.Get("ProfileNoPopulation"):Localization.T("ProfilePopulation",player.SeenUtc.ToLocalTime().ToString("g"),Localization.Get(player.InSnapshot?"PlayersSnapshot":"PlayersHistory"),player.Fame,player.Balance,player.Gold)) };
                    var summary=new Label { Name="ProfileSummary",Width=850,Height=76 };
                    identity.Padding=new Padding(14);identity.BackColor=OverlayTheme.SurfaceCard;
                    summary.Padding=new Padding(14);summary.BackColor=OverlayTheme.SurfaceCard;
                    var links=new FlowLayoutPanel { Width=850,Height=45 };
                    foreach(var target in new[]{vehiclePage,flagPage,squadPage}) { var link=new Button { Name="Open"+target.Name,Text=Localization.T("ProfileView",target.Text),AutoSize=true };link.Click+=(s,e)=>tabs.SelectedTab=target;links.Controls.Add(link); }
                    overview.Controls.AddRange(new Control[]{identity,summary,links,new Label { Width=850,Height=100,Text=Localization.Get("ProfileExplanation") }});overviewPage.Controls.Add(overview);
                    var squad=new Label { Name="SquadSummary",Dock=DockStyle.Top,Height=30,Padding=new Padding(16,5,16,0),AutoEllipsis=true,ForeColor=OverlayTheme.Accent };
                    var members=new FlowLayoutPanel { Name="SquadMembers",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(4) };
                    var memberScroll=new SidebarScrollPanel { Dock=DockStyle.Fill,MinimumContentWidth=0,Padding=new Padding(10,0,10,0) };memberScroll.Content.Controls.Add(members);squadPage.Controls.Add(memberScroll);squadPage.Controls.Add(squad);
                    var list=new ListView { Name="ProfileVehicles",Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=true,HideSelection=false };
                    var flags=new ListView { Name="ProfileFlags",Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=true,HideSelection=false };
                    foreach(var column in new[]{Localization.Get("ProfileType"),"ID",Localization.Get("PlayersName"),Localization.Get("ProfileOwnerAlias"),Localization.Get("ProfileCoordinates"),Localization.Get("ProfileImported")})list.Columns.Add(column,160);
                    foreach(ColumnHeader column in list.Columns)flags.Columns.Add(column.Text,column.Width);
                    vehiclePage.Controls.Add(list);flagPage.Controls.Add(flags);
                    var actions=new FlowLayoutPanel { Dock=DockStyle.Bottom,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(8),AutoScroll=true };
                    var import=new Button { Name="ImportProfile",Text=Localization.Get("PlayersImport"),AutoSize=true };var all=new CheckBox { Name="AllServerAssets",Text=Localization.Get("ProfileAllAssets"),AutoSize=true };
                    var map=new Button { Name="ShowProfileAssets",Text=Localization.Get("ProfileShowSelected"),AutoSize=true };var clear=new Button { Name="ClearProfileAssets",Text=Localization.Get("ProfileClearMarkers"),AutoSize=true };
                    var refreshVehicles=new Button { Name="RefreshProfileVehicles",Text=Localization.Get("ProfileRefreshVehicles"),AutoSize=true };
                    var refreshSquad=new Button { Name="RefreshProfileSquad",Text=Localization.Get("ProfileRefreshSquad"),AutoSize=true };
                    var refreshAll=new Button { Name="RefreshProfileAll",Text=Localization.Get("ProfileRefreshAll"),AutoSize=true };
                    bool busy=false;
                    actions.Controls.AddRange(new Control[]{refreshAll,refreshVehicles,refreshSquad,import,all,map,clear});
                    var status=new Label { Dock=DockStyle.Bottom,Height=40,Padding=new Padding(14,8,14,0),Text="" };
                    var utilities=new ContextMenuStrip();
                    utilities.Opening+=(s,e)=>e.Cancel=busy || adminCommandBusy;
                    utilities.Items.Add(Localization.Get("ProfileCopySquad"),null,(s,e)=> { try { Clipboard.SetText("#DumpAllSquadsInfoList true");status.Text=Localization.Get("ProfileCopySquadDone"); }catch(Exception error) { status.Text=error.Message; } });
                    utilities.Items.Add(Localization.Get("ProfileCopyVehicles"),null,(s,e)=> { try { Clipboard.SetText("#ListSpawnedVehicles true");status.Text=Localization.Get("ProfileCopyVehiclesDone"); }catch(Exception error) { status.Text=error.Message; } });
                    utilities.Items.Add(Localization.Get("AdminCopyCommand")+" · #ListFlags",null,(s,e)=> { try { Clipboard.SetText("#ListFlags 1 true");status.Text=Localization.Get("AdminCopied"); }catch(Exception error) { status.Text=error.Message; } });
                    OverlayTheme.Menu(utilities);import.ContextMenuStrip=utilities;form.Disposed+=(s,e)=>utilities.Dispose();
                    Action pageChanged=()=> {
                        foreach(Button button in navigation.Controls) { bool selected=button.Name=="Navigate"+tabs.SelectedTab.Name;button.BackColor=selected?OverlayTheme.SurfaceCard:OverlayTheme.Surface;button.ForeColor=selected?OverlayTheme.Accent:OverlayTheme.Ink; }
                        bool assetsPage=tabs.SelectedTab==vehiclePage || tabs.SelectedTab==flagPage;
                        all.Visible=map.Visible=assetsPage;clear.Visible=assetsPage;
                        refreshVehicles.Visible=tabs.SelectedTab==overviewPage || tabs.SelectedTab==vehiclePage;
                        refreshSquad.Visible=tabs.SelectedTab!=vehiclePage;
                    };tabs.SelectedIndexChanged+=(s,e)=>pageChanged();
                    AttachPlayerTeleportMenu(identity,player,form,text=>status.Text=text);
                    AttachAssetTeleportMenu(list,form,text=>status.Text=text);AttachAssetTeleportMenu(flags,form,text=>status.Text=text);
                    int profileRenderRevision=0;
                    ProfileViewSnapshot displayedSnapshot=null;
                    bool displayedAll=false;
                    Func<Task> renderProfile=async()=> {
                        System.Threading.SynchronizationContext.SetSynchronizationContext(uiContext);
                        int revision=++profileRenderRevision;
                        var snapshot=await LoadProfileView(scope,player.Id);
                        if(closing || form.IsDisposed || !form.IsHandleCreated)return;
                        Action applySnapshot=()=> {
                        if(closing || form.IsDisposed || form.Disposing || revision!=profileRenderRevision)return;
                        if(ReferenceEquals(displayedSnapshot,snapshot) && displayedAll==all.Checked)return;
                        var latest=snapshot.Player;
                        if(latest!=null) { player.Name=latest.Name;player.PlatformName=latest.PlatformName;player.Fame=latest.Fame;player.Balance=latest.Balance;player.Gold=latest.Gold;player.Location=latest.Location;player.SeenUtc=latest.SeenUtc;player.InSnapshot=latest.InSnapshot; }
                        form.Text=Localization.T("ProfileTitle",player.Name);
                        identity.Text=Localization.T("ProfileIdentity",player.Name,player.PlatformName,player.Id,player.Location==null?Localization.Get("ProfileNoPopulation"):Localization.T("ProfilePopulation",player.SeenUtc.ToLocalTime().ToString("g"),Localization.Get(player.InSnapshot?"PlayersSnapshot":"PlayersHistory"),player.Fame,player.Balance,player.Gold));
                        var squadText=snapshot.Squad;
                        var assets=snapshot.Assets;var own=assets.Where(asset=>asset.OwnerId==player.Id).ToList();
                        var squadMembers=snapshot.Members;
                        var squadIds=new HashSet<string>(squadMembers.Select(member=>member.Id));
                        list.BeginUpdate();flags.BeginUpdate();members.SuspendLayout();
                        try {
                        squad.Text=squadText;list.Items.Clear();flags.Items.Clear();
                        summary.Text=Localization.T("ProfileSummary",own.Count(asset=>asset.Kind=="Vehicle"),own.Count(asset=>asset.Kind=="Flag"),squad.Text.Split(new[]{Environment.NewLine},StringSplitOptions.None)[0]);
                        foreach(Control child in members.Controls.Cast<Control>().ToArray())child.Dispose();members.Controls.Clear();
                        foreach(var member in squadMembers) {
                            var tile=new Panel { Width=285,Height=142,Margin=new Padding(4),Padding=new Padding(8),BackColor=Color.FromArgb(38,43,49) };
                            var label=new Label { Dock=DockStyle.Fill,Text=Localization.T("ProfileMemberDetails",member.Name,member.PlatformName,member.Rank,Localization.Get(member.Status=="Online"?"ProfileOnline":member.Status=="Offline"?"ProfileOffline":"ProfileUnknownStatus")) };
                            var memberSteam=SteamProfileButton(member.Id,!diagnosticMode);
                            var open=new Button { Name="SquadProfile_"+member.Id,Dock=DockStyle.Bottom,Height=30,Text=Localization.Get("ProfileOpenPlayer") };
                            open.Click+=(s,e)=> { try { var known=adminPlayers.FirstOrDefault(item=>item.Id==member.Id)??new AdminPlayer { Id=member.Id,Name=member.Name,PlatformName=member.PlatformName };ShowAdminProfile(known); }catch(Exception error) { status.Text=error.Message; } };
                            tile.Controls.Add(label);tile.Controls.Add(memberSteam);tile.Controls.Add(open);
                            AttachPlayerTeleportMenu(tile,new AdminPlayer { Id=member.Id,Name=member.Name,PlatformName=member.PlatformName },form,text=>status.Text=text);OverlayTheme.Style(tile);tile.BackColor=label.BackColor=OverlayTheme.SurfaceCard;members.Controls.Add(tile);
                        }
                        foreach(var asset in assets.Where(asset=>all.Checked || asset.OwnerId==player.Id || (asset.Kind=="Flag" && squadIds.Contains(asset.OwnerId)))) {
                            string owner=asset.OwnerId.Length>0?asset.OwnerId:asset.OwnerDatabaseId.Length>0?Localization.T("ProfileUnresolvedOwner",asset.OwnerDatabaseId):Localization.Get("ProfileNoOwner");
                            if(asset.AliasId.Length>0)owner=Localization.T("ProfileAlias",owner,asset.AliasId);
                            var row=new ListViewItem(new[]{Localization.Get(asset.Kind=="Flag"?"ProfileFlag":"ProfileVehicle"),asset.Id,asset.Name,owner,string.Format(CultureInfo.InvariantCulture,"X={0} Y={1} Z={2}",asset.Location.X,asset.Location.Y,asset.Location.Z),asset.SeenUtc.ToLocalTime().ToString("g")}) { Tag=asset };(asset.Kind=="Flag"?flags:list).Items.Add(row);
                        }
                        } finally { list.EndUpdate();flags.EndUpdate();members.ResumeLayout(true);members.Invalidate(true); }
                        displayedSnapshot=snapshot;displayedAll=all.Checked;
                        };
                        if(form.InvokeRequired)form.Invoke(applySnapshot);else applySnapshot();
                    };
                    Action render=async()=> { System.Threading.SynchronizationContext.SetSynchronizationContext(uiContext);try { await renderProfile(); }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; } };
                    Func<bool,Task> refreshProfile=async squadReport=> {
                        System.Threading.SynchronizationContext.SetSynchronizationContext(uiContext);
                        if(busy)return;busy=true;
                        foreach(Control action in actions.Controls)action.Enabled=false;
                        status.Text=Localization.Get(squadReport?"ProfileRefreshingSquad":"ProfileRefreshingVehicles");
                        try {
                            if(squadReport) { await RefreshAdminSquadsAndFlags(scope,form,text=> { if(!form.IsDisposed)status.Text=text; });if(!form.IsDisposed)await renderProfile();return; }
                            var response=await RequestAdminProfileReport("#ListSpawnedVehicles true",null,form);
                            if(form.IsDisposed)return;
                            if(response.ClipboardResponse==null) { status.Text=response.Error??Localization.Get(squadReport?"ProfileSquadTimeout":"ProfileVehicleTimeout");return; }
                            DateTime seen=DateTime.UtcNow;
                            await Task.Run(()=>ProfileStore.Import(scope,response.ClipboardResponse,seen));
                            if(closing || form.IsDisposed)return;
                            if(string.Equals(adminPlayerServer,scope,StringComparison.OrdinalIgnoreCase))await ReloadAdminPlayersAsync(scope);ClearAdminProfileMarkers();await renderProfile();if(form.IsDisposed)return;
                            status.Text=Localization.T("ProfileImportDone",scope)+(response.ChatClosed?"":"\n"+(response.Error??Localization.Get("SpawnCartCleanupStopped")));
                        }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                        finally { busy=false;if(!form.IsDisposed) { foreach(Control action in actions.Controls)action.Enabled=true;if(string.Equals(adminPlayerServer,scope,StringComparison.OrdinalIgnoreCase))RestoreAdminTool(form,Native.AdminGameWindow()); } }
                    };
                    refreshAll.Click+=async(s,e)=> {
                        System.Threading.SynchronizationContext.SetSynchronizationContext(uiContext);
                        if(busy)return;busy=true;foreach(Control action in actions.Controls)action.Enabled=false;
                        try { await RefreshAdminProfileReports(scope,new[]{player},form,text=> { if(!form.IsDisposed)status.Text=text; },true);if(!form.IsDisposed)await renderProfile(); }
                        catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                        finally { busy=false;if(!form.IsDisposed) { foreach(Control action in actions.Controls)action.Enabled=true;if(string.Equals(adminPlayerServer,scope,StringComparison.OrdinalIgnoreCase))RestoreAdminTool(form,Native.AdminGameWindow()); } }
                    };
                    refreshVehicles.Click+=async(s,e)=>await refreshProfile(false);
                    refreshSquad.Click+=async(s,e)=>await refreshProfile(true);
                    form.FormClosing+=(s,e)=> { if(busy && !closing)e.Cancel=true; };
                    import.Click+=async(s,e)=> {
                        System.Threading.SynchronizationContext.SetSynchronizationContext(uiContext);
                        if(busy)return;busy=true;foreach(Control action in actions.Controls)action.Enabled=false;
                        try {
                            string text=Clipboard.GetText();
                            string message=await Task.Run(()=>ImportAdminClipboard(scope,text));
                            if(closing || form.IsDisposed)return;
                            if(string.Equals(adminPlayerServer,scope,StringComparison.OrdinalIgnoreCase))await ReloadAdminPlayersAsync(scope);
                            if(form.IsDisposed)return;
                            status.Text=message;await renderProfile();
                        }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                        finally { busy=false;if(!form.IsDisposed)foreach(Control action in actions.Controls)action.Enabled=true; }
                    };
                    all.CheckedChanged+=(s,e)=> { try { render(); }catch(Exception error) { status.Text=error.Message; } };
                    map.Click+=(s,e)=> {
                        var selected=tabs.SelectedTab==flagPage?flags:list;
                        if(selected.SelectedItems.Count==0 || (tabs.SelectedTab!=flagPage && tabs.SelectedTab!=vehiclePage)) { status.Text=Localization.Get("ProfileSelectLocation");return; }
                        adminProfileMarkers=selected.SelectedItems.Cast<ListViewItem>().Select(row=>(AdminAsset)row.Tag).ToList().AsReadOnly();adminPlayerRevision++;canvas.Invalidate();overlay.Invalidate();form.Close();
                        foreach(var profile in adminProfileWindows.ToArray())if(!profile.IsDisposed)profile.Hide();
                        if(adminPlayersPanel!=null && !adminPlayersPanel.IsDisposed)adminPlayersPanel.Hide();
                    };
                    clear.Click+=(s,e)=> { ClearAdminProfileMarkers();status.Text=Localization.Get("ProfileMarkersCleared"); };
                    form.VisibleChanged+=(s,e)=> { if(form.Visible && !busy) { try { render(); }catch(Exception error) { status.Text=error.Message; } } };
                    form.Controls.Add(pageHost);form.Controls.Add(navigation);form.Controls.Add(help);form.Controls.Add(actions);form.Controls.Add(status);
                    OverlayTheme.Frame(form,form.Text,()=>form.Close(),44);
                    foreach(TabPage tab in tabs.TabPages)tab.BackColor=OverlayTheme.Background;
                    StyleProfileList(list);StyleProfileList(flags);
                    identity.BackColor=summary.BackColor=OverlayTheme.SurfaceCard;identity.ForeColor=OverlayTheme.Ink;summary.ForeColor=OverlayTheme.Accent;
                    tabs.SelectedTab=tabs.TabPages.Cast<TabPage>().FirstOrDefault(tab=>tab.Name==page)??overviewPage;pageChanged();return form;
            }catch { form.Dispose();throw; }
        }
        void DrawAdminProfileAssets(Graphics graphics,Rectangle bounds,float left,float top,float side) {
            using(var font=new Font("Segoe UI",8f,FontStyle.Bold))foreach(var asset in adminProfileMarkers) {
                PointF point=ToMap(asset.Location);float x=left+point.X*side,y=top+point.Y*side;if(!bounds.Contains((int)x,(int)y))continue;
                graphics.FillRectangle(Brushes.Black,x-6,y-6,12,12);graphics.FillRectangle(asset.Kind=="Flag"?Brushes.Gold:Brushes.Orange,x-4,y-4,8,8);
                string label=Localization.T("ProfileAssetMarker",Localization.Get(asset.Kind=="Flag"?"ProfileFlag":"ProfileVehicle"),asset.Id,asset.Name,asset.SeenUtc.ToLocalTime().ToString("g"));
                SizeF size=graphics.MeasureString(label,font);graphics.FillRectangle(Brushes.Black,x+7,y-9,size.Width+4,size.Height+2);graphics.DrawString(label,font,Brushes.White,x+9,y-8);
            }
        }
    }
}
