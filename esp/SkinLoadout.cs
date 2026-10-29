using System.Text.Json;
using System.Text.Json.Serialization;
namespace CsEsp;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SkinLoadout
{
    [JsonRequired,JsonPropertyName("version")]public int Version {get;init;}=1;
    [JsonRequired,JsonPropertyName("weapons")]public Dictionary<string,int> Weapons {get;init;}=new();
    static readonly Lazy<Dictionary<string,HashSet<int>>> Pairs=new(()=>{
        using var stream=typeof(SkinLoadout).Assembly.GetManifestResourceStream("Nexo.SkinCatalog.json")??throw new InvalidOperationException("Missing skin catalog");
        using var data=JsonDocument.Parse(stream);return data.RootElement.GetProperty("weapons").EnumerateArray().ToDictionary(w=>w.GetProperty("id").GetInt32().ToString(System.Globalization.CultureInfo.InvariantCulture),w=>w.GetProperty("paints").EnumerateArray().Select(p=>p.GetProperty("id").GetInt32()).ToHashSet());
    });
    public SkinLoadout Validate()
    {
        if(Version!=1||Weapons is null||Weapons.Count>35)throw new JsonException("Invalid skin loadout");
        foreach(var (item,paint)in Weapons)if(!Pairs.Value.TryGetValue(item,out var compatible)||!compatible.Contains(paint))throw new JsonException("Invalid weapon/paint pair");
        return this with{Weapons=new(Weapons)};
    }
}
