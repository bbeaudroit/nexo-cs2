using System.Text.Json;
namespace CsEsp;
public sealed record EspSettings
{
    public SkinLoadout SkinLoadout {get;init;}=new();
    public bool Enabled {get;init;}
    public bool ShowTeammates {get;init;}
    public bool BunnyHop {get;init;}
    public bool AutoStrafe {get;init;}
    public int MovementKey {get;init;}=5;
    public int JumpKey {get;init;}=32;
    public int EspMaxDistance {get;init;}=0;
    public EspLayout EspLayout {get;init;}=new();
    public bool HideOnCaptureFailure {get;init;}=true;
    public int PrivacyKey {get;init;}=118;
    public bool ChamsEnabled {get;init;}
    public bool ChamsHistoryEnabled {get;init;}
    public int ChamsHistoryDelay {get;init;}=200;
    public int ChamsHistoryOpacity {get;init;}=12;
    public int ChamsHistoryColor {get;init;}=unchecked((int)0xff69d2e7);
    public bool ChamsSupersampling {get;init;}
    public int ChamsColor {get;init;}=unchecked((int)0xffa78bfa);
    public int ChamsOpacity {get;init;}=20;
    public bool ChamsOutline {get;init;}
    public int ChamsOutlineColor {get;init;}=unchecked((int)0xffc4a6ec);
    public int ChamsOutlineOpacity {get;init;}=85;
    public int ChamsOutlineWidth {get;init;}=1;
    public int ChamsDistance {get;init;}=60;
    public bool Boxes {get;init;}
    public bool Skeleton {get;init;}
    public bool Health {get;init;}
    public bool Names {get;init;}
    public bool Weapons {get;init;}
    public bool Distance {get;init;}
    public bool Radar {get;init;}
    public bool Status {get;init;}
    public bool DetailedStatus {get;init;}
    public bool GlowEnabled {get;init;}
    public int GlowStrength {get;init;}=45;
    public int GlowRadius {get;init;}=6;
    public int GlowColor {get;init;}=unchecked((int)0xffa78bfa);
    public bool LabelBackground {get;init;}
    public int LabelOpacity {get;init;}=68;
    public int RadarOpacity {get;init;}=78;
    public int ColorArgb {get;init;}=unchecked((int)0xff80ed99);
    public int BoneColorArgb {get;init;}=unchecked((int)0xfff1f5f9);
    public int Thickness {get;init;}=1;
    public int FontSize {get;init;}=9;
    public int Frames {get;init;}=120;
    public int RadarSize {get;init;}=190;
    public int RadarRange {get;init;}=1800;
    public int RadarX {get;init;}=24;
    public int RadarY {get;init;}=76;
    public bool CornerBoxes {get;init;}
    public bool Armor {get;init;}
    public bool HeadCircle {get;init;}
    public bool WeaponIcons {get;init;}
    public bool SoundEsp {get;init;}
    public int SoundTime {get;init;}=2;
    public int NameColor {get;init;}=unchecked((int)0xfff1f5f9);
    public int WeaponColor {get;init;}=unchecked((int)0xffd0e5e8);
    public int HealthColor {get;init;}=unchecked((int)0xff80ed99);
    public int ArmorColor {get;init;}=unchecked((int)0xff58a6ff);
    public int HeadColor {get;init;}=unchecked((int)0xff41d9e6);
    public int DotColor {get;init;}=unchecked((int)0xffff7979);
    public bool RadarOutlines {get;init;}
    public bool RadarDots {get;init;}
    public bool RadarHudPosition {get;init;}
    public bool WorldWeapons {get;init;}
    public bool WorldItems {get;init;}
    public bool WorldGrenades {get;init;}
    public bool WorldFire {get;init;}
    public bool Bomb {get;init;}
    public bool RadarWeapons {get;init;}
    public bool RadarGrenades {get;init;}
    public int WorldColor {get;init;}=unchecked((int)0xffffce73);
    public int BombColor {get;init;}=unchecked((int)0xffff7979);
    public int MenuKey {get;init;}=45;
    public int ToggleKey {get;init;}=119;
    public bool RecoilCrosshair {get;init;}
    public bool VSync {get;init;}
    public bool ExcludeFromCapture {get;init;}=true;
    public bool ChangeFov {get;init;}
    public int GameFov {get;init;}=90;
    public bool Hitmarker {get;init;}
    public bool HitSound {get;init;}
    public int HitSoundType {get;init;}
    public int HitVolume {get;init;}=35;
    public bool Spectators {get;init;}
    public bool VerboseSpectators {get;init;}
    public float Sensitivity {get;init;}=1.17f;
    public bool AutoSensitivity {get;init;}=true;
    public Dictionary<string,AimProfile>? AimProfiles {get;init;}
    public bool GrenadeHelper {get;init;}
    public bool GrenadeAlign {get;init;}
    public int GrenadeKey {get;init;}=4;
    public string Map {get;init;}="";
    public string GrenadeFilter {get;init;}="Todas";
    public int GrenadeDistance {get;init;}=1500;
    public int GrenadeColor {get;init;}=unchecked((int)0xff41d9e6);
    public bool WebRadar {get;init;}
    public AimProfile Profile(ushort weapon)
    {
        if(AimProfiles?.TryGetValue("arma:"+weapon,out var exact)==true&&exact is not null)return exact.Normalize();
        if(AimProfiles?.TryGetValue(AimMath.Group(weapon),out var group)==true&&group is not null)return group.Normalize();return new();
    }
    public EspSettings Normalize()
    {
        int menu=MenuKey is 35 or <7 or >254?45:MenuKey;
        int toggle=ToggleKey is 35 or <7 or >254?119:ToggleKey;
        if(toggle==menu)toggle=menu==119?120:119;
        int privacy=PrivacyKey is 35 or <7 or >254||PrivacyKey==menu||PrivacyKey==toggle?0:PrivacyKey;
        var movement=MovementBindings.Normalize(MovementKey,JumpKey,menu,toggle,privacy);
        return this with {SkinLoadout=(SkinLoadout??throw new JsonException("Invalid skin loadout")).Validate(),ChamsHistoryDelay=Math.Clamp(ChamsHistoryDelay,50,500),ChamsHistoryOpacity=Math.Clamp(ChamsHistoryOpacity,0,60),MovementKey=movement.Hold,JumpKey=movement.Jump,ChamsOpacity=Math.Clamp(ChamsOpacity,0,100),ChamsOutlineOpacity=Math.Clamp(ChamsOutlineOpacity,0,100),ChamsOutlineWidth=Math.Clamp(ChamsOutlineWidth,1,4),ChamsDistance=Math.Clamp(ChamsDistance,0,500),EspMaxDistance=Math.Clamp(EspMaxDistance,0,500),EspLayout=(EspLayout??new EspLayout()).Normalize(),PrivacyKey=privacy,GlowStrength=Math.Clamp(GlowStrength,0,100),GlowRadius=Math.Clamp(GlowRadius,2,14),LabelOpacity=Math.Clamp(LabelOpacity,0,100),RadarOpacity=Math.Clamp(RadarOpacity,0,100),GameFov=Math.Clamp(GameFov,60,140),HitVolume=Math.Clamp(HitVolume,0,100),HitSoundType=Math.Clamp(HitSoundType,0,2),Thickness=Math.Clamp(Thickness,1,4),FontSize=Math.Clamp(FontSize,7,16),Frames=Math.Clamp(Frames,30,240),RadarSize=Math.Clamp(RadarSize,140,320),RadarRange=Math.Clamp(RadarRange,500,5000),RadarX=Math.Clamp(RadarX,0,7000),RadarY=Math.Clamp(RadarY,0,4000),MenuKey=menu,ToggleKey=toggle,Sensitivity=Math.Clamp(float.IsFinite(Sensitivity)?Sensitivity:1.17f,.01f,20),SoundTime=Math.Clamp(SoundTime,1,10),GrenadeKey=Math.Clamp(GrenadeKey,1,254),GrenadeDistance=Math.Clamp(GrenadeDistance,100,10000),Map=(Map??"").Trim(),GrenadeFilter=GrenadeFilter??"Todas",AimProfiles=AimProfiles?.Where(p=>p.Value is not null&&p.Key.Length<64).Take(128).ToDictionary(p=>p.Key,p=>p.Value.Normalize())};
    }
    public static EspSettings Load(string file)
    {
        try {return LoadStrict(file);}
        catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException) {Console.WriteLine($"Configuracion predeterminada: {e.Message}");return new();}
    }
    public static EspSettings LoadStrict(string file)=>(JsonSerializer.Deserialize<EspSettings>(File.ReadAllText(file))??throw new JsonException("La configuración está vacía.")).Normalize();
    public void Save(string file)
    {
        string temporary=file+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(Normalize(),new JsonSerializerOptions{WriteIndented=true}));File.Move(temporary,file,true);
    }
}

