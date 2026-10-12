using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal sealed class AdminCommand {
        internal string Name,Command;
        internal bool IsBoolean;
        internal bool ToggleNumeric,NumericMode;
        internal string Quantity="",ExtraStrings="";
        internal string StateKey { get { return Build(false); } }
        internal AdminCommand(string name,string command,bool isBoolean=false) { Name=name; Command=command;IsBoolean=isBoolean; }
        internal string Build(bool enabled=false) {
            return (IsBoolean?ToggleBaseCommand():Command)+(IsBoolean?(ToggleNumeric?(enabled?" 1":" 0"):(enabled?" true":" false")):"")+(string.IsNullOrWhiteSpace(Quantity)?"":" "+Quantity.Trim())+(string.IsNullOrWhiteSpace(ExtraStrings)?"":" "+ExtraStrings.Trim());
        }
        internal string ToggleBaseCommand() {
            string command=BooleanBaseCommand(Command);
            if(ToggleNumeric)while(command!=null && (command.EndsWith(" 0",StringComparison.Ordinal) || command.EndsWith(" 1",StringComparison.Ordinal)))command=command.Substring(0,command.Length-2).TrimEnd();
            return command;
        }
        internal static string BooleanBaseCommand(string command) {
            while(command!=null) {
                if(command.EndsWith(" true",StringComparison.OrdinalIgnoreCase))command=command.Substring(0,command.Length-5).TrimEnd();
                else if(command.EndsWith(" false",StringComparison.OrdinalIgnoreCase))command=command.Substring(0,command.Length-6).TrimEnd();
                else break;
            }
            return command;
        }
        internal string ValidationError() {
            if((ToggleNumeric && !IsBoolean) || (NumericMode && IsBoolean))return "Choose a valid command type.";
            if(string.IsNullOrWhiteSpace(Name) || Name.Length>80)return "Enter a label of up to 80 characters.";
            decimal numericValue;
            if(NumericMode && Quantity.Length==0)return "Enter the numeric value to send.";
            if(Quantity.Length>0 && !decimal.TryParse(Quantity,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out numericValue))return "Enter a numeric value using a decimal point (for example 0, 0.5 or 1), or leave it empty.";
            if(!Valid(Command) || !Valid(Build(true)))return "Enter a single command beginning with #. The complete command must fit within 512 characters.";
            return null;
        }
        internal static bool Valid(string command) {
            if(string.IsNullOrWhiteSpace(command) || command.Length>512 || !command.StartsWith("#") || command.Trim()!=command) return false;
            foreach(char c in command) if(char.IsControl(c)) return false;
            return command.Length>1;
        }
        internal static List<AdminCommand> Defaults() {
            return new List<AdminCommand> {
                new AdminCommand("God mode","#SetGodMode",true),
                new AdminCommand("Player info","#ShowOtherPlayerInfo",true),
                new AdminCommand("Nameplates","#ShowNameplates",true)
            };
        }
        internal static List<AdminCommand> Toggles(List<AdminCommand> commands) {
            var result=new List<AdminCommand>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var entry in commands) {
                string command=BooleanBaseCommand(entry.Command);
                string name=entry.Name;
                if(name.EndsWith(" ON",StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-3);
                else if(name.EndsWith(" OFF",StringComparison.OrdinalIgnoreCase))name=name.Substring(0,name.Length-4);
                if(seen.Add(command))result.Add(new AdminCommand(name,command,true));
            }
            return result;
        }
        internal static List<AdminCommand> Load(string path) {
            if(!File.Exists(path))return Defaults();
            var result=new List<AdminCommand>();var legacy=new List<AdminCommand>();
            foreach(string line in File.ReadAllLines(path)) {
                string[] fields=line.Split('\t'); if(fields.Length!=2 && fields.Length!=5 && fields.Length!=7)throw new FormatException("Invalid saved admin button.");
                string name=Encoding.UTF8.GetString(Convert.FromBase64String(fields[0])),command=Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
                if(string.IsNullOrWhiteSpace(name) || name.Length>80 || !Valid(command))throw new FormatException("Invalid saved admin button.");
                var entry=new AdminCommand(name,command);
                if(fields.Length==2) {legacy.Add(entry);continue;}
                if(fields[2]!="0" && fields[2]!="1")throw new FormatException("Invalid saved boolean setting.");
                entry.IsBoolean=fields[2]=="1";
                if(fields.Length==7) {
                    if((fields[5]!="0" && fields[5]!="1") || (fields[6]!="0" && fields[6]!="1") || (fields[5]=="1" && !entry.IsBoolean) || (fields[6]=="1" && entry.IsBoolean))throw new FormatException("Invalid saved command type.");
                    entry.ToggleNumeric=fields[5]=="1";entry.NumericMode=fields[6]=="1";
                }
                if(entry.IsBoolean)entry.Command=entry.ToggleBaseCommand();
                entry.Quantity=Encoding.UTF8.GetString(Convert.FromBase64String(fields[3]));
                if(fields.Length==5 && !entry.IsBoolean && entry.Quantity.Length>0)entry.NumericMode=true;
                entry.ExtraStrings=Encoding.UTF8.GetString(Convert.FromBase64String(fields[4]));
                if(entry.ValidationError()!=null)throw new FormatException(entry.ValidationError());
                result.Add(entry);
            }
            result.AddRange(Toggles(legacy));return result;
        }
        internal static void Save(string path,List<AdminCommand> commands) {
            var lines=new List<string>();
            foreach(var c in commands) {
                if(c.ValidationError()!=null)throw new FormatException(c.ValidationError());
                lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Name))+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(c.IsBoolean?c.ToggleBaseCommand():c.Command))+"\t"+(c.IsBoolean?"1":"0")+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Quantity))+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(c.ExtraStrings))+"\t"+(c.ToggleNumeric?"1":"0")+"\t"+(c.NumericMode?"1":"0"));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllLines(path+".tmp",lines);
            if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
        }
    }

    internal sealed class AdminCommandEditor:Form {
        internal AdminCommand SavedCommand;
        internal AdminCommandEditor(AdminCommand command) {
            Text="Edit admin command";ClientSize=new Size(550,320);StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;TopMost=true;
            var label=new TextBox {Name="Label",Left=185,Top=16,Width=345,Text=command.Name,MaxLength=80};
            var value=new TextBox {Name="Command",Left=185,Top=56,Width=345,Text=command.Command,MaxLength=512};
            var boolean=new CheckBox {Name="IsBoolean",Visible=false,Checked=command.IsBoolean};
            var type=new ComboBox {Name="CommandType",Left=185,Top=96,Width=345,DropDownStyle=ComboBoxStyle.DropDownList};
            type.Items.AddRange(new object[]{"Run once / free-text arguments","Pill toggle: true / false","Pill toggle: 1 / 0","Numeric value"});
            type.SelectedIndex=command.IsBoolean?(command.ToggleNumeric?2:1):command.NumericMode?3:0;
            var quantity=new TextBox {Name="Quantity",Left=185,Top=136,Width=345,Text=command.Quantity,MaxLength=32};
            var extras=new TextBox {Name="ExtraStrings",Left=185,Top=176,Width=345,Text=command.ExtraStrings,MaxLength=512};
            string[] names={"Button label","Command (starts with #)","Command type","Value / quantity (optional)","Arguments (any text)"};
            for(int i=0;i<names.Length;i++)Controls.Add(new Label {Left=12,Top=19+i*40,Width=170,Text=names[i]});
            Controls.AddRange(new Control[]{label,value,boolean,type,quantity,extras});
            var preview=new Label {Left=12,Top=215,Width=518,Height=30};Controls.Add(preview);
            var validation=new Label {Name="ValidationError",Left=12,Top=246,Width=518,Height=27};Controls.Add(validation);
            Func<AdminCommand> read=()=>new AdminCommand(label.Text.Trim(),value.Text.Trim(),type.SelectedIndex==1 || type.SelectedIndex==2) {ToggleNumeric=type.SelectedIndex==2,NumericMode=type.SelectedIndex==3,Quantity=quantity.Text.Trim(),ExtraStrings=extras.Text.Trim()};
            Action refresh=()=> { var entry=read();preview.Text=entry.IsBoolean?"ON: "+entry.Build(true)+"\nOFF: "+entry.Build(false):"Sends: "+entry.Build();validation.Text=""; };
            type.SelectedIndexChanged+=(s,e)=> { boolean.Checked=type.SelectedIndex==1 || type.SelectedIndex==2;refresh(); };
            boolean.CheckedChanged+=(s,e)=> { if(boolean.Checked && type.SelectedIndex!=1 && type.SelectedIndex!=2)type.SelectedIndex=1;else if(!boolean.Checked && (type.SelectedIndex==1 || type.SelectedIndex==2))type.SelectedIndex=0; };
            foreach(var box in new[]{label,value,quantity,extras})box.TextChanged+=(s,e)=>refresh();boolean.CheckedChanged+=(s,e)=>refresh();refresh();
            var save=new Button {Name="Save",Left=330,Top=278,Width=95,Text="Save"};
            var cancel=new Button {Left=435,Top=278,Width=95,Text="Cancel",DialogResult=DialogResult.Cancel};
            Controls.Add(save);Controls.Add(cancel);AcceptButton=save;CancelButton=cancel;
            save.Click+=(s,e)=> {
                var entry=read();
                if(entry.IsBoolean)entry.Command=entry.ToggleBaseCommand();
                string error=entry.ValidationError();if(error!=null) {validation.Text=error;return;}
                SavedCommand=entry;DialogResult=DialogResult.OK;
            };
            OverlayTheme.Style(this);
        }
    }

    internal sealed class SpawnBatchResult {
        internal int SubmittedCount;
        internal bool Completed,ChatClosed;
        internal string Error,StopReason;
        internal string ClipboardResponse;
    }

    static partial class Native {
        internal static bool AdminInputInProgress;
        internal static bool AdminKeysHeld() {
            for(int key=1;key<256;key++) if((GetAsyncKeyState(key)&0x8000)!=0)return true;
            return false;
        }
        internal static IntPtr AdminGameWindow() {
            IntPtr foreground=GetForegroundWindow();if(IsGameWindow(foreground))return foreground;
            IntPtr target=IntPtr.Zero;
            foreach(string name in new[]{"SCUM","SCUM-Win64-Shipping"})foreach(Process process in Process.GetProcessesByName(name))using(process) {
                IntPtr window=process.MainWindowHandle;if(!IsGameWindow(window))continue;
                if(target!=IntPtr.Zero && target!=window)return IntPtr.Zero;
                target=window;
            }
            return target;
        }
        // A single foreground request can settle asynchronously. Wait only for the
        // original target, and never reclaim focus after another app takes it.
        // null means ready; every other result identifies the gate that stopped it.
        internal static async Task<string> AdminFocusReadiness(Func<bool> targetValid,Func<bool> targetFocused,
            Func<bool> ownForeground,Func<bool> keysHeld,Func<string> blockingGate,Func<int,Task> delay,
            Func<bool> transitionalForeground=null,Action retryFocus=null) {
            if(!targetValid())return "target-unavailable";
            string blocked=blockingGate();if(blocked!=null)return blocked;
            await delay(150);
            bool retried=false;
            for(int waited=0;;waited+=25) {
                if(!targetValid())return "target-unavailable";
                blocked=blockingGate();if(blocked!=null)return blocked;
                bool focused=targetFocused();
                if(!focused) {
                    bool owned=ownForeground(),transitional=transitionalForeground!=null && transitionalForeground();
                    if(!owned && !transitional)return "unrelated-foreground";
                    if(!retried && retryFocus!=null) { retried=true;retryFocus(); }
                }
                bool held=keysHeld();
                if(focused && !held)return null;
                if(waited>=500)return held?"held-keys":"focus-handoff";
                await delay(25);
            }
        }
        internal static string AdminDispatchGateError(string gate) {
            switch(gate) {
                case "clipboard-changed":case "clipboard-verification":case "clipboard-busy":case "clipboard-publication":case "clipboard-owner":case "clipboard-finalization":return Localization.Get("SpawnCartClipboardChanged");
                case "physical-input":case "focus-changed":case "target-unavailable":return Localization.Get("SpawnCartFocusInputChanged");
                case "inventory-open":case "chat-open":return Localization.Get("SpawnCartCloseChatInventory");
                case "keyboard-monitor":return Localization.Get("SpawnCartMonitorUnavailable");
                case "closing":case "admin-busy":return Localization.Get("SpawnCartBusy");
                case "unrelated-foreground":return Localization.Get("SpawnCartForegroundBlocked");
                case "held-keys":case "focus-handoff":return Localization.Get("SpawnCartFocusUnavailable");
                default:return null;
            }
        }
        // A map toggle is sent once. Only that owned transition may settle here;
        // unrelated menus, chat, clipboard changes and physical input still stop it.
        internal static async Task<string> AdminMenuCloseReadiness(bool mapCloseRequested,Func<string> blockingGate,
            Func<bool> chatOpen,Func<bool> menuOpen,Func<int,Task> delay) {
            for(int waited=0;;waited+=25) {
                string blocked=blockingGate();if(blocked!=null)return blocked;
                if(chatOpen())return "chat-open";
                if(!menuOpen())return null;
                if(!mapCloseRequested || waited>=500)return "inventory-open";
                await delay(25);
            }
        }
        internal static async Task<AdminClipboardResult> AdminClipboardPreparation(Func<AdminClipboardResult> publish,
            Func<string> blockingGate,Func<int,Task> delay) {
            for(int waited=0;;waited+=25) {
                string blocked=blockingGate();
                if(blocked!=null)return new AdminClipboardResult { StopReason=blocked };
                AdminClipboardResult result=publish();
                // Acquiring the clipboard may contend with an existing reader.
                // Retry acquisition only; never repeat a publication or command.
                if(result.Ready || result.StopReason!="clipboard-busy" || result.Sequence!=0 || waited>=100)return result;
                await delay(25);
            }
        }
        // Closing a text publication can synthesize more formats and advance its
        // sequence. Reopen read-only to verify the final owned contents; never
        // republish, and never promote a failed partial publication to ready.
        internal static async Task<AdminClipboardResult> AdminClipboardFinalization(AdminClipboardResult publication,
            Func<bool> open,Func<bool> close,Func<uint> sequence,Func<bool> owned,Func<bool> verify,
            Func<string> blockingGate,Func<int,Task> delay) {
            bool initiallyReady=publication.Ready;
            var result=new AdminClipboardResult { Sequence=publication.Sequence,NativeError=publication.NativeError,
                StopReason=initiallyReady?"clipboard-finalization":publication.StopReason };
            Func<string,AdminClipboardResult> stop=gate=> {
                result.Ready=false;result.StopReason=gate;
                if(result.Sequence==0 || !owned() || sequence()!=result.Sequence)result.Sequence=0;
                return result;
            };
            int waited=0;
            for(int pass=0;pass<3;pass++) {
                while(true) {
                    string gate=blockingGate();if(gate!=null)return stop(gate);
                    if(!owned()) { result.Sequence=0;return stop("clipboard-owner"); }
                    if(open())break;
                    if(waited>=100)return stop("clipboard-finalization");
                    await delay(25);waited+=25;
                }
                string failure=null;bool verified=false,closed=false;uint captured=0;
                try {
                    failure=blockingGate();
                    if(failure==null && !owned())failure="clipboard-owner";
                    if(failure==null) {
                        verified=verify();
                        if(!owned())failure="clipboard-owner";
                        else if(initiallyReady && !verified)failure="clipboard-verification";
                        else {
                            captured=sequence();result.Sequence=captured;
                            if(captured==0)failure="clipboard-verification";
                        }
                    }
                    if(failure=="clipboard-owner" || failure=="clipboard-verification")result.Sequence=0;
                } catch(Exception ex) {
                    failure="clipboard-finalization";
                    if(result.NativeError==0)result.NativeError=ex.HResult;
                } finally { closed=close(); }
                if(failure!=null)return stop(failure);
                if(!closed)return stop("clipboard-finalization");
                if(!owned()) { result.Sequence=0;return stop("clipboard-owner"); }
                string blocked=blockingGate();if(blocked!=null)return stop(blocked);
                if(sequence()==captured) {
                    result.Ready=initiallyReady && verified;
                    result.StopReason=result.Ready?null:publication.StopReason;
                    return result;
                }
                // A further close-induced format change gets another read-only
                // verification. The previous sequence is never a usable lease.
                result.Sequence=0;
            }
            return stop("clipboard-finalization");
        }
        // All key-downs require the original SCUM window and no intervening physical input.
        // The lease releases only our injected keys, including on failure/focus loss.
        internal static async Task<bool> AdminChatSequence(Func<uint,bool,bool> send,Func<int,Task> delay,Func<bool> allowed,
            Func<bool> clipboardReady,Action opened,int chatKey,int mapKey,Action onSubmitted=null,Func<bool> beforeChat=null,
            Action onMapClosing=null,Func<Task<bool>> beforeChatAsync=null) {
            var lease=new CopyInputLease(send);
            Func<uint,Action,Task<bool>> tap=async (key,onPressed)=> {
                if(!allowed() || !lease.Send(key,false))return false;
                if(onPressed!=null)onPressed();
                if(key==(uint)chatKey)opened();
                await delay(35);return lease.Send(key,true) && allowed();
            };
            try {
                if(mapKey>0) { if(!await tap((uint)mapKey,onMapClosing))return false;await delay(120); }
                if(!allowed() || (beforeChat!=null && !beforeChat()))return false;
                if(beforeChatAsync!=null && !await beforeChatAsync())return false;
                if(!await tap((uint)chatKey,null))return false;
                await delay(160);
                if(!allowed() || !clipboardReady())return false;
                if(!lease.Send(0xA2,false))return false;
                await delay(25);
                if(!await tap(0x41,null) || !clipboardReady())return false;
                if(!await tap(0x56,null))return false;
                if(!lease.Send(0xA2,true))return false;
                await delay(100);
                if(!clipboardReady())return false;
                // Enter-down may submit even if physical input interrupts its release.
                // Record the request at dispatch so a retry does not repeat the old value.
                if(!await tap(0x0D,onSubmitted))return false;
                await delay(80);
                return await tap(0x1B,null);
            } finally { lease.Cancel(); }
        }
        internal static Task<bool> SendAdminChat(IntPtr game,int chatKey,int chatScan,int mapKey,int mapScan,
            Func<bool> allowed,Func<bool> clipboardReady,Action opened,Action onSubmitted=null,Func<bool> beforeChat=null,
            Action onMapClosing=null,Func<Task<bool>> beforeChatAsync=null) {
            uint pid;IntPtr layout=GetKeyboardLayout(GetWindowThreadProcessId(game,out pid));
            return AdminChatSequence((key,up)=>Key(key,up,(int)key==chatKey?chatScan:((int)key==mapKey?mapScan:0),layout),
                Task.Delay,()=>GetForegroundWindow()==game && IsGameWindow(game) && allowed(),clipboardReady,opened,chatKey,mapKey,onSubmitted,beforeChat,onMapClosing,beforeChatAsync);
        }
        // Every entry shares the caller's original focus, input and clipboard lease.
        // A dispatched Enter is counted even when its key-up or Escape is interrupted.
        internal static Task<SpawnBatchResult> AdminChatBatchSequence(int commandCount,Func<int,bool> prepareClipboard,
            Func<int,Action,Task<bool>> sendCommand,Func<int,Task> delay,Func<bool> allowed,Action<int> onSubmitted=null) {
            return AdminChatBatchSequenceAsync(commandCount,index=>Task.FromResult(prepareClipboard(index)),sendCommand,delay,allowed,onSubmitted);
        }
        internal static async Task<SpawnBatchResult> AdminChatBatchSequenceAsync(int commandCount,Func<int,Task<bool>> prepareClipboard,
            Func<int,Action,Task<bool>> sendCommand,Func<int,Task> delay,Func<bool> allowed,Action<int> onSubmitted=null) {
            var result=new SpawnBatchResult();
            if(commandCount<1) { result.Error=Localization.Get("SpawnCartEmpty");return result; }
            try {
                for(int index=0;index<commandCount;index++) {
                    if(!allowed()) { result.Error=Localization.Get("SpawnCartFocusInputChanged");return result; }
                    if(!await prepareClipboard(index)) { result.Error=Localization.Get("SpawnCartClipboardChanged");return result; }
                    if(!allowed()) { result.Error=Localization.Get("SpawnCartFocusInputChanged");return result; }
                    bool submitted=false;
                    result.ChatClosed=false;
                    result.ChatClosed=await sendCommand(index,()=> {
                        if(submitted)return;
                        submitted=true;result.SubmittedCount++;
                        if(onSubmitted!=null)onSubmitted(result.SubmittedCount);
                    });
                    if(!result.ChatClosed || !submitted) {
                        result.Error=Localization.Get(submitted?"SpawnCartCleanupStopped":"SpawnCartBeforeSubmitStopped");
                        return result;
                    }
                    // Chat opening, paste settling and Escape already have their own waits.
                    if(index+1<commandCount)await delay(60);
                }
                result.Completed=true;return result;
            } catch(Exception ex) {
                Program.LogException("AdminCommandBatch",ex);
                result.Error=Localization.T("SpawnCartException",ex.Message);
                return result;
            }
        }
    }

    internal sealed class AdminControlsWindow:Form {
        internal bool FullMapPalette,SuppressActivation;
        protected override bool ShowWithoutActivation { get { return SuppressActivation; } }
        protected override CreateParams CreateParams {
            get { var parameters=base.CreateParams;parameters.ExStyle|=0x80;return parameters; }
        }
        protected override void WndProc(ref Message message) {
            if(message.Msg==0x21 && FullMapPalette) {
                // Keep button clicks over the map from taking SCUM's keyboard focus.
                // Distance and command editors can receive deliberate text input.
                Control target=this;
                while(target!=null) {
                    if(target is TextBoxBase || target is NumericUpDown || target is ComboBox)break;
                    Control child=target.GetChildAtPoint(target.PointToClient(Cursor.Position));
                    if(child==null) { message.Result=new IntPtr(3);return; }
                    target=child;
                }
            }
            base.WndProc(ref message);
        }
    }

    internal sealed class SidebarDistanceInput:NumericUpDown {
        readonly SidebarButton decrease,increase;
        internal SidebarDistanceInput() {
            BorderStyle=BorderStyle.None;TextAlign=HorizontalAlignment.Center;
            BackColor=FullMapSidebarStyle.ButtonSurface;ForeColor=Color.WhiteSmoke;
            MinimumSize=new Size(0,20);
            decrease=new SidebarButton { Text="-",Width=20,Height=20,TabStop=false };
            increase=new SidebarButton { Text="+",Width=20,Height=20,TabStop=false };
            decrease.Click+=(s,e)=>DownButton();increase.Click+=(s,e)=>UpButton();
            Controls.Add(decrease);Controls.Add(increase);
        }
        protected override void OnLayout(LayoutEventArgs e) {
            base.OnLayout(e);
            if(decrease==null || increase==null)return;
            foreach(Control child in Controls) {
                var edit=child as TextBoxBase;
                if(edit!=null) {
                    edit.BackColor=FullMapSidebarStyle.ButtonSurface;edit.ForeColor=Color.WhiteSmoke;
                    edit.SetBounds(23,2,Math.Max(1,Width-46),Math.Max(1,Height-4));
                } else if(!(child is SidebarButton))child.Visible=false;
            }
            decrease.SetBounds(0,0,20,Height);increase.SetBounds(Math.Max(20,Width-20),0,20,Height);
        }
    }

    public sealed partial class MapWindow {
        static void StyleAdminSidebar(Control control) {
            control.BackColor=control is TextBoxBase?FullMapSidebarStyle.ButtonSurface:FullMapSidebarStyle.Background;
            control.ForeColor=Color.WhiteSmoke;
            if(control is NumericUpDown)return;
            foreach(Control child in control.Controls)StyleAdminSidebar(child);
        }
        internal const int AdminControlsExpandedWidth=326;
        internal const int AdminControlsCollapsedWidth=28;
        internal const int AdminControlsCollapsedHeight=132;
        internal const int AdminControlsGap=4;
        bool adminCommandBusy;
        bool relativeTeleportBusy;
        Position lastTeleportSource;
        TeleportDirection relativeTeleportDirection=TeleportDirection.Up;
        double relativeTeleportDistanceMeters=20;
        Form adminCommandsPanel;
        bool adminControlsCommandReturn;
        bool adminPanelDismissedForFullMap;
        bool adminToolsEnabled;
        internal void SetAdminToolsEnabled(bool enabled) {
            if(adminCommandBusy || relativeTeleportBusy)return;
            adminToolsEnabled=enabled;
            if(!enabled)adminControlsCommandReturn=false;
            if(!enabled) {
                foreach(var panel in new[]{adminCommandsPanel,adminPlayersPanel,(Form)spawnCartDialog})if(panel!=null && !panel.IsDisposed)panel.Hide();
                foreach(var profile in adminProfileWindows.ToArray())if(!profile.IsDisposed)profile.Hide();
                ClearAdminProfileMarkers();showAdminPlayers=false;
            }
            AdminControlsMapModeChanged();SaveSettings();canvas.Invalidate();overlay.Invalidate();
        }
        bool adminPanelExpanded;
        bool adminPanelFitsBesideMap=true;
        bool adminPanelDockedLeft=true;
        bool adminCommandsSectionExpanded=true,adminTeleportSectionExpanded;
        Action refreshAdminControlsLayout;
        Action<bool> scrollAdminControlsSection;
        Action refreshAdminCommands;
        readonly Dictionary<string,bool> adminToggleStates=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);
        internal bool? LastAdminToggleState(AdminCommand command) {
            bool state;
            return command.IsBoolean && adminToggleStates.TryGetValue(command.StateKey,out state)?(bool?)state:null;
        }
        internal string AdminCommandLabel(AdminCommand command) {
            bool? state=LastAdminToggleState(command);
            return command.Name+(command.IsBoolean?": "+(state.HasValue?(state.Value?"ON":"OFF"):"UNKNOWN"):"");
        }
        internal async Task<bool> SubmitAdminCommand(AdminCommand command,bool? requested,Func<string,Task<bool>> send) {
            if(adminCommandBusy || relativeTeleportBusy || closing)return false;
            bool nextState=requested??!LastAdminToggleState(command).GetValueOrDefault();
            string stateKey=command.StateKey;
            bool isBoolean=command.IsBoolean;
            if(!await send(command.Build(nextState)))return false;
            if(isBoolean)adminToggleStates[stateKey]=nextState;
            if(refreshAdminCommands!=null && adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed)refreshAdminCommands();
            return true;
        }
        internal string BuildRelativeTeleportCommand(TeleportDirection direction,double metres) {
            if(adminCommandBusy || relativeTeleportBusy || closing || position==null || object.ReferenceEquals(position,lastTeleportSource))return null;
            return RelativeTeleport.BuildCommand(position,direction,metres);
        }
        internal async Task<bool> SubmitRelativeTeleport(TeleportDirection direction,double metres,Func<string,Task<bool>> send) {
            string command=BuildRelativeTeleportCommand(direction,metres);
            if(command==null || send==null)return false;
            relativeTeleportBusy=true;
            RefreshAdminCommandsPanel();
            try {
                if(!await send(command))return false;
                // A submitted command is not a confirmed game position. Wait for a new sample.
                lastTeleportSource=position;
                return true;
            } finally {
                relativeTeleportBusy=false;
                RefreshAdminCommandsPanel();
            }
        }
        void RefreshAdminCommandsPanel() {
            if(refreshAdminCommands!=null && adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed)refreshAdminCommands();
        }
        string RelativeTeleportProblem(TeleportDirection direction,double metres) {
            if(adminCommandBusy || relativeTeleportBusy || closing)return Localization.Get("AdminTeleportBusy");
            if(position==null)return Localization.Get("AdminTeleportMissing");
            if(object.ReferenceEquals(position,lastTeleportSource))return Localization.Get("AdminTeleportWaiting");
            return RelativeTeleport.BuildCommand(position,direction,metres)==null?Localization.Get("AdminTeleportInvalid"):null;
        }
        void QueueAdminCommand(AdminCommand command,bool? requested) {
            // Dispatch after the native menus release focus and mouse capture.
            overlay.CloseAdminContextMenu();
            try { BeginInvoke(new Action(async()=> {
                if(!closing && !IsDisposed)await SubmitAdminCommand(command,requested,text=>RunAdminCommand(text,null));
            })); } catch(InvalidOperationException) {}
        }
        internal static string BuildMapTeleportCommand(PointF point) {
            if(float.IsNaN(point.X) || float.IsNaN(point.Y) || float.IsInfinity(point.X) || float.IsInfinity(point.Y)
                || point.X<0 || point.X>1 || point.Y<0 || point.Y>1)return null;
            return string.Format(CultureInfo.InvariantCulture,"#Teleport {0:0.##} {1:0.##} 0",
                617718d-point.X*1521618d,618618d-point.Y*1523618d);
        }
        internal void PopulateFullMapAdminMenu(ToolStripMenuItem menu) {
            if(!adminToolsEnabled) { menu.DropDownItems.Clear();return; }
            PopulateAdminMenu(menu);
            PointF? target=pendingFullMapWaypointMapPoint;
            string command=target.HasValue?BuildMapTeleportCommand(target.Value):null;
            var teleport=new ToolStripMenuItem("Teleport here") {
                Enabled=fullMapActive && command!=null && !adminCommandBusy,
                ToolTipText=command==null?"Right-click inside the map.":"Send "+command+". Map height is unavailable; Z is 0."
            };
            teleport.Click+=(s,e)=> {
                if(!fullMapActive || command==null || closing)return;
                // Close the context menu before changing game focus or sending keys.
                overlay.CloseAdminContextMenu();
                try { BeginInvoke(new Action(async()=> { if(fullMapActive && !closing)await RunAdminCommand(command,null); })); }
                catch(InvalidOperationException) {}
            };
            menu.DropDownItems.Insert(0,teleport);
            menu.DropDownItems.Insert(1,new ToolStripSeparator());
        }
        internal void PopulateAdminMenu(ToolStripMenuItem menu) {
            while(menu.DropDownItems.Count>0) {var item=menu.DropDownItems[0];menu.DropDownItems.RemoveAt(0);item.Dispose();}
            List<AdminCommand> commands;
            try { commands=AdminCommand.Load(Path.Combine(dataFolder,"admin-commands.tsv")); }
            catch(Exception ex) { menu.DropDownItems.Add("Cannot load commands: "+ex.Message).Enabled=false;return; }
            foreach(var command in commands) {
                bool? state=LastAdminToggleState(command);
                var item=new ToolStripMenuItem(AdminCommandLabel(command)) { Enabled=!adminCommandBusy,
                    ToolTipText=command.IsBoolean?"Last submitted request; live SCUM state is unavailable. Choose ON or OFF.":"Click to send "+command.Build() };
                if(command.IsBoolean) {
                    foreach(bool enabled in new[]{true,false}) {
                        bool requested=enabled;
                        var choice=new ToolStripMenuItem(enabled?"ON":"OFF") { Checked=state.HasValue && state.Value==enabled,ToolTipText="Send "+command.Build(enabled) };
                        choice.Click+=(s,e)=>QueueAdminCommand(command,requested);
                        item.DropDownItems.Add(choice);
                    }
                } else item.Click+=(s,e)=>QueueAdminCommand(command,null);
                menu.DropDownItems.Add(item);
            }
            menu.DropDownItems.Add(new ToolStripSeparator());
            var spawnCart=new ToolStripMenuItem(Localization.Get("SpawnCartOpen")) { Enabled=!adminCommandBusy && !relativeTeleportBusy };
            spawnCart.Click+=(s,e)=>BeginInvoke(new Action(ShowSpawnCart));menu.DropDownItems.Add(spawnCart);
            var players=new ToolStripMenuItem(Localization.Get("PlayersTitle")) { Enabled=!adminCommandBusy && !relativeTeleportBusy };
            players.Click+=(s,e)=>BeginInvoke(new Action(ShowAdminPlayers));menu.DropDownItems.Add(players);
            var teleportPanel=new ToolStripMenuItem(Localization.Get("AdminTeleportMenu")) { Enabled=!adminCommandBusy && !relativeTeleportBusy };
            teleportPanel.Click+=(s,e)=>BeginInvoke(new Action(()=>ShowAdminControlsSection(true)));menu.DropDownItems.Add(teleportPanel);
            var editPanel=new ToolStripMenuItem("Edit commands...") { Enabled=!adminCommandBusy && !relativeTeleportBusy };
            editPanel.Click+=(s,e)=>BeginInvoke(new Action(ShowAdminCommands));menu.DropDownItems.Add(editPanel);
        }
        void ShowAdminDropdown(Rectangle anchor) {
            Native.ReleaseCapture();Cursor.Clip=Rectangle.Empty;
            var menu=new ContextMenuStrip();var root=new ToolStripMenuItem("Admin commands");PopulateAdminMenu(root);
            while(root.DropDownItems.Count>0) {var item=root.DropDownItems[0];root.DropDownItems.RemoveAt(0);menu.Items.Add(item);}
            root.Dispose();OverlayTheme.Menu(menu);
            menu.Closed+=(s,e)=>BeginInvoke(new Action(menu.Dispose));
            menu.Show(overlay,anchor.Left,anchor.Bottom);
        }
        void DismissAdminControls() {
            if(adminCommandsPanel==null || adminCommandsPanel.IsDisposed)return;
            adminControlsCommandReturn=false;
            bool returnFocus=adminCommandsPanel.ContainsFocus;
            // Closing the controls returns to the attached tab; the same form and
            // its command/teleport choices remain available for the next click.
            adminPanelDismissedForFullMap=false;
            SetAdminControlsExpanded(false);
            AdminControlsMapModeChanged();
            if(returnFocus && !diagnosticMode) {
                IntPtr game=Native.AdminGameWindow();
                if(game!=IntPtr.Zero)Native.SetForegroundWindow(game);
            }
        }
        internal void AdminControlsMapModeChanged() {
            SyncFullMapAdminControls(diagnosticMode || Native.GameFocused() || Native.IsOurWindow(Native.GetForegroundWindow()),
                chat.Paused || inventoryInputLocked || panelOpening || searchOpen || SettingsVisible);
        }
        internal void SyncFullMapAdminControls(bool focused,bool inputSuppressed) {
            // Dispatch owns the hide/focus sequence. Timer-driven TopMost changes
            // must not reorder an expanded palette or its popup mid-handoff.
            if(adminCommandBusy && adminFocusHandoffPending)return;
            if(!adminToolsEnabled) { if(adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed)adminCommandsPanel.Hide();return; }
            if(ToolWindowVisible) {
                if(adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed && adminCommandsPanel.Visible)adminCommandsPanel.Hide();
                return;
            }
            inputSuppressed=inputSuppressed || panelOpening || shortcutCaptureOpen || ToolWindowVisible;
            if((adminCommandsPanel==null || adminCommandsPanel.IsDisposed) && fullMapActive && overlay.Visible && !closing && !adminCommandBusy && !relativeTeleportBusy)
                ShowAdminControls(false,true);
            if(adminCommandsPanel==null || adminCommandsPanel.IsDisposed)return;
            if(!adminControlsCommandReturn || fullMapActive)PositionFullMapAdminControls();
            bool visible=(adminControlsCommandReturn || (fullMapActive && overlay.Visible && !overlay.FullMapClickThrough)) && focused && !inputSuppressed
                && adminPanelFitsBesideMap && !adminCommandBusy && !relativeTeleportBusy && !closing;
            // Reapplying TopMost reorders this window above its own popup menus.
            if(adminCommandsPanel.TopMost!=visible)adminCommandsPanel.TopMost=visible;
            if(visible && !adminCommandsPanel.Visible) {
                var palette=adminCommandsPanel as AdminControlsWindow;
                if(palette!=null)palette.SuppressActivation=true;
                adminCommandsPanel.Show();
            }
            else if(!visible && adminCommandsPanel.Visible)adminCommandsPanel.Hide();
        }
        void PositionFullMapAdminControls() {
            if(adminCommandsPanel==null || adminCommandsPanel.IsDisposed)return;
            Rectangle area=Screen.FromControl(overlay).WorkingArea;
            Rectangle bounds=OverlayWindow.AdminPanelBounds(area,overlay.Bounds,adminPanelExpanded?AdminControlsExpandedWidth:AdminControlsCollapsedWidth,
                adminPanelExpanded?660:AdminControlsCollapsedHeight,out adminPanelDockedLeft);
            adminPanelFitsBesideMap=!bounds.IsEmpty;
            if(adminPanelFitsBesideMap)adminCommandsPanel.Bounds=bounds;else adminCommandsPanel.Hide();
            var rail=adminCommandsPanel.Controls.Find("AdminControlsRail",false);
            if(rail.Length>0)rail[0].Dock=adminPanelDockedLeft?DockStyle.Right:DockStyle.Left;
            var expand=adminCommandsPanel.Controls.Find("ExpandAdminControls",true);
            if(expand.Length>0)expand[0].Text=(adminPanelExpanded==adminPanelDockedLeft)?"\u203a":"\u2039";
        }
        internal void SetAdminControlsExpanded(bool expanded) {
            adminPanelExpanded=expanded;
            if(refreshAdminControlsLayout!=null && adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed)refreshAdminControlsLayout();
            PositionFullMapAdminControls();
        }
        internal void ShowAdminControlsSection(bool teleport) {
            ShowAdminControls(false,true,true);
            if(teleport)adminTeleportSectionExpanded=true;else adminCommandsSectionExpanded=true;
            SetAdminControlsExpanded(true);
            if(scrollAdminControlsSection!=null)scrollAdminControlsSection(teleport);
            FocusAdminControls(true);
        }
        void ShowAdminCommands() {
            ShowAdminControls(true,true,true);
            adminCommandsSectionExpanded=true;
            SetAdminControlsExpanded(true);
            if(scrollAdminControlsSection!=null)scrollAdminControlsSection(false);
        }
        internal void ShowAdminCommandsFromSettings() {
            if(closing || adminCommandBusy)return;
            adminPanelDismissedForFullMap=false;
            SaveSettings();
            ShowAdminCommands();
        }
        void FocusAdminControls(bool activate) {
            if(!activate || adminCommandsPanel==null || !adminCommandsPanel.Visible)return;
            adminCommandsPanel.BringToFront();
            // Diagnostic previews and regressions never take desktop/game focus.
            if(!diagnosticMode) { adminCommandsPanel.Activate();Native.ForceForeground(adminCommandsPanel.Handle); }
        }
        void ShowAdminControls(bool activate,bool attachToMap,bool userRequested=false) {
            if(!adminToolsEnabled || closing || adminCommandBusy || relativeTeleportBusy)return;
            if(!fullMapActive) { if(adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed)adminCommandsPanel.Hide();return; }
            attachToMap=true;
            adminPanelDismissedForFullMap=false;
            if(userRequested && !overlay.Visible)overlay.Show();
            if(adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed) {
                if(refreshAdminCommands!=null)refreshAdminCommands();
                var palette=adminCommandsPanel as AdminControlsWindow;
                if(palette!=null) { palette.FullMapPalette=attachToMap;palette.SuppressActivation=!activate || diagnosticMode; }
                adminCommandsPanel.TopMost=true;
                adminCommandsPanel.WindowState=FormWindowState.Normal;
                PositionFullMapAdminControls();
                if(userRequested && overlay.Visible && adminPanelFitsBesideMap)adminCommandsPanel.Show();
                FocusAdminControls(activate);
                return;
            }
            if(closing || adminCommandBusy)return;
            string path=Path.Combine(dataFolder,"admin-commands.tsv");
            List<AdminCommand> commands;
            try { commands=AdminCommand.Load(path); } catch(Exception ex) { RunUiModal(() => MessageBox.Show(this,"Cannot load admin buttons: "+ex.Message));return; }
            var form=new AdminControlsWindow { Text=Localization.Get("AdminFloatingTitle"),ClientSize=new Size(AdminControlsCollapsedWidth,AdminControlsCollapsedHeight),StartPosition=FormStartPosition.Manual,
                ShowInTaskbar=false,TopMost=true,FullMapPalette=attachToMap,SuppressActivation=!activate || diagnosticMode };
            adminCommandsPanel=form;
            Disposed+=(s,e)=>form.Dispose();
            form.FormClosing+=(s,e)=> {
                if(e.CloseReason==CloseReason.UserClosing && !closing) {
                    e.Cancel=true;DismissAdminControls();
                }
            };
            var help=new Label { Name="AdminControlsHelp",Dock=DockStyle.Top,Height=70,Padding=new Padding(0,6,0,8),Text=Localization.Get("AdminFloatingHelp") };
            var teleport=new SidebarSection { Name="RelativeTeleport",AccessibleName=Localization.Get("AdminRelativeTeleportTitle"),Dock=DockStyle.Top,Width=240,Height=304,Padding=Padding.Empty };
            var teleportHelp=new Label { Name="RelativeTeleportHelp",Left=10,Top=10,Width=220,Height=52,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Text=Localization.Get("AdminRelativeTeleportHelp") };
            var distanceLabel=new Label { Left=10,Top=74,Width=120,Height=24,TextAlign=ContentAlignment.MiddleLeft,Text=Localization.Get("AdminTeleportDistance") };
            var distance=new SidebarDistanceInput { Name="RelativeTeleportDistance",Left=134,Top=74,Width=96,Height=24,DecimalPlaces=1,Increment=1,
                Minimum=(decimal)RelativeTeleport.MinimumDistanceMeters,Maximum=(decimal)RelativeTeleport.MaximumDistanceMeters,Value=(decimal)relativeTeleportDistanceMeters };
            var directions=new List<RadioButton>();
            string[] directionKeys={"AdminTeleportUp","AdminTeleportDown","AdminTeleportLeft","AdminTeleportRight","AdminTeleportForward","AdminTeleportBack"};
            var directionValues=new[]{TeleportDirection.Up,TeleportDirection.Down,TeleportDirection.Left,TeleportDirection.Right,TeleportDirection.Forward,TeleportDirection.Back};
            for(int i=0;i<directionValues.Length;i++) {
                TeleportDirection direction=directionValues[i];
                var choice=new SidebarRadioButton { Name="RelativeTeleport"+direction,Left=10+(i%3)*74,Top=110+(i/3)*30,Width=70,Height=26,
                    Appearance=Appearance.Button,FlatStyle=FlatStyle.Flat,TextAlign=ContentAlignment.MiddleCenter,Text=Localization.Get(directionKeys[i]),Checked=relativeTeleportDirection==direction };
                choice.FlatAppearance.CheckedBackColor=FullMapSidebarStyle.SelectedSurface;
                choice.FlatAppearance.MouseOverBackColor=FullMapSidebarStyle.ButtonSurface;
                choice.Cursor=Cursors.Hand;
                choice.CheckedChanged+=(s,e)=> { if(choice.Checked)relativeTeleportDirection=direction; };
                directions.Add(choice);teleport.Controls.Add(choice);
            }
            var teleportSource=new Label { Name="RelativeTeleportSource",Left=10,Top=177,Width=220,Height=44,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            var teleportPreview=new TextBox { Name="RelativeTeleportPreview",Left=10,Top=231,Width=220,Height=34,ReadOnly=true,Multiline=true,BorderStyle=BorderStyle.None,
                Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,
                Font=new Font("Consolas",8),AccessibleName=Localization.Get("AdminTeleportPreview") };
            var sendTeleport=new SidebarButton { Name="SendRelativeTeleport",Left=10,Top=274,Width=220,Height=28,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Text=Localization.Get("AdminTeleportSend") };
            teleport.Controls.AddRange(new Control[]{teleportHelp,distanceLabel,distance,teleportSource,teleportPreview,sendTeleport});
            bool arrangingTeleport=false;
            Action arrangeTeleport=()=> {
                if(arrangingTeleport || teleport.ClientSize.Width<180)return;
                arrangingTeleport=true;
                try {
                    int width=teleport.ClientSize.Width,usable=width-20;
                    teleportHelp.SetBounds(10,10,usable,Math.Max(52,TextRenderer.MeasureText(teleportHelp.Text,teleportHelp.Font,new Size(usable,1000),TextFormatFlags.WordBreak).Height));
                    int y=teleportHelp.Bottom+12;
                    distanceLabel.SetBounds(10,y,Math.Max(40,usable-100),24);distance.SetBounds(width-106,y,96,24);y+=36;
                    int columns=usable>=210?3:2,cellWidth=(usable-(columns-1)*4)/columns;
                    for(int index=0;index<directions.Count;index++)directions[index].SetBounds(10+(index%columns)*(cellWidth+4),y+(index/columns)*30,cellWidth,26);
                    y+=((directions.Count+columns-1)/columns)*30+7;
                    teleportSource.SetBounds(10,y,usable,Math.Max(44,TextRenderer.MeasureText(teleportSource.Text,teleportSource.Font,new Size(usable,1000),TextFormatFlags.WordBreak).Height));
                    y=teleportSource.Bottom+10;
                    teleportPreview.SetBounds(10,y,usable,Math.Max(34,TextRenderer.MeasureText(teleportPreview.Text,teleportPreview.Font,new Size(usable,1000),TextFormatFlags.WordBreak).Height+8));
                    sendTeleport.SetBounds(10,teleportPreview.Bottom+8,usable,28);teleport.Height=sendTeleport.Bottom+10;
                } finally { arrangingTeleport=false; }
            };
            teleport.SizeChanged+=(s,e)=>arrangeTeleport();teleportSource.TextChanged+=(s,e)=>arrangeTeleport();teleportPreview.TextChanged+=(s,e)=>arrangeTeleport();
            Action refreshTeleport=()=> {
                string problem=RelativeTeleportProblem(relativeTeleportDirection,relativeTeleportDistanceMeters);
                string preview=BuildRelativeTeleportCommand(relativeTeleportDirection,relativeTeleportDistanceMeters)??"";
                if(teleportPreview.Text!=preview)teleportPreview.Text=preview;
                string source=problem;
                if(source==null) {
                    int seconds=(int)Math.Min(int.MaxValue,Math.Max(0,(DateTime.UtcNow-updated).TotalSeconds));
                    source=Localization.T("AdminTeleportSource",position.X.ToString("0.###",CultureInfo.InvariantCulture),position.Y.ToString("0.###",CultureInfo.InvariantCulture),
                        position.Z.ToString("0.###",CultureInfo.InvariantCulture),Localization.T("SecondsAgo",seconds));
                }
                if(teleportSource.Text!=source)teleportSource.Text=source;
                sendTeleport.Enabled=problem==null;
                distance.Enabled=!adminCommandBusy && !relativeTeleportBusy;
                foreach(var choice in directions) {
                    choice.Enabled=distance.Enabled;
                    choice.ForeColor=choice.Checked?OverlayTheme.AccentHover:FullMapSidebarStyle.Secondary;
                    choice.FlatAppearance.BorderColor=choice.Checked?OverlayTheme.Accent:FullMapSidebarStyle.Border;
                }
            };
            distance.ValueChanged+=(s,e)=> { relativeTeleportDistanceMeters=(double)distance.Value;refreshTeleport(); };
            foreach(var choice in directions)choice.CheckedChanged+=(s,e)=>refreshTeleport();
            sendTeleport.Click+=async(s,e)=> {
                // Rebuild at click time, so the command always uses the latest accepted sample.
                string problem=RelativeTeleportProblem(relativeTeleportDirection,relativeTeleportDistanceMeters);
                if(problem!=null) { RunUiModal(() => MessageBox.Show(form,problem));return; }
                await SubmitRelativeTeleport(relativeTeleportDirection,relativeTeleportDistanceMeters,text=>RunAdminCommand(text,form));
            };
            var teleportTimer=new System.Windows.Forms.Timer { Interval=250 };
            teleportTimer.Tick+=(s,e)=> { if(form.Visible && !closing)refreshTeleport(); };
            form.Disposed+=(s,e)=>teleportTimer.Dispose();
            var list=new FlowLayoutPanel { Name="AdminCommandCards",Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,
                FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=Padding.Empty };
            var footer=new Panel { Name="AdminControlsFooter",Dock=DockStyle.Bottom,Height=48,Padding=new Padding(10,6,10,6) };
            footer.Controls.Add(new Label { Text=Localization.Get("AdminFloatingHint"),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft });
            var commandSection=new SidebarSection { Name="AdminCommandsSection",Dock=DockStyle.Top,Width=240,Padding=Padding.Empty,AccessibleName=Localization.Get("AdminCommandsSection") };
            // Establish the same 240px baseline before right-anchored children
            // are added. A default Panel width would create a negative margin.
            var addBar=new Panel { Name="AdminCommandActions",Dock=DockStyle.Top,Width=240,Height=116 };
            var add=new SidebarButton { Name="AddAdminCommand",Text=Localization.Get("AdminAddCommand"),Left=10,Top=6,Width=220,Height=28,
                Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };addBar.Controls.Add(add);
            var spawnItems=new SidebarButton { Name="OpenSpawnCart",Text=Localization.Get("SpawnCartOpen"),Left=10,Top=42,Width=220,Height=28,
                Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            spawnItems.Click+=(s,e)=>ShowSpawnCart();addBar.Controls.Add(spawnItems);
            var playersButton=new SidebarButton { Name="OpenAdminPlayers",Text=Localization.Get("PlayersTitle"),Left=10,Top=78,Width=220,Height=28,
                Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            playersButton.Click+=(s,e)=>RequestAdminPlayers(form);addBar.Controls.Add(playersButton);
            commandSection.Controls.Add(list);commandSection.Controls.Add(addBar);
            Action rebuild=null;
            bool commandEditorPending=false;
            Action<AdminCommand,bool> requestEdit=null;
            Func<AdminCommand,bool> edit=command=> {
                using(var dialog=new AdminCommandEditor(command)) {
                    // RunUiModal hides the palette; a hidden palette must not own
                    // the editor's focus and lifetime during close/cancel.
                    if(RunUiModal(() => SettingsVisible?dialog.ShowDialog(this):dialog.ShowDialog())!=DialogResult.OK)return false;
                    var saved=dialog.SavedCommand;command.Name=saved.Name;command.Command=saved.Command;
                    command.IsBoolean=saved.IsBoolean;command.ToggleNumeric=saved.ToggleNumeric;command.NumericMode=saved.NumericMode;command.Quantity=saved.Quantity;command.ExtraStrings=saved.ExtraStrings;return true;
                }
            };
            Action persist=()=> { try { AdminCommand.Save(path,commands); } catch(Exception ex) { RunUiModal(() => MessageBox.Show(form,"Could not save buttons: "+ex.Message)); }rebuild(); };
            requestEdit=(command,adding)=> {
                if(commandEditorPending || closing || form.IsDisposed || adminCommandBusy || relativeTeleportBusy)return;
                commandEditorPending=true;
                // Finish the native button's mouse-up event before hiding its
                // palette or rebuilding/disposal of the clicked command card.
                form.BeginInvoke(new Action(()=> {
                    bool returnControls=adminControlsCommandReturn;
                    try {
                        if(closing || form.IsDisposed)return;
                        if(edit(command)) { if(adding)commands.Add(command);persist(); }
                    } catch(Exception error) {
                        Program.LogException("AdminCommandEditor",error);
                        if(!closing && !form.IsDisposed)RunUiModal(()=>MessageBox.Show(SettingsVisible?this:null,"Could not edit command: "+error.Message));
                    } finally {
                        commandEditorPending=false;
                        adminControlsCommandReturn=returnControls;
                        if(!closing && !form.IsDisposed)AdminControlsMapModeChanged();
                    }
                }));
            };
            rebuild=()=> {
                DisposeChildControls(list);list.Controls.Clear();
                foreach(var command in commands) {
                    var row=new Panel { Name="AdminCommandCard",Width=240,Height=88,Margin=Padding.Empty };
                    row.Paint+=(s,e)=> { using(var border=new Pen(FullMapSidebarStyle.Divider))e.Graphics.DrawLine(border,10,row.Height-4,row.Width-10,row.Height-4); };
                    var send=new SidebarButton { Text=AdminCommandLabel(command),Left=10,Top=4,Width=220,Height=28,
                        Pill=command.IsBoolean,Selected=LastAdminToggleState(command)==true,
                        Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,AutoEllipsis=true,Enabled=!adminCommandBusy && !relativeTeleportBusy };
                    TextBox numericInput=null;
                    Label commandPreview=null;
                    send.Click+=async(s,e)=> {
                        if(adminCommandBusy || relativeTeleportBusy || commandEditorPending || closing)return;
                        if(numericInput!=null) {
                            string previous=command.Quantity;command.Quantity=numericInput.Text.Trim();
                            string error=command.ValidationError();
                            if(error!=null) { command.Quantity=previous;commandPreview.Text=error;return; }
                            try { AdminCommand.Save(path,commands); }
                            catch(Exception failure) { command.Quantity=previous;commandPreview.Text="Could not save value: "+failure.Message;return; }
                        }
                        await SubmitAdminCommand(command,null,text=>RunAdminCommand(text,form));
                    };
                    var change=new SidebarButton { Name="EditAdminCommand",Text=Localization.Get("AdminEditCommand"),Left=112,Top=38,Width=48,Height=22,
                        Anchor=AnchorStyles.Top|AnchorStyles.Right,AutoEllipsis=true };change.Click+=(s,e)=>requestEdit(command,false);
                    var remove=new SidebarButton { Name="RemoveAdminCommand",Text=Localization.Get("AdminRemoveCommand"),Left=166,Top=38,Width=64,Height=22,
                        Anchor=AnchorStyles.Top|AnchorStyles.Right,AutoEllipsis=true };remove.Click+=(s,e)=>{ commands.Remove(command);persist(); };
                    row.Controls.Add(send);
                    if(command.NumericMode) {
                        numericInput=new TextBox {Name="AdminNumericValue",Left=10,Top=38,Width=88,Height=22,Text=command.Quantity,MaxLength=32};
                        row.Controls.Add(numericInput);
                    }
                    if(command.IsBoolean)foreach(bool enabled in new[]{true,false}) {
                        bool requested=enabled;
                        var choice=new SidebarButton { Text=enabled?"ON":"OFF",Left=enabled?10:54,Top=38,Width=38,Height=22,
                            Selected=LastAdminToggleState(command)==enabled,Enabled=!adminCommandBusy && !relativeTeleportBusy };
                        choice.Click+=async(s,e)=>await SubmitAdminCommand(command,requested,text=>RunAdminCommand(text,form));
                        row.Controls.Add(choice);
                    }
                    row.Controls.Add(change);row.Controls.Add(remove);
                    commandPreview=new Label { Text=command.IsBoolean?command.ToggleBaseCommand()+(command.ToggleNumeric?" 1/0":" true/false")+(command.Quantity.Length>0?" "+command.Quantity:"")+(command.ExtraStrings.Length>0?" "+command.ExtraStrings:""):command.Build(),
                        Left=10,Top=66,Width=220,Height=16,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right,Font=new Font("Consolas",7.5f),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,ForeColor=FullMapSidebarStyle.Muted };
                    row.Controls.Add(commandPreview);
                    if(numericInput!=null)numericInput.TextChanged+=(s,e)=> {
                        var candidate=new AdminCommand(command.Name,command.Command) {NumericMode=true,Quantity=numericInput.Text.Trim(),ExtraStrings=command.ExtraStrings};
                        string error=candidate.ValidationError();commandPreview.Text=error??candidate.Build();
                        send.Enabled=error==null && !adminCommandBusy && !relativeTeleportBusy;
                    };
                    bool arrangingRow=false;
                    Action arrangeRow=()=> {
                        if(arrangingRow || row.ClientSize.Width<180)return;
                        arrangingRow=true;
                        try {
                            bool stack=(command.IsBoolean || command.NumericMode) && row.ClientSize.Width<240;
                            send.SetBounds(10,4,row.ClientSize.Width-20,28);
                            change.SetBounds(row.ClientSize.Width-128,stack?68:38,48,22);
                            remove.SetBounds(row.ClientSize.Width-74,stack?68:38,64,22);
                            commandPreview.SetBounds(10,stack?96:66,row.ClientSize.Width-20,16);
                            if(numericInput!=null)numericInput.SetBounds(10,38,stack?row.ClientSize.Width-20:Math.Max(50,row.ClientSize.Width-148),22);
                            row.Height=stack?118:88;
                        } finally { arrangingRow=false; }
                    };
                    row.SizeChanged+=(s,e)=>arrangeRow();
                    row.Width=Math.Max(180,list.ClientSize.Width);arrangeRow();list.Controls.Add(row);
                }
                StyleAdminSidebar(list);
                foreach(Control row in list.Controls) {
                    foreach(Control child in row.Controls)if(child is Label) { child.BackColor=Color.Transparent;child.ForeColor=FullMapSidebarStyle.Muted; }
                }
                commandSection.Height=addBar.Height+list.PreferredSize.Height;
            };
            bool arrangingRows=false;
            list.SizeChanged+=(s,e)=> {
                if(arrangingRows)return;arrangingRows=true;
                try {
                    foreach(Control row in list.Controls)row.Width=Math.Max(180,list.ClientSize.Width);
                    commandSection.Height=addBar.Height+list.PreferredSize.Height;
                } finally { arrangingRows=false; }
            };
            add.Click+=(s,e)=>requestEdit(new AdminCommand("","#"),true);
            refreshAdminCommands=()=> { rebuild();refreshTeleport();spawnItems.Enabled=playersButton.Enabled=!adminCommandBusy && !relativeTeleportBusy; };
            var body=new SidebarScrollPanel { Name="AdminControlsBody",Dock=DockStyle.Fill,MinimumContentWidth=180,Padding=new Padding(10,0,10,0) };
            var commandHeader=new SidebarButton { Name="AdminCommandsHeader",Dock=DockStyle.Top,Height=26,SectionHeader=true };
            var teleportHeader=new SidebarButton { Name="AdminTeleportHeader",Dock=DockStyle.Top,Height=26,SectionHeader=true };
            body.Content.Controls.Add(teleport);body.Content.Controls.Add(teleportHeader);body.Content.Controls.Add(commandSection);
            body.Content.Controls.Add(commandHeader);body.Content.Controls.Add(help);
            var content=new Panel { Dock=DockStyle.Fill,Visible=false };
            var contentHeader=new Label { Name="AdminControlsTitle",Dock=DockStyle.Top,Height=38,Padding=new Padding(10,0,8,0),Text=Localization.Get("AdminFloatingTitle").ToUpperInvariant(),
                Font=new Font("Segoe UI",9.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft };
            content.Controls.Add(body);content.Controls.Add(footer);content.Controls.Add(contentHeader);
            var rail=new Panel { Name="AdminControlsRail",Dock=DockStyle.Right,Width=AdminControlsCollapsedWidth-2 };
            var header=new Panel { Name="AdminControlsHeader",Dock=DockStyle.Top,Height=112 };
            var caption=new Label { Name="AdminRailCaption",Left=2,Top=34,Width=22,Height=72,AccessibleName=Localization.Get("AdminRailTitle"),Font=new Font("Segoe UI",8,FontStyle.Bold) };
            caption.Paint+=(s,e)=> {
                var state=e.Graphics.Save();
                e.Graphics.TranslateTransform(caption.Width/2f,caption.Height/2f);e.Graphics.RotateTransform(-90);
                using(var brush=new SolidBrush(OverlayTheme.Accent))using(var format=new StringFormat { Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center })
                    e.Graphics.DrawString(Localization.Get("AdminRailTitle").ToUpperInvariant(),caption.Font,brush,new RectangleF(-caption.Height/2f,-caption.Width/2f,caption.Height,caption.Width),format);
                e.Graphics.Restore(state);
            };
            var close=new SidebarButton { Name="CloseAdminControls",Text="\u00d7",Left=3,Top=5,Width=20,Height=22,TabStop=false,AccessibleName=Localization.Get("AdminCollapse") };
            var expand=new SidebarButton { Name="ExpandAdminControls",Left=3,Top=5,Width=20,Height=24,TabStop=false,Font=new Font("Segoe UI",11,FontStyle.Bold) };
            close.Click+=(s,e)=>DismissAdminControls();
            expand.Click+=(s,e)=>SetAdminControlsExpanded(!adminPanelExpanded);
            caption.Click+=(s,e)=>SetAdminControlsExpanded(!adminPanelExpanded);
            caption.Cursor=Cursors.Hand;
            header.Controls.AddRange(new Control[]{caption,close,expand});
            var commandToggle=new SidebarButton { Name="AdminCommandSectionToggle",Text="\u2261",Left=3,Top=112,Width=20,Height=26,
                Font=new Font("Segoe UI",12,FontStyle.Bold),AccessibleName=Localization.Get("AdminCommandsSection"),TabStop=false };
            var teleportToggle=new SidebarButton { Name="AdminTeleportSectionToggle",Text="\u2191",Left=3,Top=144,Width=20,Height=26,
                Font=new Font("Segoe UI",12,FontStyle.Bold),AccessibleName=Localization.Get("AdminTeleportSection"),TabStop=false };
            rail.Controls.AddRange(new Control[]{commandToggle,teleportToggle,header});
            form.Controls.Add(content);form.Controls.Add(rail);
            form.FormBorderStyle=FormBorderStyle.None;form.Padding=new Padding(1);form.Font=new Font("Segoe UI",8.25f);
            StyleAdminSidebar(form);
            caption.ForeColor=contentHeader.ForeColor=OverlayTheme.Accent;
            help.ForeColor=FullMapSidebarStyle.Secondary;
            footer.Controls[0].ForeColor=FullMapSidebarStyle.Secondary;
            help.Height=Math.Max(help.Height,TextRenderer.MeasureText(help.Text,help.Font,new Size(236,1000),TextFormatFlags.WordBreak).Height+help.Padding.Vertical);
            int teleportHelpHeight=Math.Max(teleportHelp.Height,TextRenderer.MeasureText(teleportHelp.Text,teleportHelp.Font,new Size(216,1000),TextFormatFlags.WordBreak).Height);
            int teleportHelpGrowth=teleportHelpHeight-teleportHelp.Height;
            teleportHelp.Height=teleportHelpHeight;
            if(teleportHelpGrowth>0) {
                foreach(Control child in teleport.Controls)if(child!=teleportHelp)child.Top+=teleportHelpGrowth;
                teleport.Height+=teleportHelpGrowth;
            }
            arrangeTeleport();
            footer.Height=Math.Max(footer.Height,TextRenderer.MeasureText(footer.Controls[0].Text,footer.Controls[0].Font,new Size(236,1000),TextFormatFlags.WordBreak).Height+footer.Padding.Vertical);
            form.Paint+=(s,e)=> { using(var border=new Pen(FullMapSidebarStyle.Border))e.Graphics.DrawRectangle(border,0,0,form.ClientSize.Width-1,form.ClientSize.Height-1); };
            rail.Paint+=(s,e)=> { using(var border=new Pen(FullMapSidebarStyle.Divider))e.Graphics.DrawLine(border,0,0,0,rail.Height); };
            form.KeyPreview=true;
            form.KeyDown+=(s,e)=> { if(e.KeyCode==Keys.Escape) { DismissAdminControls();e.Handled=true;e.SuppressKeyPress=true; } };
            var tips=new ToolTip();
            tips.SetToolTip(close,Localization.Get("AdminCollapse"));
            tips.SetToolTip(caption,Localization.Get("AdminRailTitle"));
            tips.SetToolTip(commandToggle,Localization.Get("AdminCommandsSection"));
            tips.SetToolTip(teleportToggle,Localization.Get("AdminTeleportSection"));
            tips.SetToolTip(help,help.Text);tips.SetToolTip(teleportHelp,teleportHelp.Text);
            foreach(var choice in directions)tips.SetToolTip(choice,choice.Text);
            teleportSource.TextChanged+=(s,e)=>tips.SetToolTip(teleportSource,teleportSource.Text);
            form.Disposed+=(s,e)=>tips.Dispose();
            refreshAdminControlsLayout=()=> {
                form.SuspendLayout();body.SuspendLayout();
                content.Visible=body.Visible=footer.Visible=adminPanelExpanded;
                commandSection.Visible=adminPanelExpanded && adminCommandsSectionExpanded;
                teleport.Visible=adminPanelExpanded && adminTeleportSectionExpanded;
                close.Visible=commandToggle.Visible=teleportToggle.Visible=adminPanelExpanded;
                expand.Top=adminPanelExpanded?29:5;
                caption.Top=adminPanelExpanded?58:34;
                caption.Height=adminPanelExpanded?50:72;
                header.Height=adminPanelExpanded?112:AdminControlsCollapsedHeight;
                if(adminPanelExpanded) { commandToggle.Top=144;teleportToggle.Top=178; }
                form.ClientSize=new Size(adminPanelExpanded?AdminControlsExpandedWidth:AdminControlsCollapsedWidth,adminPanelExpanded?660:AdminControlsCollapsedHeight);
                expand.Text=(adminPanelExpanded==adminPanelDockedLeft)?"\u203a":"\u2039";
                expand.AccessibleName=Localization.Get(adminPanelExpanded?"AdminCollapse":"AdminExpand");
                tips.SetToolTip(expand,expand.AccessibleName);
                commandHeader.Text=Localization.Get("AdminCommandsSection");commandHeader.Expanded=adminCommandsSectionExpanded;
                teleportHeader.Text=Localization.Get("AdminTeleportSection");teleportHeader.Expanded=adminTeleportSectionExpanded;
                commandToggle.Selected=adminCommandsSectionExpanded;teleportToggle.Selected=adminTeleportSectionExpanded;
                body.ResumeLayout(true);form.ResumeLayout(true);
                body.RefreshContentLayout();
            };
            scrollAdminControlsSection=teleportSection=>body.ScrollControlIntoView(teleportSection?teleport:commandSection);
            Action<bool> toggleSection=teleportSection=> {
                if(!adminPanelExpanded) { ShowAdminControlsSection(teleportSection);return; }
                if(teleportSection)adminTeleportSectionExpanded=!adminTeleportSectionExpanded;else adminCommandsSectionExpanded=!adminCommandsSectionExpanded;
                refreshAdminControlsLayout();
                PositionFullMapAdminControls();
                if(teleportSection?adminTeleportSectionExpanded:adminCommandsSectionExpanded)scrollAdminControlsSection(teleportSection);
            };
            commandToggle.Click+=(s,e)=>toggleSection(false);commandHeader.Click+=(s,e)=>toggleSection(false);
            teleportToggle.Click+=(s,e)=>toggleSection(true);teleportHeader.Click+=(s,e)=>toggleSection(true);
            refreshAdminCommands();refreshAdminControlsLayout();teleportTimer.Start();
            PositionFullMapAdminControls();
            if(userRequested && overlay.Visible && adminPanelFitsBesideMap)form.Show();
            FocusAdminControls(activate);
        }
        bool adminFocusHandoffPending;
        async Task<bool> RunAdminCommand(string command,Form panel) {
            if(!adminToolsEnabled || adminCommandBusy || closing || !AdminCommand.Valid(command))return false;
            SpawnBatchResult result=await RunAdminCommands(new List<string> { command },panel,null,true);
            return result.SubmittedCount>0;
        }
        internal Task<SpawnBatchResult> RunAdminSpawnCart(IList<string> commands,Form panel,Action<int> onSubmitted) {
            if(commands==null || commands.Count==0) {
                LogAdminCommandStatus("blocked","empty-cart",0,0,IntPtr.Zero);
                return Task.FromResult(new SpawnBatchResult { Error=Localization.Get("SpawnCartEmpty"),StopReason="empty-cart" });
            }
            var snapshot=new List<string>(commands);
            foreach(string command in snapshot) {
                if(!AdminCommand.Valid(command) || !command.StartsWith("#SpawnItem ",StringComparison.OrdinalIgnoreCase)) {
                    LogAdminCommandStatus("blocked","invalid-command",snapshot.Count,0,IntPtr.Zero);
                    return Task.FromResult(new SpawnBatchResult { Error=Localization.Get("SpawnCartInvalidCommand"),StopReason="invalid-command" });
                }
            }
            return RunAdminCommands(snapshot,panel,onSubmitted,false);
        }
        readonly object adminDiagnosticSync=new object();
        // Record gate state and dispatch counts, never command text or clipboard contents.
        internal void LogAdminCommandStatus(string stage,string gate,int commandCount,int submittedCount,IntPtr game,string nativeError=null) {
            if(adminDiagnosticSync==null)return;
            string logPath=string.IsNullOrEmpty(diagnosticPath)?Path.Combine(dataFolder,"admin-commands.log"):diagnosticPath;
            try {
                uint targetPid=0;
                if(game!=IntPtr.Zero)Native.GetWindowThreadProcessId(game,out targetPid);
                string failure=string.IsNullOrEmpty(nativeError)?"none":nativeError.Replace('\r',' ').Replace('\n',' ');
                if(failure.Length>256)failure=failure.Substring(0,256);
                string line=string.Format(CultureInfo.InvariantCulture,
                    "{0:o} [AdminCommand] stage={1} gate={2} count={3} submitted={4} targetpid={5} target={6} foreground_target={7} keyboard_monitor={8} chat={9} inventory={10} chatkey={11} chatscan={12} mapkey={13} mapscan={14} native_error={15}{16}",
                    DateTime.UtcNow,stage,gate,commandCount,submittedCount,targetPid,game.ToInt64(),
                    game!=IntPtr.Zero && Native.GetForegroundWindow()==game,keys!=null && keys.Available,chat!=null && chat.Paused,inventoryInputLocked,
                    scumChatKey,scumChatScanCode,scumMapKey,scumMapScanCode,failure,Environment.NewLine);
                lock(adminDiagnosticSync) {
                    if(!File.Exists(logPath) || new FileInfo(logPath).Length<8388608)File.AppendAllText(logPath,line);
                }
            }catch(Exception) { /* Diagnostics must never interrupt command handling. */ }
        }
        async Task<SpawnBatchResult> RunAdminCommands(IList<string> commands,Form panel,Action<int> onSubmitted,bool showFailures,bool capturePlayers=false,bool captureVehicles=false,string captureSquadId=null,Action<string> captureReportValidator=null) {
            if(captureReportValidator!=null && (capturePlayers || captureVehicles || captureSquadId!=null || commands.Count!=1 ||
                (commands[0]!="#DumpAllSquadsInfoList true" && !System.Text.RegularExpressions.Regex.IsMatch(commands[0],@"^#ListFlags [1-9]\d{0,2} true$"))))throw new ArgumentException("Bulk capture requires one approved squad or flag-list command.");
            bool captureReport=captureReportValidator!=null;
            if(capturePlayers && (commands.Count!=1 || commands[0]!="#ListPlayers true"))throw new ArgumentException("Player capture requires one ListPlayers command.");
            if(captureSquadId!=null && (!PlayerList.ValidId(captureSquadId) || capturePlayers || captureVehicles || commands.Count!=1 || commands[0]!="#SquadInfo "+captureSquadId))throw new ArgumentException("Squad capture requires one SquadInfo command for the selected player.");
            if(captureVehicles && (capturePlayers || commands.Count!=1 || commands[0]!="#ListSpawnedVehicles true"))throw new ArgumentException("Vehicle capture requires one ListSpawnedVehicles command.");
            var result=new SpawnBatchResult();
            LogAdminCommandStatus("attempt","none",commands.Count,0,IntPtr.Zero);
            if(!adminToolsEnabled) { result.StopReason="admin-disabled";result.Error=Localization.Get("AdminToolsEnabled");return result; }
            if(adminCommandBusy || closing) {
                result.StopReason=closing?"closing":"admin-busy";
                result.Error=Localization.Get("SpawnCartBusy");LogAdminCommandStatus("blocked",result.StopReason,commands.Count,0,IntPtr.Zero);return result;
            }
            if(!keys.Available) {
                result.StopReason="keyboard-monitor";
                result.Error=Localization.Get("SpawnCartMonitorUnavailable");
                LogAdminCommandStatus("blocked","keyboard-monitor",commands.Count,0,IntPtr.Zero);
                if(showFailures)RunUiModal(() => MessageBox.Show(panel??this,result.Error));return result;
            }
            if(chat.Paused || inventoryInputLocked) {
                result.StopReason=chat.Paused?"chat-open":"inventory-open";
                result.Error=Localization.Get("SpawnCartCloseChatInventory");
                LogAdminCommandStatus("blocked",chat.Paused?"chat-open":"inventory-open",commands.Count,0,IntPtr.Zero);
                if(showFailures)RunUiModal(() => MessageBox.Show(panel??this,result.Error));return result;
            }
            IntPtr game=Native.AdminGameWindow();
            if(game==IntPtr.Zero || (!Native.IsOurWindow(Native.GetForegroundWindow()) && Native.GetForegroundWindow()!=game)) {
                result.StopReason=game==IntPtr.Zero?"no-game-window":"unrelated-foreground";
                result.Error=Localization.Get(game==IntPtr.Zero?"SpawnCartGameUnavailable":"SpawnCartForegroundBlocked");
                LogAdminCommandStatus("blocked",game==IntPtr.Zero?"no-game-window":"unrelated-foreground",commands.Count,0,game);
                if(showFailures)RunUiModal(() => MessageBox.Show(panel??this,result.Error));return result;
            }
            var returnTools=new List<Form>();
            bool returnControls=panel!=null && Object.ReferenceEquals(panel,adminCommandsPanel) && panel.Visible;
            foreach(var tool in new[]{adminPlayersPanel,spawnCartDialog}.Concat(adminProfileWindows))
                if(tool!=null && !tool.IsDisposed && tool.Visible && !Object.ReferenceEquals(tool,panel))returnTools.Add(tool);
            if(panel!=null && !panel.IsDisposed && panel.Visible && !Object.ReferenceEquals(panel,adminCommandsPanel))returnTools.Add(panel);
            bool toolsHidden=false;
            adminCommandBusy=true;adminFocusHandoffPending=true;Native.CancelActiveCopy();pending=false;RefreshAdminCommandsPanel();
            IDataObject previous=null;uint commandSequence=0,clipboardLeaseSequence=0,replySequence=0;bool opened=false,mapCloseAccepted=false,commandEntered=false;
            IntPtr clipboardOwner=IntPtr.Zero;
            bool nativeClipboardPublication=false;
            string stoppedGate="none",nativeFailure=null,clipboardFailure=null;
            try {
                while(copyInProgress || Native.CopyInProgress) {
                    if(closing) { stoppedGate="closing";result.Error=Localization.Get("SpawnCartBusy");return result; }
                    await Task.Delay(10);
                }
                uint beforeCapture=Native.GetClipboardSequenceNumber();
                previous=await Native.CaptureClipboardAsync();
                if(closing || Native.GetClipboardSequenceNumber()!=beforeCapture || (!Native.IsOurWindow(Native.GetForegroundWindow()) && Native.GetForegroundWindow()!=game)) {
                    stoppedGate=closing?"closing":Native.GetClipboardSequenceNumber()!=beforeCapture?"clipboard-changed":"focus-changed";
                    result.Error=Localization.Get(Native.GetClipboardSequenceNumber()!=beforeCapture?"SpawnCartClipboardChanged":"SpawnCartFocusInputChanged");return result;
                }
                clipboardLeaseSequence=beforeCapture;
                int closeMap=fullMapActive?scumMapKey:0;
                if(!Native.IsGameWindow(game)) { stoppedGate="target-unavailable";result.Error=Localization.Get("SpawnCartFocusInputChanged");return result; }
                bool focusRequested=Native.SetForegroundWindow(game);
                LogAdminCommandStatus("focus-request",focusRequested?"accepted":"denied",commands.Count,0,game);
                toolsHidden=true;foreach(var tool in returnTools)tool.Hide();
                if(adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed)adminCommandsPanel.Hide();
                if(panel!=null && !panel.IsDisposed)panel.Hide();if(SettingsVisible)DismissSettings();
                adminFocusHandoffPending=false;
                string focusGate=await Native.AdminFocusReadiness(()=>Native.IsGameWindow(game),()=>Native.GetForegroundWindow()==game,
                    ()=>Native.IsOurWindow(Native.GetForegroundWindow()),Native.AdminKeysHeld,
                    ()=>closing || IsDisposed?"closing":!keys.Available?"keyboard-monitor":chat.Paused?"chat-open":inventoryInputLocked?"inventory-open":null,Task.Delay,
                    ()=>Native.GetForegroundWindow()==IntPtr.Zero,()=> {
                        IntPtr current=Native.GetForegroundWindow();
                        if(Native.IsGameWindow(game) && (current==IntPtr.Zero || Native.IsOurWindow(current)))Native.SetForegroundWindow(game);
                    });
                if(focusGate!=null) {
                    stoppedGate=focusGate;
                    result.Error=Localization.Get(focusGate=="chat-open" || focusGate=="inventory-open"?"SpawnCartCloseChatInventory":
                        focusGate=="keyboard-monitor"?"SpawnCartMonitorUnavailable":focusGate=="unrelated-foreground"?"SpawnCartForegroundBlocked":"SpawnCartFocusUnavailable");return result;
                }
                LogAdminCommandStatus("ready","none",commands.Count,0,game);
                long userInput=GameKeys.PhysicalInputRevision;
                Native.AdminInputInProgress=true;
                clipboardOwner=Handle;
                Func<bool> allowed=()=> {
                    stoppedGate=closing || IsDisposed?"closing":!keys.Available?"keyboard-monitor":inventoryInputLocked?"inventory-open":
                        GameKeys.PhysicalInputRevision!=userInput?"physical-input":Native.GetForegroundWindow()!=game?"focus-changed":!Native.IsGameWindow(game)?"target-unavailable":"none";
                    return stoppedGate=="none";
                };
                Func<bool> clipboardReady=()=> {
                    // Clipboard-report commands replace the clipboard after Enter.
                    // The command remains leased until dispatch; the response is
                    // then accepted only from the original SCUM process below.
                    if((capturePlayers || captureVehicles || captureSquadId!=null || captureReport) && commandEntered)return true;
                    if(Native.GetClipboardSequenceNumber()==commandSequence && (!nativeClipboardPublication || Native.AdminClipboardOwned(clipboardOwner,commandSequence)))return true;
                    stoppedGate="clipboard-changed";return false;
                };
                result=await Native.AdminChatBatchSequenceAsync(commands.Count,async index=> {
                    if(!allowed())return false;
                    if(Native.GetClipboardSequenceNumber()!=clipboardLeaseSequence) { stoppedGate="clipboard-changed";return false; }
                    // Error-dialog preferences must not select the clipboard transport.
                    // Use the owned, finalized publication for every supported command.
                    nativeClipboardPublication=Native.AdminClipboardTextSupported(commands[index]);
                    if(nativeClipboardPublication) {
                        AdminClipboardResult publication=await Native.AdminClipboardPreparation(
                            ()=>Native.PublishAdminClipboard(clipboardOwner,commands[index],clipboardLeaseSequence,index>0),
                            ()=> {
                                if(!allowed())return stoppedGate;
                                if(Native.GetClipboardSequenceNumber()!=clipboardLeaseSequence) { stoppedGate="clipboard-changed";return stoppedGate; }
                                return null;
                            },Task.Delay);
                        if(publication.Ready || publication.Sequence!=0)
                            publication=await Native.FinalizeAdminClipboard(clipboardOwner,commands[index],publication,
                                ()=>allowed()?null:stoppedGate);
                        if(publication.Sequence!=0)commandSequence=publication.Sequence;
                        else if(!Native.AdminClipboardOwned(clipboardOwner,commandSequence))commandSequence=0;
                        if(publication.NativeError!=0)clipboardFailure="clipboard error="+publication.NativeError.ToString(CultureInfo.InvariantCulture);
                        LogAdminCommandStatus("clipboard",publication.Ready?"ready":publication.StopReason,commands.Count,index,game,clipboardFailure);
                        if(!publication.Ready) { stoppedGate=publication.StopReason;return false; }
                        clipboardLeaseSequence=commandSequence;sequence=commandSequence;return true;
                    }
                    Clipboard.SetText(commands[index]);uint writtenSequence=Native.GetClipboardSequenceNumber();
                    if(Clipboard.GetText()!=commands[index] || Native.GetClipboardSequenceNumber()!=writtenSequence) {
                        stoppedGate="clipboard-verification";commandSequence=0;return false;
                    }
                    commandSequence=writtenSequence;
                    clipboardLeaseSequence=commandSequence;sequence=commandSequence;return true;
                },async(index,submitted)=> {
                    opened=false;
                    Native.CopyError="";
                    Func<Task<bool>> settledBeforeChat=async ()=> {
                        string gate=await Native.AdminMenuCloseReadiness(index==0 && mapCloseAccepted,
                            ()=>allowed() && clipboardReady()?null:stoppedGate,()=>chat.Paused,
                            ()=>Native.HasSelectedTopNavigationTab(game),Task.Delay);
                        if(gate==null)return true;
                        stoppedGate=gate;if(gate=="inventory-open")inventoryInputLocked=true;return false;
                    };
                    Action entered=()=> { commandEntered=true;submitted(); };
                    bool chatClosed=await Native.SendAdminChat(game,scumChatKey,scumChatScanCode,index==0?closeMap:0,scumMapScanCode,
                        ()=>allowed() && clipboardReady(),clipboardReady,
                        ()=> { opened=true;chat.Key(scumChatKey,scumChatKey,0,scumChatScanCode,scumChatScanCode);observedChatPaused=true; },entered,
                        null,
                        ()=> { mapCloseAccepted=true;SetFullMap(false); },
                        settledBeforeChat);
                    if(chatClosed) { chat.Reset();observedChatPaused=false; }
                    else if(!string.IsNullOrEmpty(Native.CopyError)) { nativeFailure=Native.CopyError;stoppedGate="native-input-rejected"; }
                    return chatClosed;
                },Task.Delay,allowed,onSubmitted);
                if((capturePlayers || captureVehicles || captureSquadId!=null || captureReport) && result.SubmittedCount==1) {
                    // No more input is injected here. A click or focus returning to
                    // our tool can stop chat cleanup without invalidating SCUM's
                    // fresh reply to the command already submitted.
                    Func<bool> captureAllowed=()=>!closing && !IsDisposed && Native.IsGameWindow(game) &&
                        (Native.GetForegroundWindow()==game || Native.IsOurWindow(Native.GetForegroundWindow()));
                    PlayerClipboardResponse response=await Native.WaitForSubmittedAdminReport(result,commandSequence,Native.GetClipboardSequenceNumber,
                        ()=>Native.GameOwnsClipboard(game),()=>Clipboard.GetText(),captureAllowed,Task.Delay,
                        captureReportValidator??(captureSquadId!=null?(Action<string>)(text=>ValidateSquadResponse(text,captureSquadId)):captureVehicles?(Action<string>)(text=> { if(AdminProfileParser.Parse(text,DateTime.UtcNow).Kind!="Vehicles")throw new FormatException(); }):null),
                        captureSquadId!=null || captureReport?15000:3000);
                    if(response!=null) { result.ClipboardResponse=response.Text;replySequence=response.Sequence; }
                    else { result.Completed=false;result.Error=Localization.Get(captureReport?"ProfileReportTimeout":captureSquadId!=null?"ProfileSquadTimeout":captureVehicles?"ProfileVehicleTimeout":"PlayersNoResponse");if(stoppedGate=="none")stoppedGate=captureReport?"report-response-timeout":captureSquadId!=null?"squad-response-timeout":captureVehicles?"vehicle-response-timeout":"player-response-timeout"; }
                }
                if(!result.Completed) {
                    string gateError=nativeFailure!=null?Localization.T("SpawnCartNativeInputRejected",nativeFailure):Native.AdminDispatchGateError(stoppedGate);
                    if(gateError!=null)result.Error=gateError+(opened && !result.ChatClosed?" "+Localization.Get(result.SubmittedCount>0?"SpawnCartCleanupStopped":"SpawnCartBeforeSubmitStopped"):"");
                }
            } catch(Exception ex) {
                stoppedGate="exception";Program.LogException("AdminCommand",ex);result.Error=Localization.T("SpawnCartException",ex.Message);
            }
            finally {
                Native.AdminInputInProgress=false;adminFocusHandoffPending=false;
                try { if((commandSequence!=0 && Native.GetClipboardSequenceNumber()==commandSequence && (!nativeClipboardPublication || Native.AdminClipboardOwned(clipboardOwner,commandSequence)))
                    || (replySequence!=0 && Native.GetClipboardSequenceNumber()==replySequence && Native.GameOwnsClipboard(game))) {
                    if(previous!=null)Clipboard.SetDataObject(previous,true,0,0);else Native.TryClearClipboard();
                } } catch(System.Runtime.InteropServices.ExternalException) {}
                sequence=Native.GetClipboardSequenceNumber();pending=false;resumeAfter=DateTime.UtcNow.AddMilliseconds(600);adminCommandBusy=false;
                if(toolsHidden && commands.Any(command=>command.StartsWith("#Teleport",StringComparison.OrdinalIgnoreCase)))RestoreFullMapOpacityAfterTeleport();
                if(result.SubmittedCount>0 && commands[0].StartsWith("#Teleport ",StringComparison.OrdinalIgnoreCase))lastTeleportSource=position;
                result.StopReason=result.Completed?"completed":stoppedGate=="none"?"chat-sequence":stoppedGate;
                LogAdminCommandStatus(result.Completed?"completed":"stopped",result.StopReason,commands.Count,result.SubmittedCount,game,nativeFailure??clipboardFailure);
                RefreshAdminCommandsPanel();
                FinishPlayerTeleport(panel,commands,result.SubmittedCount);
                if(toolsHidden && returnControls && !closing && adminToolsEnabled
                    && CanRestoreAdminTool(Native.GetForegroundWindow(),game,Native.IsOurWindow(Native.GetForegroundWindow())))
                    adminControlsCommandReturn=true;
                AdminControlsMapModeChanged();
                if(toolsHidden && returnControls && adminControlsCommandReturn)RestoreAdminTool(adminCommandsPanel,game);
                if(toolsHidden)foreach(var tool in returnTools)RestoreAdminTool(tool,game);
                if(showFailures && !result.ChatClosed && !closing && (panel==null || !panel.IsDisposed)
                    && (Native.GetForegroundWindow()==game || Native.IsOurWindow(Native.GetForegroundWindow()))) {
                    if(panel!=null && !Object.ReferenceEquals(panel,adminCommandsPanel) && !(playerToolsAwaitingReopen && IsPlayerTool(panel))) { panel.Show();if(!diagnosticMode)panel.Activate(); }
                    RunUiModal(() => MessageBox.Show(panel??overlay,(result.Error??Native.AdminDispatchGateError(result.StopReason)??Localization.Get("SpawnCartFocusUnavailable"))+" ["+result.StopReason+"]"));
                }
                note=showFailures?(result.SubmittedCount>0?(result.ChatClosed?"Admin command submitted; Escape sent to close chat. Check SCUM for the result.":"Admin command submitted; chat cleanup interrupted."):(result.Error??Localization.Get("SpawnCartFocusUnavailable"))+" ["+result.StopReason+"]"):
                    Localization.T("SpawnCartSubmittedStatus",result.SubmittedCount,commands.Count);
            }
            return result;
        }
    }
}
