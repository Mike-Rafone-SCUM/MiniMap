using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ScumMiniMap {

    enum CopyResult { Sent, Cancelled, Failed }

    internal sealed class AdminClipboardResult {
        internal bool Ready;
        internal uint Sequence;
        internal int NativeError;
        internal string StopReason;
    }



    static partial class Native {
        // Opt-in, bounded input diagnostics. Never record general typing or clipboard data.
        internal static readonly bool InputTraceEnabled=Environment.GetEnvironmentVariable("SCUM_MINIMAP_INPUT_TRACE")=="1";
        internal static readonly UIntPtr CopyInputTag=CopyInputDeliveryGate.TagFamily;
        internal static readonly UIntPtr AdminInputTag=new UIntPtr(0x53434D4Du);
        static readonly System.Collections.Concurrent.ConcurrentQueue<string> inputTrace=new System.Collections.Concurrent.ConcurrentQueue<string>();
        static readonly DateTime inputTraceEnd=DateTime.UtcNow.AddMinutes(2);
        const int InputTraceLineLimit=4096;
        static int traceWriting,inputTraceCount;
        static readonly System.Threading.Timer inputTraceTimer=InputTraceEnabled?
            new System.Threading.Timer(_=>FlushInputTrace(),null,1000,1000):null;
        internal static bool InputTraceActive {
            get { return InputTraceEnabled && DateTime.UtcNow<=inputTraceEnd && Interlocked.CompareExchange(ref inputTraceCount,0,0)<InputTraceLineLimit; }
        }
        internal static void TraceInput(string text) {
            if(!InputTraceActive || Interlocked.Increment(ref inputTraceCount)>InputTraceLineLimit) return;
            inputTrace.Enqueue(DateTime.UtcNow.ToString("o")+" "+text);
        }
        internal static void FlushInputTrace() {
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

        // Read-only token queries run only when the opt-in trace observes a focus
        // change. Failure remains unknown; it must never look like an unelevated token.
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr OpenProcess(uint access,bool inherit,uint pid);
        [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",SetLastError=true)] static extern bool GetTokenInformation(IntPtr token,int informationClass,IntPtr buffer,uint length,out uint required);
        [DllImport("advapi32.dll",EntryPoint="GetTokenInformation",SetLastError=true)] static extern bool GetTokenElevation(IntPtr token,int informationClass,out uint elevated,uint length,out uint required);
        [DllImport("advapi32.dll")] static extern bool IsValidSid(IntPtr sid);
        [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
        [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid,uint index);
        [StructLayout(LayoutKind.Sequential)] struct TokenMandatoryLabel { public IntPtr Sid; public uint Attributes; }

        internal static void TraceFocusSecurity(bool gameFocused) {
            if(!InputTraceActive) return;
            TraceInput("security self "+ProcessSecurityTrace(currentProcessId));
            if(!gameFocused || !InputTraceActive) return;
            IntPtr foreground=GetForegroundWindow();
            uint pid;
            GetWindowThreadProcessId(foreground,out pid);
            if(foreground==IntPtr.Zero || pid==0 || !IsGameWindow(foreground)) {
                TraceInput("security scum=unknown reason=foreground-changed");
                return;
            }
            TraceInput("security scum "+ProcessSecurityTrace(pid));
        }

        static string ProcessSecurityTrace(uint pid) {
            IntPtr process=OpenProcess(0x1000,false,pid); // PROCESS_QUERY_LIMITED_INFORMATION
            int processError=process==IntPtr.Zero?Marshal.GetLastWin32Error():0;
            if(process==IntPtr.Zero) return "pid="+pid+" integrity=unknown elevated=unknown OpenProcess_error="+processError;
            IntPtr token=IntPtr.Zero;
            try {
                bool opened=OpenProcessToken(process,0x0008,out token); // TOKEN_QUERY
                int tokenError=opened?0:Marshal.GetLastWin32Error();
                if(!opened) return "pid="+pid+" integrity=unknown elevated=unknown OpenProcessToken_error="+tokenError;
                string integrity=TokenIntegrityTrace(token);
                uint elevated,required;
                bool elevationKnown=GetTokenElevation(token,20,out elevated,4,out required); // TokenElevation
                int elevationError=elevationKnown?0:Marshal.GetLastWin32Error();
                string elevation=elevationKnown && required==4?(elevated!=0).ToString():"unknown";
                return "pid="+pid+" "+integrity+" elevated="+elevation+
                    (elevationKnown?(required==4?"":" elevation_size="+required):" elevation_error="+elevationError);
            } catch(Exception ex) {
                return "pid="+pid+" integrity=unknown elevated=unknown query_error="+ex.GetType().Name;
            } finally {
                if(token!=IntPtr.Zero) CloseHandle(token);
                CloseHandle(process);
            }
        }

        static string TokenIntegrityTrace(IntPtr token) {
            uint required;
            bool sized=GetTokenInformation(token,25,IntPtr.Zero,0,out required); // TokenIntegrityLevel
            int sizeError=sized?0:Marshal.GetLastWin32Error();
            int labelSize=Marshal.SizeOf(typeof(TokenMandatoryLabel));
            if((!sized && sizeError!=122) || required<labelSize || required>65536)
                return "integrity=unknown integrity_size_error="+sizeError+" required="+required;
            IntPtr buffer=Marshal.AllocHGlobal((int)required);
            try {
                uint returned;
                bool read=GetTokenInformation(token,25,buffer,required,out returned);
                int readError=read?0:Marshal.GetLastWin32Error();
                if(!read || returned<labelSize || returned>required) return "integrity=unknown integrity_error="+readError;
                TokenMandatoryLabel label=(TokenMandatoryLabel)Marshal.PtrToStructure(buffer,typeof(TokenMandatoryLabel));
                if(label.Sid==IntPtr.Zero || !IsValidSid(label.Sid)) return "integrity=unknown integrity_reason=invalid-sid";
                IntPtr countPointer=GetSidSubAuthorityCount(label.Sid);
                if(countPointer==IntPtr.Zero) return "integrity=unknown integrity_reason=no-sid-count";
                byte count=Marshal.ReadByte(countPointer);
                if(count==0) return "integrity=unknown integrity_reason=empty-sid";
                IntPtr ridPointer=GetSidSubAuthority(label.Sid,(uint)(count-1));
                if(ridPointer==IntPtr.Zero) return "integrity=unknown integrity_reason=no-sid-rid";
                uint rid=unchecked((uint)Marshal.ReadInt32(ridPointer));
                string name;
                switch(rid) {
                    case 0x0000: name="untrusted"; break;
                    case 0x1000: name="low"; break;
                    case 0x2000: name="medium"; break;
                    case 0x2100: name="medium-plus"; break;
                    case 0x3000: name="high"; break;
                    case 0x4000: name="system"; break;
                    case 0x5000: name="protected"; break;
                    default: name="other"; break;
                }
                return "integrity="+name+" integrity_rid=0x"+rid.ToString("X");
            } finally { Marshal.FreeHGlobal(buffer); }
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
        [DllImport("user32.dll",SetLastError=true)] static extern bool OpenClipboard(IntPtr owner);
        [DllImport("user32.dll",SetLastError=true)] static extern bool CloseClipboard();
        [DllImport("user32.dll",SetLastError=true)] static extern bool EmptyClipboard();
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr GetClipboardData(uint format);
        [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetClipboardData(uint format,IntPtr data);
        [DllImport("user32.dll")] static extern IntPtr GetClipboardOwner();
        [DllImport("user32.dll")] static extern bool IsClipboardFormatAvailable(uint format);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr GlobalLock(IntPtr data);
        [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr data);
        [DllImport("kernel32.dll")] static extern UIntPtr GlobalSize(IntPtr data);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr GlobalAlloc(uint flags,UIntPtr bytes);
        [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr data);
        internal static bool AdminClipboardOwned(IntPtr owner,uint sequence) {
            return owner!=IntPtr.Zero && sequence!=0 && GetClipboardSequenceNumber()==sequence && GetClipboardOwner()==owner;
        }
        // Capture the exclusive write, including partial failure. Text formats may
        // be synthesized when this lock closes; finalization verifies that later
        // sequence before any command uses it or restores the original clipboard.
        internal static AdminClipboardResult AdminClipboardPublication(uint expectedSequence,Func<bool> open,Func<bool> close,
            Func<uint> sequence,Func<bool> empty,Func<bool> write,Func<bool> verify,Func<bool> owned,bool requireOwnership=false) {
            var result=new AdminClipboardResult { StopReason="clipboard-busy" };
            if(!open())return result;
            bool changed=false;
            try {
                result.StopReason="clipboard-changed";
                if(expectedSequence==0 || sequence()!=expectedSequence)return result;
                result.StopReason="clipboard-owner";
                if(requireOwnership && !owned())return result;
                result.StopReason="clipboard-publication";
                if(!empty())return result;
                changed=true;
                if(!write())return result;
                result.StopReason="clipboard-verification";
                if(!verify())return result;
                result.StopReason="clipboard-owner";
                if(!owned())return result;
                result.Ready=true;result.StopReason=null;
                return result;
            } catch(Exception ex) {
                result.Ready=false;result.NativeError=ex.HResult;return result;
            } finally {
                try {
                    if(changed && owned())result.Sequence=sequence();
                    if(result.Ready && result.Sequence==0) { result.Ready=false;result.StopReason="clipboard-verification"; }
                } finally {
                    if(!close()) { result.Ready=false;result.StopReason="clipboard-publication"; }
                }
            }
        }
        internal static bool AdminClipboardTextSupported(string text) {
            if(string.IsNullOrEmpty(text) || text.Length>512)return false;
            foreach(char character in text)if(character==0 || character>127)return false;
            return true;
        }
        static bool AdminClipboardTextMatches(uint format,string text,bool unicode,ref int nativeError) {
            IntPtr data=GetClipboardData(format);
            if(data==IntPtr.Zero) { if(nativeError==0)nativeError=Marshal.GetLastWin32Error();return false; }
            int required=(text.Length+1)*(unicode?2:1);
            if(GlobalSize(data).ToUInt64()<(ulong)required)return false;
            IntPtr address=GlobalLock(data);
            if(address==IntPtr.Zero) { if(nativeError==0)nativeError=Marshal.GetLastWin32Error();return false; }
            try {
                if(unicode)return Marshal.PtrToStringUni(address,text.Length)==text && Marshal.ReadInt16(address,text.Length*2)==0;
                for(int index=0;index<text.Length;index++)if(Marshal.ReadByte(address,index)!=(byte)text[index])return false;
                return Marshal.ReadByte(address,text.Length)==0;
            } finally { GlobalUnlock(data); }
        }
        internal static async Task<AdminClipboardResult> FinalizeAdminClipboard(IntPtr owner,string text,
            AdminClipboardResult publication,Func<string> blockingGate) {
            int nativeError=0;
            var result=await AdminClipboardFinalization(publication,
                ()=> { bool ok=OpenClipboard(owner);nativeError=ok?0:Marshal.GetLastWin32Error();return ok; },
                ()=> { bool ok=CloseClipboard();if(!ok)nativeError=Marshal.GetLastWin32Error();return ok; },
                GetClipboardSequenceNumber,()=>owner!=IntPtr.Zero && GetClipboardOwner()==owner,
                ()=>AdminClipboardTextSupported(text) && AdminClipboardTextMatches(13,text,true,ref nativeError) &&
                    AdminClipboardTextMatches(1,text,false,ref nativeError) && AdminClipboardTextMatches(7,text,false,ref nativeError),
                blockingGate,Task.Delay);
            if(result.NativeError==0 && nativeError!=0)result.NativeError=nativeError;
            return result;
        }
        internal static AdminClipboardResult PublishAdminClipboard(IntPtr owner,string text,uint expectedSequence,bool requireOwnership=false) {
            if(owner==IntPtr.Zero || !IsOurWindow(owner))return new AdminClipboardResult { StopReason="clipboard-owner" };
            if(!AdminClipboardTextSupported(text))
                return new AdminClipboardResult { StopReason="clipboard-publication" };
            char[] characters=(text+'\0').ToCharArray();
            IntPtr memory=GlobalAlloc(0x42,new UIntPtr((uint)(characters.Length*2)));
            if(memory==IntPtr.Zero)return new AdminClipboardResult { StopReason="clipboard-publication",NativeError=Marshal.GetLastWin32Error() };
            bool transferred=false;int nativeError=0;
            try {
                IntPtr address=GlobalLock(memory);
                if(address==IntPtr.Zero)return new AdminClipboardResult { StopReason="clipboard-publication",NativeError=Marshal.GetLastWin32Error() };
                try { Marshal.Copy(characters,0,address,characters.Length); }
                finally { GlobalUnlock(memory); }
                var result=AdminClipboardPublication(expectedSequence,
                    ()=> { bool ok=OpenClipboard(owner);if(!ok)nativeError=Marshal.GetLastWin32Error();return ok; },
                    ()=> { bool ok=CloseClipboard();if(!ok)nativeError=Marshal.GetLastWin32Error();return ok; },
                    GetClipboardSequenceNumber,
                    ()=> { bool ok=EmptyClipboard();if(!ok)nativeError=Marshal.GetLastWin32Error();return ok; },
                    ()=> {
                        transferred=SetClipboardData(13,memory)!=IntPtr.Zero;
                        if(!transferred)nativeError=Marshal.GetLastWin32Error();return transferred;
                    },
                    ()=>AdminClipboardTextMatches(13,text,true,ref nativeError),()=>GetClipboardOwner()==owner,requireOwnership);
                if(nativeError!=0)result.NativeError=nativeError;return result;
            } finally { if(!transferred)GlobalFree(memory); }
        }
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

        static uint cachedObsPid;
        static IntPtr cachedObsWindow;
        static bool cachedObsFocused;
        internal static bool ObsFocused() {
            IntPtr window=GetForegroundWindow();uint pid;
            if(window==IntPtr.Zero || GetWindowThreadProcessId(window,out pid)==0 || pid==0)return false;
            if(window==cachedObsWindow && pid==cachedObsPid)return cachedObsFocused;
            cachedObsWindow=window;cachedObsPid=pid;cachedObsFocused=false;
            try { using(var process=Process.GetProcessById((int)pid))cachedObsFocused=IsObsProcessName(process.ProcessName); }
            catch(ArgumentException){}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            return cachedObsFocused;
        }
        internal static bool IsObsProcessName(string name) {
            return string.Equals(name,"obs64",StringComparison.OrdinalIgnoreCase) || string.Equals(name,"obs32",StringComparison.OrdinalIgnoreCase);
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



        internal static async void ReturnMapKey(IntPtr window,int key,int scan) {
            // Only deliver the user's closing map press to the SCUM window that opened it.
            if(window==IntPtr.Zero||!IsWindow(window))return;
            uint pid;GetWindowThreadProcessId(window,out pid);
            try { using(var process=Process.GetProcessById((int)pid))if(!process.ProcessName.Equals("SCUM",StringComparison.OrdinalIgnoreCase)&&!process.ProcessName.Equals("SCUM-Win64-Shipping",StringComparison.OrdinalIgnoreCase))return; } catch { return; }
            if(!SetForegroundWindow(window))return;
            for(int i=0;i<50&&(GetAsyncKeyState(key)&0x8000)!=0;i++)await Task.Delay(10);
            if(GetForegroundWindow()!=window||(GetAsyncKeyState(key)&0x8000)!=0||IsAltOrTabOrWinDown())return;
            var layout=GetKeyboardLayout(GetWindowThreadProcessId(window,out pid));
            if(Key((uint)key,false,scan,layout))Key((uint)key,true,scan,layout);
        }
        internal static void ForceForeground(IntPtr window) {



            if(window==IntPtr.Zero || !IsWindow(window)) return;



            SetWindowPos(window,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0040); // HWND_TOPMOST, SWP_NOSIZE | SWP_NOMOVE | SWP_SHOWWINDOW



            SetForegroundWindow(window);



        }



        [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);



        [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);



        [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(System.Drawing.Point point);
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
        static CopyInputDeliveryGate activeCopyDelivery;
        internal static void CancelActiveCopy() {
            if(activeCopyDelivery!=null)activeCopyDelivery.Cancel();
            if(activeCopy!=null) activeCopy.Cancel();
        }
        internal static bool IsCoordinateCopyInput(UIntPtr tag){return CopyInputDeliveryGate.OwnsTag(tag);}
        internal static bool ShouldSuppressCoordinateInput(bool injected,bool down,UIntPtr tag) {
            // Key-ups always reach Windows, including cleanup after focus/chat loss.
            // Admin sends and other applications' injected input have separate tags.
            if(!injected || !down || !IsCoordinateCopyInput(tag))return false;
            var delivery=activeCopyDelivery;
            if(delivery==null || delivery.Tag!=tag)return true;
            if(CopyInProgress && delivery.AllowsDown(tag))return false;
            CancelActiveCopy();
            return true;
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



        static bool Key(uint vk,bool up,int capturedScan=0,IntPtr layout=default(IntPtr),UIntPtr inputTag=default(UIntPtr)) {



            INPUT input=new INPUT(); input.type=1;



            input.data.keyboard.vk=0;
            input.data.keyboard.extra=inputTag==UIntPtr.Zero?AdminInputTag:inputTag;



            uint mapped=capturedScan!=0?(uint)capturedScan:MapVirtualKeyEx(vk,4,layout);
            if(mapped==0) mapped=MapVirtualKey(vk,0);
            if((mapped&0xFF)==0) { CopyError="Keyboard scan code is unavailable. Capture the Copy Location key in Settings."; return false; }
            input.data.keyboard.scan=(ushort)(mapped&0xFF);



            input.data.keyboard.flags=8u|(up?2u:0u)|((mapped&0x100)!=0 || (mapped&0xFF00)!=0?1u:0u);



            bool ok=SendInput(1,new INPUT[]{input},Marshal.SizeOf(typeof(INPUT)))==1;
            int inputError=ok?0:Marshal.GetLastWin32Error();
            if(InputTraceActive) TraceInput("copy vk="+vk+" scan="+input.data.keyboard.scan+" flags="+input.data.keyboard.flags+" sent="+ok+" Windows_error="+inputError+
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
            CopyInputDeliveryGate delivery=null;
            var lease=new CopyInputLease((vk,up)=>Key(vk,up,(int)vk==effectiveCopyKey?copyScanCode:0,layout,delivery.Tag));
            delivery=new CopyInputDeliveryGate(()=>!lease.Cancelled && allowed() && GetForegroundWindow()==game && GameFocused()
                && !UserTypingOrActive() && !CursorBlocksCopy(IsCursorVisible(),fullMapActive!=null && fullMapActive()) && !IsRightMouseDown() && !IsAltOrTabOrWinDown());
            activeCopy=lease;
            activeCopyDelivery=delivery;



            try {



                bool sent=await CopyChord(lease.Send,Task.Delay,()=>delivery.AllowsDown(delivery.Tag),
                    copyModifierKey,effectiveCopyKey);
                return ClassifyCopy(sent && !lease.Cancelled,CopyError);
            } finally { CancelActiveCopy(); activeCopy=null; activeCopyDelivery=null; CopyInProgress=false; }
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
