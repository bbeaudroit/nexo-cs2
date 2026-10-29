namespace CsEsp;
internal sealed partial class Overlay
{
    internal bool ExternalUi;
    bool externalVisible;
    bool menuRequested=true;
    long menuVersion;
    bool externalHotkey;
    bool overlayCaptureProtected;
    uint captureAffinity;
    int captureError;
    nint externalWindow;
    readonly CancellationTokenSource controlStopping=new();
    internal EspSettings ControlSettings=>settings;
    long settingsVersion;
    bool settingsSaved=true;
    string saveError="";
    internal void ControlUpdate(EspSettings value)=>UpdateSettings(value);
    internal object ControlState()=>new
    {
        connected=true,gameRunning=!game.Process.HasExited,players=Latest.Players.Length,fps,readMs=Latest.ReadMs,sampleAt=Latest.Timestamp,
        ageMs=System.Diagnostics.Stopwatch.GetElapsedTime(Latest.Timestamp).TotalMilliseconds,
        menuRequested,menuVersion,externalVisible,menuKey=settings.MenuKey,
        gameFocused=gameWindow!=0&&Native.GetForegroundWindow()==gameWindow,uiFocused=externalWindow!=0&&Native.GetForegroundWindow()==externalWindow,
        gameMinimized=gameWindow!=0&&Native.IsIconic(gameWindow),gameBounds=GameBounds(),
        feedback=feedbackText,webLink=web.Link,webStatus=web.Status,settings,settingsVersion,settingsSaved,saveError,overlayCaptureProtected=CaptureProtected,captureAffinity,captureError,captureMode=captureCompatible?"compatible":"alpha",
        spots=grenades.Spots,weapon=WeaponNames.Label(Latest.Local.Weapon),weaponId=Latest.Local.Weapon,weaponGroup=AimMath.Group(Latest.Local.Weapon),aimStatus=input.Status,triggerStatus=input.TriggerStatus,
        cameraFovStatus=cameraFov.Status,cameraFov=Latest.Local.Fov,desiredFov=Latest.Local.DesiredFov,cameraValuesValid=Latest.Local.CameraValuesValid,chamsStatus=meshChams.Status,chamsHistorySamples=chamsHistory.Count,radarMap=mapFeed.Current,radarMapStatus=mapFeed.Status,
        renderMs,chamsMs,movementStatus=movement.Status,movementDataValid=Latest.Local.MovementValid,movementOnGround=(Latest.Local.MovementFlags&1)!=0,movementSpeed=Latest.Local.MovementValid?new System.Numerics.Vector2(Latest.Local.Velocity.X,Latest.Local.Velocity.Y).Length():0,detectedSensitivity=settings.AutoSensitivity?sensitivity.Value:null,effectiveSensitivity=SensitivityConfig.Resolve(settings.AutoSensitivity,sensitivity.Value,settings.Sensitivity),sensitivityStatus=settings.AutoSensitivity?sensitivity.Status:"Manual sensitivity"
    };
    internal string ControlCommand(string action,string argument)
    {
        switch(action)
        {
            case "ui-visible":externalVisible=argument=="1";return "ok";
            case "ui-request":RequestMenu(argument=="1");return menuVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case "ui-hotkey":externalHotkey=argument=="1";return "ok";
            case "ui-toggle":
                var foreground=Native.GetForegroundWindow();if(foreground!=gameWindow&&foreground!=externalWindow)return "ignored";
                RequestMenu(!menuRequested);return $"{menuVersion}:{(menuRequested?1:0)}";
            case "ui-focus-game":
                var focus=Native.GetForegroundWindow();if(!menuRequested&&(focus==externalWindow||focus==gameWindow)&&gameWindow!=0&&!Native.IsIconic(gameWindow))Native.SetForegroundWindow(gameWindow);return "ok";
            case "ui-window":if(long.TryParse(argument,out long handle))externalWindow=(nint)handle;return "ok";
        }
        string[] allowed=["unload","config-save","config-load","grenade-save","grenade-delete","web-local","web-internet","web-copy","web-stop","sound-test"];
        if(!allowed.Contains(action))return "unsupported";MenuCommand(action,argument);return "ok";
    }
    void ApplyCaptureProtection()
    {
        uint wanted=settings.ExcludeFromCapture?0x11u:0u;
        if(!IsHandleCreated)return;
        if(!settings.ExcludeFromCapture&&captureCompatible)SetCaptureMode(false);
        bool applied=Native.SetWindowDisplayAffinity(Handle,wanted);captureError=applied?0:System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        // Windows can reject exclusion on a per-pixel UpdateLayeredWindow surface.
        // A redirected color-key surface keeps the same draw loop and supports it.
        if(settings.ExcludeFromCapture&&!applied&&captureError==8&&!captureCompatible){SetCaptureMode(true);applied=Native.SetWindowDisplayAffinity(Handle,wanted);captureError=applied?0:System.Runtime.InteropServices.Marshal.GetLastWin32Error();}
        bool queried=Native.GetWindowDisplayAffinity(Handle,out captureAffinity);if(!queried&&captureError==0)captureError=System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        overlayCaptureProtected=applied&&queried&&captureAffinity==0x11;
        if(settings.ExcludeFromCapture&&!overlayCaptureProtected)Console.Error.WriteLine("Windows could not apply overlay capture protection.");
        if(menu.IsHandleCreated)Native.SetWindowDisplayAffinity(menu.Handle,wanted);
    }
    bool CaptureProtected=>overlayCaptureProtected&&IsHandleCreated&&Native.GetWindowDisplayAffinity(Handle,out uint actual)&&actual==0x11;
    void SetCaptureMode(bool compatible)
    {
        captureCompatible=compatible;canvas?.Dispose();canvas=null;
        TransparencyKey=compatible?Color.Black:Color.Empty;
        RecreateHandle();
        Interlocked.Exchange(ref pending,0);
    }
    void RequestMenu(bool visible){if(menuRequested==visible)return;menuRequested=visible;menuVersion++;}
    object? GameBounds()
    {
        if(gameWindow==0||!Native.GetClientRect(gameWindow,out var rect))return null;
        var origin=new Point(0,0);if(!Native.ClientToScreen(gameWindow,ref origin)||rect.Right<=0||rect.Bottom<=0)return null;
        return new{x=origin.X,y=origin.Y,width=rect.Right,height=rect.Bottom};
    }
    internal Task<T> OnUi<T>(Func<T> fn)
    {
        var source=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        if(IsDisposed||!IsHandleCreated){source.SetException(new InvalidOperationException("El motor se está cerrando."));return source.Task;}
        var cancel=controlStopping.Token.Register(()=>source.TrySetCanceled());_ = source.Task.ContinueWith(_=>cancel.Dispose(),TaskScheduler.Default);
        try{BeginInvoke((Action)(()=>{if(closing){source.TrySetCanceled();return;}try{source.TrySetResult(fn());}catch(Exception e){source.TrySetException(e);}}));}
        catch(InvalidOperationException e){source.TrySetException(e);}return source.Task;
    }
    string feedbackText="";
    void ReportFeedback(string text){feedbackText=text;if(!menu.IsDisposed)menu.Feedback(text);}
}
