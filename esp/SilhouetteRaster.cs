using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace CsEsp;

// Union of projected triangles in a bounded mask. This avoids constructing a GDI
// clipping region from thousands of overlapping polygons for every player/frame.
internal sealed class SilhouetteRaster : IDisposable
{
    byte[] mask=[],horizontal=[],dilated=[];
    int[] pixels=[];
    Bitmap? bitmap;
    public void Dispose(){bitmap?.Dispose();bitmap=null;}
    static void Capacity<T>(ref T[] buffer,int size){if(buffer.Length<size)buffer=new T[size];}
    internal void Draw(Graphics graphics,List<(PointF A,PointF B,PointF C)> triangles,int viewportWidth,int viewportHeight,EspSettings settings)
    {
        if(triangles.Count==0||settings.ChamsOpacity==0&&(!settings.ChamsOutline||settings.ChamsOutlineOpacity==0))return;
        float minX=float.PositiveInfinity,minY=float.PositiveInfinity,maxX=float.NegativeInfinity,maxY=float.NegativeInfinity;
        foreach(var (a,b,c) in triangles){minX=Math.Min(minX,Math.Min(a.X,Math.Min(b.X,c.X)));minY=Math.Min(minY,Math.Min(a.Y,Math.Min(b.Y,c.Y)));maxX=Math.Max(maxX,Math.Max(a.X,Math.Max(b.X,c.X)));maxY=Math.Max(maxY,Math.Max(a.Y,Math.Max(b.Y,c.Y)));}
        int pad=settings.ChamsOutlineWidth+2;
        int left=Math.Max(0,(int)Math.Floor(minX)-pad),top=Math.Max(0,(int)Math.Floor(minY)-pad),right=Math.Min(viewportWidth,(int)Math.Ceiling(maxX)+pad),bottom=Math.Min(viewportHeight,(int)Math.Ceiling(maxY)+pad);
        int width=right-left,height=bottom-top;if(width<=0||height<=0)return;
        int scale=settings.ChamsSupersampling&&(long)width*height<=150000?2:1,w=width*scale,h=height*scale,count=checked(w*h);
        Capacity(ref mask,count);mask.AsSpan(0,count).Clear();
        foreach(var (sourceA,sourceB,sourceC) in triangles)
        {
            var a=new PointF((sourceA.X-left)*scale,(sourceA.Y-top)*scale);var b=new PointF((sourceB.X-left)*scale,(sourceB.Y-top)*scale);var c=new PointF((sourceC.X-left)*scale,(sourceC.Y-top)*scale);
            int first=Math.Max(0,(int)Math.Ceiling(Math.Min(a.Y,Math.Min(b.Y,c.Y))-.5f)),last=Math.Min(h-1,(int)Math.Floor(Math.Max(a.Y,Math.Max(b.Y,c.Y))-.5f));
            for(int y=first;y<=last;y++)
            {
                float scan=y+.5f,lo=float.PositiveInfinity,hi=float.NegativeInfinity;int crossings=0;
                void Edge(PointF p,PointF q){if((p.Y<=scan&&q.Y>scan)||(q.Y<=scan&&p.Y>scan)){float x=p.X+(scan-p.Y)*(q.X-p.X)/(q.Y-p.Y);lo=Math.Min(lo,x);hi=Math.Max(hi,x);crossings++;}}
                Edge(a,b);Edge(b,c);Edge(c,a);if(crossings<2)continue;
                int x0=Math.Max(0,(int)Math.Ceiling(lo-.5f)),x1=Math.Min(w-1,(int)Math.Floor(hi-.5f));if(x1>=x0)mask.AsSpan(y*w+x0,x1-x0+1).Fill(1);
            }
        }
        bool outline=settings.ChamsOutline&&settings.ChamsOutlineOpacity>0;
        if(outline)
        {
            Capacity(ref horizontal,count);Capacity(ref dilated,count);int radius=settings.ChamsOutlineWidth*scale;
            for(int y=0;y<h;y++)
            {
                int row=y*w,sum=0;for(int k=0;k<=Math.Min(radius,w-1);k++)sum+=mask[row+k];
                for(int x=0;x<w;x++){horizontal[row+x]=(byte)(sum>0?1:0);if(x-radius>=0)sum-=mask[row+x-radius];if(x+radius+1<w)sum+=mask[row+x+radius+1];}
            }
            for(int x=0;x<w;x++)
            {
                int sum=0;for(int k=0;k<=Math.Min(radius,h-1);k++)sum+=horizontal[k*w+x];
                for(int y=0;y<h;y++){dilated[y*w+x]=(byte)(sum>0?1:0);if(y-radius>=0)sum-=horizontal[(y-radius)*w+x];if(y+radius+1<h)sum+=horizontal[(y+radius+1)*w+x];}
            }
        }
        int bodyA=settings.ChamsOpacity*255/100,edgeA=settings.ChamsOutlineOpacity*255/100;
        Color body=Color.FromArgb(settings.ChamsColor),edge=Color.FromArgb(settings.ChamsOutlineColor);int samples=scale*scale;
        Capacity(ref pixels,width*height);
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int inside=0,border=0;
            for(int dy=0;dy<scale;dy++)for(int dx=0;dx<scale;dx++){int i=(y*scale+dy)*w+x*scale+dx;if(mask[i]!=0)inside++;else if(outline&&dilated[i]!=0)border++;}
            int ba=inside*bodyA,ea=border*edgeA,a=(ba+ea)/samples;
            int r=(body.R*ba+edge.R*ea)/(255*samples),g=(body.G*ba+edge.G*ea)/(255*samples),b=(body.B*ba+edge.B*ea)/(255*samples);
            pixels[y*width+x]=(a<<24)|(r<<16)|(g<<8)|b;
        }
        if(bitmap is null||bitmap.Width<width||bitmap.Height<height){int bw=Math.Max(width,bitmap?.Width??0),bh=Math.Max(height,bitmap?.Height??0);bitmap?.Dispose();bitmap=new Bitmap(bw,bh,PixelFormat.Format32bppPArgb);}
        var data=bitmap.LockBits(new(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppPArgb);
        try{for(int y=0;y<height;y++)Marshal.Copy(pixels,y*width,data.Scan0+y*data.Stride,width);}finally{bitmap.UnlockBits(data);}
        graphics.DrawImage(bitmap,new Rectangle(left,top,width,height),0,0,width,height,GraphicsUnit.Pixel);
    }
}
