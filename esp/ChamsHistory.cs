using System.Diagnostics;
using System.Numerics;
namespace CsEsp;
// Visual history only: never supplies targets or changes server lag compensation.
internal sealed class ChamsHistory
{
    readonly Dictionary<int,List<(long Time,Player Player)>> poses=[];
    long observed,lastCapture;
    string map="";
    internal int Count=>poses.Values.Sum(p=>p.Count);
    internal void Capture(Frame frame,bool enabled,int delay,string currentMap)
    {
        if(!enabled||currentMap!=map||observed!=0&&(frame.Timestamp<observed||Stopwatch.GetElapsedTime(observed,frame.Timestamp).TotalMilliseconds>500))Clear();
        map=currentMap;
        if(!enabled||frame.Timestamp==observed)return;
        observed=frame.Timestamp;
        var present=frame.Players.Where(p=>p.Health>0&&p.Bones.Length>0).Select(p=>p.EntityId).ToHashSet();
        foreach(int id in poses.Keys.Where(id=>!present.Contains(id)).ToArray())poses.Remove(id);
        if(lastCapture!=0&&Stopwatch.GetElapsedTime(lastCapture,frame.Timestamp).TotalMilliseconds<49)return;
        lastCapture=frame.Timestamp;
        foreach(var player in frame.Players)
        {
            if(player.Health<=0||player.Bones.Length==0||!Finite(player.Position)||player.Bones.Any(b=>!Finite(b))){poses.Remove(player.EntityId);continue;}
            if(!poses.TryGetValue(player.EntityId,out var list))poses[player.EntityId]=list=[];
            if(list.Count>0){var last=list[^1].Player;if(last.EntityHandle!=player.EntityHandle||last.Team!=player.Team||Vector3.DistanceSquared(last.Position,player.Position)>128*128)list.Clear();}
            list.Add((frame.Timestamp,player with{Bones=(Vector3[])player.Bones.Clone()}));
            while(list.Count>16||list.Count>1&&Stopwatch.GetElapsedTime(list[1].Time,frame.Timestamp).TotalMilliseconds>delay+60)list.RemoveAt(0);
        }
    }
    internal Player? Get(int id,Player current,long now,int delay)
    {
        if(!poses.TryGetValue(id,out var list)||list.Count==0||list[^1].Player.EntityHandle!=current.EntityHandle||list[^1].Player.Team!=current.Team)return null;
        for(int i=list.Count-1;i>=0;i--){double age=Stopwatch.GetElapsedTime(list[i].Time,now).TotalMilliseconds;if(age>=delay&&age<=delay+80)return list[i].Player;}
        return null;
    }
    static bool Finite(Vector3 p)=>float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z);
    internal void Clear(){poses.Clear();observed=lastCapture=0;}
}
