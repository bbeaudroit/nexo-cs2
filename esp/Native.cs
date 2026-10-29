using System.Runtime.InteropServices;

namespace CsEsp;
internal static class Native
{
    [DllImport("kernel32.dll", SetLastError=true)] internal static extern nint OpenProcess(uint access, bool inherit, int id);
    [DllImport("kernel32.dll", SetLastError=true)] internal static extern bool ReadProcessMemory(nint handle, nint address, byte[] data, nuint size, out nuint read);
    [DllImport("kernel32.dll", EntryPoint="ReadProcessMemory", SetLastError=true)] internal static extern unsafe bool ReadValue(nint handle, nint address, void* data, nuint size, out nuint read);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll",SetLastError=true)] internal static extern bool WriteProcessMemory(nint handle,nint address,ref int data,nuint size,out nuint written);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] internal static extern nint GetModuleHandle(string? module);
    [DllImport("user32.dll")] internal static extern bool GetClientRect(nint window, out Rect rect);
    [DllImport("user32.dll")] internal static extern bool ClientToScreen(nint window, ref Point point);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] internal static extern bool SetWindowPos(nint window,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("gdi32.dll",SetLastError=true)] internal static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] internal static extern nint SelectObject(nint dc,nint obj);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll",SetLastError=true)] internal static extern nint CreateDIBSection(nint dc,ref BitmapInfo info,uint usage,out nint bits,nint section,uint offset);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool UpdateLayeredWindow(nint window,nint screen,ref Point position,ref Size size,nint source,ref Point origin,uint key,ref Blend blend,uint flags);
    [StructLayout(LayoutKind.Sequential,Pack=1)] internal struct Blend {internal byte Op,Flags,Alpha,Format;}
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo
    {
        internal uint HeaderSize;internal int Width,Height;internal ushort Planes,Bits;internal uint Compression,ImageSize;internal int XResolution,YResolution;internal uint UsedColors,ImportantColors,Color;
    }
    [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool SetWindowDisplayAffinity(nint window,uint affinity);
    [DllImport("user32.dll",SetLastError=true)] internal static extern bool GetWindowDisplayAffinity(nint window,out uint affinity);
    [DllImport("dwmapi.dll")] internal static extern int DwmFlush();
    [DllImport("user32.dll",SetLastError=true)] internal static extern uint SendInput(uint count,Input[] inputs,int size);
    internal delegate nint KeyboardProc(int code,nint message,nint data);
    [DllImport("user32.dll",SetLastError=true)] internal static extern nint SetWindowsHookEx(int type,KeyboardProc callback,nint module,uint thread);
    [DllImport("user32.dll")] internal static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] internal static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [StructLayout(LayoutKind.Sequential)] internal struct KeyboardEvent {internal uint Code,Scan,Flags,Time;internal nuint Extra;}
    [StructLayout(LayoutKind.Explicit,Size=40)] internal struct Input
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(8)] public int X;
        [FieldOffset(8)] public ushort KeyCode;
        [FieldOffset(12)] public int Y;
        [FieldOffset(12)] public uint KeyFlags;
        [FieldOffset(20)] public uint Flags;
    }
    [DllImport("winmm.dll")] internal static extern uint timeBeginPeriod(uint period);
    [DllImport("winmm.dll")] internal static extern uint timeEndPeriod(uint period);
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int Left,Top,Right,Bottom; }
}
