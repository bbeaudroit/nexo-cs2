using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace CsEsp;
// GSI map identity only. This listener is separate from the public radar tunnel.
internal sealed class RadarMapFeed(string gameFolder,string root,int port=21738):IDisposable
{
    WebApplication? app;
    string name="";
    long received;
    internal string Current=>Stopwatch.GetElapsedTime(Interlocked.Read(ref received)).TotalSeconds<20?Volatile.Read(ref name):"";
    internal string Status {get;private set;}="Map auto-detection not started";
    internal int Port {get;private set;}
    internal static string? Parse(string json,string token)
    {
        try{
            using var doc=JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=12});var data=doc.RootElement;
            if(data.ValueKind!=JsonValueKind.Object)return null;
            if(!data.TryGetProperty("auth",out var auth)||auth.ValueKind!=JsonValueKind.Object||!auth.TryGetProperty("token",out var key)||key.ValueKind!=JsonValueKind.String)return null;
            byte[] actual=Encoding.UTF8.GetBytes(key.GetString()??""),expected=Encoding.UTF8.GetBytes(token);
            if(!CryptographicOperations.FixedTimeEquals(actual,expected))return null;
            if(!data.TryGetProperty("map",out var map)||map.ValueKind==JsonValueKind.Null)return "";
            if(map.ValueKind!=JsonValueKind.Object||!map.TryGetProperty("name",out var mapName)||mapName.ValueKind!=JsonValueKind.String)return "";
            return RadarMapCatalog.Normalize(mapName.GetString());
        }catch(JsonException){return null;}
    }
    internal async Task Start(CancellationToken cancellation)
    {
        if(app is not null)return;
        if(port is <0 or >65535)throw new ArgumentOutOfRangeException(nameof(port));
        try{
            string keyPath=Path.Combine(root,"radar-gsi-token.txt");
            string token=File.Exists(keyPath)?File.ReadAllText(keyPath).Trim():"";
            if(token.Length!=48||token.Any(c=>!Uri.IsHexDigit(c))){token=Convert.ToHexString(RandomNumberGenerator.GetBytes(24));File.WriteAllText(keyPath,token);}
            var builder=WebApplication.CreateSlimBuilder();builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(o=>{o.Listen(IPAddress.Loopback,port);o.Limits.MaxRequestBodySize=16384;});
            var host=builder.Build();
            host.MapPost("/nexo-map",async(HttpContext context)=>{
                if(context.Request.ContentLength>16384)return Results.StatusCode(413);
                using var reader=new StreamReader(context.Request.Body);string body=await reader.ReadToEndAsync(context.RequestAborted);
                string? map=Parse(body,token);if(map is null)return Results.Unauthorized();
                Volatile.Write(ref name,map);Interlocked.Exchange(ref received,Stopwatch.GetTimestamp());return Results.Ok();
            });
            try{
                await host.StartAsync(cancellation).ConfigureAwait(false);
                Port=new Uri(host.Urls.Single()).Port;
                string cfg=Path.Combine(gameFolder,"cfg","gamestate_integration_nexo_radar.cfg");
                string text="\"Nexo radar map integration\"\n{\n\t\"uri\" \"http://127.0.0.1:"+Port+"/nexo-map\"\n\t\"timeout\" \"2.0\"\n\t\"buffer\" \"0.1\"\n\t\"throttle\" \"0.5\"\n\t\"heartbeat\" \"5.0\"\n\t\"auth\" { \"token\" \""+token+"\" }\n\t\"data\" { \"provider\" \"1\" \"map\" \"1\" }\n}\n";
                if(!Directory.Exists(Path.GetDirectoryName(cfg)))throw new DirectoryNotFoundException("CS2 cfg directory unavailable");
                if(File.Exists(cfg)){
                    string old=File.ReadAllText(cfg);if(!old.StartsWith("\"Nexo radar map integration\"",StringComparison.Ordinal))throw new IOException("Existing Nexo GSI file is not managed by this version");
                    if(old!=text){File.Copy(cfg,cfg+".bak",true);File.WriteAllText(cfg,text);}
                }else File.WriteAllText(cfg,text);
                app=host;Status="Auto map ready · restart CS2 once if no map arrives";
            }catch{await host.DisposeAsync().ConfigureAwait(false);throw;}
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or InvalidOperationException){Status="Auto map unavailable · use manual map selection";Console.Error.WriteLine("Radar map feed: "+e.Message);}
    }
    public void Dispose(){if(app is null)return;using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2));try{app.StopAsync(timeout.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){}app.DisposeAsync().AsTask().GetAwaiter().GetResult();app=null;}
}
