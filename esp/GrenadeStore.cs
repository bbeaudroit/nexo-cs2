using System.Numerics;
using System.Text.Json;
namespace CsEsp;
public sealed record GrenadeSpot(string Id,string Map,string Name,string Grenade,string ThrowType,float X,float Y,float Z,float Pitch,float Yaw)
{
    public Vector3 Position=>new(X,Y,Z);
    public Vector2 Angles=>new(Pitch,Yaw);
}
internal sealed class GrenadeStore
{
    readonly string file;
    internal GrenadeSpot[] Spots {get;private set;}=[];
    internal GrenadeStore(string path){file=path;Reload();}
    internal void Reload(){try{Spots=(JsonSerializer.Deserialize<GrenadeSpot[]>(File.ReadAllText(file))??[]).Where(s=>!string.IsNullOrWhiteSpace(s.Map)&&float.IsFinite(s.X)&&float.IsFinite(s.Y)&&float.IsFinite(s.Z)&&float.IsFinite(s.Pitch)&&float.IsFinite(s.Yaw)).Take(5000).ToArray();}catch(Exception e)when(e is IOException or JsonException){Spots=[];}}
    internal void Add(GrenadeSpot spot){if(string.IsNullOrWhiteSpace(spot.Map)||string.IsNullOrWhiteSpace(spot.Name))throw new InvalidOperationException("Indicá mapa y nombre de la posición.");Spots=[..Spots,spot];Save();}
    internal void Remove(string id){Spots=Spots.Where(s=>s.Id!=id).ToArray();Save();}
    void Save(){File.WriteAllText(file+".tmp",JsonSerializer.Serialize(Spots,new JsonSerializerOptions{WriteIndented=true}));File.Move(file+".tmp",file,true);}
    internal GrenadeSpot[] Visible(EspSettings s,Vector3 position)=>s.Map.Length==0?[]:Spots.Where(p=>p.Map.Equals(s.Map,StringComparison.OrdinalIgnoreCase)&&(s.GrenadeFilter=="Todas"||p.Grenade==s.GrenadeFilter)&&Vector3.Distance(position,p.Position)<=s.GrenadeDistance).ToArray();
}
