using System;
using System.Drawing;
using System.Threading.Tasks;
using ScumMiniMap;

static class ChatCopyRegressionTests {
    static void Check(bool condition,string message) {
        if(!condition) throw new Exception(message);
        Console.WriteLine("PASS: "+message);
    }
    static void Main() {
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
