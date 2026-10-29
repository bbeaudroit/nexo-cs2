using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;
namespace CsEsp;
internal sealed partial class Overlay
{
    internal void RenderDemo(string directory)
    {
        ClientSize=new Size(1280,720);settings=settings with{ColorArgb=Color.FromArgb(232,218,255).ToArgb(),BoneColorArgb=Color.FromArgb(195,165,255).ToArgb(),GlowColor=Color.FromArgb(167,139,250).ToArgb(),CornerBoxes=true,DetailedStatus=false,GlowEnabled=true,HealthColor=Color.FromArgb(125,205,162).ToArgb(),ArmorColor=Color.FromArgb(151,118,202).ToArgb()};
        Vector3[] Bones(Vector3 origin)
        {
            var b=Enumerable.Repeat(origin+new Vector3(0,0,38),23).ToArray();void Set(int i,float x,float z)=>b[i]=origin+new Vector3(x,0,z);
            Set(0,0,0);Set(1,0,36);Set(2,0,42);Set(4,0,50);Set(5,0,57);Set(6,0,64);Set(7,0,68);Set(8,-7,56);Set(9,-12,54);Set(10,-17,43);Set(11,-14,36);Set(12,7,56);Set(13,13,54);Set(14,20,44);Set(15,14,38);Set(17,-7,34);Set(18,-8,20);Set(19,-9,2);Set(20,7,34);Set(21,8,20);Set(22,10,2);return b;
        }
        Vector3 a=new(-47,155,0),b=new(57,230,0);
        var players=new[]{new Player(a,84,3,"Jugador",7,24,Bones(a),24){Armor=75,EntityId=1},new Player(b,100,3,"Jugador",60,30,Bones(b),38){Armor=100,EntityId=2}};
        previewMatrix=[1,0,0,0, 0,0,1.77778f,-113.7779f, 0,0,1,0, 0,1,0,0];
        previewFrame=new(players,previewMatrix,default,0,"Vista previa",0,System.Diagnostics.Stopwatch.GetTimestamp(),2);
        using var layer=new LayeredCanvas(1280,720);layer.Graphics.Clear(Color.Transparent);DrawScene(layer.Graphics);layer.Graphics.Flush();
        int transparent=0,partial=0;for(int y=0;y<720;y+=4)for(int x=0;x<1280;x+=4){byte alpha=layer.Image.GetPixel(x,y).A;if(alpha==0)transparent++;else if(alpha<255)partial++;}
        if(transparent<1000||partial<100)throw new InvalidOperationException("La capa no conserva la transparencia y el halo.");
        _ = Handle;layer.Present(Handle,new Point(0,0));Directory.CreateDirectory(directory);layer.Image.Save(Path.Combine(directory,"esp-violet-alpha.png"),ImageFormat.Png);
        using var composite=new Bitmap(1280,720);using var graphics=Graphics.FromImage(composite);using var background=new LinearGradientBrush(new Rectangle(0,0,1280,720),Color.FromArgb(35,32,39),Color.FromArgb(14,13,17),90);
        graphics.FillRectangle(background,0,0,1280,720);using var wall=new SolidBrush(Color.FromArgb(39,36,43));graphics.FillPolygon(wall,new Point[]{new(0,170),new(400,280),new(400,720),new(0,720)});using var wall2=new SolidBrush(Color.FromArgb(25,23,29));graphics.FillPolygon(wall2,new Point[]{new(820,280),new(1280,130),new(1280,720),new(820,720)});
        using var grid=new Pen(Color.FromArgb(40,95,84,106));for(int x=-600;x<1900;x+=180)graphics.DrawLine(grid,640,260,x,720);for(int y=400;y<720;y+=60)graphics.DrawLine(grid,0,y,1280,y);
        graphics.DrawImageUnscaled(layer.Image,0,0);using var font=new Font("Segoe UI",10);using var text=new SolidBrush(Color.FromArgb(185,177,197));graphics.DrawString("Vista previa ilustrativa · renderer real con transparencia por píxel",font,text,750,28);composite.Save(Path.Combine(directory,"esp-violet-preview.png"),ImageFormat.Png);
        Console.WriteLine($"Capa alpha y halo verificados: {transparent} muestras transparentes, {partial} semitransparentes. Presentación nativa correcta.");
    }
}
