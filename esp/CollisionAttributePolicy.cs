using System.Text.Json;

namespace CsEsp;

// Participation in a static bullet collision query only. This is not a penetration
// or damage classification. Presets: local Valve work/collision_properties.txt.
internal static class CollisionAttributePolicy
{
 internal enum Decision { Participates, Excluded, Unknown }
 internal sealed record Result(Decision Decision,string Reason);
 static Result Unknown(string reason)=>new(Decision.Unknown,reason);
 static readonly HashSet<string> SupportedFields=new(StringComparer.Ordinal)
 {
  "m_CollisionGroup","m_CollisionGroupString",
  "m_InteractAs","m_InteractWith","m_InteractExclude",
  "m_InteractAsStrings","m_InteractWithStrings","m_InteractExcludeStrings",
  "m_DetailLayers","m_DetailLayerStrings","m_nIncludeDetailLayerCount"
 };

 internal static Result Evaluate(JsonElement attr)
 {
  if(attr.ValueKind!=JsonValueKind.Object)return Unknown("Collision attribute is not an object.");
  var names=new HashSet<string>(StringComparer.Ordinal);
  foreach(var property in attr.EnumerateObject())
  {
   if(!names.Add(property.Name))return Unknown("Duplicate collision attribute field.");
   if(!SupportedFields.Contains(property.Name))return Unknown("Unsupported collision attribute field: "+property.Name);
  }
  if(!attr.TryGetProperty("m_CollisionGroupString",out var groupValue)||groupValue.ValueKind!=JsonValueKind.String)return Unknown("Missing or invalid symbolic collision group.");
  string? group=groupValue.GetString();
  if(string.IsNullOrWhiteSpace(group))return Unknown("Missing or invalid symbolic collision group.");
  bool Tags(string key,out HashSet<string> tags)
  {
   tags=new(StringComparer.Ordinal);
   if(!attr.TryGetProperty(key,out var value)||value.ValueKind!=JsonValueKind.Array)return false;
   foreach(var tag in value.EnumerateArray())if(tag.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(tag.GetString())||!tags.Add(tag.GetString()!))return false;
   return true;
  }
  if(!Tags("m_InteractAsStrings",out var asTags)||!Tags("m_InteractWithStrings",out var withTags)||!Tags("m_InteractExcludeStrings",out var excludeTags))return Unknown("Missing, invalid or duplicate symbolic interaction tags.");
  // Numeric values are opaque hashes, never contents bits. Validate parallel array
  // shape if supplied; the symbolic preset alone determines the decision below.
  bool NumericTags(string key,int expected)
  {
   if(!attr.TryGetProperty(key,out var value))return true;
   return value.ValueKind==JsonValueKind.Array&&value.GetArrayLength()==expected&&value.EnumerateArray().All(x=>x.ValueKind==JsonValueKind.Number&&x.TryGetUInt32(out _));
  }
  if(!NumericTags("m_InteractAs",asTags.Count)||!NumericTags("m_InteractWith",withTags.Count)||!NumericTags("m_InteractExclude",excludeTags.Count))return Unknown("Invalid parallel numeric interaction metadata.");
  if(attr.TryGetProperty("m_CollisionGroup",out var numericGroup)&&(numericGroup.ValueKind!=JsonValueKind.Number||!numericGroup.TryGetUInt32(out _)))return Unknown("Invalid numeric collision group metadata.");
  foreach(string key in new[]{"m_DetailLayers","m_DetailLayerStrings"})
   if(attr.TryGetProperty(key,out var detail)&&(detail.ValueKind!=JsonValueKind.Array||detail.GetArrayLength()!=0))return Unknown("Unsupported collision detail layers.");
  if(attr.TryGetProperty("m_nIncludeDetailLayerCount",out var detailCount)&&(detailCount.ValueKind!=JsonValueKind.Number||!detailCount.TryGetInt32(out int count)||count!=0))return Unknown("Unsupported collision detail layer count.");
  if(withTags.Count!=0)return Unknown("Nonempty interact_with has no supported bullet preset.");
  if(group.Equals("Default",StringComparison.OrdinalIgnoreCase)&&asTags.Count==0)
  {
   if(excludeTags.Count==0)return new(Decision.Participates,"Valve default collision preset; participation does not establish penetration.");
   if(excludeTags.SetEquals(["player"]))return new(Decision.Participates,"Valve default_player_movement_exclude preset excludes player movement only.");
  }
  if(group.Equals("ConditionallySolid",StringComparison.OrdinalIgnoreCase)&&excludeTags.Count==0)
  {
   if(asTags.SetEquals(["passbullets"]))return new(Decision.Excluded,"Valve grating preset: solid to everything but bullets.");
   if(asTags.SetEquals(["npcclip","playerclip"]))return new(Decision.Excluded,"Valve clip preset: solid only to players and NPCs.");
   if(asTags.SetEquals(["csgo_grenadeclip"]))return new(Decision.Excluded,"Valve csgo_grenadeclip preset: solid to grenades only.");
  }
  return Unknown("Collision attributes do not exactly match a supported documented preset.");
 }
}
