using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {

    enum CopyResult { Sent, Cancelled, Failed }



    static partial class Native {
        // Opt-in, bounded input diagnostics. Never record general typing or clipboard data.
        internal static readonly bool InputTraceEnabled=Environment.GetEnvironmentVariable("SCUM_MINIMAP_INPUT_TRACE")=="1";
        internal static readonly UIntPtr CopyInputTag=new UIntPtr(0x53434D4Du);
        static readonly System.Collections.Concurrent.ConcurrentQueue<string> inputTrace=new System.Collections.Concurrent.ConcurrentQueue<string>();
        static readonly DateTime inputTraceEnd=DateTime.UtcNow.AddMinutes(2);
        static int traceWriting;
        static readonly System.Threading.Timer inputTraceTimer=InputTraceEnabled?
            new System.Threading.Timer(_=>FlushInputTrace(),null,1000,1000):null;
        internal static void TraceInput(string text) {
            if(!InputTraceEnabled || DateTime.UtcNow>inputTraceEnd || inputTrace.Count>=2048) return;
            inputTrace.Enqueue(DateTime.UtcNow.ToString("o")+" "+text);
        }
        static void FlushInputTrace() {
            if(System.Threading.Interlocked.Exchange(ref traceWriting,1)!=0) return;
            try {
                var lines=new System.Collections.Generic.List<string>();
                string line;
                while(inputTrace.TryDequeue(out line)) lines.Add(line);
                if(lines.Count>0) {
                    string folder=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Program.DataFolderName);
                    System.IO.Directory.CreateDirectory(folder);
                    System.IO.File.AppendAllLines(System.IO.Path.Combine(folder,"input-trace.log"),lines);
                }
            } catch(System.IO.IOException) {} catch(UnauthorizedAccessException) {}
            finally { System.Threading.Interlocked.Exchange(ref traceWriting,0); }
        }



        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();

        [StructLayout(LayoutKind.Sequential)] internal struct WindowRect { public int Left,Top,Right,Bottom; }
        [DllImport("user32.dll",SetLastError=true)] static extern bool GetWindowRect(IntPtr window,out WindowRect rect);

        // SCUM's selected top navigation tab has a bright, rounded highlight
        // regardless of the language used for its label. Sample just that
        // narrow strip from the foreground game window; never OCR or retain it.
        internal static bool HasSelectedTopNavigationTab(IntPtr gameWindow) {
            if(gameWindow==IntPtr.Zero || GetForegroundWindow()!=gameWindow || !IsGameWindow(gameWindow)) return false;
            return InventoryInputActive(IsCursorVisible(),()=>DetectSelectedTopNavigationTab(gameWindow));
        }

        internal static bool InventoryInputActive(bool cursorVisible,Func<bool> detectSelectedTab) {
            // Inventory/navigation menus expose the cursor. Bright scenery alone
            // must never lock tracking and map shortcuts during normal gameplay.
            return cursorVisible && detectSelectedTab();
        }

        static bool DetectSelectedTopNavigationTab(IntPtr gameWindow) {
            WindowRect rect;
            if(!GetWindowRect(gameWindow,out rect)) return false;
            int width=rect.Right-rect.Left, height=rect.Bottom-rect.Top;
            if(width<700 || height<300) return false;
            int stripWidth=Math.Max(100,width/5);
            int stripHeight=Math.Max(36,Math.Min(90,height/12));
            try {
                using(var captured=new System.Drawing.Bitmap(stripWidth,stripHeight))
                using(var sample=new System.Drawing.Bitmap(200,20))
                using(var sourceGraphics=System.Drawing.Graphics.FromImage(captured))
                using(var graphics=System.Drawing.Graphics.FromImage(sample)) {
                    sourceGraphics.CopyFromScreen(rect.Left,rect.Top,0,0,new System.Drawing.Size(stripWidth,stripHeight));
                    graphics.DrawImage(captured,new System.Drawing.Rectangle(0,0,sample.Width,sample.Height));
                    return HasSelectedTabHighlight(sample);
                }
            } catch { return false; }
        }

        internal static bool HasSelectedTabHighlight(System.Drawing.Bitmap sample) {
            if(sample==null || sample.Width<40 || sample.Height<12) return false;
            // Normalize brightness vertically: translated tab labels remain text,
            // while the selected tab creates a broad, continuous light region.
            int highlightedRows=0;
            for(int y=1;y<sample.Height-1;y++) {
                int brightPixels=0;
                for(int x=0;x<sample.Width;x++) {
                    System.Drawing.Color c=sample.GetPixel(x,y);
                    if(c.R>170 && c.G>170 && c.B>170 && Math.Abs(c.R-c.G)<55 && Math.Abs(c.G-c.B)<55) brightPixels++;
                }
                if(brightPixels>=24) highlightedRows++;
            }
            return highlightedRows>=3;
        }



        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);



        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);



        [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
        [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll")] static extern bool CloseClipboard();
        [DllImport("user32.dll")] static extern bool EmptyClipboard();
        [DllImport("user32.dll")] static extern IntPtr GetClipboardData(uint format);
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr data);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr data);
        [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr data);
        internal static bool TryReadCoordinateClipboard(out string text) {
            text=null;
            if(!OpenClipboard(IntPtr.Zero)) return false;
            try {
                if(!IsClipboardFormatAvailable(13)) return true;
                IntPtr data=GetClipboardData(13);
                if(data==IntPtr.Zero) return false;
                ulong bytes=GlobalSize(data).ToUInt64();
                if(bytes<2 || bytes>2048) return true;
                IntPtr value=GlobalLock(data);
                if(value==IntPtr.Zero) return false;
                try {
                    text=Marshal.PtrToStringUni(value,(int)(bytes/2));
                    int end=text.IndexOf('\0'); if(end>=0) text=text.Substring(0,end);
                    return true;
                }
                finally { GlobalUnlock(data); }
            } finally { CloseClipboard(); }
        }
        internal static bool TryClearClipboard() {
            if(!OpenClipboard(IntPtr.Zero)) return false;
            try { return EmptyClipboard(); } finally { CloseClipboard(); }
        }
        internal static Task<IDataObject> CaptureClipboardAsync() {
            var completion=new TaskCompletionSource<IDataObject>();
            var worker=new Thread(()=> {
                try {
                    IDataObject original=Clipboard.GetDataObject();
                    var snapshot=new DataObject();
                    if(original!=null) foreach(string format in original.GetFormats(false)) snapshot.SetData(format,false,original.GetData(format,false));
                    completion.SetResult(snapshot);
                } catch(Exception ex) { completion.SetException(ex); }
            }) { IsBackground=true,Name="MiniMap clipboard snapshot" };
            worker.SetApartmentState(ApartmentState.STA); worker.Start();
            return completion.Task;
        }



        [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code,uint mapType);
        [DllImport("user32.dll",EntryPoint="MapVirtualKeyExW")] static extern uint MapVirtualKeyEx(uint code,uint mapType,IntPtr layout);
        internal static int ScanCodeToVirtualKey(int scanCode) {
            if(!PhysicalKeyCapture.Valid(scanCode)) return 0;
            uint encoded=(uint)((scanCode&0xFF)|((scanCode&0x100)!=0?0xE000:0));
            uint mapped=MapVirtualKeyEx(encoded,3,GetKeyboardLayout(0));
            return mapped>0 && mapped<256?(int)mapped:0;
        }
        [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint threadId);



        [DllImport("user32.dll")] internal static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);



        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);

        [DllImport("user32.dll")] internal static extern bool ReleaseCapture();



        [DllImport("user32.dll",SetLastError=true)] static extern uint SendInput(uint n,INPUT[] inputs,int size);



        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public UNION data; }



        [StructLayout(LayoutKind.Explicit)] struct UNION {



            [FieldOffset(0)] public KEY keyboard;



            [FieldOffset(0)] public MOUSE mouse;



        }



        [StructLayout(LayoutKind.Sequential)] struct KEY { public ushort vk,scan; public uint flags,time; public UIntPtr extra; }



        [StructLayout(LayoutKind.Sequential)] struct MOUSE { public int x,y; public uint data,flags,time; public UIntPtr extra; }



        static uint cachedPid;



        static bool cachedGame;



        static long cacheUntil;



        static readonly uint currentProcessId=ReadCurrentProcessId();



        internal static DateTime LastAltTabTime = DateTime.MinValue;
        static uint knownGamePid = 0;
        static readonly int[] busyKeys={
            0x02,             // Right mouse
            0x10, 0xA0, 0xA1, // Shift, LShift, RShift
            0x11, 0xA2, 0xA3, // Ctrl, LCtrl, RCtrl
            0x12, 0xA4, 0xA5, // Alt, LAlt, RAlt
            0x43,             // 'C'
            0x5B, 0x5C,       // LWin, RWin
            0x09,             // Tab
            0x20,             // Space
            0x45,             // 'E'
            0x46,             // 'F'
            0x52              // 'R'
        };

        static uint ReadCurrentProcessId() {
            using(Process process=Process.GetCurrentProcess()) return (uint)process.Id;
        }

        internal static bool IsRightMouseDown() {
            return (GetAsyncKeyState(0x02)&0x8000)!=0;
        }

        internal static bool GameFocused() {
            IntPtr fg=GetForegroundWindow();
            if(fg==IntPtr.Zero) return false;
            uint pid; GetWindowThreadProcessId(fg,out pid);
            // Process identity is revalidated after the short foreground cache expires.
            long now=Stopwatch.GetTimestamp();
            if(pid==cachedPid && now<cacheUntil)return cachedGame;
            cachedPid=pid; cacheUntil=now+(Stopwatch.Frequency/4); // 250ms cache for instant focus detection
            try { using (Process p=Process.GetProcessById((int)pid)) {
                cachedGame=p.ProcessName.Equals("SCUM",StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("SCUM-Win64-Shipping",StringComparison.OrdinalIgnoreCase);
                if(cachedGame) knownGamePid=pid;
                return cachedGame;
            }} catch { cachedGame=false; }
            return cachedGame;
        }

        internal static bool IsAltOrTabOrWinDown() {
            return (GetAsyncKeyState(0x12)&0x8000)!=0
                || (GetAsyncKeyState(0xA4)&0x8000)!=0
                || (GetAsyncKeyState(0xA5)&0x8000)!=0
                || (GetAsyncKeyState(0x09)&0x8000)!=0
                || (GetAsyncKeyState(0x5B)&0x8000)!=0
                || (GetAsyncKeyState(0x5C)&0x8000)!=0;
        }




        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr window);



        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd,IntPtr hWndInsertAfter,int X,int Y,int cx,int cy,uint uFlags);



        internal static void ForceForeground(IntPtr window) {



            if(window==IntPtr.Zero || !IsWindow(window)) return;



            SetWindowPos(window,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0040); // HWND_TOPMOST, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW



            SetForegroundWindow(window);



        }



        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);



        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);



        internal static bool IsOurWindow(IntPtr window) {



            if(window==IntPtr.Zero || !IsWindow(window)) return false;



            uint pid; GetWindowThreadProcessId(window,out pid);



            return pid==currentProcessId;



        }



        internal static bool IsGameWindow(IntPtr window) {



            if(window==IntPtr.Zero || !IsWindow(window) || IsIconic(window)) return false;



            uint pid; GetWindowThreadProcessId(window,out pid);



            try { using(Process p=Process.GetProcessById((int)pid)) {



                return p.ProcessName.Equals("SCUM",StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("SCUM-Win64-Shipping",StringComparison.OrdinalIgnoreCase);



            } } catch { return false; }



        }



        internal static bool CopyInProgress;
        static CopyInputLease activeCopy;
        internal static void CancelActiveCopy() {
            if(activeCopy!=null) activeCopy.Cancel();
        }



        internal static bool CursorBlocksCopy(bool cursorVisible,bool fullMapActive) { return cursorVisible && !fullMapActive; }

        internal static bool KeysBusy(int copyModifierKey,int copyKey,bool fullMapActive=false) {



            if(UserTypingOrActive()) return true;



            if(CursorBlocksCopy(IsCursorVisible(),fullMapActive)) return true;



            foreach(int k in busyKeys) if ((GetAsyncKeyState(k)&0x8000)!=0) return true;



            if(copyModifierKey>0 && copyModifierKey<256 && (GetAsyncKeyState(copyModifierKey)&0x8000)!=0) return true;



            if(copyKey>0 && copyKey<256 && (GetAsyncKeyState(copyKey)&0x8000)!=0) return true;



            return false;



        }



        [DllImport("user32.dll")] internal static extern bool GetCursorInfo(out CURSORINFO pci);



        [StructLayout(LayoutKind.Sequential)] internal struct CURSORINFO {



            public int cbSize;



            public int flags;



            public IntPtr hCursor;



            public POINT ptScreenPos;



        }



        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int x,y; }



        const int CURSOR_SHOWING = 0x00000001;



        internal static bool IsCursorVisible() {



            try {



                CURSORINFO ci=new CURSORINFO();



                ci.cbSize=Marshal.SizeOf(typeof(CURSORINFO));



                if(GetCursorInfo(out ci)) {



                    return (ci.flags & CURSOR_SHOWING) != 0;



                }



            } catch {}



            return false;



        }



        internal static bool UserTypingOrActive() {



            return GameKeys.UserActiveRecently(90);



        }



        internal static string CopyError="";



        static bool Key(uint vk,bool up,int capturedScan=0,IntPtr layout=default(IntPtr)) {



            INPUT input=new INPUT(); input.type=1;



            input.data.keyboard.vk=0;
            input.data.keyboard.extra=CopyInputTag;



            uint mapped=capturedScan!=0?(uint)capturedScan:MapVirtualKeyEx(vk,4,layout);
            if(mapped==0) mapped=MapVirtualKey(vk,0);
            if((mapped&0xFF)==0) { CopyError="Keyboard scan code is unavailable. Capture the Copy Location key in Settings."; return false; }
            input.data.keyboard.scan=(ushort)(mapped&0xFF);



            input.data.keyboard.flags=8u|(up?2u:0u)|((mapped&0x100)!=0 || (mapped&0xFF00)!=0?1u:0u);



            bool ok=SendInput(1,new INPUT[]{input},Marshal.SizeOf(typeof(INPUT)))==1;
            int inputError=ok?0:Marshal.GetLastWin32Error();
            if(InputTraceEnabled) TraceInput("copy vk="+vk+" scan="+input.data.keyboard.scan+" flags="+input.data.keyboard.flags+" sent="+ok+
                " W="+((GetAsyncKeyState(0x57)&0x8000)!=0)+" Shift="+((GetAsyncKeyState(0x10)&0x8000)!=0));



            if(!ok) CopyError="SendInput failed (Windows error "+inputError+").";



            return ok;



        }



        internal static async Task<CopyResult> Copy(Func<bool> allowed,int copyModifierKey,int copyKey,Func<bool> fullMapActive=null,int copyScanCode=0) {



            CopyError="";



            if(CopyInProgress || !allowed() || !GameFocused() || KeysBusy(copyModifierKey,copyKey,fullMapActive!=null && fullMapActive()) || IsAltOrTabOrWinDown()) { CopyError="Focus, chat, or modifier keys detected."; return CopyResult.Cancelled; }



            IntPtr game=GetForegroundWindow();
            uint processId;
            IntPtr layout=GetKeyboardLayout(GetWindowThreadProcessId(game,out processId));
            int effectiveCopyKey=copyKey;



            CopyInProgress=true;
            activeCopy=new CopyInputLease((vk,up)=>Key(vk,up,(int)vk==effectiveCopyKey?copyScanCode:0,layout));



            try {



                bool sent=await CopyChord(activeCopy.Send,Task.Delay,()=>!activeCopy.Cancelled && allowed() && GetForegroundWindow()==game && GameFocused()
                    && !UserTypingOrActive() && !CursorBlocksCopy(IsCursorVisible(),fullMapActive!=null && fullMapActive()) && !IsRightMouseDown() && !IsAltOrTabOrWinDown(),
                    copyModifierKey,effectiveCopyKey);
                return ClassifyCopy(sent && !activeCopy.Cancelled,CopyError);
            } finally { CancelActiveCopy(); activeCopy=null; CopyInProgress=false; }
        }

        internal static CopyResult ClassifyCopy(bool sent,string error) {
            return sent?CopyResult.Sent:(string.IsNullOrEmpty(error)?CopyResult.Cancelled:CopyResult.Failed);
        }

        // Hold the configured modifier across game frames around the copy key. Never press it
        // after a focus/user-input change, and retain failed key-ups for cleanup.
        internal static async Task<bool> CopyChord(Func<uint,bool,bool> key,Func<int,Task> delay,Func<bool> canPressCopy,int modifierKey,int copyKey) {
            bool modifierHeld=false,copyHeld=false,ok=false;
            try {
                if(modifierKey>0) {
                    modifierHeld=key((uint)modifierKey,false);
                    if(modifierHeld) {
                        await delay(60);
                        if(canPressCopy()) {
                            copyHeld=key((uint)copyKey,false);
                            if(copyHeld) {
                                await delay(30);
                                ok=key((uint)copyKey,true);
                                copyHeld=!ok;
                            }
                            await delay(80);
                        }
                    }
                } else {
                    if(canPressCopy()) {
                        copyHeld=key((uint)copyKey,false);
                        if(copyHeld) {
                            // Cover more than one game frame without a modifier or a trailing hold.
                            await delay(60);
                            ok=key((uint)copyKey,true);
                            copyHeld=!ok;
                        }
                    }
                }



            } finally {



                if(copyHeld) { bool released=key((uint)copyKey,true); copyHeld=!released; ok=false; }



                // A second cleanup attempt precedes releasing the modifier.



                if(copyHeld) { key((uint)copyKey,true); ok=false; }



                if(modifierHeld && !key((uint)modifierKey,true)) { key((uint)modifierKey,true); ok=false; }



            }



            return ok;



        }



    }

}
