using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
namespace CsEsp;
internal sealed class WeaponSourceCatalog
{
 internal sealed record Entry(string Name,string? WeaponType,int Damage,double Penetration,double Range,double RangeModifier,double ArmorRatio,double HeadshotMultiplier)
 {
  internal bool IsBallisticFirearm=>WeaponType is "WEAPONTYPE_PISTOL" or "WEAPONTYPE_RIFLE" or "WEAPONTYPE_SNIPER_RIFLE" or "WEAPONTYPE_SUBMACHINEGUN" or "WEAPONTYPE_MACHINEGUN" or "WEAPONTYPE_SHOTGUN";
 }
 readonly Dictionary<ushort,Entry> entries=[];
 internal Entry? Find(ushort itemId)=>entries.GetValueOrDefault(itemId);
 internal static WeaponSourceCatalog Load(string linksFile,string weaponsFile)
 {
  try{
   byte[] linksBytes=Read(linksFile),weaponBytes=Read(weaponsFile);
   using var linksDoc=JsonDocument.Parse(linksBytes,new(){MaxDepth=64});using var weaponDoc=JsonDocument.Parse(weaponBytes,new(){MaxDepth=64});
   var links=linksDoc.RootElement;var weaponsRoot=weaponDoc.RootElement;ValidateJson(links);ValidateJson(weaponsRoot);
   foreach(var root in new[]{links,weaponsRoot})if(root.GetProperty("format").GetInt32()!=1||!root.GetProperty("localOnly").GetBoolean()||root.GetProperty("damageComputationEnabled").GetBoolean())throw new InvalidDataException("Unsupported source catalogue format");
   string digest=links.GetProperty("provenance").GetProperty("weaponsSHA256").GetString()??"";
   if(digest!=Convert.ToHexString(SHA256.HashData(weaponBytes)).ToLowerInvariant())throw new InvalidDataException("Weapon source catalogue digest mismatch; regenerate item links");
   var weapons=weaponsRoot.GetProperty("weapons");if(weapons.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Invalid weapon definitions");
   var byId=links.GetProperty("byItemId");if(byId.ValueKind!=JsonValueKind.Object)throw new InvalidDataException("Invalid item mappings");
   var result=new WeaponSourceCatalog();
   foreach(var mapping in byId.EnumerateObject()){
    if(result.entries.Count>=10000||!ushort.TryParse(mapping.Name,NumberStyles.None,CultureInfo.InvariantCulture,out ushort id)||mapping.Name!=id.ToString(CultureInfo.InvariantCulture))throw new InvalidDataException("Invalid item definition ID");
    var row=mapping.Value;if(row.GetProperty("itemDefinitionId").GetInt32()!=id||row.GetProperty("sourceField").GetString()!=$"items_game.items.{id}.name")throw new InvalidDataException("Inconsistent item mapping");
    string name=row.GetProperty("weaponName").GetString()??"";
    if(name.Length is <1 or >100||name.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='_')||!weapons.TryGetProperty(name,out var definition)||definition.GetProperty("name").GetString()!=name)throw new InvalidDataException("Missing or inconsistent weapon source");
    var stats=definition.GetProperty("stats");int damage=stats.GetProperty("m_nDamage").GetInt32();if(damage is <0 or >100000)throw new InvalidDataException("Invalid source damage");
    double Stat(string field,double min,double max){double value=stats.GetProperty(field).GetDouble();if(!double.IsFinite(value)||value<min||value>max)throw new InvalidDataException("Invalid source coefficient: "+field);return value;}
    string? type=definition.TryGetProperty("weaponType",out var category)&&category.ValueKind!=JsonValueKind.Null?category.GetString():null;
    if(type is not null&&(type.Length>100||type.Any(char.IsControl)))throw new InvalidDataException("Invalid weapon category");
    double range=Stat("m_flRange",0,10000000);if(range==0)throw new InvalidDataException("Invalid source range");
    result.entries.Add(id,new(name,type,damage,Stat("m_flPenetration",0,1000),range,Stat("m_flRangeModifier",0,1),Stat("m_flArmorRatio",0,1000),Stat("m_flHeadshotMultiplier",0,1000)));
   }
   return result;
  }catch(Exception e)when(e is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException){throw new InvalidDataException("Invalid weapon source catalogue",e);}
 }
 static byte[] Read(string file){if(new FileInfo(file).Length>16*1024*1024)throw new InvalidDataException("Weapon catalogue size limit");byte[] bytes=File.ReadAllBytes(file);if(bytes.Length>16*1024*1024)throw new InvalidDataException("Weapon catalogue size limit");return bytes;}
 static void ValidateJson(JsonElement value){if(value.ValueKind==JsonValueKind.Object){var keys=new HashSet<string>(StringComparer.Ordinal);foreach(var property in value.EnumerateObject()){if(!keys.Add(property.Name))throw new InvalidDataException("Duplicate JSON field");ValidateJson(property.Value);}}else if(value.ValueKind==JsonValueKind.Array)foreach(var item in value.EnumerateArray())ValidateJson(item);}
}
