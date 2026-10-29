using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
namespace CsEsp;
internal record LocalState
{
    internal bool Alive {get;init;}
    internal bool MovementValid {get;init;}
    internal uint MovementFlags {get;init;}
    internal Vector3 Velocity {get;init;}
    internal byte MoveType {get;init;}
    internal float WaterLevel {get;init;}
    internal Vector3 Eye {get;init;}
    internal bool EyeValid {get;init;}
    internal bool AnglesValid {get;init;}
    internal Vector2 Angles {get;init;}
    internal Vector2 Recoil {get;init;}
    internal bool RecoilValid {get;init;}
    internal ushort Weapon {get;init;}
    internal int Ammo {get;init;}=-1;
    internal int CrosshairEntity {get;init;}=-1;
    internal int Shots {get;init;}
    internal long Camera {get;init;}
    internal int Fov {get;init;}
    internal int FovStart {get;init;}
    internal int FovOffset {get;init;}
    internal int FovStartOffset {get;init;}
    internal long Controller {get;init;}
    internal int DesiredFov {get;init;}
    internal int DesiredFovOffset {get;init;}
    internal bool CameraValuesValid {get;init;}
    internal bool Scoped {get;init;}
    internal float SensitivityScale {get;init;}=1;
    internal int TotalHits {get;init;}
    internal bool DamageValid {get;init;}
}
internal record WorldItem(Vector3 Position,string Kind,string Name,float Remaining=-1,float Duration=-1,float Defuse=-1,string Site="",Vector3[]? Points=null);
internal sealed partial class GameReader
{
    int armorOffset,shotsOffset,punchServicesOffset,punchBaseOffset,punchUnpredictableOffset,cameraServicesOffset,cameraPositionOffset,crosshairOffset;
    int observerPawnOffset,observerServicesOffset,observerTargetOffset,designerOffset,ownerOffset;
    int bombTickOffset,bombBlowOffset,bombDefusingOffset,bombDefusedOffset,bombDefuseOffset,bombLengthOffset,bombSiteOffset;
    int infernoCountOffset,infernoPositionsOffset;
    int movementFlagsOffset,velocityOffset,moveTypeOffset,waterLevelOffset;
    int fovValueOffset,fovStartOffset,desiredFovOffset,scopedOffset,sensitivityScaleOffset,damageServicesOffset,damageVectorOffset,damageAttackerOffset,damageRecipientOffset,damageHitsOffset;
    long highestOffset,globalOffset,bombPointerOffset;
    long worldUpdated,spectatorsUpdated;
    WorldItem[] worldCache=[];
    string[] spectatorsCache=[];
    readonly Dictionary<long,(long Time,string Name)> entityClasses=[];
    internal string WorldDiagnostics {get;private set;}="Sin sondeo de mundo";
    void InitializeExtras(Func<string,string,int> f,Func<string,long> o)
    {
        movementFlagsOffset=f("C_BaseEntity","m_fFlags");velocityOffset=f("C_BaseEntity","m_vecAbsVelocity");moveTypeOffset=f("C_BaseEntity","m_nActualMoveType");waterLevelOffset=f("C_BaseEntity","m_flWaterLevel");
        armorOffset=f("C_CSPlayerPawn","m_ArmorValue");shotsOffset=f("C_CSPlayerPawn","m_iShotsFired");punchServicesOffset=f("C_CSPlayerPawn","m_pAimPunchServices");punchBaseOffset=f("CCSPlayer_AimPunchServices","m_predictableBaseAngle");punchUnpredictableOffset=f("CCSPlayer_AimPunchServices","m_unpredictableBaseAngle");
        cameraServicesOffset=f("C_BasePlayerPawn","m_pCameraServices");cameraPositionOffset=f("C_BasePlayerPawn","m_vecLastCameraSetupLocalOrigin");crosshairOffset=f("C_CSPlayerPawn","m_iIDEntIndex");
        observerPawnOffset=f("CCSPlayerController","m_hObserverPawn");observerServicesOffset=f("C_BasePlayerPawn","m_pObserverServices");observerTargetOffset=f("CPlayer_ObserverServices","m_hObserverTarget");designerOffset=f("CEntityIdentity","m_designerName");ownerOffset=f("C_BaseEntity","m_hOwnerEntity");
        highestOffset=o("dwGameEntitySystem_highestEntityIndex");globalOffset=o("dwGlobalVars");bombPointerOffset=o("dwPlantedC4");
        bombTickOffset=f("C_PlantedC4","m_bBombTicking");bombBlowOffset=f("C_PlantedC4","m_flC4Blow");bombDefusingOffset=f("C_PlantedC4","m_bBeingDefused");bombDefusedOffset=f("C_PlantedC4","m_bBombDefused");bombDefuseOffset=f("C_PlantedC4","m_flDefuseCountDown");bombLengthOffset=f("C_PlantedC4","m_flTimerLength");bombSiteOffset=f("C_PlantedC4","m_nBombSite");
        infernoCountOffset=f("C_Inferno","m_fireCount");infernoPositionsOffset=f("C_Inferno","m_firePositions");
        fovValueOffset=f("CCSPlayerBase_CameraServices","m_iFOV");fovStartOffset=f("CCSPlayerBase_CameraServices","m_iFOVStart");scopedOffset=f("C_CSPlayerPawn","m_bIsScoped");sensitivityScaleOffset=f("C_BasePlayerPawn","m_flFOVSensitivityAdjust");
        desiredFovOffset=f("CBasePlayerController","m_iDesiredFOV");
        damageServicesOffset=f("CCSPlayerController","m_pDamageServices");damageVectorOffset=f("CCSPlayerController_DamageServices","m_DamageList");damageAttackerOffset=f("CDamageRecord","m_hPlayerControllerDamager");damageRecipientOffset=f("CDamageRecord","m_hPlayerControllerRecipient");damageHitsOffset=f("CDamageRecord","m_iNumHits");
    }
    LocalState ReadLocal(long local,Vector3 position,Vector3 angles,bool movement=false,bool poseValid=false)
    {
        Read(local+healthOffset,out int health);Read(local+lifeOffset,out byte life);Read(local+crosshairOffset,out int crosshair);Read(local+shotsOffset,out int shots);
        var details=Details(Pointer((long)client+controllerOffset),local);
        long punch=Pointer(local+punchServicesOffset);bool valid=Read(punch+punchBaseOffset,out Vector3 predictable)&&Read(punch+punchUnpredictableOffset,out Vector3 unpredictable);
        Vector2 recoil=default;
        if(valid){Read(punch+punchUnpredictableOffset,out unpredictable);recoil=new(predictable.X+unpredictable.X,predictable.Y+unpredictable.Y);valid=float.IsFinite(recoil.X)&&float.IsFinite(recoil.Y)&&recoil.Length()<45;}
        bool auditedEye=TryReadCameraEye(local,out Vector3 eye);
        bool eyeValid=auditedEye&&poseValid;
        if(!auditedEye){
            bool eyeRead=Read(local+cameraPositionOffset,out eye);
            bool eyeUsable=eyeRead&&float.IsFinite(eye.X)&&float.IsFinite(eye.Y)&&float.IsFinite(eye.Z)&&Vector3.Distance(eye,position)<=120&&Vector3.Distance(eye,position)>=15;
            if(!eyeUsable)eye=position+new Vector3(0,0,64);
        }
        long camera=Pointer(local+cameraServicesOffset),controller=CurrentController();
        bool readFov=Read(camera+fovValueOffset,out int fov),readStart=Read(camera+fovStartOffset,out int fovStart),readDesired=Read(controller+desiredFovOffset,out int desiredFov);
        Read(local+scopedOffset,out byte scoped);Read(local+sensitivityScaleOffset,out float sensitivity);
        if(!float.IsFinite(sensitivity)||sensitivity<=.01f||sensitivity>10)sensitivity=1;
        var damage=ReadDamage(Pointer((long)client+controllerOffset));
        uint moveFlags=0;Vector3 velocity=default;byte moveType=0;float water=0;
        bool movementValid=movement&&Read(local+movementFlagsOffset,out moveFlags)&&Read(local+velocityOffset,out velocity)&&Read(local+moveTypeOffset,out moveType)&&Read(local+waterLevelOffset,out water)&&float.IsFinite(velocity.X)&&float.IsFinite(velocity.Y)&&float.IsFinite(velocity.Z)&&velocity.LengthSquared()<=36000000&&float.IsFinite(water)&&water is >=0 and <=1&&moveType<=11;
        if(!movementValid){moveFlags=0;velocity=default;moveType=0;water=0;}
        return new(){MovementValid=movementValid,MovementFlags=moveFlags,Velocity=velocity,MoveType=moveType,WaterLevel=water,Alive=health>0&&life==0,Eye=eye,EyeValid=eyeValid,AnglesValid=poseValid,Angles=new(angles.X,angles.Y),Recoil=recoil,RecoilValid=valid,Weapon=details.Weapon,Ammo=details.Ammo,CrosshairEntity=crosshair,Shots=shots,Camera=camera,Fov=fov,FovStart=fovStart,FovOffset=fovValueOffset,FovStartOffset=fovStartOffset,Controller=controller,DesiredFov=desiredFov,DesiredFovOffset=desiredFovOffset,CameraValuesValid=camera!=0&&controller!=0&&readFov&&readStart&&readDesired,Scoped=scoped!=0,SensitivityScale=sensitivity,TotalHits=damage.Hits,DamageValid=damage.Valid};
    }
    (int Hits,bool Valid) ReadDamage(long controller)
    {
        long services=Pointer(controller+damageServicesOffset);if(services==0)return(0,false);
        long vector=services+damageVectorOffset;
        foreach(var layout in new[]{(Count:0,Data:8),(Count:16,Data:0)})
        {
            if(!Read(vector+layout.Count,out int count)||count<=0||count>128)continue;long data=Pointer(vector+layout.Data);if(data<0x10000)continue;
            foreach(bool indirect in new[]{false,true})
            {
                int hits=0,matched=0;
                for(int i=0;i<count;i++)
                {
                    long record=indirect?Pointer(data+i*8):data+i*0x78;
                    if(!Read(record+damageAttackerOffset,out uint attacker)||attacker==uint.MaxValue||Entity((int)attacker,Stride)!=controller)continue;
                    if(!Read(record+damageRecipientOffset,out uint recipient)||recipient==uint.MaxValue)continue;long other=Entity((int)recipient,Stride);if(other==0||other==controller)continue;
                    if(Read(record+damageHitsOffset,out int amount)&&amount is >=0 and <10000){hits+=amount;matched++;}
                }
                if(matched>0)return(hits,true);
            }
        }
        return(0,false);
    }
    internal long CurrentCamera()=>Pointer(Pointer((long)client+localOffset)+cameraServicesOffset);
    internal long CurrentController()=>Pointer((long)client+controllerOffset);
    string TextAt(long pointer,int length=96){var bytes=Bytes(pointer,length);return bytes is null?"":Encoding.UTF8.GetString(bytes).Split('\0')[0];}
    string ClassName(long entity)
    {
        long now=Stopwatch.GetTimestamp();if(entityClasses.TryGetValue(entity,out var old)&&Stopwatch.GetElapsedTime(old.Time,now).TotalSeconds<3)return old.Name;
        long identity=Pointer(entity+0x10);string name=TextAt(Pointer(identity+designerOffset));if(name.Length>80||name.Any(c=>c<32||c>126))name="";
        if(entityClasses.Count>8192)entityClasses.Clear();entityClasses[entity]=(now,name);return name;
    }
    string[] ReadSpectators()
    {
        if(list==0||Stride==0)return [];
        long now=Stopwatch.GetTimestamp();if(Stopwatch.GetElapsedTime(spectatorsUpdated,now).TotalMilliseconds<500)return spectatorsCache;spectatorsUpdated=now;
        long local=Pointer((long)client+localOffset);var names=new List<string>();
        for(int i=1;i<=64;i++)
        {
            long controller=Entity(i,Stride);if(controller==0||!Read(controller+observerPawnOffset,out uint observer)||observer==uint.MaxValue)continue;
            long pawn=Entity((int)observer,Stride),services=Pointer(pawn+observerServicesOffset);
            if(services==0||!Read(services+observerTargetOffset,out uint target)||target==uint.MaxValue||Entity((int)target,Stride)!=local)continue;
            string name=TextAt(controller+nameOffset,128);if(name.Length>0)names.Add(new string(name.Where(c=>!char.IsControl(c)).Take(28).ToArray()));
        }
        return spectatorsCache=names.Distinct().ToArray();
    }
    WorldItem[] ReadWorld(bool scanItems)
    {
        if(list==0||Stride==0)return [];
        long now=Stopwatch.GetTimestamp();if(Stopwatch.GetElapsedTime(worldUpdated,now).TotalMilliseconds<300)return worldCache;worldUpdated=now;
        var items=new List<WorldItem>();Read(list+highestOffset,out int highest);highest=Math.Clamp(highest,64,16384);
        var classesSeen=new HashSet<string>();int seen=0;
        for(int i=65;scanItems&&i<=highest;i++)
        {
            long entity=Entity(i,Stride);if(entity==0)continue;seen++;string name=ClassName(entity);if(name.Length==0)continue;if(classesSeen.Count<12)classesSeen.Add(name);
            bool inferno=name=="inferno",weapon=name.StartsWith("weapon_"),projectile=name.EndsWith("_projectile"),pickup=name.StartsWith("item_");if(!inferno&&!weapon&&!projectile&&!pickup)continue;
            long scene=Pointer(entity+sceneOffset);if(!Read(scene+originOffset,out Vector3 pos)||!float.IsFinite(pos.X)||!float.IsFinite(pos.Y)||!float.IsFinite(pos.Z))continue;
            if(weapon)
            {
                if(!Read(entity+ownerOffset,out uint owner)||owner is not(0 or uint.MaxValue))continue;
                Read(entity+itemOffset,out ushort id);bool grenade=name.Contains("grenade")||name.Contains("molotov")||name.Contains("flashbang")||name.Contains("decoy");
                items.Add(new(pos,grenade?"grenade":"weapon",id==0?name[7..]:WeaponNames.Label(id)));
            }
            else if(projectile)items.Add(new(pos,"grenade",name.Replace("_projectile","").Replace("grenade","")));
            else if(pickup){if(Read(entity+ownerOffset,out uint owner)&&owner is 0 or uint.MaxValue)items.Add(new(pos,"item",name[5..]));}
            else
            {
                Read(entity+infernoCountOffset,out int count);count=Math.Clamp(count,0,64);var raw=Bytes(entity+infernoPositionsOffset,count*12);var points=new List<Vector3>();
                if(raw is not null)for(int n=0;n<count;n++){var p=MemoryMarshal.Read<Vector3>(raw.AsSpan(n*12,12));if(float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z)&&Vector3.Distance(p,pos)<500)points.Add(p);}
                items.Add(new(pos,"fire","Fuego",Points:points.ToArray()));
            }
        }
        long bomb=Pointer(Pointer((long)client+bombPointerOffset));
        if(bomb!=0&&Read(bomb+bombTickOffset,out byte ticking)&&ticking!=0&&Read(bomb+bombDefusedOffset,out byte defused)&&defused==0)
        {
            long scene=Pointer(bomb+sceneOffset);if(Read(scene+originOffset,out Vector3 pos))
            {
                Read(bomb+bombBlowOffset,out float blow);Read(bomb+bombLengthOffset,out float duration);Read(bomb+bombSiteOffset,out int site);float remaining=-1,defuse=-1;
                long globals=Pointer((long)client+globalOffset);
                foreach(int offset in new[]{0x30,0x34})
                    if(Read(globals+offset,out float clock)&&float.IsFinite(clock)&&clock>0&&blow-clock is >=0 and <=60){remaining=blow-clock;if(Read(bomb+bombDefusingOffset,out byte doing)&&doing!=0&&Read(bomb+bombDefuseOffset,out float end)&&end-clock is >=0 and <=11)defuse=end-clock;break;}
                items.Add(new(pos,"bomb","C4",remaining,duration,defuse,site==0?"A":"B"));
            }
        }
        WorldDiagnostics=$"Mayor indice={highest}; entidades={seen}; clases={string.Join(',',classesSeen)}";
        return worldCache=items.ToArray();
    }
}
