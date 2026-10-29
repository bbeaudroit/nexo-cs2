namespace CsEsp;
// Translation of a bounded arithmetic branch from the locally audited server binary.
// Not connected to gameplay: it does not establish trace completeness or live accuracy.
internal static class DecodedPenetration14192
{
 internal enum Outcome { Unknown,Stopped,Continues }
 internal sealed record State(float Damage,float Power,int Remaining);
 internal sealed record Span(float Thickness,float EndDistance,float EntryModifier,float MinimumModifier,char EntryMaterial,char ExitMaterial,ulong? EntryFlags,ulong? ExitFlags,bool StaticNonPlayer);
 internal sealed record Result(Outcome Outcome,State State,float? Loss,string Reason);
 internal static Result Obstruction(State state,Span span)
 {
  Result Unknown(string why)=>new(Outcome.Unknown,state,null,why);
  Result Stop(string why)=>new(Outcome.Stopped,state,null,why);
  if(!float.IsFinite(state.Damage)||state.Damage<0||state.Damage>100000||!float.IsFinite(state.Power)||state.Power<0||state.Power>1000||state.Remaining<0)return Unknown("Invalid state");
  if(!float.IsFinite(span.Thickness)||span.Thickness<0||span.Thickness>10000000||!float.IsFinite(span.EndDistance)||span.EndDistance<0||span.EndDistance>10000000||!float.IsFinite(span.EntryModifier)||span.EntryModifier<0||span.EntryModifier>1000||!float.IsFinite(span.MinimumModifier)||span.MinimumModifier<0||span.MinimumModifier>span.EntryModifier)return Unknown("Invalid resolved span");
  if(!span.StaticNonPlayer||span.EntryFlags is null||span.ExitFlags is null||span.EntryMaterial=='F'||span.ExitMaterial=='F')return Unknown("Dynamic/player/contents branch not resolved");
  if(span.EndDistance>3000||span.EntryModifier<.1f)state=state with{Remaining=0};
  if(state.Remaining==0||state.Power==0||state.Damage<1)return Stop("No remaining penetration budget or damage");
  float modifier=span.MinimumModifier,damageFraction=.16f;
  // A9D73A–A9D7D8: the low-modifier branch bypasses all material overrides.
  if(modifier>=.1f&&span.EntryMaterial==span.ExitMaterial){
   if(span.EntryMaterial is 'U' or 'W')modifier=3;else if(span.EntryMaterial=='L')modifier=2;
   bool bothBit13=(span.EntryFlags.Value&(1UL<<13))!=0&&(span.ExitFlags.Value&(1UL<<13))!=0;
   if(span.Thickness<6){
    if(span.EntryMaterial is 'G' or 'Y'){modifier=3;damageFraction=.05f;}
    if(bothBit13){modifier=32;damageFraction=.00001f;}
   }else if(bothBit13)modifier=3;
  }
  if(modifier==0)return Stop("Zero resolved distance modifier");
  // A9D7D8–A9D957; retain float operation order rather than using a double formula.
  float inverse=MathF.Max(0,1/modifier);
  float weaponTerm=MathF.Max(0,(3/state.Power)*1.25f);
  float flatLoss=weaponTerm*(inverse*3)+damageFraction*state.Damage;
  float loss=span.Thickness*span.Thickness*inverse/24+flatLoss;
  if(!float.IsFinite(loss))return Unknown("Arithmetic overflow");
  float damage=state.Damage-MathF.Max(0,loss);
  bool continues=damage>=1;
  return new(continues?Outcome.Continues:Outcome.Stopped,state with{Damage=damage,Remaining=continues?state.Remaining-1:state.Remaining},loss,continues?"Decoded arithmetic permits next segment":"Damage exhausted");
 }
 internal static float FreeFlight(float damage,float rangeModifier,float distance)
 {
  if(!float.IsFinite(damage)||damage<0||damage>100000||!float.IsFinite(rangeModifier)||rangeModifier<0||rangeModifier>1||!float.IsFinite(distance)||distance<0||distance>10000000)throw new ArgumentOutOfRangeException(nameof(distance),"Invalid free-flight source data");
  // A9D1AD–A9D1D1: V_powf(rangeModifier, distance/500) * current damage.
  return MathF.Pow(rangeModifier,distance/500)*damage;
 }
}
