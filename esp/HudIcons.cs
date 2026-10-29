using System.Drawing.Drawing2D;
namespace CsEsp;
internal static class HudIcons
{
    // All symbols share a 12 px canvas and a 1.25 px stroke.
    internal static void Draw(Graphics g,string kind,float x,float y,Color color)
    {
        using var pen=new Pen(color,1.25f){StartCap=LineCap.Round,EndCap=LineCap.Round};
        if(kind=="health")
        {
            g.DrawLines(pen,new PointF[]{new(x+6,y+11),new(x+1,y+6),new(x+1,y+3),new(x+3,y+1),new(x+6,y+3),new(x+9,y+1),new(x+11,y+3),new(x+11,y+6),new(x+6,y+11)});
        }
        else if(kind=="user") {g.DrawEllipse(pen,x+4,y,4,4);g.DrawArc(pen,x+1,y+6,10,9,180,180);}
        else if(kind=="weapon") {g.DrawLine(pen,x,y+4,x+12,y+4);g.DrawLine(pen,x+2,y+6,x+9,y+6);g.DrawLine(pen,x+5,y+6,x+4,y+10);g.DrawLine(pen,x+8,y+6,x+10,y+9);g.DrawLine(pen,x+1,y+4,x+1,y+7);}
        else if(kind=="distance") {g.DrawLine(pen,x,y+6,x+12,y+6);g.DrawLines(pen,new PointF[]{new(x+3,y+3),new(x,y+6),new(x+3,y+9)});g.DrawLines(pen,new PointF[]{new(x+9,y+3),new(x+12,y+6),new(x+9,y+9)});}
    }
}
