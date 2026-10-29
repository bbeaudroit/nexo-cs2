using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
namespace CsEsp;
internal sealed class UiBridge : IDisposable
{
    WebApplication? host;
    static readonly JsonSerializerOptions json=new(){PropertyNamingPolicy=null};
    readonly string token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    internal int Port {get;private set;}
    internal string Token=>token;
    internal async Task Start(Overlay overlay,bool announce=true)
    {
        var builder=WebApplication.CreateSlimBuilder();builder.Logging.ClearProviders();builder.WebHost.ConfigureKestrel(o=>{o.Listen(IPAddress.Loopback,0);o.Limits.MaxRequestBodySize=256*1024;});host=builder.Build();
        host.Use(async(ctx,next)=>
        {
            ctx.Response.Headers.CacheControl="no-store";
            if(!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.UTF8.GetBytes(ctx.Request.Headers["X-Nexo-Token"].ToString()),System.Text.Encoding.UTF8.GetBytes(token))){ctx.Response.StatusCode=401;return;}
            await next(ctx);
        });
        host.MapGet("/state",async()=>Results.Json(await overlay.OnUi(()=>overlay.ControlState()),json));
        host.MapGet("/settings",async()=>Results.Json(await overlay.OnUi(()=>overlay.ControlSettings),json));
        host.MapPut("/settings",async(HttpContext ctx)=>
        {
            try{var s=await JsonSerializer.DeserializeAsync<EspSettings>(ctx.Request.Body);if(s is null)return Results.BadRequest();return Results.Json(await overlay.OnUi(()=>{overlay.ControlUpdate(s);return overlay.ControlState();}),json);}
            catch(JsonException){return Results.BadRequest();}
        });
        host.MapPost("/command",async(HttpContext ctx)=>
        {
            try{var c=await JsonSerializer.DeserializeAsync<Command>(ctx.Request.Body);if(c is null||string.IsNullOrEmpty(c.Action)||c.Argument is null||c.Action.Length>80||c.Argument.Length>1000)return Results.BadRequest();return Results.Json(await overlay.OnUi(()=>overlay.ControlCommand(c.Action,c.Argument)),json);}
            catch(JsonException){return Results.BadRequest();}
        });
        await host.StartAsync().ConfigureAwait(false);int port=new Uri(host.Urls.Single()).Port;Port=port;
        if(announce)Console.WriteLine(JsonSerializer.Serialize(new{type="bridge-ready",port,token}));
    }
    record Command(string Action,string Argument);
    public void Dispose(){if(host is not null){using var timeout=new CancellationTokenSource(2500);try{host.StopAsync(timeout.Token).GetAwaiter().GetResult();}catch(OperationCanceledException){}host.DisposeAsync().AsTask().GetAwaiter().GetResult();host=null;}}
}
