using System.Security.Cryptography;
using System.Text.Json.Nodes;
using CsEsp;
internal static class WeaponSourceChecks
{
 internal static void Run()
 {
  int passed=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);passed++;}
  string dir=Path.Combine(Path.GetTempPath(),"nexo-weapon-"+Guid.NewGuid());Directory.CreateDirectory(dir);
  string weaponsFile=Path.Combine(dir,"weapons.json"),linksFile=Path.Combine(dir,"links.json");
  var weapons=JsonNode.Parse("""
  {"format":1,"localOnly":true,"damageComputationEnabled":false,"weapons":{"weapon_test":{"name":"weapon_test","weaponType":"WEAPONTYPE_PISTOL","stats":{"m_nDamage":30,"m_flPenetration":1,"m_flRange":4096,"m_flRangeModifier":0.85,"m_flArmorRatio":0.94,"m_flHeadshotMultiplier":4},"missingFields":[]}}}
  """)!;
  var links=JsonNode.Parse("""
  {"format":1,"localOnly":true,"damageComputationEnabled":false,"provenance":{"weaponsSHA256":""},"byItemId":{"4":{"itemDefinitionId":4,"weaponName":"weapon_test","sourceField":"items_game.items.4.name"}}}
  """)!;
  void Save(){File.WriteAllText(weaponsFile,weapons.ToJsonString());links["provenance"]!["weaponsSHA256"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(weaponsFile))).ToLowerInvariant();File.WriteAllText(linksFile,links.ToJsonString());}
  try{
   Save();var catalogue=WeaponSourceCatalog.Load(linksFile,weaponsFile);var entry=catalogue.Find(4);
   Check(entry is {Name:"weapon_test",Damage:30,Penetration:1,Range:4096},"Explicit item join and actual source stats");
   Check(entry!.IsBallisticFirearm,"Verified firearm source category must be distinguishable from equipment");
   Check(catalogue.Find(0) is null&&catalogue.Find(7) is null,"Unknown IDs must not inherit another weapon's stats");
   File.AppendAllText(weaponsFile," ");bool rejected=false;try{WeaponSourceCatalog.Load(linksFile,weaponsFile);}catch(InvalidDataException){rejected=true;}Check(rejected,"Mismatched source digest must fail");Save();
   foreach(var change in new Action<JsonNode>[] {x=>x["byItemId"]!["4"]!["weaponName"]="missing",x=>x["byItemId"]!["4"]!["itemDefinitionId"]=7,x=>x["byItemId"]!["04"]=x["byItemId"]!["4"]!.DeepClone()}){
    var bad=links.DeepClone();change(bad);File.WriteAllText(linksFile,bad.ToJsonString());rejected=false;try{WeaponSourceCatalog.Load(linksFile,weaponsFile);}catch(InvalidDataException){rejected=true;}Check(rejected,"Ambiguous or missing weapon mapping must fail");
   }
   Save();var badWeapon=weapons.DeepClone();badWeapon["weapons"]!["weapon_test"]!["stats"]!["m_flRangeModifier"]=2;File.WriteAllText(weaponsFile,badWeapon.ToJsonString());links["provenance"]!["weaponsSHA256"]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(weaponsFile))).ToLowerInvariant();File.WriteAllText(linksFile,links.ToJsonString());rejected=false;try{WeaponSourceCatalog.Load(linksFile,weaponsFile);}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid source coefficient must fail even with matching digest");
   Save();File.WriteAllText(linksFile,links.ToJsonString().Replace("\"format\":1","\"format\":1,\"format\":1"));rejected=false;try{WeaponSourceCatalog.Load(linksFile,weaponsFile);}catch(InvalidDataException){rejected=true;}Check(rejected,"Duplicate JSON fields must fail rather than choose a value");
   string realLinks=Path.GetFullPath("local-assets/collision/item-links.json"),realWeapons=Path.GetFullPath("local-assets/collision/weapons.json");
   if(File.Exists(realLinks)&&File.Exists(realWeapons)){var actual=WeaponSourceCatalog.Load(realLinks,realWeapons);Check(actual.Find(4) is {Name:"weapon_glock",Damage:30,Penetration:1}&&actual.Find(7) is {Name:"weapon_ak47",Damage:36,Penetration:2},"Local CS2 source data joins actual IDs");Check(actual.Find(60)?.Name=="weapon_m4a1_silencer"&&actual.Find(61)?.Name=="weapon_usp_silencer","Silenced variants stay distinct");Check(actual.Find(59)?.IsBallisticFirearm==false&&actual.Find(44)?.IsBallisticFirearm==false&&actual.Find(7)?.IsBallisticFirearm==true,"Knife and grenade stats must not be treated as firearm ballistics");}
  }finally{Directory.Delete(dir,true);}
  Console.WriteLine($"{passed} weapon source catalogue checks passed; no damage prediction or game writes.");
 }
}
