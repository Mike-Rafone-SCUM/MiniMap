using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SynchronizationContext=System.Threading.SynchronizationContext;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal static class PlayerDirectoryQuery {
        internal static int Count(IDictionary<string,int> counts,string id) { int value;return counts.TryGetValue(id,out value)?value:0; }
        internal static IEnumerable<AdminPlayer> Apply(IEnumerable<AdminPlayer> players,string search,int sort,
            IDictionary<string,int> squads,IDictionary<string,int> flags,IDictionary<string,int> vehicles) {
            var filtered=players.Where(player=>(player.Name??"").IndexOf(search,StringComparison.CurrentCultureIgnoreCase)>=0 || (player.PlatformName??"").IndexOf(search,StringComparison.CurrentCultureIgnoreCase)>=0 || player.Id.Contains(search));
            if(sort<2)return (sort==1?filtered.OrderByDescending(player=>player.Name,StringComparer.CurrentCultureIgnoreCase):filtered.OrderBy(player=>player.Name,StringComparer.CurrentCultureIgnoreCase)).ThenBy(player=>player.Id,StringComparer.Ordinal);
            var sortCounts=sort<4?squads:sort<6?flags:vehicles;
            return (sort%2==0?filtered.OrderByDescending(player=>Count(sortCounts,player.Id)):filtered.OrderBy(player=>Count(sortCounts,player.Id))).ThenBy(player=>player.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(player=>player.Id,StringComparer.Ordinal);
        }
    }
    internal sealed class AdminPlayer {
        internal string Id,Name,PlatformName;
        internal long Fame,Balance,Gold;
        internal Position Location;
        internal DateTime SeenUtc;
        internal bool InSnapshot;
        internal string TeleportTo { get { return PlayerList.ValidId(Id)?"#TeleportTo "+Id:null; } }
        internal string TeleportHere { get { return PlayerList.ValidId(Id)?"#TeleportToMe "+Id:null; } }
    }

    internal static class PlayerList {
        internal const int MaximumLength=1048576,MaximumPlayers=1000;
        static readonly Regex Headers=new Regex(@"(?m)^\s*(?<number>\d{1,4})\. (?<name>[^\r\n]+)\r?$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        static readonly Regex Coordinates=new Regex(@"^X=(?<x>-?\d+(?:\.\d+)?) Y=(?<y>-?\d+(?:\.\d+)?) Z=(?<z>-?\d+(?:\.\d+)?)$",RegexOptions.CultureInvariant,TimeSpan.FromSeconds(1));
        internal static bool ValidId(string id) {
            return id!=null && id.Length==17 && id.All(character=>character>='0' && character<='9');
        }
        static string Name(string value) {
            value=value.Trim();
            if(value.Length==0 || value.Length>256 || value.Any(char.IsControl))throw new FormatException(Localization.Get("PlayersInvalid"));
            return value;
        }
        static double Coordinate(Match match,string field) {
            double value;
            if(!double.TryParse(match.Groups[field].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out value)
                || double.IsNaN(value) || double.IsInfinity(value) || Math.Abs(value)>2000000)
                throw new FormatException(Localization.Get("PlayersInvalid"));
            return value;
        }
        internal static IList<AdminPlayer> Parse(string text,DateTime seenUtc) {
            if(string.IsNullOrWhiteSpace(text) || text.Length>MaximumLength)throw new FormatException(Localization.Get("PlayersInvalid"));
            text=text.Replace("\r\n","\n");
            MatchCollection headers=Headers.Matches(text);
            if(headers.Count==0 || headers.Count>MaximumPlayers)throw new FormatException(Localization.Get("PlayersInvalid"));
            string prefix=text.Substring(0,headers[0].Index).Trim();
            DateTime stamp;
            if(prefix.Length>0 && !DateTime.TryParseExact(prefix,"[HH:mm:ss]",CultureInfo.InvariantCulture,DateTimeStyles.None,out stamp))
                throw new FormatException(Localization.Get("PlayersInvalid"));
            var result=new List<AdminPlayer>();var ids=new HashSet<string>(StringComparer.Ordinal);
            for(int index=0;index<headers.Count;index++) {
                Match header=headers[index];
                if(int.Parse(header.Groups["number"].Value,CultureInfo.InvariantCulture)!=index+1)throw new FormatException(Localization.Get("PlayersInvalid"));
                int end=index+1<headers.Count?headers[index+1].Index:text.Length;
                string block=text.Substring(header.Index+header.Length,end-header.Index-header.Length);
                var fields=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(string raw in block.Split('\n')) {
                    string line=raw.Trim();if(line.Length==0)continue;
                    int colon=line.IndexOf(": ");
                    if(colon<1 || fields.ContainsKey(line.Substring(0,colon)))throw new FormatException(Localization.Get("PlayersInvalid"));
                    fields.Add(line.Substring(0,colon),line.Substring(colon+2));
                }
                string id,platform,location,fame,balance,gold;
                if(fields.Count!=6 || !fields.TryGetValue("Platform ID",out id) || !ValidId(id) || !ids.Add(id)
                    || !fields.TryGetValue("Platform Name",out platform) || !fields.TryGetValue("Location",out location)
                    || !fields.TryGetValue("Fame",out fame) || !fields.TryGetValue("Account balance",out balance) || !fields.TryGetValue("Gold balance",out gold))
                    throw new FormatException(Localization.Get("PlayersInvalid"));
                Match coordinates=Coordinates.Match(location);long fameValue,balanceValue,goldValue;
                if(!coordinates.Success || !long.TryParse(fame,NumberStyles.Integer,CultureInfo.InvariantCulture,out fameValue)
                    || !long.TryParse(balance,NumberStyles.Integer,CultureInfo.InvariantCulture,out balanceValue)
                    || !long.TryParse(gold,NumberStyles.Integer,CultureInfo.InvariantCulture,out goldValue))
                    throw new FormatException(Localization.Get("PlayersInvalid"));
                result.Add(new AdminPlayer { Id=id,Name=Name(header.Groups["name"].Value),PlatformName=Name(platform),
                    Fame=fameValue,Balance=balanceValue,Gold=goldValue,SeenUtc=seenUtc.ToUniversalTime(),InSnapshot=true,
                    Location=new Position { X=Coordinate(coordinates,"x"),Y=Coordinate(coordinates,"y"),Z=Coordinate(coordinates,"z") } });
            }
            return result.AsReadOnly();
        }
    }

    // This is a local directory of copied player information. Balance values are
    // read-only snapshots; this database never writes to a SCUM server database.
    internal sealed class PlayerDatabase {
        readonly string path;
        internal PlayerDatabase(string path) { this.path=Path.GetFullPath(path); }
        internal static string ServerName(string name) {
            name=(name??"").Trim().Normalize();
            if(name.Length==0 || name.Length>80 || name.Any(char.IsControl))throw new FormatException(Localization.Get("PlayersChooseServer"));
            return name;
        }
        LocalSqliteDatabase Open() {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            return new LocalSqliteDatabase(path,database=> {
                long version=database.ScalarLong("PRAGMA user_version");
                if(version!=0 && version!=1)throw new IOException(Localization.Get("PlayersDatabaseVersion"));
                if(version==0)database.Transaction(()=> {
                    database.Execute("CREATE TABLE player_servers(name_key TEXT PRIMARY KEY,name TEXT NOT NULL)");
                    database.Execute("CREATE TABLE player_meta(key TEXT PRIMARY KEY,value TEXT NOT NULL)");
                    database.Execute("CREATE TABLE players(server_key TEXT NOT NULL REFERENCES player_servers(name_key),platform_id TEXT NOT NULL,name TEXT NOT NULL,platform_name TEXT NOT NULL,x TEXT NOT NULL,y TEXT NOT NULL,z TEXT NOT NULL,fame INTEGER NOT NULL,balance INTEGER NOT NULL,gold INTEGER NOT NULL,seen_utc TEXT NOT NULL,in_snapshot INTEGER NOT NULL CHECK(in_snapshot IN(0,1)),PRIMARY KEY(server_key,platform_id))");
                    database.Execute("PRAGMA user_version=1");
                });
            });
        }
        internal IList<string> Servers() {
            var result=new List<string>();using(var db=Open())using(var query=db.Prepare("SELECT name FROM player_servers ORDER BY name_key"))
                while(query.Read())result.Add(query.Text(0));return result.AsReadOnly();
        }
        internal string ActiveServer() {
            using(var db=Open())using(var query=db.Prepare("SELECT value FROM player_meta WHERE key='active_server'"))
                return query.Read()?query.Text(0):"";
        }
        internal void SelectServer(string server) {
            server=ServerName(server);using(var db=Open())db.Transaction(()=> {
                db.Execute("INSERT OR IGNORE INTO player_servers(name_key,name) VALUES(?,?)",server.ToUpperInvariant(),server);
                db.Execute("INSERT OR REPLACE INTO player_meta(key,value) VALUES('active_server',?)",server);
            });
        }
        internal void Import(string server,string text,DateTime seenUtc) {
            server=ServerName(server);IList<AdminPlayer> parsed=PlayerList.Parse(text,seenUtc);string key=server.ToUpperInvariant();
            using(var db=Open())db.Transaction(()=> {
                db.Execute("INSERT OR IGNORE INTO player_servers(name_key,name) VALUES(?,?)",key,server);
                db.Execute("UPDATE players SET in_snapshot=0 WHERE server_key=?",key);
                foreach(var player in parsed)db.Execute("INSERT OR REPLACE INTO players(server_key,platform_id,name,platform_name,x,y,z,fame,balance,gold,seen_utc,in_snapshot) VALUES(?,?,?,?,?,?,?,?,?,?,?,1)",
                    key,player.Id,player.Name,player.PlatformName,player.Location.X.ToString("R",CultureInfo.InvariantCulture),player.Location.Y.ToString("R",CultureInfo.InvariantCulture),player.Location.Z.ToString("R",CultureInfo.InvariantCulture),
                    player.Fame,player.Balance,player.Gold,player.SeenUtc.ToString("o",CultureInfo.InvariantCulture));
            });
        }
        internal IList<AdminPlayer> Load(string server) {
            server=ServerName(server);var result=new List<AdminPlayer>();
            using(var db=Open())using(var query=db.Prepare("SELECT platform_id,name,platform_name,x,y,z,fame,balance,gold,seen_utc,in_snapshot FROM players WHERE server_key=? ORDER BY in_snapshot DESC,name COLLATE NOCASE,platform_id",server.ToUpperInvariant())) {
                while(query.Read()) {
                    DateTime seen;double x,y,z;string id=query.Text(0);
                    if(!PlayerList.ValidId(id) || !double.TryParse(query.Text(3),NumberStyles.Float,CultureInfo.InvariantCulture,out x)
                        || !double.TryParse(query.Text(4),NumberStyles.Float,CultureInfo.InvariantCulture,out y) || !double.TryParse(query.Text(5),NumberStyles.Float,CultureInfo.InvariantCulture,out z)
                        || !DateTime.TryParseExact(query.Text(9),"o",CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out seen)
                        || double.IsNaN(x) || double.IsNaN(y) || double.IsNaN(z) || Math.Abs(x)>2000000 || Math.Abs(y)>2000000 || Math.Abs(z)>2000000)
                        throw new FormatException(Localization.Get("PlayersInvalid"));
                    result.Add(new AdminPlayer { Id=id,Name=query.Text(1),PlatformName=query.Text(2),Location=new Position { X=x,Y=y,Z=z },
                        Fame=query.Long(6),Balance=query.Long(7),Gold=query.Long(8),SeenUtc=seen,InSnapshot=query.Long(10)==1 });
                }
            }
            return result.AsReadOnly();
        }
    }

    internal sealed class PlayerClipboardResponse {
        internal string Text;
        internal uint Sequence;
    }
    static partial class Native {
        // Once Enter was dispatched, importing a report is read-only. Escape
        // cleanup and physical-input cancellation must not discard that reply.
        internal static Task<PlayerClipboardResponse> WaitForSubmittedAdminReport(SpawnBatchResult dispatch,uint commandSequence,
            Func<uint> sequence,Func<bool> gameOwnsClipboard,Func<string> read,Func<bool> allowed,
            Func<int,Task> delay,Action<string> validate=null,int timeoutMilliseconds=3000) {
            if(dispatch.SubmittedCount!=1)return Task.FromResult<PlayerClipboardResponse>(null);
            return WaitForPlayerClipboard(commandSequence,sequence,gameOwnsClipboard,read,allowed,delay,validate,timeoutMilliseconds);
        }
        internal static bool GameOwnsClipboard(IntPtr game) {
            uint gamePid,ownerPid;IntPtr owner=GetClipboardOwner();
            if(game==IntPtr.Zero || owner==IntPtr.Zero)return false;
            GetWindowThreadProcessId(game,out gamePid);GetWindowThreadProcessId(owner,out ownerPid);
            return gamePid!=0 && ownerPid==gamePid;
        }
        internal static async Task<PlayerClipboardResponse> WaitForPlayerClipboard(uint commandSequence,Func<uint> sequence,
            Func<bool> gameOwnsClipboard,Func<string> read,Func<bool> allowed,Func<int,Task> delay,Action<string> validate=null,int timeoutMilliseconds=3000) {
            if(timeoutMilliseconds<0 || timeoutMilliseconds>15000)throw new ArgumentOutOfRangeException("timeoutMilliseconds");
            for(int waited=0;waited<=timeoutMilliseconds;waited+=100) {
                if(!allowed())return null;
                uint before=sequence();
                if(before!=0 && before!=commandSequence && gameOwnsClipboard()) {
                    try {
                        string text=read();
                        if(sequence()==before && gameOwnsClipboard() && allowed()) {
                            await Task.Run(()=> { if(validate==null)PlayerList.Parse(text,DateTime.UtcNow);else validate(text); });
                            if(sequence()==before && gameOwnsClipboard() && allowed())
                                return new PlayerClipboardResponse { Text=text,Sequence=before };
                        }
                    }catch(FormatException){}catch(System.Runtime.InteropServices.ExternalException){}
                }
                if(waited<timeoutMilliseconds)await delay(Math.Min(100,timeoutMilliseconds-waited));
            }
            return null;
        }
    }

    internal sealed class AdminPlayersWindow:Form {
        internal AdminPlayersWindow() { DoubleBuffered=true; }
        internal bool SuppressActivation;
        protected override bool ShowWithoutActivation { get { return SuppressActivation; } }
    }
    public partial class MapWindow {
        Form adminPlayersPanel;
        string adminPlayerServer="";
        IList<AdminPlayer> adminPlayers=new List<AdminPlayer>().AsReadOnly();
        bool showAdminPlayers;
        bool playerToolsAwaitingReopen;
        bool IsPlayerTool(Form tool) {
            return tool!=null && (Object.ReferenceEquals(tool,adminPlayersPanel) || adminProfileWindows.Contains(tool));
        }
        internal void FinishPlayerTeleport(Form panel,IList<string> commands,int submitted) {
            if(submitted<=0 || !IsPlayerTool(panel) || !commands.Any(command=>command.StartsWith("#Teleport",StringComparison.OrdinalIgnoreCase)))return;
            playerToolsAwaitingReopen=true;
            foreach(var profile in adminProfileWindows)if(!profile.IsDisposed)profile.Hide();
            if(adminPlayersPanel!=null && !adminPlayersPanel.IsDisposed)adminPlayersPanel.Hide();
        }
        long adminPlayerRevision;
        PlayerDatabase PlayerStore { get { return new PlayerDatabase(Path.Combine(dataFolder,"admin-players.db")); } }
        internal void ReloadAdminPlayers(string server) {
            ApplyAdminPlayers(server,LoadAdminPlayers(server));
        }
        IList<AdminPlayer> LoadAdminPlayers(string server) {
            var loaded=PlayerStore.Load(server);var ids=new HashSet<string>(loaded.Select(player=>player.Id));
            return loaded.Concat(ProfileStore.Players(server).Where(member=>!ids.Contains(member.Id))).ToList().AsReadOnly();
        }
        async Task ReloadAdminPlayersAsync(string server) {
            var loaded=await Task.Run(()=>LoadAdminServerSnapshot(server).Players);
            if(!closing && !IsDisposed)ApplyAdminPlayers(server,loaded);
        }
        void ApplyAdminPlayers(string server,IList<AdminPlayer> loaded) {
            if(!string.Equals(adminPlayerServer,server,StringComparison.OrdinalIgnoreCase)) {
                foreach(var profile in adminProfileWindows.ToArray())if(!profile.IsDisposed)profile.Hide();ClearAdminProfileMarkers();
            }
            adminPlayerServer=server;adminPlayers=loaded;adminPlayerRevision++;
            canvas.Invalidate();overlay.Invalidate();
        }
        internal IList<AdminPlayer> AdminPlayerMarkers(DateTime now) {
            return (showAdminPlayers?adminPlayers.Where(player=>player.InSnapshot && (now-player.SeenUtc).TotalSeconds>=0 && (now-player.SeenUtc).TotalMinutes<=5):Enumerable.Empty<AdminPlayer>()).ToList().AsReadOnly();
        }
        long AdminPlayerFrame() {
            DateTime now=DateTime.UtcNow;
            return showAdminPlayers && adminPlayers.Any(player=>player.InSnapshot && now>=player.SeenUtc && (now-player.SeenUtc).TotalMinutes<=5)
                ?now.Ticks/TimeSpan.TicksPerSecond:0;
        }
        void DrawAdminPlayers(Graphics graphics,Rectangle bounds,float left,float top,float side) {
            var state=graphics.Save();
            try {
                graphics.SetClip(bounds,CombineMode.Intersect);
                DrawAdminProfileAssets(graphics,bounds,left,top,side);
                using(var font=new Font("Segoe UI",8f,FontStyle.Bold))foreach(var player in AdminPlayerMarkers(DateTime.UtcNow)) {
                    PointF point=ToMap(player.Location);float x=left+point.X*side,y=top+point.Y*side;
                    if(!bounds.Contains((int)x,(int)y))continue;
                    bool stale=(DateTime.UtcNow-player.SeenUtc).TotalSeconds>60;
                    using(var brush=new SolidBrush(stale?Color.Gray:Color.DeepSkyBlue)) {
                        graphics.FillEllipse(Brushes.Black,x-6,y-6,12,12);graphics.FillEllipse(brush,x-4,y-4,8,8);
                        string label=player.Name+" · "+Localization.T("HistorySecondsAgo",Math.Max(0,(int)(DateTime.UtcNow-player.SeenUtc).TotalSeconds));
                        SizeF size=graphics.MeasureString(label,font);graphics.FillRectangle(Brushes.Black,x+7,y-9,size.Width+4,size.Height+2);
                        graphics.DrawString(label,font,brush,x+9,y-8);
                    }
                }
            } finally { graphics.Restore(state); }
        }
        bool playerDirectoryOpenQueued;
        internal void RequestAdminPlayers(Form source) {
            if(playerDirectoryOpenQueued || !adminToolsEnabled || closing || IsDisposed || adminCommandBusy || relativeTeleportBusy)return;
            playerDirectoryOpenQueued=true;
            try {
                // Finish the palette's native mouse-up before opening a tool
                // whose visibility handler hides that same palette.
                IntPtr handle=Handle;
                BeginInvoke(new Action(()=> {
                    try {
                        if(closing || IsDisposed || (source!=null && source.IsDisposed))return;
                        ShowAdminPlayers();
                    }catch(Exception error) {
                        Program.LogException("OpenAdminPlayers",error);
                        if(!closing && !IsDisposed)note=error.Message;
                    }finally { playerDirectoryOpenQueued=false; }
                }));
            }catch(InvalidOperationException) { playerDirectoryOpenQueued=false; }
        }
        internal void ShowAdminPlayers() {
            if(!adminToolsEnabled || closing || adminCommandBusy || relativeTeleportBusy)return;
            playerToolsAwaitingReopen=false;
            adminPlayerToolOpening++;
            try { UpdateIntensiveWorkPause();ShowAdminPlayersCore(); }
            finally { adminPlayerToolOpening--;UpdateIntensiveWorkPause(); }
        }
        void ShowAdminPlayersCore() {
            var uiContext=new WindowsFormsSynchronizationContext();
            PrepareUiInput();
            if(adminPlayersPanel!=null && !adminPlayersPanel.IsDisposed) { adminPlayersPanel.Show();if(!diagnosticMode) { adminPlayersPanel.Activate();Native.ForceForeground(adminPlayersPanel.Handle); }return; }
            var form=new AdminPlayersWindow { SuppressActivation=diagnosticMode,Text=Localization.Get("PlayersTitle"),ClientSize=new Size(970,590),MinimumSize=new Size(800,500),StartPosition=FormStartPosition.CenterScreen,ShowInTaskbar=false,Font=new Font("Segoe UI",9f) };
            adminPlayersPanel=form;Disposed+=(s,e)=>form.Dispose();
            ProtectToolWindow(form);
            var toolbar=new FlowLayoutPanel { Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(10,4,10,4),WrapContents=true };
            var server=new ComboBox { Name="PlayerServer",Width=205,MaxLength=80,DropDownStyle=ComboBoxStyle.DropDown };
            var choose=new Button { Name="ChoosePlayerServer",Width=125,Text=Localization.Get("PlayersServer") };
            var import=new Button { Name="ImportPlayers",Width=155,Text=Localization.Get("PlayersImport") };
            var refresh=new Button { Name="RefreshPlayers",Width=155,Text=Localization.Get("PlayersRefresh") };
            var show=new CheckBox { Name="ShowPlayerMarkers",Width=230,Text=Localization.Get("PlayersShow"),Checked=showAdminPlayers };
            toolbar.Controls.AddRange(new Control[]{server,choose,import,refresh,show});
            var search=new TextBox { Name="SearchPlayers",Dock=DockStyle.Fill,MaxLength=256,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",11f) };
            var searchBox=new Panel { Dock=DockStyle.Top,Height=34,Padding=new Padding(14,6,14,4),BackColor=OverlayTheme.SurfaceCard };searchBox.Controls.Add(search);
            var searchLabel=new Label { Dock=DockStyle.Top,Height=24,Padding=new Padding(10,2,0,0),Text=Localization.Get("PlayersSearch") };
            var ordering=new FlowLayoutPanel { Name="PlayerOrdering",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(10,2,10,2),WrapContents=true };
            var sort=new ComboBox { Name="PlayerSort",DropDownStyle=ComboBoxStyle.DropDownList,Width=180 };
            sort.Items.AddRange(new object[]{"A–Z","Z–A",Localization.Get("PlayersSquadSize")+" ↓",Localization.Get("PlayersSquadSize")+" ↑",Localization.Get("ProfileFlags")+" ↓",Localization.Get("ProfileFlags")+" ↑",Localization.Get("ProfileVehicles")+" ↓",Localization.Get("ProfileVehicles")+" ↑"});sort.SelectedIndex=0;
            ordering.Controls.Add(new Label { AutoSize=true,Padding=new Padding(0,5,0,0),Text=Localization.Get("PlayersSort") });ordering.Controls.Add(sort);
            var list=new ListView { Name="PlayerDirectory",Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false };
            var tiles=new FlowLayoutPanel { Name="PlayerTiles",Dock=DockStyle.Top,AutoSize=false,Height=0,Padding=new Padding(4),Visible=!diagnosticMode };
            var paging=new FlowLayoutPanel { Name="PlayerPaging",Dock=DockStyle.Bottom,Height=42,Padding=new Padding(10,4,10,4),WrapContents=false,Visible=!diagnosticMode };
            var previousPage=new Button { Name="PreviousPlayerPage",Width=115,Height=30,Text=Localization.Get("WizardBtnPrev") };
            var pageNumber=new NumericUpDown { Name="PlayerPageNumber",Minimum=1,Maximum=1,Value=1,Width=78,Height=30 };
            var pageCount=new Label { Name="PlayerPageCount",AutoSize=true,Padding=new Padding(4,6,10,0) };
            var nextPage=new Button { Name="NextPlayerPage",Width=115,Height=30,Text=Localization.Get("WizardBtnNext") };
            var reopenHint=new Label { AutoSize=true,Padding=new Padding(12,6,0,0),Text=Localization.Get("PlayersTitle")+": "+PhysicalKeyCapture.KeyName(AppControlKey(AppControlAction.AdminPlayers)) };
            paging.Controls.AddRange(new Control[]{previousPage,pageNumber,pageCount,nextPage,reopenHint});
            var tileScroll=new SidebarScrollPanel { Dock=DockStyle.Fill,MinimumContentWidth=0,Visible=!diagnosticMode };tileScroll.Content.Controls.Add(tiles);
            list.Visible=diagnosticMode;
            list.Columns.Add(Localization.Get("PlayersName"),165);list.Columns.Add(Localization.Get("PlayersPlatformName"),165);list.Columns.Add("Platform ID",155);
            list.Columns.Add(Localization.Get("PlayersState"),145);list.Columns.Add(Localization.Get("PlayersSeen"),145);list.Columns.Add("X / Y / Z",235);
            var actions=new FlowLayoutPanel { Dock=DockStyle.Bottom,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new Padding(10,4,10,4),AutoScroll=true };
            var go=new Button { Name="TeleportToPlayer",Width=175,Text=Localization.Get("PlayersGo") };
            var here=new Button { Name="TeleportPlayerHere",Width=175,Text=Localization.Get("PlayersHere") };
            var profile=new Button { Name="PlayerProfile",Width=175,Text=Localization.Get("ProfileButton") };
            var vehicles=new Button { Name="RefreshVehicles",Width=175,Text=Localization.Get("ProfileRefreshVehicles") };
            var view=new Button { Name="PlayerView",Width=110,Text=Localization.Get(diagnosticMode?"ProfileTileView":"ProfileTableView") };
            actions.Controls.AddRange(new Control[]{go,here,profile,vehicles,view});
            var status=new Label { Name="PlayerStatus",Dock=DockStyle.Bottom,Height=38,Padding=new Padding(10,4,10,0) };
            var detail=new Label { Name="PlayerDetails",Dock=DockStyle.Bottom,Height=28,Padding=new Padding(10,2,10,0) };
            form.Controls.Add(tileScroll);form.Controls.Add(list);form.Controls.Add(ordering);form.Controls.Add(searchBox);form.Controls.Add(searchLabel);form.Controls.Add(toolbar);form.Controls.Add(detail);form.Controls.Add(paging);form.Controls.Add(actions);form.Controls.Add(status);
            OverlayTheme.Frame(form,form.Text,()=>form.Close(),44);StyleProfileList(list);form.TopMost=true;form.ShowInTaskbar=false;
            bool busy=false;
            bool initializing=true;
            Action updateActions=()=> { bool selected=!busy && list.SelectedItems.Count==1;go.Enabled=here.Enabled=profile.Enabled=selected;
                go.Visible=here.Visible=profile.Visible=detail.Visible=list.Visible;
                refresh.Enabled=import.Enabled=!busy && !initializing && adminPlayerServer.Length>0 && string.Equals(server.Text.Trim(),adminPlayerServer,StringComparison.OrdinalIgnoreCase);choose.Enabled=server.Enabled=!busy && !initializing;
                vehicles.Enabled=refresh.Enabled;
                detail.Text=selected?(((AdminPlayer)list.SelectedItems[0].Tag).Location==null?Localization.Get("ProfileNoPopulation"):Localization.T("PlayersDetails",((AdminPlayer)list.SelectedItems[0].Tag).Fame,((AdminPlayer)list.SelectedItems[0].Tag).Balance,((AdminPlayer)list.SelectedItems[0].Tag).Gold)):""; };
            int renderRevision=0;
            string directoryCountScope=null;
            long directoryCountRevision=-1;
            IDictionary<string,int> cachedVehicles=null,cachedFlags=null,cachedSquads=null;
            var tilePlayers=new List<Tuple<AdminPlayer,ListViewItem>>();
            int tileGeneration=0,displayedGeneration=-1,displayedPage=-1,displayedWidth=-1,displayedPageSize=-1;
            int currentPage=0,playersPerPage=1;
            bool updatingTiles=false,updatingPageNumber=false;
            Action refreshTiles=null;
            refreshTiles=()=> {
                if(updatingTiles || form.IsDisposed || form.Disposing || tiles.IsDisposed || !tiles.Visible)return;
                updatingTiles=true;
                tileScroll.BeginContentUpdate();tiles.SuspendLayout();
                try {
                    var viewport=tileScroll.VisibleContentBounds;
                    int available=Math.Max(285,viewport.Width-8);
                    int columns=Math.Max(1,available/303),width=Math.Max(285,available/columns-8);
                    int pageSize=Math.Min(24,columns*Math.Max(1,(viewport.Height-8)/268));
                    if(pageSize!=playersPerPage)currentPage=currentPage*playersPerPage/pageSize;
                    playersPerPage=pageSize;
                    int pages=Math.Max(1,(tilePlayers.Count+pageSize-1)/pageSize);
                    currentPage=Math.Min(currentPage,pages-1);
                    updatingPageNumber=true;
                    pageNumber.Maximum=pages;pageNumber.Value=currentPage+1;
                    updatingPageNumber=false;
                    pageCount.Text="/ "+pages+"  ·  "+tilePlayers.Count;
                    previousPage.Enabled=currentPage>0;nextPage.Enabled=currentPage<pages-1;
                    if(displayedGeneration==tileGeneration && displayedPage==currentPage && displayedWidth==width && displayedPageSize==pageSize)return;
                    int first=currentPage*pageSize,last=Math.Min(tilePlayers.Count,first+pageSize);
                    // Height-only viewport changes retain the unchanged prefix
                    // of cards. Rebuilding it on grow and shrink made a small
                    // resize perform several complete pages of native layout.
                    int keep=displayedGeneration==tileGeneration&&displayedWidth==width&&displayedPage*displayedPageSize==first?Math.Min(tiles.Controls.Count,last-first):0;
                    displayedGeneration=tileGeneration;displayedPage=currentPage;displayedWidth=width;displayedPageSize=pageSize;
                    // Each page contains only enough cards to fit the viewport.
                    // Navigation replaces only this bounded page of player controls.
                    tileScroll.ScrollBy(0,-int.MaxValue);
                    while(tiles.Controls.Count>keep) {var old=tiles.Controls[tiles.Controls.Count-1];tiles.Controls.RemoveAt(tiles.Controls.Count-1);old.Dispose();}
                    tiles.Padding=new Padding(4);
                    tiles.Height=Math.Max(8,((last-first+columns-1)/columns)*268+8);
                    for(int index=first+keep;index<last;index++) {
                        var player=tilePlayers[index].Item1;var row=tilePlayers[index].Item2;
                        var tile=CreateAdminPlayerTile(player,PlayerDirectoryQuery.Count(cachedVehicles,player.Id),PlayerDirectoryQuery.Count(cachedFlags,player.Id),page=> {
                            if(busy)return;
                            foreach(ListViewItem other in list.SelectedItems.Cast<ListViewItem>().ToArray())other.Selected=false;row.Selected=true;updateActions();
                            try { ShowAdminProfile(player,page); }catch(Exception error) { status.Text=error.Message; }
                        },async bring=> {
                            if(busy || adminCommandBusy || relativeTeleportBusy)return;
                            busy=true;updateActions();
                            try { bool sent=await RunAdminCommand(bring?player.TeleportHere:player.TeleportTo,form);if(!form.IsDisposed)status.Text=Localization.Get(sent?"PlayersTeleportSent":"PlayersTeleportStopped"); }
                            finally { busy=false;if(!closing && !form.IsDisposed) { updateActions();RestoreAdminTool(form,Native.AdminGameWindow()); } }
                        },!diagnosticMode);
                        tile.Width=width;AttachPlayerTeleportMenu(tile,player,form,text=>status.Text=text);tiles.Controls.Add(tile);
                    }
                } catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                finally { updatingPageNumber=false;if(!tiles.IsDisposed)tiles.ResumeLayout(true);tileScroll.EndContentUpdate();updatingTiles=false; }
            };
            previousPage.Click+=(s,e)=> { if(currentPage>0) { currentPage--;refreshTiles(); } };
            nextPage.Click+=(s,e)=> { if(currentPage<(int)pageNumber.Maximum-1) { currentPage++;refreshTiles(); } };
            pageNumber.ValueChanged+=(s,e)=> { if(!updatingPageNumber) { currentPage=(int)pageNumber.Value-1;refreshTiles(); } };
            tileScroll.WheelNavigation=steps=> {
                if(busy || updatingTiles || !tiles.Visible || form.IsDisposed)return;
                int target=Math.Max(0,Math.Min((int)pageNumber.Maximum-1,currentPage+steps));
                if(target==currentPage)return;
                currentPage=target;refreshTiles();
            };
            tileScroll.SizeChanged+=(s,e)=> { if(form.IsHandleCreated && !form.IsDisposed)form.BeginInvoke(new Action(()=> { if(!form.IsDisposed)refreshTiles(); })); };
            Func<bool,Task> renderDirectory=async background=> {
                SynchronizationContext.SetSynchronizationContext(uiContext);
                int revision=++renderRevision;
                string selected=list.SelectedItems.Count==1?((AdminPlayer)list.SelectedItems[0].Tag).Id:null;
                string scope=adminPlayerServer,query=search.Text;int order=sort.SelectedIndex;
                long countRevision=adminPlayerRevision;
                var population=adminPlayers;
                IList<AdminAsset> assets=null;IDictionary<string,int> squadFlagCounts=null,squadSizes=null;
                Action load=()=> {
                    var snapshot=scope.Length>0?LoadAdminServerSnapshot(scope):null;
                    assets=snapshot==null?new List<AdminAsset>().AsReadOnly():snapshot.Assets;
                    squadFlagCounts=snapshot==null?new Dictionary<string,int>():snapshot.SquadFlags;
                    squadSizes=snapshot==null?new Dictionary<string,int>():snapshot.SquadSizes;
                };
                bool reloadCounts=cachedVehicles==null || directoryCountRevision!=adminPlayerRevision || !string.Equals(directoryCountScope,scope,StringComparison.OrdinalIgnoreCase);
                if(reloadCounts)await Task.Run(load);
                if(closing || form.IsDisposed || revision!=renderRevision)return;
                if(reloadCounts) {
                    cachedVehicles=assets.Where(asset=>asset.Kind=="Vehicle" && asset.OwnerId.Length>0).GroupBy(asset=>asset.OwnerId).ToDictionary(group=>group.Key,group=>group.Count());
                    cachedFlags=assets.Where(asset=>asset.Kind=="Flag" && asset.OwnerId.Length>0).GroupBy(asset=>asset.OwnerId).ToDictionary(group=>group.Key,group=>group.Count());foreach(var entry in squadFlagCounts)cachedFlags[entry.Key]=entry.Value;
                    cachedSquads=squadSizes;directoryCountScope=scope;directoryCountRevision=countRevision;
                }
                var vehicleCounts=cachedVehicles;var flagCounts=cachedFlags;squadSizes=cachedSquads;
                var rows=new List<ListViewItem>();var entries=new List<Tuple<AdminPlayer,ListViewItem>>();
                foreach(var player in PlayerDirectoryQuery.Apply(population,query,order,squadSizes,flagCounts,vehicleCounts)) {
                    var row=new ListViewItem(new[]{player.Name,player.PlatformName,player.Id,Localization.Get(player.InSnapshot?"PlayersSnapshot":"PlayersHistory"),player.SeenUtc.ToLocalTime().ToString("g",CultureInfo.CurrentCulture),
                        player.Location==null?"—":string.Format(CultureInfo.InvariantCulture,"{0:0.###} / {1:0.###} / {2:0.###}",player.Location.X,player.Location.Y,player.Location.Z)}) { Tag=player };
                    rows.Add(row);if(player.Id==selected)row.Selected=true;
                    entries.Add(Tuple.Create(player,row));
                    if(rows.Count%100==0)await Task.Delay(1);
                    if(closing || form.IsDisposed || revision!=renderRevision)return;
                }
                list.BeginUpdate();
                try {
                    list.Items.Clear();list.Items.AddRange(rows.ToArray());
                    tilePlayers=entries;tileGeneration++;currentPage=0;
                } finally { list.EndUpdate(); }
                refreshTiles();
                updateActions();
            };
            Action render=async()=> { SynchronizationContext.SetSynchronizationContext(uiContext);try { await renderDirectory(false); }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; } };
            view.Click+=(s,e)=> { bool useTiles=!tiles.Visible;tileScroll.Visible=useTiles;tiles.Visible=useTiles;paging.Visible=useTiles;list.Visible=!useTiles;if(useTiles)tileScroll.BringToFront();else list.BringToFront();view.Text=Localization.Get(useTiles?"ProfileTableView":"ProfileTileView");updateActions();if(useTiles)refreshTiles(); };
            Action<string> selectServer=async name=> {
                SynchronizationContext.SetSynchronizationContext(uiContext);
                if(busy)return;
                ++renderRevision;busy=true;updateActions();
                try {
                    name=PlayerDatabase.ServerName(name);
                    IList<string> savedServers=null;IList<AdminPlayer> loaded=null;
                    await Task.Run(()=> { if(!string.Equals(PlayerStore.ActiveServer(),name,StringComparison.OrdinalIgnoreCase))PlayerStore.SelectServer(name);
                        loaded=LoadAdminServerSnapshot(name).Players;savedServers=PlayerStore.Servers(); });
                    if(closing || form.IsDisposed)return;
                    showAdminPlayers=show.Checked=false;ApplyAdminPlayers(name,loaded);
                    server.Items.Clear();foreach(string saved in savedServers)server.Items.Add(saved);server.Text=name;
                    await renderDirectory(true);if(!form.IsDisposed)status.Text=Localization.T("PlayersScope",name);
                }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                finally { busy=false;if(!form.IsDisposed)updateActions(); }
            };
            choose.Click+=(s,e)=> { try { selectServer(server.Text); }catch(Exception error) { status.Text=error.Message; } };
            server.SelectionChangeCommitted+=(s,e)=> { try { selectServer(server.Text); }catch(Exception error) { status.Text=error.Message; } };
            server.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Enter && !busy) { choose.PerformClick();e.Handled=true;e.SuppressKeyPress=true; } };
            import.Click+=async(s,e)=> {
                SynchronizationContext.SetSynchronizationContext(uiContext);
                if(busy)return;
                string scope=adminPlayerServer;++renderRevision;busy=true;updateActions();
                try {
                    string text=Clipboard.GetText();
                    string message=await Task.Run(()=>ImportAdminClipboard(scope,text));
                    await ReloadAdminPlayersAsync(scope);
                    if(closing || form.IsDisposed)return;
                    await renderDirectory(true);if(!form.IsDisposed)status.Text=message;
                }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                finally { busy=false;if(!form.IsDisposed)updateActions(); }
            };
            refresh.Click+=async(s,e)=> {
                SynchronizationContext.SetSynchronizationContext(uiContext);
                string scope=adminPlayerServer;busy=true;updateActions();status.Text=Localization.Get("PlayersRefreshing");
                try {
                    SpawnBatchResult response=await RunAdminCommands(new List<string> { "#ListPlayers true" },form,null,false,true);
                    if(response.ClipboardResponse==null) { status.Text=response.Error??Localization.Get("PlayersNoResponse");return; }
                    if(closing || form.IsDisposed)return;
                    DateTime seen=DateTime.UtcNow;
                    await Task.Run(()=>PlayerStore.Import(scope,response.ClipboardResponse,seen));
                    await ReloadAdminPlayersAsync(scope);
                    if(closing || form.IsDisposed)return;
                    if(!response.ChatClosed) { await renderDirectory(true);status.Text=response.Error??Localization.Get("SpawnCartCleanupStopped");return; }
                    await RefreshAdminProfileReports(scope,adminPlayers.Where(player=>player.InSnapshot),form,text=> { if(!form.IsDisposed)status.Text=text; });
                    if(!form.IsDisposed)await renderDirectory(true);
                }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                finally { busy=false;if(!closing && !form.IsDisposed) { updateActions();RestoreAdminTool(form,Native.AdminGameWindow()); } }
            };
            Func<bool,Task> teleport=async bring=> {
                SynchronizationContext.SetSynchronizationContext(uiContext);
                if(busy || list.SelectedItems.Count!=1)return;var player=(AdminPlayer)list.SelectedItems[0].Tag;
                busy=true;updateActions();
                try { bool sent=await RunAdminCommand(bring?player.TeleportHere:player.TeleportTo,form);if(!form.IsDisposed)status.Text=Localization.Get(sent?"PlayersTeleportSent":"PlayersTeleportStopped"); }
                finally { busy=false;if(!closing && !form.IsDisposed) { updateActions();RestoreAdminTool(form,Native.AdminGameWindow()); } }
            };
            go.Click+=async(s,e)=>await teleport(false);here.Click+=async(s,e)=>await teleport(true);
            profile.Click+=(s,e)=> { if(busy || list.SelectedItems.Count!=1)return;try { ShowAdminProfile((AdminPlayer)list.SelectedItems[0].Tag); }catch(Exception error) { status.Text=error.Message; } };
            vehicles.Click+=async(s,e)=> {
                SynchronizationContext.SetSynchronizationContext(uiContext);
                string scope=adminPlayerServer;busy=true;updateActions();status.Text=Localization.Get("ProfileRefreshingVehicles");
                try {
                    SpawnBatchResult response=await RunAdminCommands(new List<string> { "#ListSpawnedVehicles true" },form,null,false,false,true);
                    if(response.ClipboardResponse==null) { status.Text=response.Error??Localization.Get("ProfileVehicleTimeout");return; }
                    if(closing || form.IsDisposed)return;
                    DateTime seen=DateTime.UtcNow;
                    await Task.Run(()=>ProfileStore.Import(scope,response.ClipboardResponse,seen));
                    if(closing || form.IsDisposed)return;
                    ClearAdminProfileMarkers();await renderDirectory(true);status.Text=Localization.T("ProfileVehiclesSaved",scope);
                }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                finally { busy=false;if(!closing && !form.IsDisposed) { updateActions();RestoreAdminTool(form,Native.AdminGameWindow()); } }
            };
            var playerMenu=new ContextMenuStrip();
            foreach(bool bring in new[]{false,true}) { var item=new ToolStripMenuItem(Localization.Get(bring?"PlayersHere":"PlayersGo"));item.Click+=async(s,e)=>await teleport(bring);playerMenu.Items.Add(item); }
            AddPlayerInformationMenu(playerMenu,()=>list.SelectedItems.Count==1?(AdminPlayer)list.SelectedItems[0].Tag:null,form,text=>status.Text=text);
            OverlayTheme.Menu(playerMenu);list.ContextMenuStrip=playerMenu;
            list.MouseDown+=(s,e)=> { if(e.Button!=MouseButtons.Right)return;var hit=list.GetItemAt(e.X,e.Y);foreach(ListViewItem row in list.Items)row.Selected=false;if(hit!=null) { hit.Selected=true;hit.Focused=true; } };
            playerMenu.Opening+=(s,e)=>e.Cancel=busy || list.SelectedItems.Count!=1;list.Disposed+=(s,e)=>playerMenu.Dispose();
            var searchDelay=new Timer { Interval=180 };
            searchDelay.Tick+=(s,e)=> { searchDelay.Stop();render(); };
            form.Disposed+=(s,e)=> { ++renderRevision;searchDelay.Dispose(); };
            list.SelectedIndexChanged+=(s,e)=>updateActions();search.TextChanged+=(s,e)=> { ++renderRevision;searchDelay.Stop();searchDelay.Start(); };
            sort.SelectedIndexChanged+=(s,e)=>render();
            server.TextChanged+=(s,e)=>updateActions();
            show.CheckedChanged+=(s,e)=> { showAdminPlayers=show.Checked;canvas.Invalidate();overlay.Invalidate(); };
            form.FormClosing+=(s,e)=> { if(busy && !closing)e.Cancel=true; };
            form.Shown+=async(s,e)=> {
                SynchronizationContext.SetSynchronizationContext(uiContext);
                IList<string> savedServers=null;string savedServer=null;
                try {
                    await Task.Run(()=> { savedServers=PlayerStore.Servers();savedServer=PlayerStore.ActiveServer(); });
                    if(closing || form.IsDisposed)return;
                    foreach(string saved in savedServers)server.Items.Add(saved);
                    initializing=false;
                    if(savedServer.Length>0)selectServer(savedServer);else status.Text=Localization.Get("PlayersChooseServer");
                }catch(Exception error) { if(!form.IsDisposed)status.Text=error.Message; }
                finally { initializing=false;if(!form.IsDisposed)updateActions(); }
            };
            updateActions();RestoreAdminTool(form,Native.AdminGameWindow());if(!diagnosticMode) { form.Activate();Native.ForceForeground(form.Handle); }
        }
    }
}
