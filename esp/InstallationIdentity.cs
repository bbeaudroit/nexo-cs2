using System.Runtime.InteropServices;using System.Security.Cryptography;
namespace CsEsp;
internal static class InstallationIdentity
{
    [StructLayout(LayoutKind.Sequential)]struct Blob{public int Size;public IntPtr Data;}
    [DllImport("crypt32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool CryptProtectData(ref Blob input,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)]static extern bool CryptUnprotectData(ref Blob input,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,int flags,out Blob output);
    [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr memory);
    static byte[] Protect(byte[] data,bool encrypt)
    {
        var input=new Blob{Size=data.Length,Data=Marshal.AllocHGlobal(data.Length)};Blob output=default;
        try{Marshal.Copy(data,0,input.Data,data.Length);bool ok=encrypt?CryptProtectData(ref input,"Nexo installation",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);if(!ok)throw new CryptographicException("Windows no pudo proteger la identidad de Nexo");var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;}
        finally{if(output.Data!=IntPtr.Zero)LocalFree(output.Data);Marshal.Copy(new byte[input.Size],0,input.Data,input.Size);Marshal.FreeHGlobal(input.Data);}
    }
    internal static string Get()
    {
        using var mutex=new Mutex(false,"Local\\Nexo.InstallationIdentity.v1");if(!mutex.WaitOne(5000))throw new IOException("Identidad de instalación ocupada");
        try
        {
            string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Nexo");Directory.CreateDirectory(dir);string file=Path.Combine(dir,"installation.dat");
            if(!File.Exists(file)){byte[] seed=RandomNumberGenerator.GetBytes(32);try{byte[] encrypted=Protect(seed,true);using var target=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None);target.Write(encrypted);target.Flush(true);}finally{CryptographicOperations.ZeroMemory(seed);}}
            byte[] protectedBytes=File.ReadAllBytes(file);if(protectedBytes.Length>4096)throw new CryptographicException("Identidad inválida");byte[] secret=Protect(protectedBytes,false);
            try{if(secret.Length!=32)throw new CryptographicException("Identidad inválida");return Convert.ToHexStringLower(SHA256.HashData(secret));}finally{CryptographicOperations.ZeroMemory(secret);}
        }
        finally{mutex.ReleaseMutex();}
    }
}
