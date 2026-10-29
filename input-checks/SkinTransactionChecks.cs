using CsEsp;
internal static class SkinTransactionChecks
{
    internal static void Run()
    {
        int passed=0;void Check(bool ok,string message){if(!ok)throw new Exception(message);passed++;}
        var absent=SkinPaintValue.Absent;
        var desired=new SkinPaintValue(true,180);
        var backend=new FakeBackend();var transaction=new SkinTransaction(backend);
        Check(transaction.Apply(desired)==SkinTransactionState.PendingRefresh,"Committed paint must await material refresh");
        Check(backend.Value==desired&&transaction.State!=SkinTransactionState.Applied,"Byte/value application alone cannot imply visual completion");
        backend.Refresh=SkinRefreshState.Complete;
        Check(transaction.Poll()==SkinTransactionState.Applied,"Applied requires refresh completion and matching readback");
        Check(transaction.Restore()==SkinTransactionState.Restoring,"Restore also waits for its material refresh");
        Check(backend.Value==absent,"Restoring original absence removes the attribute rather than writing zero");
        Check(transaction.Poll()==SkinTransactionState.Restored,"Restoration completion is tracked independently");

        backend=new(){SupportsRemoval=false};transaction=new(backend);
        Check(transaction.Apply(desired)==SkinTransactionState.Unavailable&&backend.Writes==0,"Absent original requires explicit removal support before applying");
        backend=new(){Value=new(true,17),SupportsRemoval=false};transaction=new(backend);
        Check(transaction.Apply(desired)==SkinTransactionState.PendingRefresh,"Existing original can be changed without removal support");
        backend.Refresh=SkinRefreshState.Complete;transaction.Poll();transaction.Restore();
        Check(backend.Value==new SkinPaintValue(true,17),"Original and last-written values remain separate");

        backend=new(){Identity=FakeBackend.ValidIdentity with{OwnerHandle=4}};transaction=new(backend);
        Check(transaction.Apply(desired)==SkinTransactionState.Rejected&&backend.Writes==0,"Foreign ownership is rejected");
        foreach(var changed in new[]{FakeBackend.ValidIdentity with{ProcessId=0},FakeBackend.ValidIdentity with{Serial=0},FakeBackend.ValidIdentity with{ProcessStartTicks=0},FakeBackend.ValidIdentity with{ItemDefinition=0}}){backend=new(){Identity=changed};transaction=new(backend);Check(transaction.Apply(desired)==SkinTransactionState.Rejected&&backend.Writes==0,"Incomplete identity cannot begin a transaction");}

        backend=new();transaction=new(backend);transaction.Apply(desired);backend.Value=new(true,999);
        Check(transaction.Restore()==SkinTransactionState.Conflict&&backend.Value==new SkinPaintValue(true,999)&&backend.Writes==1,"Game/server changes must not be overwritten by restoration");
        backend=new();transaction=new(backend);transaction.Apply(desired);backend.Value=new(true,999);backend.Refresh=SkinRefreshState.Complete;
        Check(transaction.Poll()==SkinTransactionState.Conflict&&backend.Writes==1,"Refresh completion with changed paint never becomes Applied");

        foreach(var changed in new[]{FakeBackend.ValidIdentity with{Handle=0x20005,Serial=4},FakeBackend.ValidIdentity with{ProcessStartTicks=9},FakeBackend.ValidIdentity with{Generation=9},FakeBackend.ValidIdentity with{LocalPawnHandle=8,OwnerHandle=8},FakeBackend.ValidIdentity with{ItemDefinition=16}}){backend=new();transaction=new(backend);transaction.Apply(desired);backend.Identity=changed;backend.Refresh=SkinRefreshState.Complete;Check(transaction.Poll()==SkinTransactionState.Conflict&&backend.Writes==1,"Switch, respawn, recycled entity or process generation cannot receive another entity's writes");Check(transaction.Restore()==SkinTransactionState.Conflict&&backend.Writes==1,"Stale restoration cannot write to replacement entity");}

        backend=new(){FailFirstWriteAfterCommit=true};transaction=new(backend);
        Check(transaction.Apply(desired)==SkinTransactionState.Restoring&&backend.Value==absent&&backend.Writes==2,"Partial application failure rolls back only its confirmed committed value");
        backend.Refresh=SkinRefreshState.Complete;
        Check(transaction.Poll()==SkinTransactionState.Restored&&transaction.Error.Length>0,"Rollback completion preserves the original failure reason");
        backend=new(){FailFirstWriteBeforeCommit=true};transaction=new(backend);
        Check(transaction.Apply(desired)==SkinTransactionState.Failed&&backend.Value==absent&&backend.Writes==0,"Failure before mutation does not manufacture ownership or restoration");

        backend=new();transaction=new(backend);transaction.Apply(desired);backend.Refresh=SkinRefreshState.Failed;
        Check(transaction.Poll()==SkinTransactionState.Restoring&&backend.Value==absent,"Refresh failure rolls back committed paint");
        backend=new(){BeginRefreshAvailable=false};transaction=new(backend);
        Check(transaction.Apply(desired)!=SkinTransactionState.Applied&&backend.Value==absent,"Missing refresh capability never reports Applied and restores owned data");
        transaction=new(null);Check(transaction.Apply(desired)==SkinTransactionState.Unavailable,"Missing real backend is explicitly unavailable");
        backend=new(){ReadAvailable=false};transaction=new(backend);Check(transaction.Apply(desired)==SkinTransactionState.Unavailable&&backend.Writes==0,"Missing source value prevents application");

        backend=new(){BeforeCompare=()=>{}};transaction=new(backend);
        backend.BeforeCompare=()=>backend.Identity=backend.Identity with{Generation=77};
        Check(transaction.Apply(desired)==SkinTransactionState.Conflict&&backend.Writes==0,"Backend identity CAS closes the capture-to-write race");
        backend=new();transaction=new(backend);backend.BeforeCompare=()=>backend.Value=new(true,88);
        Check(transaction.Apply(desired)==SkinTransactionState.Conflict&&backend.Value.Kit==88&&backend.Writes==0,"Backend value CAS preserves changes between read and write");

        backend=new();transaction=new(backend);transaction.Apply(desired);backend.ThrowOnPoll=true;
        Check(transaction.Poll()!=SkinTransactionState.Applied&&backend.Value==absent,"Backend refresh exception cannot report success and rolls back owned data");
        backend=new();transaction=new(backend);transaction.Apply(desired);int writes=backend.Writes;
        Check(transaction.Apply(new(true,222))==SkinTransactionState.Rejected&&backend.Writes==writes,"A second apply cannot discard an active transaction's original");
        Check(transaction.Restore()==SkinTransactionState.Restoring&&backend.Value==absent,"Rejected second apply retains the original restoration target");
        backend=new();transaction=new(backend);Check(transaction.Apply(new(false,10))==SkinTransactionState.Rejected&&backend.Writes==0,"Absent values have one canonical representation");
        Check(transaction.Apply(new(true,-1))==SkinTransactionState.Rejected&&backend.Writes==0,"Negative paint identifiers rejected");
        Console.WriteLine($"{passed} skin transaction checks passed using an in-memory backend; no game integration or visual skin claim.");
    }

    sealed class FakeBackend:ISkinTransactionBackend
    {
        internal static readonly SkinIdentity ValidIdentity=new(41,123,0x10005,2,0x10006,0x10006,7,7);
        internal SkinIdentity Identity=ValidIdentity;
        internal SkinPaintValue Value=SkinPaintValue.Absent;
        public bool SupportsRemoval {get;set;}=true;
        internal bool ReadAvailable=true,BeginRefreshAvailable=true,FailFirstWriteAfterCommit,FailFirstWriteBeforeCommit,ThrowOnPoll;
        internal SkinRefreshState Refresh=SkinRefreshState.Pending;
        internal int Writes;
        internal Action? BeforeCompare;
        long ticket;
        public bool TryCapture(out SkinIdentity identity){identity=Identity;return true;}
        public bool TryRead(SkinIdentity identity,out SkinPaintValue value){value=Value;return ReadAvailable&&identity==Identity;}
        public SkinWriteState CompareExchange(SkinIdentity identity,SkinPaintValue expected,SkinPaintValue replacement)
        {
            BeforeCompare?.Invoke();BeforeCompare=null;
            if(identity!=Identity||expected!=Value)return SkinWriteState.Conflict;
            if(!replacement.Present&&!SupportsRemoval)return SkinWriteState.Unavailable;
            if(FailFirstWriteBeforeCommit){FailFirstWriteBeforeCommit=false;return SkinWriteState.FailedBeforeWrite;}
            Value=replacement;Writes++;
            if(FailFirstWriteAfterCommit){FailFirstWriteAfterCommit=false;return SkinWriteState.FailedAfterWrite;}
            return SkinWriteState.Applied;
        }
        public bool TryBeginRefresh(SkinIdentity identity,out long refreshTicket){refreshTicket=++ticket;return BeginRefreshAvailable&&identity==Identity;}
        public SkinRefreshState PollRefresh(SkinIdentity identity,long refreshTicket){if(ThrowOnPoll)throw new IOException("Injected refresh transport failure");return identity==Identity&&refreshTicket==ticket?Refresh:SkinRefreshState.Unavailable;}
    }
}
