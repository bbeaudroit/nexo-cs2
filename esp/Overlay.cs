using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Numerics;

namespace CsEsp;
internal sealed partial class Overlay : Form
{
    readonly GameReader game;
    internal bool ControlTest;
    readonly string settingsPath;
    EspSettings settings;
    readonly CancellationTokenSource stop=new();
    readonly System.Windows.Forms.Timer watchdog=new(){Interval=100};
    readonly SettingsMenu menu;
    LayeredCanvas? canvas;
    bool captureCompatible;
    Frame? previewFrame;
    float[]? previewMatrix;
    readonly InputController input=new();
    readonly SensitivityConfig sensitivity=new();
    double renderMs,chamsMs;
    readonly MovementInput movement=new();
    readonly CameraFov cameraFov;
    readonly MeshChams meshChams;
    readonly ChamsHistory chamsHistory=new();
    readonly RadarMapFeed mapFeed;
    readonly HitSound hitSound=new();
    readonly GrenadeStore grenades;
    WebRadar web;
    readonly string root;
    readonly string dataRoot;
    Task? sampler;
    Frame frame=Frame.Empty;
    int pending,paintCount,fps;
    long fpsAt=Stopwatch.GetTimestamp(),windowAt;
    bool wasInsert,wasF8,wasEnd,closing;
    nint gameWindow;
    Font labelFont=new("Segoe UI",9,FontStyle.Bold);
    internal Frame Latest=>Volatile.Read(ref frame);
    internal Overlay(GameReader reader,string configuration,string? resourceRoot=null)
    {
        game=reader;settingsPath=Path.GetFullPath(configuration);settings=EspSettings.Load(settingsPath);labelFont.Dispose();labelFont=new("Segoe UI",settings.FontSize,FontStyle.Bold);
        dataRoot=Path.GetDirectoryName(settingsPath)!;root=Path.GetFullPath(resourceRoot??dataRoot);grenades=new GrenadeStore(Path.Combine(dataRoot,"grenades.json"));
        string gameFolder=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(game.Process.MainModule!.FileName)!,"..","..","csgo"));
        mapFeed=new(gameFolder,dataRoot);web=CreateWebRadar();cameraFov=new CameraFov(game.Process.Id,game.CurrentCamera,game.CurrentController);
        Text="Nexo · Overlay";FormBorderStyle=FormBorderStyle.None;BackColor=Color.Black;TopMost=true;ShowInTaskbar=false;DoubleBuffered=true;
        meshChams=new(Path.Combine(root,"local-assets","sas.glb"));
        menu=new SettingsMenu(()=>settings,UpdateSettings,false,MenuCommand,_=>web.Link+"\r\n"+web.Status,()=>grenades.Spots);menu.VisibleChanged+=(_,_)=>{if(menu.Visible)Native.SetWindowDisplayAffinity(menu.Handle,settings.ExcludeFromCapture?0x11u:0u);if(!menu.Visible&&!closing&&gameWindow!=0)Native.SetForegroundWindow(gameWindow);};
        watchdog.Tick+=(_,_)=>{if(!closing)TickFrame();};
    }
    WebRadar CreateWebRadar()=>new(()=>Volatile.Read(ref frame),mapsDirectory:Path.Combine(root,"local-assets","maps"),mapSource:()=>mapFeed.Current,startFeed:mapFeed.Start,feedStatus:()=>mapFeed.Status);
    protected override bool ShowWithoutActivation=>true;
    protected override CreateParams CreateParams {get{var p=base.CreateParams;p.ExStyle|=0x80000|0x20|0x80|0x08000000;return p;}}
    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);ApplyCaptureProtection();watchdog.Start();if(sampler is not null)return;
        sampler=Task.Run(async()=>
        {
            try
            {
                while(!stop.IsCancellationRequested)
                {
                    long began=Stopwatch.GetTimestamp();var config=Volatile.Read(ref settings);
                    bool world=config.WorldWeapons||config.WorldItems||config.WorldGrenades||config.WorldFire||config.RadarWeapons||config.RadarGrenades;
                    bool bones=config.ChamsEnabled||config.ChamsHistoryEnabled||config.Skeleton||config.Boxes||config.HeadCircle||config.AimProfiles?.Values.Any(p=>p.Enabled||p.TriggerAim)==true;
                    Volatile.Write(ref frame,game.Sample(bones,world||config.Bomb,world,config.ShowTeammates,config.BunnyHop||config.AutoStrafe));
                    if(Interlocked.Exchange(ref pending,1)==0)
                    {
                        try {BeginInvoke((Action)(()=>{Interlocked.Exchange(ref pending,0);if(!closing)TickFrame();}));}
                        catch(InvalidOperationException){Interlocked.Exchange(ref pending,0);if(closing||stop.IsCancellationRequested)break;}
                    }
                    double delay=1000d/config.Frames-Stopwatch.GetElapsedTime(began).TotalMilliseconds;
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(1,delay)),stop.Token);
                }
            }
            catch(OperationCanceledException){}
            catch(Exception ex){Console.Error.WriteLine($"Error de lectura: {ex}");if(!closing)try{BeginInvoke((Action)(()=>Close()));}catch(InvalidOperationException){}}
        });
    }
    void UpdateSettings(EspSettings value)
    {
        value=value.Normalize();if(value.FontSize!=settings.FontSize){labelFont.Dispose();labelFont=new("Segoe UI",value.FontSize,FontStyle.Bold);}
        Volatile.Write(ref settings,value);
        settingsVersion++;
        if(IsHandleCreated)ApplyCaptureProtection();
        try{value.Save(settingsPath);settingsSaved=true;saveError="";ReportFeedback("Cambios guardados");}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){settingsSaved=false;saveError="Cambios activos, pero no se pudo guardar. Revisá el permiso de escritura de la carpeta de Nexo.";ReportFeedback(saveError);Console.Error.WriteLine(ex.Message);}
        menu.Synchronize();
        Invalidate();
    }
    static bool Pressed(int key,ref bool before){bool down=(Native.GetAsyncKeyState(key)&0x8000)!=0;bool edge=down&&!before;before=down;return edge;}
    void TickFrame()
    {
        if(game.Process.HasExited){Close();return;}
        long now=Stopwatch.GetTimestamp();
        if(gameWindow==0||Stopwatch.GetElapsedTime(windowAt,now).TotalMilliseconds>=300){game.Process.Refresh();gameWindow=game.Process.MainWindowHandle;windowAt=now;}
        nint foreground=Native.GetForegroundWindow();bool inGame=foreground==gameWindow,inMenu=ExternalUi?externalVisible&&foreground==externalWindow:menu.Visible&&(menu.ContainsFocus||foreground==menu.Handle);
        bool insert=Pressed(settings.MenuKey,ref wasInsert),toggle=Pressed(settings.ToggleKey,ref wasF8),end=Pressed(0x23,ref wasEnd);
        if(inGame||inMenu)
        {
            if(end){Close();return;}
            if(toggle)UpdateSettings(settings with{Enabled=!settings.Enabled});
            if(insert&&(!ExternalUi||!externalHotkey))
            {
                if(ExternalUi)RequestMenu(!menuRequested);else{if(menu.Visible)menu.Hide();else{menu.Show();menu.Activate();}}
                inMenu=ExternalUi?externalVisible:menu.Visible;
            }
        }
        var snapshot=Volatile.Read(ref frame);ObserveShots(snapshot);
        chamsHistory.Capture(snapshot,settings.Enabled&&settings.ChamsHistoryEnabled,settings.ChamsHistoryDelay,mapFeed.Current);
        var lineup=settings.GrenadeHelper?grenades.Visible(settings,snapshot.LocalPosition).OrderBy(s=>Vector3.Distance(s.Position,snapshot.LocalPosition)).FirstOrDefault(s=>Vector3.Distance(s.Position,snapshot.LocalPosition)<30):null;
        bool menuOpen=ControlTest||(ExternalUi?menuRequested||externalVisible:menu.Visible);
        if(settings.AutoSensitivity)sensitivity.Refresh();
        float effectiveSensitivity=SensitivityConfig.Resolve(settings.AutoSensitivity,sensitivity.Value,settings.Sensitivity);
        input.Update(snapshot,effectiveSensitivity==settings.Sensitivity?settings:settings with{Sensitivity=effectiveSensitivity},inGame&&!menuOpen,lineup);
        movement.Configure(snapshot,settings,inGame&&!menuOpen,gameWindow);
        cameraFov.Update(snapshot.Local,settings,inGame&&!menuOpen&&Stopwatch.GetElapsedTime(snapshot.Timestamp).TotalMilliseconds<120);
        if(gameWindow==0||Native.IsIconic(gameWindow)||(!inGame&&!inMenu)){if(Visible)Hide();return;}
        if(!Native.GetClientRect(gameWindow,out var rect))return;var origin=new Point(0,0);if(!Native.ClientToScreen(gameWindow,ref origin))return;
        if(rect.Right<=0||rect.Bottom<=0)return;var bounds=new Rectangle(origin.X,origin.Y,rect.Right,rect.Bottom);if(Bounds!=bounds)Bounds=bounds;
        if(!Visible)Show();if(ExternalUi&&externalVisible&&externalWindow!=0)Native.SetWindowPos(Handle,externalWindow,0,0,0,0,0x13);if(settings.VSync&&!menuOpen)Native.DwmFlush();RenderFrame();
    }
    protected override void OnPaint(PaintEventArgs e){if(captureCompatible){e.Graphics.Clear(Color.Black);DrawScene(e.Graphics);}}
    protected override void OnPaintBackground(PaintEventArgs e){}
    void RenderFrame()
    {
        if(!Visible||Opacity==0||ClientSize.Width<1||ClientSize.Height<1)return;
        long began=Stopwatch.GetTimestamp();
        if(captureCompatible){Invalidate();Update();renderMs=Stopwatch.GetElapsedTime(began).TotalMilliseconds;return;}
        if(canvas is null||canvas.Size!=ClientSize){canvas?.Dispose();canvas=new LayeredCanvas(ClientSize.Width,ClientSize.Height);}
        var g=canvas.Graphics;g.CompositingMode=CompositingMode.SourceCopy;g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceOver;
        DrawScene(g);g.Flush();canvas.Present(Handle,Location);
        renderMs=Stopwatch.GetElapsedTime(began).TotalMilliseconds;
    }
    void DrawScene(Graphics g)
    {
        chamsMs=0;
        g.SmoothingMode=SmoothingMode.AntiAlias;g.PixelOffsetMode=PixelOffsetMode.HighQuality;g.TextRenderingHint=TextRenderingHint.AntiAliasGridFit;
        var config=settings;var snapshot=previewFrame??Volatile.Read(ref frame);
        if(config.PrivacyKey>0&&(Native.GetAsyncKeyState(config.PrivacyKey)&0x8000)!=0)return;
        if(config.ExcludeFromCapture&&config.HideOnCaptureFailure&&!CaptureProtected)return;
        paintCount++;long now=Stopwatch.GetTimestamp();if(Stopwatch.GetElapsedTime(fpsAt,now).TotalSeconds>=1){fps=(int)(paintCount/Stopwatch.GetElapsedTime(fpsAt,now).TotalSeconds);paintCount=0;fpsAt=now;}
        double age=Stopwatch.GetElapsedTime(snapshot.Timestamp,now).TotalMilliseconds;
        if(config.Status)
        {
            string status=config.DetailedStatus?$"NEXO  ·  {fps} FPS  ·  lectura {snapshot.ReadMs:F1} ms  ·  datos {age:F0} ms":$"NEXO  ·  {(config.Enabled?"activo":"en pausa")}  ·  {((Keys)config.MenuKey)}";
            Badge(g,"",status,22,22,Color.FromArgb(224,213,255));
        }
        // Stop displaying stale positions after a stalled read loop.
        if(age>200)return;
        var matrix=previewMatrix??game.ReadCamera();
        DrawAuxiliary(g,snapshot,matrix,config);
        if(!config.Enabled)return;
        Color color=Color.FromArgb(config.ColorArgb),boneColor=Color.FromArgb(config.BoneColorArgb);
        using var outline=new Pen(Color.FromArgb(120,10,8,16),config.Thickness+1);
        using var boxPen=new Pen(color,config.Thickness);using var bonePen=new Pen(boneColor,config.Thickness);
        int historicalDrawn=0;double historyMs=0;
        var historyConfig=config with{ChamsColor=config.ChamsHistoryColor,ChamsOpacity=config.ChamsHistoryOpacity,ChamsOutline=false,ChamsSupersampling=false};
        foreach(var player in snapshot.Players)
        {
            if(player.IsTeammate&&!config.ShowTeammates)continue;
            if(config.EspMaxDistance>0&&player.Distance>config.EspMaxDistance)continue;
            if(!TryBox(matrix,player,out var box))continue;
            if(config.ChamsHistoryEnabled&&historicalDrawn<4&&historyMs<6&&(config.ChamsDistance==0||player.Distance<=config.ChamsDistance))
            {
                var old=chamsHistory.Get(player.EntityId,player,snapshot.Timestamp,config.ChamsHistoryDelay);
                if(old is not null&&Vector3.DistanceSquared(old.Position,player.Position)>4&&TryBox(matrix,old,out _))
                {long began=Stopwatch.GetTimestamp();meshChams.Draw(g,old,matrix,ClientSize.Width,ClientSize.Height,historyConfig);double elapsed=Stopwatch.GetElapsedTime(began).TotalMilliseconds;historyMs+=elapsed;chamsMs+=elapsed;historicalDrawn++;}
            }
            if(config.ChamsEnabled&&(config.ChamsDistance==0||player.Distance<=config.ChamsDistance)){long began=Stopwatch.GetTimestamp();meshChams.Draw(g,player,matrix,ClientSize.Width,ClientSize.Height,config);chamsMs+=Stopwatch.GetElapsedTime(began).TotalMilliseconds;}
            if(config.Boxes){using var path=BoxPath(box,config.CornerBoxes);GlowPath(g,path,config.Thickness);g.DrawPath(outline,path);g.DrawPath(boxPen,path);}
            if(config.Skeleton&&player.Bones.Length>0)
            {
                using var path=new GraphicsPath();foreach(var(a,b) in RenderMath.LinksFor(player.Bones.Length))
                {
                    if(!Projection.ToScreen(matrix,player.Bones[a],ClientSize.Width,ClientSize.Height,out var start)||!Projection.ToScreen(matrix,player.Bones[b],ClientSize.Width,ClientSize.Height,out var finish))continue;
                    if(Vector2.Distance(start,finish)>ClientSize.Height*.7f)continue;
                    path.StartFigure();path.AddLine(start.X,start.Y,finish.X,finish.Y);
                }
                GlowPath(g,path,config.Thickness);g.DrawPath(outline,path);g.DrawPath(bonePen,path);
            }
            if(config.Health)
            {
                Color hp=Color.FromArgb(config.HealthColor);
                var healthAt=config.EspLayout.Offset("Health",box);float healthX=box.X-7+healthAt.X,healthY=box.Y+healthAt.Y;
                using var track=new SolidBrush(Color.FromArgb(130,18,15,26));g.FillRectangle(track,healthX,healthY,2,box.Height);using var brush=new SolidBrush(hp);
                g.FillRectangle(brush,healthX,healthY+box.Height*(1-player.Health/100f),2,box.Height*player.Health/100f);
                if(player.Health<100)Badge(g,"",$"{player.Health}",box.Right+7,box.Y,hp);
            }
            var nameAt=config.EspLayout.Offset("Name",box);
            if(config.Names)BadgeCentered(g,"",player.Name,box.X+box.Width/2+nameAt.X,box.Y-labelFont.Height-12+nameAt.Y,Color.FromArgb(config.NameColor));
            if(config.HeadCircle&&RenderMath.TryHeadMarker(player.Bones,out var headMarker)&&Projection.ToScreen(matrix,headMarker,ClientSize.Width,ClientSize.Height,out var center)){using var headPen=new Pen(Color.FromArgb(config.HeadColor),config.Thickness);float r=Math.Clamp(box.Height*.045f,2,25);g.DrawEllipse(headPen,center.X-r,center.Y-r,r*2,r*2);}
            var armorAt=config.EspLayout.Offset("Armor",box);var weaponAt=config.EspLayout.Offset("Weapon",box);var distanceAt=config.EspLayout.Offset("Distance",box);
            if(config.Armor&&player.Armor>0){using var track=new SolidBrush(Color.FromArgb(100,18,15,26));g.FillRectangle(track,box.X+armorAt.X,box.Bottom+3+armorAt.Y,box.Width,2);using var armor=new SolidBrush(Color.FromArgb(config.ArmorColor));g.FillRectangle(armor,box.X+armorAt.X,box.Bottom+3+armorAt.Y,box.Width*player.Armor/100f,2);}
            if((config.Weapons||config.WeaponIcons)&&player.Weapon!=0){string weapon=config.Weapons?WeaponNames.Label(player.Weapon)+(player.Ammo>=0?$" · {player.Ammo}":""):"";BadgeCentered(g,config.WeaponIcons?"weapon":"",weapon,box.X+box.Width/2+weaponAt.X,box.Bottom+11+weaponAt.Y,Color.FromArgb(config.WeaponColor));}
            if(config.Distance)BadgeCentered(g,"",$"{player.Distance:F0} m",box.X+box.Width/2+distanceAt.X,box.Bottom+labelFont.Height+20+distanceAt.Y,Color.FromArgb(176,168,196));
        }
        if(config.Radar)DrawRadar(g,snapshot,config,color);
        if(snapshot.Players.Length==0&&config.Status)Badge(g,"user",snapshot.Status,24,48,Color.White);
    }
    bool TryBox(float[] matrix,Player p,out RectangleF box)
    {
        box=default;Vector3 top=p.Bones.Length>6?p.Bones[6]+new Vector3(0,0,5):p.Position+new Vector3(0,0,72);
        if(!Projection.ToScreen(matrix,p.Position,ClientSize.Width,ClientSize.Height,out var foot)||!Projection.ToScreen(matrix,top,ClientSize.Width,ClientSize.Height,out var head))return false;
        float h=foot.Y-head.Y;if(h<4||h>ClientSize.Height*3)return false;
        float left=head.X-h*.22f,right=head.X+h*.22f,upper=head.Y,lower=foot.Y;
        if(p.Bones.Length>0)
        {
            foreach(int index in RenderMath.BoxJointsFor(p.Bones.Length))
            {
                if(!Projection.ToScreen(matrix,p.Bones[index],ClientSize.Width,ClientSize.Height,out var point))continue;
                left=Math.Min(left,point.X-3);right=Math.Max(right,point.X+3);upper=Math.Min(upper,point.Y-3);lower=Math.Max(lower,point.Y+3);
            }
        }
        if(right<0||left>ClientSize.Width||lower<0||upper>ClientSize.Height)return false;box=RectangleF.FromLTRB(left,upper,right,lower);return true;
    }
    void BadgeCentered(Graphics g,string icon,string text,float center,float y,Color color)
    {
        float width=g.MeasureString(text,labelFont).Width+(icon.Length>0?25:12);Badge(g,icon,text,center-width/2,y,color);
    }
    void Badge(Graphics g,string icon,string text,float x,float y,Color color)
    {
        var size=g.MeasureString(text,labelFont);float height=Math.Max(20,size.Height+5);float width=size.Width+(icon.Length>0?27:13);
        if(settings.LabelBackground){using var shape=Rounded(new RectangleF(x-4,y-3,width,height),5);using var background=new SolidBrush(Color.FromArgb(Math.Clamp(settings.LabelOpacity,0,100)*255/100,16,14,23));g.FillPath(background,shape);using var edge=new Pen(Color.FromArgb(32,167,139,250));g.DrawPath(edge,shape);}
        float tx=x+(icon.Length>0?18:2);if(icon.Length>0)HudIcons.Draw(g,icon,x+2,y+2,color);
        using var shadow=new SolidBrush(Color.FromArgb(160,0,0,0));g.DrawString(text,labelFont,shadow,tx+1,y+1);using var brush=new SolidBrush(color);g.DrawString(text,labelFont,brush,tx,y);
    }
    void DrawRadar(Graphics g,Frame snapshot,EspSettings config,Color color)
    {
        var bounds=RenderMath.RadarBounds(ClientSize.Width,ClientSize.Height,config.RadarSize,config.RadarHudPosition?24:config.RadarX,config.RadarHudPosition?70:config.RadarY);if(bounds.Z==0)return;
        float x=bounds.X,y=bounds.Y,size=bounds.Z;
        using var shape=Rounded(new RectangleF(x,y,size,size+34),10);using var background=new SolidBrush(Color.FromArgb(Math.Clamp(config.RadarOpacity,0,100)*255/100,16,14,23));g.FillPath(background,shape);using var border=new Pen(Color.FromArgb(75,167,139,250));if(config.RadarOutlines)g.DrawPath(border,shape);
        g.DrawString("Radar",labelFont,Brushes.White,x+12,y+8);g.DrawString($"{config.RadarRange*.0254f:F0} m",labelFont,Brushes.Silver,x+size-62,y+8);
        float radius=size/2-16,cx=x+size/2,cy=y+34+size/2;
        using var grid=new Pen(Color.FromArgb(40,158,141,188));if(config.RadarOutlines){g.DrawEllipse(grid,cx-radius,cy-radius,2*radius,2*radius);g.DrawEllipse(grid,cx-radius/2,cy-radius/2,radius,radius);g.DrawLine(grid,cx-radius,cy,cx+radius,cy);g.DrawLine(grid,cx,cy-radius,cx,cy+radius);}
        g.FillPolygon(Brushes.White,new PointF[]{new(cx,cy-6),new(cx-4,cy+4),new(cx+4,cy+4)});
        using var dot=new SolidBrush(Color.FromArgb(config.DotColor));
        foreach(var player in snapshot.Players)
        {
            if(player.IsTeammate&&!config.ShowTeammates||!config.RadarDots||(config.EspMaxDistance>0&&player.Distance>config.EspMaxDistance))continue;var p=RenderMath.Radar(snapshot.LocalPosition,player.Position,snapshot.Yaw,radius-5,config.RadarRange);float px=cx+p.X,py=cy+p.Y;
            if(config.GlowEnabled){using var aura=new SolidBrush(Color.FromArgb(35,Color.FromArgb(config.GlowColor)));g.FillEllipse(aura,px-7,py-7,14,14);}g.FillEllipse(dot,px-2.5f,py-2.5f,5,5);
            float dz=player.Position.Z-snapshot.LocalPosition.Z;if(Math.Abs(dz)>80)g.DrawLine(Pens.White,px-2,py+(dz>0?-6:6),px+2,py+(dz>0?-6:6));
        }
        using var itemPen=new Pen(Color.FromArgb(config.WorldColor));foreach(var item in snapshot.World){if(!(item.Kind=="weapon"&&config.RadarWeapons||item.Kind=="grenade"&&config.RadarGrenades))continue;var p=RenderMath.Radar(snapshot.LocalPosition,item.Position,snapshot.Yaw,radius-5,config.RadarRange);g.DrawRectangle(itemPen,cx+p.X-2,cy+p.Y-2,4,4);}
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        closing=true;controlStopping.Cancel();watchdog.Stop();input.Dispose();movement.Dispose();cameraFov.Dispose();hitSound.Dispose();stop.Cancel();sampler?.GetAwaiter().GetResult();web.Dispose();mapFeed.Dispose();menu.Dispose();base.OnFormClosing(e);
    }
    protected override void Dispose(bool disposing){if(disposing){meshChams.Dispose();canvas?.Dispose();watchdog.Dispose();stop.Dispose();controlStopping.Dispose();labelFont.Dispose();}base.Dispose(disposing);}
}

