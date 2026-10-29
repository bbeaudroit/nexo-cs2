using System.Text.Json;
using System.Text.Json.Nodes;
using CsEsp;

internal static class CollisionAttributePolicyChecks
{
 internal static void Run()
 {
  int passed=0;
  JsonObject Attr(string group="Default",string[]? asTags=null,string[]? withTags=null,string[]? excludeTags=null)=>new()
  {
   ["m_CollisionGroupString"]=group,
   ["m_InteractAsStrings"]=JsonSerializer.SerializeToNode(asTags??[]),
   ["m_InteractWithStrings"]=JsonSerializer.SerializeToNode(withTags??[]),
   ["m_InteractExcludeStrings"]=JsonSerializer.SerializeToNode(excludeTags??[])
  };
  void Check(JsonNode? input,CollisionAttributePolicy.Decision expected,string label)
  {
   using var doc=JsonDocument.Parse(input?.ToJsonString()??"null");
   var result=CollisionAttributePolicy.Evaluate(doc.RootElement);
   if(result.Decision!=expected||string.IsNullOrWhiteSpace(result.Reason))throw new Exception("Collision attribute policy: "+label);
   passed++;
  }
  Check(Attr(),CollisionAttributePolicy.Decision.Participates,"documented default");
  var futureField=Attr();futureField["m_UnknownBulletMask"]=1;
  Check(futureField,CollisionAttributePolicy.Decision.Unknown,"unrecognized schema fields must not inherit a known preset");
  Check(Attr("default",excludeTags:["player"]),CollisionAttributePolicy.Decision.Participates,"documented default movement exclusion");
  Check(Attr("ConditionallySolid",["passbullets"]),CollisionAttributePolicy.Decision.Excluded,"documented grating");
  Check(Attr("conditionallysolid",["playerclip","npcclip"]),CollisionAttributePolicy.Decision.Excluded,"documented clip, unordered tags");
  Check(Attr("ConditionallySolid",["csgo_grenadeclip"]),CollisionAttributePolicy.Decision.Excluded,"documented grenade-only collision");
  foreach(var tags in new[]{new[]{"passbullets","window"},new[]{"ladder","passbullets"},new[]{"ladder","npcclip","playerclip"},new[]{"sky"},new[]{"window"},new[]{"npcclip"},new[]{"CONTENTS_SOLID"}})
   Check(Attr("ConditionallySolid",tags),CollisionAttributePolicy.Decision.Unknown,"undocumented combination "+string.Join(',',tags));
  Check(Attr(withTags:["bullet"]),CollisionAttributePolicy.Decision.Unknown,"nonempty interact_with");
  Check(Attr(excludeTags:["bullet"]),CollisionAttributePolicy.Decision.Unknown,"undocumented exclusion");
  Check(Attr("Default",["passbullets"]),CollisionAttributePolicy.Decision.Unknown,"group must match preset");
  Check(Attr("ConditionallySolid",["passbullets","passbullets"]),CollisionAttributePolicy.Decision.Unknown,"duplicate tags fail closed");
  foreach(string field in new[]{"m_CollisionGroupString","m_InteractAsStrings","m_InteractWithStrings","m_InteractExcludeStrings"})
  {
   var missing=Attr();missing.Remove(field);Check(missing,CollisionAttributePolicy.Decision.Unknown,"missing "+field);
   var wrong=Attr();wrong[field]=42;Check(wrong,CollisionAttributePolicy.Decision.Unknown,"invalid "+field);
  }
  var invalidElement=Attr();invalidElement["m_InteractAsStrings"]=new JsonArray(1);Check(invalidElement,CollisionAttributePolicy.Decision.Unknown,"non-string tag");
  Check(null,CollisionAttributePolicy.Decision.Unknown,"null attribute");
  Check(new JsonArray(),CollisionAttributePolicy.Decision.Unknown,"array attribute");
  using(var duplicate=JsonDocument.Parse("{\"m_CollisionGroupString\":\"Default\",\"m_CollisionGroupString\":\"ConditionallySolid\",\"m_InteractAsStrings\":[],\"m_InteractWithStrings\":[],\"m_InteractExcludeStrings\":[]}"))
  {if(CollisionAttributePolicy.Evaluate(duplicate.RootElement).Decision!=CollisionAttributePolicy.Decision.Unknown)throw new Exception("Duplicate attributes must fail closed");passed++;}
  var numeric=Attr();numeric["m_InteractAs"]=new JsonArray(3421025643L);
  Check(numeric,CollisionAttributePolicy.Decision.Unknown,"numeric tags cannot substitute or contradict symbolic preset");
  var complete=Attr("ConditionallySolid",["passbullets"]);complete["m_CollisionGroup"]=4258586587L;complete["m_InteractAs"]=new JsonArray(3421025643L);complete["m_InteractWith"]=new JsonArray();complete["m_InteractExclude"]=new JsonArray();complete["m_DetailLayers"]=new JsonArray();complete["m_DetailLayerStrings"]=new JsonArray();complete["m_nIncludeDetailLayerCount"]=0;
  Check(complete,CollisionAttributePolicy.Decision.Excluded,"imported metadata accepts documented symbolic preset");
  foreach(var mutate in new Action<JsonObject>[] {a=>a["m_CollisionGroup"]="hash",a=>a["m_InteractAs"]=new JsonArray(-1),a=>a["m_DetailLayers"]=new JsonArray(1),a=>a["m_DetailLayerStrings"]=new JsonArray("layer"),a=>a["m_nIncludeDetailLayerCount"]=1})
  {var bad=(JsonObject)complete.DeepClone();mutate(bad);Check(bad,CollisionAttributePolicy.Decision.Unknown,"invalid or unsupported imported metadata");}
  Console.WriteLine($"{passed} collision attribute policy checks passed; symbolic presets only, no damage prediction.");
 }
}
