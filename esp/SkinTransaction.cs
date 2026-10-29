namespace CsEsp;
internal readonly record struct SkinIdentity(int ProcessId,long ProcessStartTicks,uint Handle,uint Serial,uint OwnerHandle,uint LocalPawnHandle,long Generation,ushort ItemDefinition);
internal readonly record struct SkinPaintValue(bool Present,int Kit){internal static SkinPaintValue Absent=>new(false,0);}
internal enum SkinWriteState{Applied,Conflict,Unavailable,FailedBeforeWrite,FailedAfterWrite}
internal enum SkinRefreshState{Pending,Complete,Failed,Unavailable}
internal enum SkinTransactionState{Idle,Unavailable,Rejected,PendingRefresh,Applied,Restoring,Restored,Conflict,Failed}
internal interface ISkinTransactionBackend
{
    bool SupportsRemoval{get;}
    bool TryCapture(out SkinIdentity identity);
    bool TryRead(SkinIdentity identity,out SkinPaintValue value);
    // A future backend must serialize identity validation and the typed value CAS.
    // FailedAfterWrite means the complete replacement was committed, not torn bytes.
    // It must never use a stale pointer or silently replace a different generation.
    SkinWriteState CompareExchange(SkinIdentity identity,SkinPaintValue expected,SkinPaintValue replacement);
    // A real backend must serialize/cancel earlier material work before a new
    // refresh or restoration; Pending/Unavailable do not prove quiescence.
    // Complete must describe the requested weapon/viewmodel generation, not
    // merely attribute readback or submission of a material request.
    bool TryBeginRefresh(SkinIdentity identity,out long ticket);
    SkinRefreshState PollRefresh(SkinIdentity identity,long ticket);
}
internal sealed class SkinTransaction(ISkinTransactionBackend? backend)
{
    SkinIdentity identity;
    SkinPaintValue original,lastWritten;
    bool transaction,owned,pending,restoring;
    long ticket;
    internal SkinTransactionState State{get;private set;}=SkinTransactionState.Idle;
    internal string Error{get;private set;}="";
    static bool ValidHandle(uint handle)=>handle is >0 and <0xfffffffe;
    static bool Valid(SkinIdentity value)=>value.ProcessId>0&&value.ProcessStartTicks>0&&ValidHandle(value.Handle)&&value.Serial==value.Handle>>15&&ValidHandle(value.OwnerHandle)&&value.OwnerHandle==value.LocalPawnHandle&&value.Generation>=0&&value.ItemDefinition>0;
    static bool Valid(SkinPaintValue value)=>value.Present?value.Kit>=0:value.Kit==0;
    bool Current()=>backend is not null&&backend.TryCapture(out var current)&&current==identity&&Valid(current);
    SkinTransactionState Set(SkinTransactionState state,string error=""){State=state;if(error.Length>0)Error=error;return state;}
    internal SkinTransactionState Apply(SkinPaintValue desired)
    {
        if(transaction)return Set(SkinTransactionState.Rejected,"Restore the existing transaction before applying another paint.");
        if(!Valid(desired))return Set(SkinTransactionState.Rejected,"Invalid paint value.");
        if(backend is null)return Set(SkinTransactionState.Unavailable,"No verified skin backend is installed.");
        try
        {
            if(!backend.TryCapture(out identity))return Set(SkinTransactionState.Unavailable,"Weapon identity unavailable.");
            if(!Valid(identity))return Set(SkinTransactionState.Rejected,"Weapon identity or local ownership is invalid.");
            if(!backend.TryRead(identity,out original)||!Valid(original))return Set(SkinTransactionState.Unavailable,"Original paint unavailable.");
            if((!original.Present||!desired.Present)&&!backend.SupportsRemoval)return Set(SkinTransactionState.Unavailable,"Restoring an absent attribute requires explicit removal support.");
            lastWritten=desired;transaction=true;owned=false;pending=false;restoring=false;Error="";
            if(original!=desired)
            {
                var result=backend.CompareExchange(identity,original,desired);
                owned=result is SkinWriteState.Applied or SkinWriteState.FailedAfterWrite;
                if(result==SkinWriteState.FailedAfterWrite)return RestoreInternal("Application failed after committing paint.");
                if(!owned){transaction=false;return Set(result==SkinWriteState.Conflict?SkinTransactionState.Conflict:result==SkinWriteState.Unavailable?SkinTransactionState.Unavailable:SkinTransactionState.Failed,"Paint was not committed.");}
            }
            if(!Current()||!backend.TryRead(identity,out var current)||current!=desired)return Set(SkinTransactionState.Conflict,"Identity or paint changed after application.");
            if(!backend.TryBeginRefresh(identity,out ticket)||ticket<=0)return RestoreInternal("Material refresh is unavailable.");
            pending=true;return Set(SkinTransactionState.PendingRefresh);
        }
        catch(Exception){return owned?RestoreInternal("Backend failed during application."):Set(SkinTransactionState.Unavailable,"Backend failed before a confirmed mutation.");}
    }
    internal SkinTransactionState Restore()=>RestoreInternal("");
    SkinTransactionState RestoreInternal(string error)
    {
        if(error.Length>0)Error=error;
        pending=false;
        if(!transaction)return Set(SkinTransactionState.Idle);
        if(backend is null)return Set(SkinTransactionState.Unavailable,"Restoration backend unavailable.");
        try
        {
            if(!Current())return Set(SkinTransactionState.Conflict,"Original weapon identity or ownership changed; no restoration written.");
            if(!backend.TryRead(identity,out var current))return Set(SkinTransactionState.Unavailable,"Current paint unavailable; no restoration written.");
            if(current!=lastWritten)return Set(SkinTransactionState.Conflict,"Paint changed outside this transaction; no restoration written.");
            if(owned)
            {
                var result=backend.CompareExchange(identity,lastWritten,original);
                if(result is not(SkinWriteState.Applied or SkinWriteState.FailedAfterWrite))return Set(result==SkinWriteState.Conflict?SkinTransactionState.Conflict:result==SkinWriteState.Unavailable?SkinTransactionState.Unavailable:SkinTransactionState.Failed,"Could not restore the owned paint.");
                owned=false;lastWritten=original;
            }
            if(!Current()||!backend.TryRead(identity,out current)||current!=original)return Set(SkinTransactionState.Conflict,"Identity or paint changed during restoration.");
            if(!backend.TryBeginRefresh(identity,out ticket)||ticket<=0)return Set(SkinTransactionState.Unavailable,"Original data restored, but its material refresh is unavailable.");
            restoring=true;pending=true;return Set(SkinTransactionState.Restoring);
        }
        catch(Exception){return Set(SkinTransactionState.Unavailable,"Restoration backend failed; visual completion is unknown.");}
    }
    internal SkinTransactionState Poll()
    {
        if(!transaction)return State;
        if(backend is null)return Set(SkinTransactionState.Unavailable,"Refresh backend unavailable.");
        try
        {
            if(!Current()||!backend.TryRead(identity,out var current)||current!=lastWritten)return Set(SkinTransactionState.Conflict,"Identity or paint changed; no automatic overwrite.");
            if(!pending)return State;
            switch(backend.PollRefresh(identity,ticket))
            {
                case SkinRefreshState.Pending:return State;
                case SkinRefreshState.Complete:
                    // Completion may be asynchronous: revalidate after polling too.
                    if(!Current()||!backend.TryRead(identity,out current)||current!=lastWritten)return Set(SkinTransactionState.Conflict,"Identity or paint changed during refresh completion.");
                    pending=false;
                    if(restoring){transaction=false;return Set(SkinTransactionState.Restored);}
                    return Set(SkinTransactionState.Applied);
                default:
                    if(restoring){pending=false;return Set(SkinTransactionState.Unavailable,"Original data restored, but material restoration was not confirmed.");}
                    return RestoreInternal("Material application failed or became unavailable.");
            }
        }
        catch(Exception){return restoring?Set(SkinTransactionState.Unavailable,"Material restoration failed."):RestoreInternal("Material application backend failed.");}
    }
}
