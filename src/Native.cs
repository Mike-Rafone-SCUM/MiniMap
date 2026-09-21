using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {

    enum CopyResult { Sent, Cancelled, Failed }



    static class Native {



        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();



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

        internal static void EmergencyReleaseModifier(int modifierKey=0xA2) {
            if(modifierKey<=0) return;
            try { Key((uint)modifierKey, true); } catch {}
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



        internal static bool KeysBusy(int copyModifierKey=0xA2,int copyKey=0x43) {



            if(UserTypingOrActive()) return true;



            if(IsCursorVisible()) return true;



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



        static bool Key(uint vk,bool up) {



            INPUT input=new INPUT(); input.type=1;



            input.data.keyboard.vk=(ushort)vk;



            input.data.keyboard.scan=(ushort)MapVirtualKey(vk,0);



            input.data.keyboard.flags=8u|(up?2u:0u);



            bool ok=SendInput(1,new INPUT[]{input},Marshal.SizeOf(typeof(INPUT)))==1;



            if(!ok) CopyError="SendInput failed (Windows error "+Marshal.GetLastWin32Error()+").";



            return ok;



        }



        internal static async Task<CopyResult> Copy(Func<bool> allowed,int copyModifierKey=0xA2,int copyKey=0x43) {



            CopyError="";



            if(CopyInProgress || !allowed() || !GameFocused() || KeysBusy(copyModifierKey,copyKey) || IsAltOrTabOrWinDown()) { CopyError="Focus, chat, or modifier keys detected."; return CopyResult.Cancelled; }



            IntPtr game=GetForegroundWindow();



            CopyInProgress=true;
            activeCopy=new CopyInputLease(Key);



            try {



                bool sent=await CopyChord(activeCopy.Send,Task.Delay,()=>!activeCopy.Cancelled && allowed() && GetForegroundWindow()==game && GameFocused()
                    && !UserTypingOrActive() && !IsCursorVisible() && !IsRightMouseDown() && !IsAltOrTabOrWinDown(),
                    copyModifierKey,copyKey);
                return ClassifyCopy(sent && !activeCopy.Cancelled,CopyError);
            } finally { CancelActiveCopy(); activeCopy=null; CopyInProgress=false; }
        }

        internal static CopyResult ClassifyCopy(bool sent,string error) {
            return sent?CopyResult.Sent:(string.IsNullOrEmpty(error)?CopyResult.Cancelled:CopyResult.Failed);
        }

        // Leave Control down across game frames on both sides of C. Never send C
        // after a focus/user-input change, and retain failed key-ups for cleanup.
        internal static async Task<bool> CopyChord(Func<uint,bool,bool> key,Func<int,Task> delay,Func<bool> canPressC,int modifierKey=0xA2,int copyKey=0x43) {
            bool ctrl=false,c=false,ok=false;
            try {
                if(modifierKey>0) {
                    ctrl=key((uint)modifierKey,false);
                    if(ctrl) {
                        await delay(60);
                        if(canPressC()) {
                            c=key((uint)copyKey,false);
                            if(c) {
                                await delay(30);
                                ok=key((uint)copyKey,true);
                                c=!ok;
                            }
                            await delay(80);
                        }
                    }
                } else {
                    if(canPressC()) {
                        c=key((uint)copyKey,false);
                        if(c) {
                            // Cover more than one game frame without a modifier or a trailing hold.
                            await delay(60);
                            ok=key((uint)copyKey,true);
                            c=!ok;
                        }
                    }
                }



            } finally {



                if(c) { bool released=key((uint)copyKey,true); c=!released; ok=false; }



                // A second cleanup attempt precedes releasing the modifier.



                if(c) { key((uint)copyKey,true); ok=false; }



                if(ctrl && !key((uint)modifierKey,true)) { key((uint)modifierKey,true); ok=false; }



            }



            return ok;



        }



    }

}
