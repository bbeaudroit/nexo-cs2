using System.Runtime.InteropServices;
namespace CsEsp;

// Observes only jump/direction keys; ignores injected events and never consumes physical input.
internal sealed class MovementInput:IDisposable
{
    readonly HashSet<int> physical=[];
    readonly Native.KeyboardProc callback;
    readonly MovementController controller;
    nint hook,window;
    int jumpKey=32,oldJumpKey=32,holdKey=5;
    bool armed,hookFailed;
    string? overrideStatus;
    internal MovementInput(){callback=Observe;controller=new(Physical,Send);}
    internal string Status=>overrideStatus??controller.Status;
    bool Physical(int key)=>key==holdKey&&!controller.OwnsKey(key)?(Native.GetAsyncKeyState(key)&0x8000)!=0:physical.Contains(key);
    bool Send(Native.Input[] keys)
    {
        if(keys.Any(k=>k.KeyFlags==0)&&(window==0||Native.GetForegroundWindow()!=window||ShortcutModifierHeld()))return false;
        return Native.SendInput((uint)keys.Length,keys,40)==keys.Length;
    }
    bool Released(int key)=>key==0||(Native.GetAsyncKeyState(key)&0x8000)==0;
    static bool ShortcutModifierHeld()=>(Native.GetAsyncKeyState(17)&0x8000)!=0||(Native.GetAsyncKeyState(18)&0x8000)!=0||(Native.GetAsyncKeyState(91)&0x8000)!=0||(Native.GetAsyncKeyState(92)&0x8000)!=0;
    internal void Configure(Frame frame,EspSettings settings,bool foreground,nint target)
    {
        window=target;overrideStatus=null;
        bool enabled=settings.BunnyHop||settings.AutoStrafe;
        bool changed=jumpKey!=settings.JumpKey||holdKey!=settings.MovementKey;
        if(changed){oldJumpKey=jumpKey;jumpKey=settings.JumpKey;holdKey=settings.MovementKey;armed=false;}
        if(!enabled)
        {
            armed=false;hookFailed=false;controller.Update(frame,settings,false);
            if(!controller.OwnsInput&&hook!=0){Native.UnhookWindowsHookEx(hook);hook=0;physical.Clear();}
            return;
        }
        if(hook==0&&!hookFailed){hook=Native.SetWindowsHookEx(13,callback,Native.GetModuleHandle(null),0);hookFailed=hook==0;}
        if(hookFailed){controller.Update(frame,settings,false);overrideStatus="Keyboard tracker unavailable; movement paused";return;}
        if(!foreground||target==0){armed=false;controller.Update(frame,settings,false);return;}
        if(ShortcutModifierHeld()){armed=false;controller.Update(frame,settings,false);overrideStatus="Release Ctrl / Alt / Windows to use movement";return;}
        if(!armed)
        {
            controller.Update(frame,settings,false);
            if(controller.OwnsInput||!Released(holdKey)||!Released(jumpKey)||!Released(oldJumpKey)||!Released(65)||!Released(68)||!Released(87)||!Released(83))
            {overrideStatus="Release assist, jump and direction keys to arm";return;}
            physical.Clear();armed=true;oldJumpKey=jumpKey;
        }
        controller.Update(frame,settings,true);
    }
    nint Observe(int code,nint message,nint data)
    {
        if(code>=0&&message is 0x100 or 0x101 or 0x104 or 0x105)
        {
            var e=Marshal.PtrToStructure<Native.KeyboardEvent>(data);int key=(int)e.Code;
            if((e.Flags&0x10)==0&&(key==jumpKey||key==oldJumpKey||controller.OwnsKey(key)||key is 65 or 68 or 87 or 83))
            {if(message is 0x100 or 0x104)physical.Add(key);else physical.Remove(key);}
        }
        return Native.CallNextHookEx(hook,code,message,data);
    }
    public void Dispose(){armed=false;controller.Dispose();if(hook!=0){Native.UnhookWindowsHookEx(hook);hook=0;}physical.Clear();}
}
