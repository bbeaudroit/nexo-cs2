using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.ComponentModel;

namespace CsEsp;
internal record Player(Vector3 Position,int Health,byte Team,string Name,ushort Weapon,int Ammo,Vector3[] Bones,float Distance)
{
    internal int Armor {get;init;}
    internal int Shots {get;init;}
    internal int EntityId {get;init;}
    internal uint EntityHandle {get;init;}
    internal bool IsTeammate {get;init;}
}
internal record Frame(Player[] Players,float[] Matrix,Vector3 LocalPosition,float Yaw,string Status,double ReadMs,long Timestamp,int BonesValid)
{
    internal static Frame Empty=new([],new float[16],default,0,"Waiting for match",0,Stopwatch.GetTimestamp(),0);
    internal LocalState Local {get;init;}=new();
    internal WorldItem[] World {get;init;}=[];
    internal string[] Spectators {get;init;}=[];
}
internal sealed partial class GameReader : IDisposable
{
    internal readonly Process Process;
    readonly nint handle,client;
    readonly long entitiesOffset,localOffset,controllerOffset,matrixOffset,anglesOffset;
    readonly int healthOffset,teamOffset,sceneOffset,originOffset,pawnOffset,lifeOffset,modelOffset,nameOffset;
    readonly int servicesOffset,activeOffset,itemOffset,ammoOffset;
    readonly Dictionary<long,(long Pawn,long Updated,string Name,ushort Weapon,int Ammo)> metadata=[];
    internal int Stride {get;private set;}
    internal long FailedReads {get;private set;}
    internal bool ProbeBones {get;set;}
    DateTime nextProbe;
    long list;
    readonly Dictionary<int,long> chunks=[];
    internal GameReader(string folder)
    {
        using var offsets=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"offsets.json")));
        using var schema=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"client_dll.json")));
        var off=offsets.RootElement.GetProperty("client.dll");
        long O(string n)=>off.GetProperty(n).GetInt64();
        entitiesOffset=O("dwEntityList");localOffset=O("dwLocalPlayerPawn");controllerOffset=O("dwLocalPlayerController");matrixOffset=O("dwViewMatrix");anglesOffset=O("dwViewAngles");
        var classes=schema.RootElement.GetProperty("client.dll").GetProperty("classes");
        int F(string cls,string n)=>classes.GetProperty(cls).GetProperty("fields").GetProperty(n).GetInt32();
        healthOffset=F("C_BaseEntity","m_iHealth");teamOffset=F("C_BaseEntity","m_iTeamNum");sceneOffset=F("C_BaseEntity","m_pGameSceneNode");lifeOffset=F("C_BaseEntity","m_lifeState");
        originOffset=F("CGameSceneNode","m_vecAbsOrigin");pawnOffset=F("CCSPlayerController","m_hPlayerPawn");modelOffset=F("CSkeletonInstance","m_modelState");nameOffset=F("CBasePlayerController","m_iszPlayerName");
        servicesOffset=F("C_BasePlayerPawn","m_pWeaponServices");activeOffset=F("CPlayer_WeaponServices","m_hActiveWeapon");ammoOffset=F("C_BasePlayerWeapon","m_iClip1");
        itemOffset=F("C_EconEntity","m_AttributeManager")+F("C_AttributeContainer","m_Item")+F("C_EconItemView","m_iItemDefinitionIndex");
        InitializeExtras(F,O);
        var matches=System.Diagnostics.Process.GetProcessesByName("cs2");if(matches.Length!=1)throw new InvalidOperationException("Open one instance of CS2.");Process=matches[0];
        var modules=Process.Modules.Cast<ProcessModule>().ToArray();var clientModule=modules.First(m=>m.ModuleName.Equals("client.dll",StringComparison.OrdinalIgnoreCase));client=clientModule.BaseAddress;
        handle=Native.OpenProcess(0x1010,false,Process.Id);if(handle==0)throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not open CS2 for reading");
        try
        {
            var engine=modules.First(m=>m.ModuleName.Equals("engine2.dll",StringComparison.OrdinalIgnoreCase)).BaseAddress;
            long buildOffset=offsets.RootElement.GetProperty("engine2.dll").GetProperty("dwBuildNumber").GetInt64();
            using var info=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"info.json")));int expected=info.RootElement.GetProperty("build_number").GetInt32();
            if(!Read((long)engine+buildOffset,out int actual)||actual!=expected)throw new InvalidOperationException($"Outdated dump: expected {expected}; read {actual}. Run the dumper again.");
            InitializeCameraSource(clientModule,actual,F("C_BaseModelEntity","m_vecViewOffset"));
            Console.WriteLine($"CS2 PID {Process.Id}; build {actual}; volcado: {folder}");
        }
        catch {Native.CloseHandle(handle);Process.Dispose();throw;}
    }
    unsafe bool Read<T>(long address,out T value) where T:unmanaged
    {
        T temp=default;value=default;if(address<0x10000||address>0x00007FFFFFFFFFFF)return false;
        if(!Native.ReadValue(handle,(nint)address,&temp,(nuint)sizeof(T),out var n)||n!=(nuint)sizeof(T)){FailedReads++;return false;}
        value=temp;return true;
    }
    byte[]? Bytes(long address,int length)
    {
        if(address<0x10000)return null;var data=new byte[length];
        if(!Native.ReadProcessMemory(handle,(nint)address,data,(nuint)length,out var n)||n!=(nuint)length){FailedReads++;return null;}return data;
    }
    long Pointer(long address)=>Read(address,out long p)?p:0;
    long Entity(int index,int stride)
    {
        index&=0x7fff;int page=index>>9;
        if(!chunks.TryGetValue(page,out long chunk))chunks[page]=chunk=Pointer(list+0x10+8*page);
        return chunk==0?0:Pointer(chunk+stride*(index&0x1ff));
    }
    long Pawn(long controller,int stride)
    {
        if(controller==0||!Read(controller+pawnOffset,out uint id)||id==0||id==uint.MaxValue)return 0;
        return Entity((int)id,stride);
    }
    int FindStride()
    {
        long localController=Pointer((long)client+controllerOffset);if(localController==0)return 0;
        int best=0,bestScore=0;
        for(int stride=0x60;stride<=0x100;stride+=8)
        {
            int score=0;
            for(int i=1;i<=64;i++)
            {
                long controller=Entity(i,stride);if(controller==localController)score+=100;
                long pawn=Pawn(controller,stride);
                if(pawn!=0&&Read(pawn+healthOffset,out int hp)&&hp is >=0 and <=100&&Read(pawn+teamOffset,out byte team)&&team is 2 or 3)score++;
            }
            if(score>bestScore){bestScore=score;best=stride;}
        }
        return bestScore>=100?best:0;
    }
    internal float[] ReadCamera()
    {
        var data=Bytes((long)client+matrixOffset,64);return data is null?new float[16]:MemoryMarshal.Cast<byte,float>(data).ToArray();
    }
    Vector3[] Bones(long scene,Vector3 origin)
    {
        // The schema exposes m_modelState; its internal bone pointer is validated
        // against live joint positions before any skeleton is drawn.
        long boneArray=Pointer(scene+modelOffset+0x80);var data=Bytes(boneArray,28*32);if(data is null)return [];
        var bones=new Vector3[28];for(int i=0;i<bones.Length;i++)bones[i]=MemoryMarshal.Read<Vector3>(data.AsSpan(i*32,12));
        if(ProbeBones){Console.WriteLine($"Huesos: origen={origin}; array=0x{boneArray:X}");for(int i=0;i<bones.Length;i++)Console.WriteLine($"{i}: {bones[i]} distancia={Vector3.Distance(bones[i],origin):F1}");ProbeBones=false;}
        var compact=bones[..23];
        if(RenderMath.ValidBones(compact,origin))return compact;
        return RenderMath.ValidBones(bones,origin)?bones:[];
    }
    (string Name,ushort Weapon,int Ammo) Details(long controller,long pawn)
    {
        long now=Stopwatch.GetTimestamp();
        if(metadata.TryGetValue(controller,out var old)&&old.Pawn==pawn&&Stopwatch.GetElapsedTime(old.Updated,now).TotalMilliseconds<150)return(old.Name,old.Weapon,old.Ammo);
        var raw=Bytes(controller+nameOffset,128);string name=raw is null?"Jugador":Encoding.UTF8.GetString(raw).Split('\0')[0];
        name=new string(name.Where(c=>!char.IsControl(c)).Take(28).ToArray());if(string.IsNullOrWhiteSpace(name))name="Jugador";
        long services=Pointer(pawn+servicesOffset),weapon=0;
        if(services!=0&&Read(services+activeOffset,out uint id)&&id!=uint.MaxValue)weapon=Entity((int)id,Stride);
        ushort type=weapon!=0&&Read(weapon+itemOffset,out ushort item)?item:(ushort)0;
        int ammo=weapon!=0&&Read(weapon+ammoOffset,out int a)&&a is >=0 and <=200?a:-1;
        metadata[controller]=(pawn,now,name,type,ammo);if(metadata.Count>256)metadata.Clear();return(name,type,ammo);
    }
    internal Frame Sample(bool skeleton=true,bool world=false,bool fullWorld=false,bool includeTeammates=false,bool movement=false)
    {
        long start=Stopwatch.GetTimestamp();var players=new List<Player>();LocalState extra=new();
        Frame Finish(string status,Vector3 origin=default,float yaw=0)
        {
            var worldItems=world?ReadWorld(fullWorld):[];var spectators=ReadSpectators();var matrix=ReadCamera();
            return new(players.ToArray(),matrix,origin,yaw,status,Stopwatch.GetElapsedTime(start).TotalMilliseconds,Stopwatch.GetTimestamp(),players.Count(p=>p.Bones.Length>0)){Local=extra,World=worldItems,Spectators=spectators};
        }
        if(Process.HasExited)return Frame.Empty with {Status="CS2 closed"};
        list=Pointer((long)client+entitiesOffset);long local=Pointer((long)client+localOffset);chunks.Clear();
        if(list==0||local==0){Stride=0;return Finish("Waiting for match and team");}
        if(!Read(local+teamOffset,out byte localTeam)||localTeam is not(2 or 3))return Finish("Choose a team");
        long localScene=Pointer(local+sceneOffset);bool positionRead=Read(localScene+originOffset,out Vector3 localPosition);bool anglesRead=Read((long)client+anglesOffset,out Vector3 angles);
        bool poseValid=positionRead&&anglesRead&&float.IsFinite(localPosition.X)&&float.IsFinite(localPosition.Y)&&float.IsFinite(localPosition.Z)&&float.IsFinite(angles.X)&&float.IsFinite(angles.Y)&&Math.Abs(angles.X)<=90&&Math.Abs(angles.Y)<=360;
        if(Stride==0&&DateTime.UtcNow>=nextProbe){nextProbe=DateTime.UtcNow.AddSeconds(2);Stride=FindStride();if(Stride!=0)Console.WriteLine($"Entidades validadas: stride 0x{Stride:X}");}
        if(Stride==0)return Finish("Could not validate entity structure");
        extra=ReadLocal(local,localPosition,angles,movement,poseValid);
        long page0=Pointer(list+0x10);var controllers=Bytes(page0,Stride*65);if(controllers is null)return Finish("Player list unavailable");
        for(int i=1;i<=64;i++)
        {
            long controller=MemoryMarshal.Read<long>(controllers.AsSpan(i*Stride,8)),pawn=Pawn(controller,Stride);if(pawn==0||pawn==local)continue;
            if(!Read(pawn+teamOffset,out byte team)||team is not(2 or 3)||!includeTeammates&&!RenderMath.IsEnemy(localTeam,team))continue;
            if(!Read(pawn+healthOffset,out int hp)||hp is <=0 or >100||!Read(pawn+lifeOffset,out byte life)||life!=0)continue;
            long scene=Pointer(pawn+sceneOffset);if(!Read(scene+originOffset,out Vector3 pos)||!float.IsFinite(pos.X)||!float.IsFinite(pos.Y)||!float.IsFinite(pos.Z))continue;
            var details=Details(controller,pawn);var bones=skeleton?Bones(scene,pos):[];
            Read(pawn+armorOffset,out int armor);Read(pawn+shotsOffset,out int shots);Read(controller+pawnOffset,out uint entityHandle);
            players.Add(new(pos,hp,team,details.Name,details.Weapon,details.Ammo,bones,Vector3.Distance(pos,localPosition)*.0254f){IsTeammate=team==localTeam,Armor=Math.Clamp(armor,0,100),Shots=shots,EntityHandle=entityHandle,EntityId=(int)(entityHandle&0x7fff)});
        }
        return Finish(players.Count==0?"No live players":includeTeammates?"Enemies and teammates":"Enemies only",localPosition,angles.Y);
    }
    public void Dispose(){Native.CloseHandle(handle);Process.Dispose();}
}
