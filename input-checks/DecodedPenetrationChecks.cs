using CsEsp;
internal static class DecodedPenetrationChecks
{
 internal static void Run(){
  int passed=0;void Check(bool ok,string why){if(!ok)throw new Exception(why);passed++;}
  var state=new DecodedPenetration14192.State(36,2,4);
  var concrete=new DecodedPenetration14192.Span(8,100,.5f,.5f,'C','C',0,0,true);
  var result=DecodedPenetration14192.Obstruction(state,concrete);
  Check(result.Outcome==DecodedPenetration14192.Outcome.Continues&&Math.Abs(result.State.Damage-13.656666f)<.0001f&&result.State.Remaining==3,"Decoded concrete arithmetic golden case includes both factors of three");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{Thickness=32}).Outcome==DecodedPenetration14192.Outcome.Stopped,"Thicker obstruction exhausts damage");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{EntryMaterial='W',ExitMaterial='W'}).State.Damage>result.State.Damage,"Same wood branch overrides distance coefficient");
  var glass=concrete with{Thickness=4,EntryMaterial='G',ExitMaterial='G'};
  Check(Math.Abs(DecodedPenetration14192.Obstruction(state,glass).State.Damage-32.10278f)<.0001f,"Thin glass arithmetic uses decoded 0.05/3 constants");
  var mixedGlass=glass with{ExitMaterial='C'};
  Check(Math.Abs(DecodedPenetration14192.Obstruction(state,mixedGlass).State.Damage-17.656666f)<.0001f,"Different exit material bypasses glass and flag overrides");
  Check(DecodedPenetration14192.Obstruction(state,glass with{Thickness=6}).State.Damage<30,"Six-unit boundary does not take thin-glass branch");
  var flags=concrete with{Thickness=4,EntryFlags=1UL<<13,ExitFlags=1UL<<13};
  Check(DecodedPenetration14192.Obstruction(state,flags).State.Damage>35,"Thin matching bit-13 flags use special decoded loss branch");
  Check(DecodedPenetration14192.Obstruction(state,flags with{ExitFlags=0}).State.Damage<30,"One-sided flag does not enable the paired override");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{EntryMaterial='L',ExitMaterial='L'}).State.Damage>result.State.Damage,"Same plastic branch uses distance modifier two");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{EndDistance=3000}).Outcome==DecodedPenetration14192.Outcome.Continues,"Endpoint cutoff is strictly greater than 3000");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{EntryMaterial='W',ExitMaterial='C'}).State.Damage==result.State.Damage,"Different exit material does not enable same-material override");
  float previous=36;for(int thickness=0;thickness<=32;thickness++){
   float remaining=DecodedPenetration14192.Obstruction(state,concrete with{Thickness=thickness}).State.Damage;
   Check(remaining<=previous,"Resolved concrete damage must not rise with thickness");previous=remaining;
  }
  foreach(var invalid in new[]{concrete with{EntryFlags=null},concrete with{ExitFlags=null},concrete with{StaticNonPlayer=false},concrete with{EntryMaterial='F'},concrete with{Thickness=float.NaN},concrete with{MinimumModifier=-1},concrete with{Thickness=-1},concrete with{EntryModifier=float.PositiveInfinity}})Check(DecodedPenetration14192.Obstruction(state,invalid).Outcome==DecodedPenetration14192.Outcome.Unknown,"Incomplete or unsupported trace cannot imply hard or penetrable");
  Check(DecodedPenetration14192.Obstruction(state with{Remaining=0},concrete).Outcome==DecodedPenetration14192.Outcome.Stopped,"No penetration budget");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{EndDistance=3001}).Outcome==DecodedPenetration14192.Outcome.Stopped,"Decoded 3000-unit endpoint cutoff");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{EndDistance=3001}).State.Remaining==0,"Native cutoff clears the remaining penetration budget");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{EntryModifier=.09f,MinimumModifier=.09f}).Outcome==DecodedPenetration14192.Outcome.Stopped,"Entry coefficient below decoded cutoff");
  Check(DecodedPenetration14192.Obstruction(state,concrete with{EntryModifier=.09f,MinimumModifier=.09f}).State.Remaining==0,"Low entry coefficient also clears native budget");
  Check(DecodedPenetration14192.Obstruction(state with{Power=0},concrete).Outcome==DecodedPenetration14192.Outcome.Stopped,"Zero-power weapon does not divide by zero");
  var weaker=DecodedPenetration14192.Obstruction(state with{Power=1},concrete);Check(weaker.State.Damage<result.State.Damage,"Weapon power affects remaining damage");
  Check(Math.Abs(DecodedPenetration14192.FreeFlight(30,.85f,500)-25.5f)<.0001f,"Decoded free-flight attenuation over 500 units");
  Check(DecodedPenetration14192.FreeFlight(30,.85f,0)==30,"Zero free-flight distance retains damage");
  bool rejected=false;try{DecodedPenetration14192.FreeFlight(30,2,500);}catch(ArgumentOutOfRangeException){rejected=true;}Check(rejected,"Invalid attenuation inputs rejected");
  Console.WriteLine($"{passed} decoded arithmetic checks passed; offline branch only, no live prediction claim.");
 }
}
