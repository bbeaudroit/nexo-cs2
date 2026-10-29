namespace CsEsp;
internal sealed class LicenseGuard : IDisposable
{
    readonly System.Windows.Forms.Timer? timer=null;
#if NEXO_COMMERCIAL
    readonly string file,device,publicKey;
    bool Valid(){try{return LicenseTicket.Validate(File.ReadAllText(file),publicKey,device,DateTimeOffset.UtcNow.ToUnixTimeSeconds());}catch(IOException){return false;}catch(UnauthorizedAccessException){return false;}}
#endif
    internal LicenseGuard()
    {
#if NEXO_COMMERCIAL
        file=Environment.GetEnvironmentVariable("NEXO_LICENSE_PATH")??"";device=InstallationIdentity.Get();
        using var stream=typeof(LicenseGuard).Assembly.GetManifestResourceStream("Nexo.LicensePublicKey.pem")??throw new InvalidOperationException("Falta la clave pública de licencias");using var reader=new StreamReader(stream);publicKey=reader.ReadToEnd();
        if(!Valid())throw new InvalidOperationException("Activá una licencia válida desde Nexo.");
        timer=new System.Windows.Forms.Timer{Interval=5000};timer.Tick+=(_,_)=>{if(!Valid()){timer.Stop();Console.Error.WriteLine("La sesión de licencia venció. Cerrando el motor.");Application.Exit();}};
#endif
    }
    internal void Monitor()=>timer?.Start();
    public void Dispose()=>timer?.Dispose();
}
