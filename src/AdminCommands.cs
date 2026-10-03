using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {
    internal sealed class AdminCommand {
        internal string Name,Command;
        internal bool IsBoolean;
        internal string Quantity="",ExtraStrings="";
        internal string StateKey { get { return Build(false); } }
        internal AdminCommand(string name,string command,bool isBoolean=false) { Name=name; Command=command;IsBoolean=isBoolean; }
        internal string Build(bool enabled=false) {
            return (IsBoolean?BooleanBaseCommand(Command):Command)+(IsBoolean?(enabled?" true":" false"):"")+(string.IsNullOrWhiteSpace(Quantity)?"":" "+Quantity.Trim())+(string.IsNullOrWhiteSpace(ExtraStrings)?"":" "+ExtraStrings.Trim());
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
            if(string.IsNullOrWhiteSpace(Name) || Name.Length>80)return "Enter a label of up to 80 characters.";
            int quantity;
            if(Quantity.Length>0 && (!int.TryParse(Quantity,out quantity) || quantity<1))return "Quantity must be a positive whole number, or left empty.";
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
                string[] fields=line.Split('\t'); if(fields.Length!=2 && fields.Length!=5)throw new FormatException("Invalid saved admin button.");
                string name=Encoding.UTF8.GetString(Convert.FromBase64String(fields[0])),command=Encoding.UTF8.GetString(Convert.FromBase64String(fields[1]));
                if(string.IsNullOrWhiteSpace(name) || name.Length>80 || !Valid(command))throw new FormatException("Invalid saved admin button.");
                var entry=new AdminCommand(name,command);
                if(fields.Length==2) {legacy.Add(entry);continue;}
                if(fields[2]!="0" && fields[2]!="1")throw new FormatException("Invalid saved boolean setting.");
                entry.IsBoolean=fields[2]=="1";
                if(entry.IsBoolean)entry.Command=BooleanBaseCommand(entry.Command);
                entry.Quantity=Encoding.UTF8.GetString(Convert.FromBase64String(fields[3]));
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
                lines.Add(Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Name))+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(c.IsBoolean?BooleanBaseCommand(c.Command):c.Command))+"\t"+(c.IsBoolean?"1":"0")+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(c.Quantity))+"\t"+Convert.ToBase64String(Encoding.UTF8.GetBytes(c.ExtraStrings)));
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllLines(path+".tmp",lines);
            if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);
        }
    }

    internal sealed class AdminCommandEditor:Form {
        internal AdminCommand SavedCommand;
        internal AdminCommandEditor(AdminCommand command) {
            Text="Edit admin command";ClientSize=new Size(550,320);StartPosition=FormStartPosition.CenterParent;
            FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
            var label=new TextBox {Name="Label",Left=185,Top=16,Width=345,Text=command.Name,MaxLength=80};
            var value=new TextBox {Name="Command",Left=185,Top=56,Width=345,Text=command.Command,MaxLength=512};
            var boolean=new CheckBox {Name="IsBoolean",Left=185,Top=96,Width=345,Text="True/false command",Checked=command.IsBoolean};
            var quantity=new TextBox {Name="Quantity",Left=185,Top=136,Width=345,Text=command.Quantity,MaxLength=10};
            var extras=new TextBox {Name="ExtraStrings",Left=185,Top=176,Width=345,Text=command.ExtraStrings,MaxLength=512};
            string[] names={"Label","Command","Is boolean?","Quantity (optional)","Extra strings (optional)"};
            for(int i=0;i<names.Length;i++)Controls.Add(new Label {Left=12,Top=19+i*40,Width=170,Text=names[i]});
            Controls.AddRange(new Control[]{label,value,boolean,quantity,extras});
            var preview=new Label {Left=12,Top=215,Width=518,Height=55};Controls.Add(preview);
            Func<AdminCommand> read=()=>new AdminCommand(label.Text.Trim(),value.Text.Trim(),boolean.Checked) {Quantity=quantity.Text.Trim(),ExtraStrings=extras.Text.Trim()};
            Action refresh=()=>preview.Text="Preview: "+read().Build(true);
            foreach(var box in new[]{label,value,quantity,extras})box.TextChanged+=(s,e)=>refresh();boolean.CheckedChanged+=(s,e)=>refresh();refresh();
            var save=new Button {Name="Save",Left=330,Top=278,Width=95,Text="Save"};
            var cancel=new Button {Left=435,Top=278,Width=95,Text="Cancel",DialogResult=DialogResult.Cancel};
            Controls.Add(save);Controls.Add(cancel);AcceptButton=save;CancelButton=cancel;
            save.Click+=(s,e)=> {
                var entry=read();
                if(entry.IsBoolean)entry.Command=AdminCommand.BooleanBaseCommand(entry.Command);
                string error=entry.ValidationError();if(error!=null) {MessageBox.Show(this,error);return;}
                SavedCommand=entry;DialogResult=DialogResult.OK;
            };
            OverlayTheme.Style(this);
        }
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
        // All key-downs require the original SCUM window and no intervening physical input.
        // The lease releases only our injected keys, including on failure/focus loss.
        internal static async Task<bool> AdminChatSequence(Func<uint,bool,bool> send,Func<int,Task> delay,Func<bool> allowed,
            Func<bool> clipboardReady,Action opened,int chatKey,int mapKey,Action onSubmitted=null) {
            var lease=new CopyInputLease(send);
            Func<uint,Action,Task<bool>> tap=async (key,onPressed)=> {
                if(!allowed() || !lease.Send(key,false))return false;
                if(onPressed!=null)onPressed();
                if(key==(uint)chatKey)opened();
                await delay(35);return lease.Send(key,true) && allowed();
            };
            try {
                if(mapKey>0) { if(!await tap((uint)mapKey,null))return false;await delay(120); }
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
            Func<bool> allowed,Func<bool> clipboardReady,Action opened,Action onSubmitted=null) {
            uint pid;IntPtr layout=GetKeyboardLayout(GetWindowThreadProcessId(game,out pid));
            return AdminChatSequence((key,up)=>Key(key,up,(int)key==chatKey?chatScan:((int)key==mapKey?mapScan:0),layout),
                Task.Delay,()=>GetForegroundWindow()==game && IsGameWindow(game) && allowed(),clipboardReady,opened,chatKey,mapKey,onSubmitted);
        }
    }

    public sealed partial class MapWindow {
        bool adminCommandBusy;
        Form adminCommandsPanel;
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
            if(adminCommandBusy || closing)return false;
            bool nextState=requested??!LastAdminToggleState(command).GetValueOrDefault();
            string stateKey=command.StateKey;
            bool isBoolean=command.IsBoolean;
            if(!await send(command.Build(nextState)))return false;
            if(isBoolean)adminToggleStates[stateKey]=nextState;
            if(refreshAdminCommands!=null && adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed)refreshAdminCommands();
            return true;
        }
        void QueueAdminCommand(AdminCommand command,bool? requested) {
            // Dispatch after the native menus release focus and mouse capture.
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
            menu.DropDownItems.Add("Edit commands...",null,(s,e)=>BeginInvoke(new Action(ShowAdminCommands)));
        }
        void ShowAdminDropdown(Rectangle anchor) {
            var menu=new ContextMenuStrip();var root=new ToolStripMenuItem("Admin commands");PopulateAdminMenu(root);
            while(root.DropDownItems.Count>0) {var item=root.DropDownItems[0];root.DropDownItems.RemoveAt(0);menu.Items.Add(item);}
            root.Dispose();OverlayTheme.Menu(menu);
            menu.Closed+=(s,e)=>BeginInvoke(new Action(menu.Dispose));
            menu.Show(overlay,anchor.Left,anchor.Bottom);
        }
        void ShowAdminCommands() {
            if(adminCommandsPanel!=null && !adminCommandsPanel.IsDisposed) {
                if(refreshAdminCommands!=null)refreshAdminCommands();
                adminCommandsPanel.TopMost=true;
                adminCommandsPanel.WindowState=FormWindowState.Normal;
                adminCommandsPanel.Show();adminCommandsPanel.BringToFront();adminCommandsPanel.Activate();
                Native.ForceForeground(adminCommandsPanel.Handle);return;
            }
            if(closing || adminCommandBusy)return;
            string path=Path.Combine(dataFolder,"admin-commands.tsv");
            List<AdminCommand> commands;
            try { commands=AdminCommand.Load(path); } catch(Exception ex) { MessageBox.Show(this,"Cannot load admin buttons: "+ex.Message);return; }
            var form=new Form { Text="SCUM MiniMap — Admin commands",ClientSize=new Size(640,500),MinimumSize=new Size(600,420),StartPosition=FormStartPosition.Manual,ShowInTaskbar=true,TopMost=true };
            adminCommandsPanel=form;
            Disposed+=(s,e)=>form.Dispose();
            var help=new Label { Dock=DockStyle.Top,Height=96,Padding=new Padding(12),Text="For SCUM admins. Click a boolean command to toggle, or use ON/OFF to send an exact value. States are last submitted requests; UNKNOWN on launch. Check SCUM for the result. Other commands run once. Close chat and inventory first. Enter submits; Escape closes chat." };
            var list=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(10) };
            var footer=new FlowLayoutPanel { Dock=DockStyle.Bottom,Height=40 };
            var add=new Button { Text="Add command button",Width=160 };footer.Controls.Add(add);
            Action rebuild=null;
            Func<AdminCommand,bool> edit=command=> {
                using(var dialog=new AdminCommandEditor(command)) {
                    if(dialog.ShowDialog(form)!=DialogResult.OK)return false;
                    var saved=dialog.SavedCommand;command.Name=saved.Name;command.Command=saved.Command;
                    command.IsBoolean=saved.IsBoolean;command.Quantity=saved.Quantity;command.ExtraStrings=saved.ExtraStrings;return true;
                }
            };
            Action persist=()=> { try { AdminCommand.Save(path,commands); } catch(Exception ex) { MessageBox.Show(form,"Could not save buttons: "+ex.Message); }rebuild(); };
            rebuild=()=> {
                DisposeChildControls(list);list.Controls.Clear();
                foreach(var command in commands) {
                    var row=new FlowLayoutPanel { Width=585,Height=66 };
                    var send=new Button { Text=AdminCommandLabel(command),Width=command.IsBoolean?220:350,Height=28,Enabled=!adminCommandBusy };
                    send.Click+=async(s,e)=>await SubmitAdminCommand(command,null,text=>RunAdminCommand(text,form));
                    var change=new Button { Text="Edit",Width=75 };change.Click+=(s,e)=>{ if(edit(command))persist(); };
                    var remove=new Button { Text="Remove",Width=90 };remove.Click+=(s,e)=>{ commands.Remove(command);persist(); };
                    row.Controls.Add(send);
                    if(command.IsBoolean)foreach(bool enabled in new[]{true,false}) {
                        bool requested=enabled;
                        var choice=new Button { Text=enabled?"ON":"OFF",Width=60,Height=28,Enabled=!adminCommandBusy };
                        choice.Click+=async(s,e)=>await SubmitAdminCommand(command,requested,text=>RunAdminCommand(text,form));
                        row.Controls.Add(choice);
                    }
                    row.Controls.Add(change);row.Controls.Add(remove);
                    row.Controls.Add(new Label { Text=command.IsBoolean?command.Command+" true/false"+(command.Quantity.Length>0?" "+command.Quantity:"")+(command.ExtraStrings.Length>0?" "+command.ExtraStrings:""):command.Build(),Width=565,Height=25,ForeColor=OverlayTheme.InkMuted });list.Controls.Add(row);
                }
                OverlayTheme.Style(list);
            };
            add.Click+=(s,e)=> { var command=new AdminCommand("","#");if(edit(command)){commands.Add(command);persist();} };
            refreshAdminCommands=rebuild;
            form.Controls.Add(list);form.Controls.Add(help);form.Controls.Add(footer);OverlayTheme.Style(form);rebuild();
            OverlayTheme.Anchor(form,overlay);form.Show();form.BringToFront();form.Activate();Native.ForceForeground(form.Handle);
        }
        async Task<bool> RunAdminCommand(string command,Form panel) {
            if(adminCommandBusy || closing || !AdminCommand.Valid(command))return false;
            if(!keys.Available) { MessageBox.Show(panel??this,"Keyboard monitoring is unavailable. Restart MiniMap before sending commands.");return false; }
            if(chat.Paused || inventoryInputLocked) { MessageBox.Show(panel??this,"Close SCUM chat and inventory before sending an admin command.");return false; }
            IntPtr game=Native.AdminGameWindow();
            if(game==IntPtr.Zero || (!Native.IsOurWindow(Native.GetForegroundWindow()) && Native.GetForegroundWindow()!=game)) { MessageBox.Show(panel??this,"Open SCUM in windowed or borderless mode, then click the command again.");return false; }
            adminCommandBusy=true;Native.CancelActiveCopy();pending=false;
            IDataObject previous=null;uint commandSequence=0;bool opened=false,submitted=false,chatClosed=false;
            try {
                while(copyInProgress || Native.CopyInProgress) { if(closing)return false;await Task.Delay(10); }
                uint beforeCapture=Native.GetClipboardSequenceNumber();
                previous=await Native.CaptureClipboardAsync();
                if(closing || Native.GetClipboardSequenceNumber()!=beforeCapture || (!Native.IsOurWindow(Native.GetForegroundWindow()) && Native.GetForegroundWindow()!=game))return false;
                Clipboard.SetText(command);commandSequence=Native.GetClipboardSequenceNumber();sequence=commandSequence;
                int closeMap=fullMapActive?scumMapKey:0;
                SetFullMap(false);if(panel!=null)panel.Hide();if(SettingsVisible)DismissSettings();
                if(!Native.IsGameWindow(game))return false;
                Native.SetForegroundWindow(game);await Task.Delay(150);
                if(!Native.GameFocused() || Native.GetForegroundWindow()!=game || Native.AdminKeysHeld() || chat.Paused)return false;
                long userInput=GameKeys.PhysicalInputRevision;
                Native.AdminInputInProgress=true;
                chatClosed=await Native.SendAdminChat(game,scumChatKey,scumChatScanCode,closeMap,scumMapScanCode,
                    ()=>!closing && !IsDisposed && GameKeys.PhysicalInputRevision==userInput,
                    ()=>Native.GetClipboardSequenceNumber()==commandSequence,
                    ()=> { opened=true;chat.Key(scumChatKey,scumChatKey,0,scumChatScanCode,scumChatScanCode);observedChatPaused=true; },()=>submitted=true);
            } catch(Exception ex) { Program.LogException("AdminCommand",ex); }
            finally {
                Native.AdminInputInProgress=false;
                if(chatClosed) { chat.Reset();observedChatPaused=false; }
                try { if(commandSequence!=0 && Native.GetClipboardSequenceNumber()==commandSequence) {
                    if(previous!=null)Clipboard.SetDataObject(previous,true,0,0);else Native.TryClearClipboard();
                } } catch(System.Runtime.InteropServices.ExternalException) {}
                sequence=Native.GetClipboardSequenceNumber();pending=false;resumeAfter=DateTime.UtcNow.AddMilliseconds(600);adminCommandBusy=false;
                if(!chatClosed && !closing && panel!=null && !panel.IsDisposed && (Native.GetForegroundWindow()==game || Native.IsOurWindow(Native.GetForegroundWindow()))) {
                    panel.Show();panel.Activate();MessageBox.Show(panel,submitted?"Command submitted, but Escape cleanup stopped. Close SCUM chat before retrying.":(opened?"Command stopped before submission. Check and close SCUM chat before retrying.":"Command not sent. Keep SCUM available and release held keys before retrying."));
                }
                note=submitted?(chatClosed?"Admin command submitted; Escape sent to close chat. Check SCUM for the result.":"Admin command submitted; chat cleanup interrupted."):"Admin command cancelled.";
            }
            return submitted;
        }
    }
}
