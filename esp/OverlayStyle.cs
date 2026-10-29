using System.Drawing.Drawing2D;
namespace CsEsp;
internal sealed partial class Overlay
{
    static GraphicsPath Rounded(RectangleF r,float radius)
    {
        var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    static GraphicsPath BoxPath(RectangleF b,bool corner)
    {
        var p=new GraphicsPath();if(!corner){p.AddRectangle(b);return p;}
        float w=b.Width*.24f,h=b.Height*.16f;
        foreach(var(x,y,sx,sy)in new[]{(b.Left,b.Top,1,1),(b.Right,b.Top,-1,1),(b.Left,b.Bottom,1,-1),(b.Right,b.Bottom,-1,-1)}){p.StartFigure();p.AddLine(x+sx*w,y,x,y);p.AddLine(x,y,x,y+sy*h);}return p;
    }
    void GlowPath(Graphics g,GraphicsPath path,float width,float opacity=1)
    {
        if(!settings.GlowEnabled||path.PointCount==0)return;int strength=Math.Clamp(settings.GlowStrength,0,100),radius=Math.Clamp(settings.GlowRadius,2,14);Color tint=Color.FromArgb(settings.GlowColor);
        for(int i=3;i>=1;i--){using var pen=new Pen(Color.FromArgb((int)(strength*(4-i)/7*Math.Clamp(opacity,0,1)),tint),width+radius*i*.55f){LineJoin=LineJoin.Round,StartCap=LineCap.Round,EndCap=LineCap.Round};g.DrawPath(pen,path);}
    }
}
