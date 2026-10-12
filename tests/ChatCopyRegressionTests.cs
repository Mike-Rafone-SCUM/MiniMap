using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using ScumMiniMap;

static class ChatCopyRegressionTests {
    [StructLayout(LayoutKind.Sequential)]
    struct QueuedKeyboardEvent { public uint key,scan,flags,time; public UIntPtr extra; }
    static void QueuedSlashMustNotEnterChat() {
        var chat=new ChatState();chat.Key(0x54);
        using(var hooks=new GameKeys((key,scan)=>{},(key,scan)=>false,chat)) {
            var expiredCopy=new CopyInputDeliveryGate(()=>!chat.Paused);
            var packet=new QueuedKeyboardEvent {key=0x6F,scan=0x35,flags=0x11,extra=expiredCopy.Tag};
            IntPtr data=Marshal.AllocHGlobal(Marshal.SizeOf(typeof(QueuedKeyboardEvent)));
            try {
                Marshal.StructureToPtr(packet,data,false);
                var method=typeof(GameKeys).GetMethod("OnKey",BindingFlags.Instance|BindingFlags.NonPublic);
                IntPtr result=(IntPtr)method.Invoke(hooks,new object[]{0,new IntPtr(0x100),data});
                Check(result==new IntPtr(1),"A queued coordinate-copy Num / key-down is blocked after chat opens and its copy lease ends");
            }finally{Marshal.FreeHGlobal(data);}
        }
    }
    static void VerifyDeliveryOwnership() {
        var fields=typeof(Native);
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var deliveryField=fields.GetField("activeCopyDelivery",flags);
        var leaseField=fields.GetField("activeCopy",flags);
        object previousDelivery=deliveryField.GetValue(null),previousLease=leaseField.GetValue(null);
        bool previousProgress=Native.CopyInProgress;
        try {
            foreach(int chatKey in new[]{0x54,0x59}) {
                var chat=new ChatState();bool focused=true;
                var delivery=new CopyInputDeliveryGate(()=>!chat.Paused&&focused);
                var lease=new CopyInputLease((key,up)=>true);
                deliveryField.SetValue(null,delivery);leaseField.SetValue(null,lease);Native.CopyInProgress=true;
                Check(!Native.ShouldSuppressCoordinateInput(true,true,delivery.Tag),"Current coordinate copy is allowed in gameplay");
                // The request has already passed the sender's check. Opening chat
                // before Windows delivers that packet must still stop the key-down.
                chat.Key(chatKey,chatKey);
                Check(Native.ShouldSuppressCoordinateInput(true,true,delivery.Tag)&&lease.Cancelled,"Chat opening after enqueue blocks Num / and cancels its owned input lease");
                chat.Key(0x1B);
                Check(Native.ShouldSuppressCoordinateInput(true,true,delivery.Tag),"Closing chat cannot revive the cancelled copy's pending key-down");
                Check(!Native.ShouldSuppressCoordinateInput(true,false,delivery.Tag),"Cancelled copy key-ups remain available for modifier/key cleanup");
                var newer=new CopyInputDeliveryGate(()=>true);
                deliveryField.SetValue(null,newer);leaseField.SetValue(null,new CopyInputLease((key,up)=>true));
                Check(newer.Tag!=delivery.Tag && Native.ShouldSuppressCoordinateInput(true,true,delivery.Tag)
                    && !Native.ShouldSuppressCoordinateInput(true,true,newer.Tag),"An old copy packet cannot borrow a newer request's permission or cancel it");
                focused=false;
                var focusDelivery=new CopyInputDeliveryGate(()=>focused);
                deliveryField.SetValue(null,focusDelivery);
                Check(Native.ShouldSuppressCoordinateInput(true,true,focusDelivery.Tag),"Focus loss after enqueue blocks copy delivery");
            }
            Check(!Native.ShouldSuppressCoordinateInput(true,true,Native.AdminInputTag),"Admin chat sequences retain their independent input ownership");
            Check(!Native.ShouldSuppressCoordinateInput(true,true,new UIntPtr(123)),"Other applications' injected input is not swallowed");
            Check(!Native.ShouldSuppressCoordinateInput(false,true,Native.CopyInputTag),"Physical keyboard input is not swallowed by coordinate-copy ownership");
            var invalidContext=new CopyInputDeliveryGate(()=>{throw new InvalidOperationException("Disposed context");});
            Check(!invalidContext.AllowsDown(invalidContext.Tag),"An unavailable copy context fails closed at delivery");
            deliveryField.SetValue(null,null);Native.CopyInProgress=false;
            Check(Native.ShouldSuppressCoordinateInput(true,true,Native.CopyInputTag)
                && !Native.ShouldSuppressCoordinateInput(true,false,Native.CopyInputTag),"Ended requests reject late key-downs while permitting late cleanup releases");
        } finally {deliveryField.SetValue(null,previousDelivery);leaseField.SetValue(null,previousLease);Native.CopyInProgress=previousProgress;}
    }
    static void Check(bool condition,string message) {
        if(!condition) throw new Exception(message);
        Console.WriteLine("PASS: "+message);
    }
    static void Main() {
        QueuedSlashMustNotEnterChat();
        VerifyDeliveryOwnership();
        foreach(int openVirtualKey in new[]{0x54,0x45}) {
            var typingChat=new ChatState();var typingMonitor=new ChatKeyMonitor();
            typingMonitor.Observe(openVirtualKey,true,true,typingChat,0x54,0x6F,0x14,0x14);
            typingMonitor.Observe(openVirtualKey,false,true,typingChat,0x54,0x6F,0x14,0x14);
            Check(typingChat.Paused,"Physical chat-opening key is recognized across virtual-key layout mappings");
            long beforeTyping=typingChat.Revision;int copies=0;
            for(int repeat=0;repeat<100;repeat++) {
                foreach(int mapVirtualKey in new[]{0x4D,0x42}) {
                    bool before=typingChat.Paused;
                    typingMonitor.Observe(mapVirtualKey,true,true,typingChat,0x54,0x6F,0x32,0x14);
                    if(!GameKeys.ChatOwnsShortcut(before,typingChat.Paused))
                        throw new Exception("Physical M/ь escaped chat ownership during rapid typing or a layout change");
                    typingMonitor.Observe(mapVirtualKey,false,true,typingChat,0x54,0x6F,0x32,0x14);
                }
                Native.CopyChord((key,up)=> { copies++;return true; },ms=>Task.FromResult(0),()=>!typingChat.Paused,0,0x6F).GetAwaiter().GetResult();
            }
            Check(typingChat.Paused,"Physical M/ь stays owned by chat through 200 rapid presses and layout changes");
            Check(copies==0 && typingChat.Revision==beforeTyping,"Typing cannot inject coordinate-copy characters or release the chat gate");
            typingMonitor.Observe(0x1B,true,true,typingChat,0x54,0x6F,1,0x14);
            Check(!typingChat.Paused && !typingChat.AllowsQueuedShortcut(beforeTyping),"Closing chat cannot replay a map shortcut queued before typing");
        }
        bool probed=false;
        Check(!Native.InventoryInputActive(false,()=>{ probed=true; return true; }) && !probed,
            "Hidden gameplay cursor prevents bright scenery from locking tracking and M, without screen probing");
        Check(Native.InventoryInputActive(true,()=>true),"Visible cursor and selected navigation tab protect inventory text input");
        Check(!Native.InventoryInputActive(true,()=>false),"Visible cursor without a navigation highlight does not claim inventory input");
        using(var selectedTab=new Bitmap(200,20)) {
            using(Graphics g=Graphics.FromImage(selectedTab)) {
                g.Clear(Color.Black);
                g.FillRectangle(Brushes.Gainsboro,3,4,31,12);
                // Simulated translated tab lettering: dark strokes inside the
                // highlight must not be required to spell an English label.
                g.FillRectangle(Brushes.Black,11,7,2,6);
                g.FillRectangle(Brushes.Black,18,6,2,7);
                g.FillRectangle(Brushes.Black,25,7,2,6);
            }
            Check(Native.HasSelectedTabHighlight(selectedTab),"Selected game navigation tab is recognized independently of its translated label");
        }
        using(var textOnly=new Bitmap(200,20)) {
            using(Graphics g=Graphics.FromImage(textOnly)) {
                g.Clear(Color.Black);
                g.FillRectangle(Brushes.White,4,6,2,8);
                g.FillRectangle(Brushes.White,9,5,2,9);
                g.FillRectangle(Brushes.White,15,6,2,8);
                g.FillRectangle(Brushes.White,22,5,2,9);
                g.FillRectangle(Brushes.White,30,6,2,8);
            }
            Check(!Native.HasSelectedTabHighlight(textOnly),"Unselected navigation text does not trigger the inventory input lock");
        }
        var focusChat=new ChatState();
        var focusMonitor=new ChatKeyMonitor();
        IntPtr gameWindow=new IntPtr(123),desktopWindow=new IntPtr(456);
        focusMonitor.Observe(0x54,true,GameKeys.FocusStillMatches(true,gameWindow,desktopWindow),focusChat,0x54);
        Check(!focusChat.Paused,"Typing T after Alt-Tab cannot open a phantom chat gate before the next timer tick");
        focusMonitor.Observe(0x54,false,false,focusChat,0x54);
        focusMonitor.Observe(0x54,true,GameKeys.FocusStillMatches(true,gameWindow,gameWindow),focusChat,0x54);
        Check(focusChat.Paused,"Real in-game chat still blocks Home, Insert, and Delete");
        focusMonitor.Observe(0x1B,true,true,focusChat,0x54);
        Check(!focusChat.Paused,"Closing real chat releases shortcut protection");
        foreach(int opening in new[]{0x54,0xBF,0x6F}) foreach(int closing in new[]{0x0D,0x1B}) {
            var state=new ChatState();
            foreach(int key in new[]{opening,0x09,0x4D,0xBF,0x09,0x4D,closing}) {
                bool wasPaused=state.Paused;
                state.Key(key);
                Check(GameKeys.ChatOwnsShortcut(wasPaused,state.Paused),"Chat owns shortcut through typing/channel/close sequence: "+key);
            }
            Check(!state.Paused,"Chat close releases sampling after protected key dispatch");
            Check(!GameKeys.ChatOwnsShortcut(false,false),"Map shortcuts remain available after chat closes");
        }
        foreach(int closeKey in new[]{0x0D,0x1B}) foreach(int openKey in new[]{0x54,0x59}) {
            var chat=new ChatState();
            var monitor=new ChatKeyMonitor();
            monitor.Observe(closeKey,true,true,chat,openKey);
            monitor.Observe(openKey,true,true,chat,openKey);
            monitor.Observe(openKey,false,true,chat,openKey);
            // The next timer tick still sees the earlier close key held down.
            monitor.Observe(closeKey,true,true,chat,openKey);
            Check(chat.Paused,"Polling cannot replay a close key after chat reopens: "+closeKey+" / "+openKey);
            int injected=0;
            for(int tick=0;tick<100;tick++) {
                bool sent=Native.CopyChord((key,up)=>{ injected++; return true; },
                    ms=>Task.FromResult(0),()=>!chat.Paused,0,0xDC).GetAwaiter().GetResult();
                if(sent || injected!=0) throw new Exception("Backslash injected while chat was open");
            }
            Check(injected==0,"Open chat blocks 100 consecutive copy-key sampling attempts");
            monitor.Observe(closeKey,false,true,chat,openKey);
            monitor.Observe(closeKey,true,true,chat,openKey);
            Check(!chat.Paused,"A new close-key press releases the pause");
            monitor.Observe(openKey,true,true,chat,openKey);
            Check(chat.Paused,"Polling alone detects a missed chat-opening hook event");
            monitor.Observe(0x09,true,true,chat,openKey);
            monitor.Observe(0xDC,true,true,chat,openKey);
            Check(chat.Paused,"Tab and typed keys preserve chat protection");
        }
        var numpadCopyChat=new ChatState();
        var numpadMonitor=new ChatKeyMonitor();
        numpadMonitor.Observe(0x6F,true,true,numpadCopyChat,0x54,0x6F);
        Check(!numpadCopyChat.Paused,"Configured NumPad Divide copy key does not falsely open the chat lock");
        numpadMonitor.Observe(0x54,true,true,numpadCopyChat,0x54,0x6F);
        Check(numpadCopyChat.Paused,"Configured chat key still opens chat while NumPad Divide is used for copying");
    }
}
