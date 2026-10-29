using System.ComponentModel;
using System.Drawing.Imaging;
namespace CsEsp;
internal sealed class LayeredCanvas : IDisposable
{
    readonly nint dc,bitmap,previous;
    internal Bitmap Image {get;}
    internal Graphics Graphics {get;}
    internal Size Size=>Image.Size;
    internal LayeredCanvas(int width,int height)
    {
        dc=Native.CreateCompatibleDC(0);if(dc==0)throw new Win32Exception();
        var info=new Native.BitmapInfo{HeaderSize=40,Width=width,Height=-height,Planes=1,Bits=32};
        bitmap=Native.CreateDIBSection(dc,ref info,0,out var bits,0,0);
        if(bitmap==0){Native.DeleteDC(dc);throw new Win32Exception();}
        previous=Native.SelectObject(dc,bitmap);Image=new Bitmap(width,height,width*4,PixelFormat.Format32bppPArgb,bits);Graphics=System.Drawing.Graphics.FromImage(Image);
    }
    internal void Present(nint window,Point location)
    {
        var size=Size;var origin=Point.Empty;var blend=new Native.Blend{Alpha=255,Format=1};
        if(!Native.UpdateLayeredWindow(window,0,ref location,ref size,dc,ref origin,0,ref blend,2))throw new Win32Exception();
    }
    public void Dispose(){Graphics.Dispose();Image.Dispose();Native.SelectObject(dc,previous);Native.DeleteObject(bitmap);Native.DeleteDC(dc);}
}
