using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace CsEsp;
internal sealed record RadarLayer(string Name,float Min,float Max,string Image);
internal sealed record RadarMap(string Name,float X,float Y,float Scale,int Width,int Height,string Image,RadarLayer[] Layers)
{
    internal Vector2 Project(Vector3 position)=>new((position.X-X)/(Scale*Width),(Y-position.Y)/(Scale*Height));
}
internal sealed class RadarMapCatalog
{
    readonly Dictionary<string,RadarMap> maps=new(StringComparer.Ordinal);
    internal string DirectoryPath {get;}
    internal IEnumerable<RadarMap> All=>maps.Values;
    internal RadarMapCatalog(string folder)
    {
        DirectoryPath=Path.GetFullPath(folder);
        string file=Path.Combine(folder,"maps.json");if(!File.Exists(file))return;
        try{
            if(new FileInfo(file).Length>1024*1024)return;
            foreach(var map in JsonSerializer.Deserialize<RadarMap[]>(File.ReadAllText(file),new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??[])
                if(map is not null&&Normalize(map.Name)==map.Name&&float.IsFinite(map.X)&&float.IsFinite(map.Y)&&float.IsFinite(map.Scale)&&map.Scale>0&&map.Width==1024&&map.Height==1024&&SafeImage(map.Image)&&map.Layers is not null&&map.Layers.Length<=8&&map.Layers.All(l=>l is not null&&SafeImage(l.Image)&&float.IsFinite(l.Min)&&float.IsFinite(l.Max)&&l.Max>l.Min)&&File.Exists(Path.Combine(folder,map.Image)))maps.TryAdd(map.Name,map);
        }catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){Console.Error.WriteLine("Radar maps unavailable: "+e.Message);}
    }
    static bool SafeImage(string? file)=>file is not null&&Regex.IsMatch(file,@"\A[a-z0-9_]{1,80}\.png\z",RegexOptions.CultureInvariant);
    internal static string Normalize(string? input)
    {
        if(string.IsNullOrEmpty(input)||input.Length>160||input.Contains("..")||input.Contains('\\'))return "";
        string name=input.Split('/').Last();return Regex.IsMatch(name,@"\A[a-z0-9_]{1,64}\z",RegexOptions.CultureInvariant)?name:"";
    }
    internal RadarMap? Find(string name)=>maps.GetValueOrDefault(Normalize(name));
    internal string? ImagePath(string name)=>SafeImage(name)&&maps.Values.Any(m=>m.Image==name||m.Layers.Any(l=>l.Image==name))&&File.Exists(Path.Combine(DirectoryPath,name))?Path.Combine(DirectoryPath,name):null;
}
