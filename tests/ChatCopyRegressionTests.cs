using System;
using System.Threading.Tasks;
using ScumMiniMap;

static class ChatCopyRegressionTests {
    static void Check(bool condition,string message) {
        if(!condition) throw new Exception(message);
        Console.WriteLine("PASS: "+message);
    }
    static void Main() {
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
            Check(injected==0,"Open chat blocks 100 consecutive backslash sampling attempts");
            monitor.Observe(closeKey,false,true,chat,openKey);
            monitor.Observe(closeKey,true,true,chat,openKey);
            Check(!chat.Paused,"A new close-key press releases the pause");
            monitor.Observe(openKey,true,true,chat,openKey);
            Check(chat.Paused,"Polling alone detects a missed chat-opening hook event");
            monitor.Observe(0x09,true,true,chat,openKey);
            monitor.Observe(0xDC,true,true,chat,openKey);
            Check(chat.Paused,"Tab and backslash preserve chat protection");
        }
    }
}
