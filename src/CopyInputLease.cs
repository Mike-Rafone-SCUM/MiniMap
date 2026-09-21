using System;
using System.Collections.Generic;

namespace ScumMiniMap {
    // Own only the keys injected for one copy, so interruption never releases unrelated keys.
    internal sealed class CopyInputLease {
        readonly Func<uint,bool,bool> send;
        readonly List<uint> held=new List<uint>();
        internal bool Cancelled { get; private set; }
        internal CopyInputLease(Func<uint,bool,bool> send) { this.send=send; }
        internal bool Send(uint key,bool up) {
            if(!up && Cancelled) return false;
            if(up && !held.Contains(key)) return true;
            bool ok=send(key,up);
            if(ok) { if(up) held.Remove(key); else if(!held.Contains(key)) held.Add(key); }
            return ok;
        }
        internal void Cancel() {
            Cancelled=true;
            // Copy key first, then its modifier. Do not replay an already released key-up.
            for(int i=held.Count-1;i>=0;i--) {
                uint key=held[i];
                if(send(key,true) || send(key,true)) held.RemoveAt(i);
            }
        }
    }
}
