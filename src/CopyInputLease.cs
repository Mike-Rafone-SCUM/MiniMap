using System;
using System.Collections.Generic;
using System.Threading;

namespace ScumMiniMap {
    // SendInput queues events. Revalidate an individual copy when its key-down
    // reaches the low-level hook, not just when the request was enqueued.
    internal sealed class CopyInputDeliveryGate {
        const ulong Family=0x5343000000000000UL;
        const ulong SequenceMask=0x0000FFFFFFFFFFFFUL;
        static long sequence=DateTime.UtcNow.Ticks&(long)SequenceMask;
        readonly Func<bool> allowed;
        volatile bool cancelled;
        internal static UIntPtr TagFamily {get{return new UIntPtr(Family);}}
        internal UIntPtr Tag {get;private set;}
        internal CopyInputDeliveryGate(Func<bool> allowed) {
            this.allowed=allowed;
            Tag=new UIntPtr(Family|((ulong)Interlocked.Increment(ref sequence)&SequenceMask));
        }
        internal static bool OwnsTag(UIntPtr tag) {return (tag.ToUInt64()&~SequenceMask)==Family;}
        internal void Cancel(){cancelled=true;}
        internal bool AllowsDown(UIntPtr tag) {
            if(tag!=Tag || cancelled || allowed==null)return false;
            try{return allowed();}catch(Exception){return false;}
        }
    }
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
