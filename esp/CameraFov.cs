namespace CsEsp;
internal sealed class CameraFov : IDisposable
{
    readonly int process;
    readonly Func<long> liveCamera;
    readonly Func<long> liveController;
    nint writer;
    long camera,controller;
    int fovOffset,startOffset,desiredOffset;
    OwnedInt valueOwner=new(0),startOwner=new(0),desiredOwner=new(0);
    internal string Status {get;private set;}="Camera FOV off";
    internal CameraFov(int id,Func<long> currentCamera,Func<long> currentController){process=id;liveCamera=currentCamera;liveController=currentController;}
    bool Write(long address,int value)=>Native.WriteProcessMemory(writer,(nint)address,ref value,4,out var bytes)&&bytes==4;
    internal void Update(LocalState local,EspSettings settings,bool foreground)
    {
        if(camera!=0&&(camera!=local.Camera||controller!=local.Controller)){camera=controller=0;CloseWriter();}
        if(!settings.ChangeFov||!foreground||!local.Alive||local.Scoped){Restore();Status=!settings.ChangeFov?"Camera FOV off":local.Scoped?"Paused while scoped":"Paused outside gameplay";return;}
        if(!local.CameraValuesValid||local.FovOffset<=0||local.FovStartOffset<=0||local.DesiredFovOffset<=0||local.Camera!=liveCamera()||local.Controller!=liveController()||local.Fov is <0 or >179||local.FovStart is <0 or >179||local.DesiredFov is <0 or >179){Restore();Status="Camera data unavailable; no values written";return;}
        if(writer==0){writer=Native.OpenProcess(0x1038,false,process);if(writer==0){Status="Windows denied camera access";return;}}
        if(camera==0){camera=local.Camera;controller=local.Controller;valueOwner=new(local.Fov);startOwner=new(local.FovStart);desiredOwner=new(local.DesiredFov);fovOffset=local.FovOffset;startOffset=local.FovStartOffset;desiredOffset=local.DesiredFovOffset;}
        int value=Math.Clamp(settings.GameFov,60,140);
        if(local.Fov==value&&local.FovStart==value&&local.DesiredFov==value){Status="Requested FOV observed in camera data";return;}
        if(!Write(controller+desiredOffset,value)){Restore();Status="Could not write desired FOV";return;}
        desiredOwner.Written(value);
        if(Write(camera+fovOffset,value))
        {
            valueOwner.Written(value);
            if(Write(camera+startOffset,value)){startOwner.Written(value);Status="Camera FOV written; awaiting next sample";}
            else{Restore();Status="FOV restored after a partial write";}
        }
        else{Restore();Status="Could not apply camera FOV";}
    }
    unsafe void Restore()
    {
        if(camera!=0&&writer!=0&&camera==liveCamera())
        {
            RestoreField(camera+fovOffset,valueOwner);RestoreField(camera+startOffset,startOwner);
        }
        if(controller!=0&&writer!=0&&controller==liveController())RestoreField(controller+desiredOffset,desiredOwner);
        camera=controller=0;CloseWriter();
    }
    unsafe void RestoreField(long address,OwnedInt ownership)
    {
        int current=0;if(!Native.ReadValue(writer,(nint)address,&current,4,out var count)||count!=4)return;
        int? desired=ownership.RestoreValue(current);if(desired is not null)Write(address,desired.Value);
    }
    void CloseWriter(){if(writer!=0){Native.CloseHandle(writer);writer=0;}}
    public void Dispose()=>Restore();
}
