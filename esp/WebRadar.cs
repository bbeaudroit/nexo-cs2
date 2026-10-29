using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace CsEsp;
internal sealed class WebRadar : IDisposable
{
    readonly Func<Frame> frame;
    readonly RadarMapCatalog maps;
    readonly Func<string> currentMap;
    readonly Func<CancellationToken,Task>? startMapFeed;
    readonly Func<string> mapStatus;
    readonly string token=Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
    WebApplication? app;
    Process? tunnel;
    readonly SemaphoreSlim lifecycle=new(1,1);
    readonly CancellationTokenSource lifetime=new();
    readonly int startupDelay;
    int disposed;
    internal string Link {get;private set;}="Web radar stopped";
    internal string Status {get;private set;}="Not sharing";
    internal int Port {get;private set;}
    internal WebRadar(Func<Frame> snapshot,int startupDelayMs=0,string? mapsDirectory=null,Func<string>? mapSource=null,Func<CancellationToken,Task>? startFeed=null,Func<string>? feedStatus=null){frame=snapshot;startupDelay=startupDelayMs;maps=new(mapsDirectory??Path.Combine(AppContext.BaseDirectory,"..","local-assets","maps"));currentMap=mapSource??(()=>"");startMapFeed=startFeed;mapStatus=feedStatus??(()=>"Choose a map manually or start CS2 map integration");}
    internal async Task Start()
    {
        await lifecycle.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        if(app is not null)return;
        if(startupDelay>0)await Task.Delay(startupDelay,lifetime.Token).ConfigureAwait(false);
        if(startMapFeed is not null)await startMapFeed(lifetime.Token).ConfigureAwait(false);
        var builder=WebApplication.CreateSlimBuilder();builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(o=>o.Listen(IPAddress.Loopback,0));
        var host=builder.Build();
        host.Use(async(ctx,next)=>{ctx.Response.Headers.CacheControl="no-store";ctx.Response.Headers["Referrer-Policy"]="no-referrer";ctx.Response.Headers["X-Content-Type-Options"]="nosniff";ctx.Response.Headers["Content-Security-Policy"]="default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'";await next(ctx);});
        host.MapGet("/"+token+"/",()=>Results.Content(Page,"text/html; charset=utf-8"));
        host.MapGet("/"+token+"/math.mjs",()=>Results.Content(Resource("WebRadarMath.mjs"),"text/javascript; charset=utf-8"));
        host.MapGet("/"+token+"/maps",()=>Results.Json(maps.All));
        host.MapGet("/"+token+"/image/{file}",(string file)=>maps.ImagePath(file) is string path?Results.File(path,"image/png"):Results.NotFound());
        host.MapGet("/"+token+"/data",(HttpContext context)=>
        {
            var f=frame();double age=Stopwatch.GetElapsedTime(f.Timestamp).TotalMilliseconds;bool fresh=age is >=0 and <500;
            string manual=context.Request.Query["map"].ToString();var map=maps.Find(manual.Length>0?manual:currentMap());
            bool localValid=float.IsFinite(f.LocalPosition.X)&&float.IsFinite(f.LocalPosition.Y)&&float.IsFinite(f.LocalPosition.Z);
            return Results.Json(new{fresh,map,manual=manual.Length>0,mapStatus=mapStatus(),local=new{x=localValid?f.LocalPosition.X:0,y=localValid?f.LocalPosition.Y:0,z=localValid?f.LocalPosition.Z:0,alive=fresh&&f.Local.Alive&&localValid},players=fresh?f.Players.Where(p=>p.Health>0&&float.IsFinite(p.Position.X)&&float.IsFinite(p.Position.Y)&&float.IsFinite(p.Position.Z)).Select(p=>new{x=p.Position.X,y=p.Position.Y,z=p.Position.Z,hp=p.Health,name=p.Name,weapon=WeaponNames.Label(p.Weapon),teammate=p.IsTeammate}).ToArray():[]});
        });
        try{await host.StartAsync(lifetime.Token).ConfigureAwait(false);lifetime.Token.ThrowIfCancellationRequested();app=host;var address=host.Urls.Single();Port=new Uri(address).Port;Link=$"http://127.0.0.1:{Port}/{token}/";Status="This PC only";}
        catch{await host.DisposeAsync().ConfigureAwait(false);throw;}
        }
        finally{lifecycle.Release();}
    }
    internal async Task ShareInternet(string executable)
    {
        await Start().ConfigureAwait(false);
        await lifecycle.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposed)!=0,this);
        if(tunnel is not null&&!tunnel.HasExited)return;
        if(!File.Exists(executable))throw new FileNotFoundException("Missing tools/cloudflared.exe. Run the radar setup.");
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true,RedirectStandardOutput=true};
        foreach(string arg in new[]{"tunnel","--no-autoupdate","--url",$"http://127.0.0.1:{Port}"})info.ArgumentList.Add(arg);
        var ready=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        tunnel=new Process{StartInfo=info,EnableRaisingEvents=true};tunnel.Exited+=(_,_)=>{Status="Tunnel stopped";ready.TrySetException(new InvalidOperationException("Cloudflare closed the tunnel. Check the connection."));};
        void Line(object _,DataReceivedEventArgs e)
        {
            if(e.Data is null)return;var match=Regex.Match(e.Data,@"https://[a-zA-Z0-9-]+\.trycloudflare\.com");if(match.Success)ready.TrySetResult(match.Value);
        }
        tunnel.ErrorDataReceived+=Line;tunnel.OutputDataReceived+=Line;tunnel.Start();tunnel.BeginErrorReadLine();tunnel.BeginOutputReadLine();Status="Creating private link";
        try{string url=await ready.Task.WaitAsync(TimeSpan.FromSeconds(45),lifetime.Token).ConfigureAwait(false);lifetime.Token.ThrowIfCancellationRequested();Link=url+"/"+token+"/";Status="Sharing online";}
        catch{StopTunnel();throw;}
        }
        finally{lifecycle.Release();}
    }
    void StopTunnel(){if(tunnel is not null){try{if(!tunnel.HasExited)tunnel.Kill();}catch(InvalidOperationException){}tunnel.Dispose();tunnel=null;}}
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;lifetime.Cancel();lifecycle.Wait();
        try{StopTunnel();if(app is not null){using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));try{app.StopAsync(timeout.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){}app.DisposeAsync().AsTask().GetAwaiter().GetResult();app=null;}Link="Web radar stopped";Status="Not sharing";}
        finally{lifecycle.Release();}
    }
    static string Page=>Resource("WebRadarPage.html");
    static string Resource(string name)
    {
        using var stream=typeof(WebRadar).Assembly.GetManifestResourceStream("Nexo."+name)??throw new InvalidOperationException("Radar asset missing");
        using var reader=new StreamReader(stream);return reader.ReadToEnd();
    }
}
