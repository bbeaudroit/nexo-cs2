using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Numerics;
namespace CsEsp;
internal sealed partial class Overlay
{
    readonly Dictionary<int,(int Shots,Vector3 Position,long Time)> shotHistory=[];
    readonly List<(Vector3 Position,long Time)> soundMarks=[];
    long observed;
    bool sharing;
    int previousHits=-1;
    long hitAt;
    void ObserveShots(Frame f)
    {
        if(observed==f.Timestamp)return;observed=f.Timestamp;
        if(f.Local.DamageValid)
        {
            if(previousHits>=0&&f.Local.TotalHits>previousHits&&f.Local.TotalHits-previousHits<=10)
            {
                hitAt=Stopwatch.GetTimestamp();if(settings.HitSound)try{hitSound.Play(settings.HitSoundType,settings.HitVolume);}catch(Exception ex){Console.Error.WriteLine("Sonido de impacto: "+ex.Message);}
            }
            previousHits=f.Local.TotalHits;
        }
        else previousHits=-1;
        foreach(var p in f.Players)
        {
            if(shotHistory.TryGetValue(p.EntityId,out var old)&&p.Shots>old.Shots&&p.Shots-old.Shots<=5)soundMarks.Add((p.Position,f.Timestamp));
            shotHistory[p.EntityId]=(p.Shots,p.Position,f.Timestamp);
        }
        soundMarks.RemoveAll(s=>Stopwatch.GetElapsedTime(s.Time).TotalSeconds>settings.SoundTime);foreach(int id in shotHistory.Where(p=>Stopwatch.GetElapsedTime(p.Value.Time).TotalSeconds>10).Select(p=>p.Key).ToArray())shotHistory.Remove(id);
    }
    static void Corners(Graphics g,Pen pen,RectangleF b)
    {
        float w=b.Width*.24f,h=b.Height*.18f;
        foreach(var(x,y,sx,sy) in new[]{(b.Left,b.Top,1,1),(b.Right,b.Top,-1,1),(b.Left,b.Bottom,1,-1),(b.Right,b.Bottom,-1,-1)}){g.DrawLine(pen,x,y,x+sx*w,y);g.DrawLine(pen,x,y,x,y+sy*h);}
    }
    void CircleWorld(Graphics g,Pen pen,float[] matrix,Vector3 origin,float radius)
    {
        Vector2? last=null;for(int i=0;i<=32;i++){float angle=i*MathF.PI/16;var p=origin+new Vector3(MathF.Cos(angle)*radius,MathF.Sin(angle)*radius,2);if(Projection.ToScreen(matrix,p,ClientSize.Width,ClientSize.Height,out var screen)){if(last is not null)g.DrawLine(pen,last.Value.X,last.Value.Y,screen.X,screen.Y);last=screen;}else last=null;}
    }
    void DrawAuxiliary(Graphics g,Frame f,float[] matrix,EspSettings s)
    {
        if(s.Hitmarker&&hitAt!=0&&Stopwatch.GetElapsedTime(hitAt).TotalMilliseconds<220)
        {
            float cx=ClientSize.Width/2f,cy=ClientSize.Height/2f,fade=Math.Clamp(1-(float)Stopwatch.GetElapsedTime(hitAt).TotalMilliseconds/220,0,1);using var pen=new Pen(Color.FromArgb((int)(255*fade),242,232,255),1.4f);using var path=new GraphicsPath();
            foreach(var(x,y)in new[]{(-1,-1),(1,-1),(-1,1),(1,1)}){path.StartFigure();path.AddLine(cx+x*4,cy+y*4,cx+x*10,cy+y*10);}GlowPath(g,path,1,fade);g.DrawPath(pen,path);
        }
        var p=s.Profile(f.Local.Weapon);
        if(f.Local.Alive&&p.ShowFov)
        {
            float projection=MathF.Sqrt(matrix[0]*matrix[0]+matrix[1]*matrix[1]+matrix[2]*matrix[2]);float radius=MathF.Tan((!p.Enabled&&p.TriggerAim?p.TriggerFov:p.Fov)*MathF.PI/180)*ClientSize.Width*.5f*projection;
            if(float.IsFinite(radius)&&radius>0&&radius<ClientSize.Width)
            {
                float x=ClientSize.Width*.5f,y=ClientSize.Height*.5f;
                if(p.FollowRecoil&&f.Local.RecoilValid){x-=MathF.Tan(f.Local.Recoil.Y*2*MathF.PI/180)*ClientSize.Width*.5f*projection;y+=MathF.Tan(f.Local.Recoil.X*2*MathF.PI/180)*ClientSize.Width*.5f*projection;}
                using var pen=new Pen(Color.FromArgb(135,Color.FromArgb(p.FollowRecoil?p.RecoilColor:p.FovColor)),.9f);using var path=new GraphicsPath();path.AddEllipse(x-radius,y-radius,radius*2,radius*2);GlowPath(g,path,1,.3f);g.DrawPath(pen,path);
            }
        }
        if(s.RecoilCrosshair&&f.Local.RecoilValid)
        {
            float scale=MathF.Sqrt(matrix[0]*matrix[0]+matrix[1]*matrix[1]+matrix[2]*matrix[2])*ClientSize.Width*.5f;
            float x=ClientSize.Width*.5f-MathF.Tan(f.Local.Recoil.Y*2*MathF.PI/180)*scale,y=ClientSize.Height*.5f+MathF.Tan(f.Local.Recoil.X*2*MathF.PI/180)*scale;
            if(float.IsFinite(x)&&float.IsFinite(y)){using var path=new GraphicsPath();path.AddLine(x-4,y,x+4,y);path.StartFigure();path.AddLine(x,y-4,x,y+4);GlowPath(g,path,1,.6f);using var pen=new Pen(Color.FromArgb(220,Color.FromArgb(s.GlowColor)));g.DrawPath(pen,path);}
        }
        if(s.Spectators&&f.Spectators.Length>0)
        {
            float y=70;Badge(g,"user",$"Espectadores: {f.Spectators.Length}",ClientSize.Width-220,y,Color.White);foreach(string name in f.Spectators){y+=labelFont.Height+9;Badge(g,"user",s.VerboseSpectators?name+" · observando":name,ClientSize.Width-220,y,Color.White);}
        }
        using var worldPen=new Pen(Color.FromArgb(s.WorldColor),s.Thickness);
        if(s.Enabled&&s.SoundEsp)foreach(var sound in soundMarks){float progress=(float)(Stopwatch.GetElapsedTime(sound.Time).TotalSeconds/s.SoundTime);CircleWorld(g,worldPen,matrix,sound.Position,10+progress*45);if(Projection.ToScreen(matrix,sound.Position,ClientSize.Width,ClientSize.Height,out var at))Badge(g,"weapon","Disparo",at.X,at.Y,Color.FromArgb(s.WorldColor));}
        foreach(var item in f.World)
        {
            bool show=item.Kind switch{"bomb"=>s.Bomb,"weapon"=>s.WorldWeapons,"item"=>s.WorldItems,"grenade"=>s.WorldGrenades,"fire"=>s.WorldFire,_=>false};if(!show)continue;
            if(item.Kind=="fire"){foreach(var point in item.Points??[])CircleWorld(g,worldPen,matrix,point,16);if(item.Points is null||item.Points.Length==0)CircleWorld(g,worldPen,matrix,item.Position,45);}
            if(!Projection.ToScreen(matrix,item.Position,ClientSize.Width,ClientSize.Height,out var at))continue;
            Color color=Color.FromArgb(item.Kind=="bomb"?s.BombColor:s.WorldColor);
            string text=item.Kind=="bomb"?$"C4 · {item.Site} · {(item.Remaining>=0?$"{item.Remaining:F1} s":"reloj sin validar")}":item.Name;
            BadgeCentered(g,item.Kind=="weapon"?"weapon":"",text,at.X,at.Y,color);
            if(item.Kind=="bomb"&&item.Remaining>=0&&item.Duration>0)
            {
                float progress=Math.Clamp(item.Remaining/item.Duration,0,1);g.FillRectangle(Brushes.Black,at.X-60,at.Y+22,120,5);using var fill=new SolidBrush(color);g.FillRectangle(fill,at.X-60,at.Y+22,120*progress,5);
                if(item.Defuse>=0){BadgeCentered(g,"",$"Desactivación {item.Defuse:F1} s",at.X,at.Y+33,Color.Cyan);using var defuse=new Pen(Color.Cyan,2);g.DrawArc(defuse,at.X+80,at.Y-4,25,25,-90,Math.Clamp(item.Defuse/10,0,1)*360);}
            }
        }
        if(s.GrenadeHelper)
        {
            using var pen=new Pen(Color.FromArgb(s.GrenadeColor));
            foreach(var spot in grenades.Visible(s,f.LocalPosition))
            {
                CircleWorld(g,pen,matrix,spot.Position,8);
                if(Projection.ToScreen(matrix,spot.Position,ClientSize.Width,ClientSize.Height,out var point))BadgeCentered(g,"",$"{spot.Name} · {spot.Grenade} · {spot.ThrowType}",point.X,point.Y,pen.Color);
                if(Vector3.Distance(spot.Position,f.LocalPosition)>60)continue;
                float pitch=spot.Pitch*MathF.PI/180,yaw=spot.Yaw*MathF.PI/180;var direction=new Vector3(MathF.Cos(pitch)*MathF.Cos(yaw),MathF.Cos(pitch)*MathF.Sin(yaw),-MathF.Sin(pitch));
                if(Projection.ToScreen(matrix,spot.Position+new Vector3(0,0,64)+direction*800,ClientSize.Width,ClientSize.Height,out var aim)){g.DrawEllipse(pen,aim.X-5,aim.Y-5,10,10);g.DrawLine(pen,ClientSize.Width/2f,ClientSize.Height/2f,aim.X,aim.Y);}
            }
        }
    }
    static string ConfigName(string name)
    {
        name=new string(name.Trim().Where(c=>char.IsLetterOrDigit(c)||c is '-' or '_').Take(40).ToArray());if(name.Length==0)throw new InvalidOperationException("Indicá un nombre válido de configuración.");return name+".json";
    }
    async void MenuCommand(string action,string argument)
    {
        try
        {
            switch(action)
            {
                case "unload":Close();return;
                case "sound-test":hitSound.Play(settings.HitSoundType,settings.HitVolume);break;
                case "config-save":{string dir=Path.Combine(dataRoot,"configs");Directory.CreateDirectory(dir);settings.Save(Path.Combine(dir,ConfigName(argument)));break;}
                case "config-load":{string path=Path.Combine(dataRoot,"configs",ConfigName(argument));if(!File.Exists(path))throw new FileNotFoundException("No existe esa configuración.");UpdateSettings(EspSettings.LoadStrict(path));break;}
                case "grenade-save":
                {
                    var f=Volatile.Read(ref frame);if(!f.Local.Alive||Stopwatch.GetElapsedTime(f.Timestamp).TotalMilliseconds>200)throw new InvalidOperationException("Entrá a la partida y elegí un equipo antes de guardar.");
                    var values=argument.Split('|');if(values.Length!=3)throw new InvalidOperationException("El nombre no puede contener |.");
                    grenades.Add(new(Guid.NewGuid().ToString("N"),settings.Map,values[0].Trim(),values[1],values[2],f.LocalPosition.X,f.LocalPosition.Y,f.LocalPosition.Z,f.Local.Angles.X,f.Local.Angles.Y));break;
                }
                case "grenade-delete":grenades.Remove(argument);break;
                case "web-copy":if(!web.Link.StartsWith("http",StringComparison.Ordinal))throw new InvalidOperationException("Iniciá el radar web primero.");Clipboard.SetText(web.Link);break;
                case "web-stop":web.Dispose();web=CreateWebRadar();break;
                case "web-local":case "web-internet":
                {
                    if(sharing)throw new InvalidOperationException("El radar ya se está iniciando.");sharing=true;
                    try{if(action=="web-local")await web.Start();else await web.ShareInternet(Path.Combine(root,"tools","cloudflared.exe"));}finally{sharing=false;}break;
                }
            }
            if(!menu.IsDisposed)ReportFeedback("Cambios aplicados");
        }
        catch(Exception ex){Console.Error.WriteLine(ex.Message);if(!menu.IsDisposed)ReportFeedback(ex.Message);}
    }
}
