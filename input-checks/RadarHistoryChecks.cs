using CsEsp;
using System.Diagnostics;
using System.Numerics;
internal static class RadarHistoryChecks
{
    internal static void Run()
    {
        int count=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);count++;}
        Check(RadarMapFeed.Parse("{\"auth\":{\"token\":\"secret\"},\"map\":{\"name\":\"workshop/123/de_mirage\"}}","secret")=="de_mirage","GSI must normalize workshop map names");
        Check(RadarMapFeed.Parse("{\"auth\":{\"token\":\"wrong\"},\"map\":{\"name\":\"de_nuke\"}}","secret") is null,"Unauthenticated map updates must be ignored");
        Check(RadarMapFeed.Parse("{\"auth\":{\"token\":\"secret\"}}","secret")=="","Leaving a map must clear its identity");
        Check(RadarMapFeed.Parse("not json","secret") is null,"Malformed GSI must be rejected");
        Check(RadarMapFeed.Parse("[]","secret") is null,"Non-object GSI must be rejected without a server error");
        Check(RadarMapCatalog.Normalize("../../evil") == "" && RadarMapCatalog.Normalize("de_dust2")=="de_dust2","Only map identifiers are accepted");
        var map=new RadarMap("de_test",-1000,2000,4,1024,1024,"de_test.png",[]);
        var point=map.Project(new(1048,-48,0));
        Check(point==new Vector2(.5f,.5f),"Map transform must use fixed origin and scale, invert Y and normalize image size");
        var history=new ChamsHistory();long start=Stopwatch.GetTimestamp();
        Frame Sample(int ms,float x=100,uint identity=42)=>new([new(new(x,0,0),100,3,"Bot",7,30,[new(x,0,64)],10){EntityId=42,EntityHandle=identity}],new float[16],default,0,"Fixture",0,start+(long)(ms*Stopwatch.Frequency/1000d),1){Local=new(){Alive=true}};
        history.Capture(Sample(0),true,200,"de_test");
        Check(history.Get(42,Sample(0).Players[0],start,200) is null,"History must wait for an old-enough pose");
        for(int ms=50;ms<=250;ms+=50)history.Capture(Sample(ms,100+ms/10f),true,200,"de_test");
        var old=history.Get(42,Sample(250,125).Players[0],Sample(250).Timestamp,200);
        Check(old is not null&&old.Position.X==105,"History must select the requested delayed pose");
        Check(history.Get(42,Sample(250,125,99).Players[0],Sample(250).Timestamp,200) is null,"Entity reuse must never inherit another player's history");
        history.Capture(Sample(300,900),true,200,"de_test");
        Check(history.Get(42,Sample(300,900).Players[0],Sample(300).Timestamp,200) is null,"Teleport must clear old poses");
        history.Capture(Sample(350),true,200,"de_other");Check(history.Count<=1,"Changing map must clear history");
        history.Capture(Sample(400),false,200,"de_other");Check(history.Count==0,"Disabling history must release retained poses");
        history.Capture(Sample(450),true,200,"de_other");history.Capture(Sample(500) with{Players=[]},true,200,"de_other");Check(history.Count==0,"Removed or dead players must not leave ghosts");
        var source=Sample(600);history.Capture(source,true,50,"de_other");source.Players[0].Bones[0]=new(999,999,999);history.Capture(Sample(650),true,50,"de_other");
        Check(history.Get(42,Sample(650).Players[0],Sample(650).Timestamp,50)?.Bones[0].X==100,"Recorded poses must own their bone arrays");
        var invalid=Sample(700) with{Players=[Sample(700).Players[0] with{Bones=[new(float.NaN,0,0)]}]};history.Capture(invalid,true,50,"de_other");
        Check(!new EspSettings().ChamsHistoryEnabled&&new EspSettings{ChamsHistoryDelay=999,ChamsHistoryOpacity=999}.Normalize() is{ChamsHistoryDelay:500,ChamsHistoryOpacity:60},"History must be opt-in and bounded");
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
        using var mapFixture=new RadarMapFixture();
        Frame published=Sample(0) with{Timestamp=Stopwatch.GetTimestamp()};
        using(var server=new WebRadar(()=>published,mapsDirectory:mapFixture.Folder,mapSource:()=>"de_mirage")){
            server.Start().GetAwaiter().GetResult();using var http=new HttpClient();
            var page=http.GetStringAsync(server.Link).GetAwaiter().GetResult();Check(page.Contains("fixed orientation")&&!page.Contains("f.yaw*Math"),"Shared page must use the fixed-map layout");
            using var json=System.Text.Json.JsonDocument.Parse(http.GetStringAsync(server.Link+"data").GetAwaiter().GetResult());
            Check(json.RootElement.GetProperty("map").GetProperty("name").GetString()=="de_mirage","Server must resolve the detected map metadata");
            Check(json.RootElement.GetProperty("players").GetArrayLength()==1,"Server must publish fresh player coordinates");
            Check(http.GetAsync(server.Link+"image/de_mirage.png").GetAwaiter().GetResult().IsSuccessStatusCode,"Authenticated radar must serve the map image");
            Check(http.GetAsync(server.Link+"image/not-a-map.png").GetAwaiter().GetResult().StatusCode==System.Net.HttpStatusCode.NotFound,"Radar must reject arbitrary files");
            Check(http.GetAsync($"http://127.0.0.1:{server.Port}/maps").GetAwaiter().GetResult().StatusCode==System.Net.HttpStatusCode.NotFound,"Map catalog must remain under the private URL");
            using var selected=System.Text.Json.JsonDocument.Parse(http.GetStringAsync(server.Link+"data?map=de_nuke").GetAwaiter().GetResult());
            Check(selected.RootElement.GetProperty("map").GetProperty("layers").GetArrayLength()==2,"Manual map selection must expose both synthetic fixture floors");
            published=published with{Timestamp=Stopwatch.GetTimestamp()-Stopwatch.Frequency};
            using var stale=System.Text.Json.JsonDocument.Parse(http.GetStringAsync(server.Link+"data").GetAwaiter().GetResult());Check(!stale.RootElement.GetProperty("fresh").GetBoolean()&&stale.RootElement.GetProperty("players").GetArrayLength()==0&&!stale.RootElement.GetProperty("local").GetProperty("alive").GetBoolean(),"Stale radar must remove all player markers");
        }
        string temp=Path.Combine(Path.GetTempPath(),"nexo-gsi-check-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(temp,"cfg"));
        try{using var feed=new RadarMapFeed(temp,temp,0);feed.Start(CancellationToken.None).GetAwaiter().GetResult();Check(feed.Status.StartsWith("Auto map ready"),"GSI test server must start on loopback");
            Check(feed.Port>0&&feed.Port!=21738,"GSI checks must use an isolated OS-assigned port");
            Check(File.ReadAllText(Path.Combine(temp,"cfg","gamestate_integration_nexo_radar.cfg")).Contains($"http://127.0.0.1:{feed.Port}/nexo-map"),"Generated integration must target the bound listener port");
            using var http=new HttpClient();string token=File.ReadAllText(Path.Combine(temp,"radar-gsi-token.txt"));
            var body=System.Text.Json.JsonSerializer.Serialize(new{auth=new{token},map=new{name="de_nuke"}});
            Check(http.PostAsync($"http://127.0.0.1:{feed.Port}/nexo-map",new StringContent(body)).GetAwaiter().GetResult().IsSuccessStatusCode&&feed.Current=="de_nuke","Authenticated GSI must update the map");
            Check(http.PostAsync($"http://127.0.0.1:{feed.Port}/nexo-map",new StringContent("{}")).GetAwaiter().GetResult().StatusCode==System.Net.HttpStatusCode.Unauthorized&&feed.Current=="de_nuke","Unauthenticated GSI must not replace the detected map");
            Check(File.ReadAllText(Path.Combine(temp,"cfg","gamestate_integration_nexo_radar.cfg")).Contains("\"map\" \"1\""),"Generated integration must request map state");
        }finally{Directory.Delete(temp,true);}
        Console.WriteLine($"{count} fixed-map / pose history checks passed.");
    }
}

internal sealed class RadarMapFixture:IDisposable
{
 internal string Folder {get;}=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"nexo-radar-fixture-"+Guid.NewGuid().ToString("N")));
 internal RadarMapFixture(){
  Directory.CreateDirectory(Folder);
  foreach(string name in new[]{"de_mirage.png","de_nuke.png"}){using var image=new System.Drawing.Bitmap(1024,1024);image.Save(Path.Combine(Folder,name),System.Drawing.Imaging.ImageFormat.Png);}
  RadarMap[] maps=[new("de_mirage",0,2048,4,1024,1024,"de_mirage.png",[]),new("de_nuke",0,2048,4,1024,1024,"de_nuke.png",[new("Upper",0,100,"de_nuke.png"),new("Lower",-100,0,"de_nuke.png")])];
  File.WriteAllText(Path.Combine(Folder,"maps.json"),System.Text.Json.JsonSerializer.Serialize(maps));
 }
 public void Dispose(){if(Path.GetDirectoryName(Folder)!=Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()))||!Path.GetFileName(Folder).StartsWith("nexo-radar-fixture-",StringComparison.Ordinal))throw new InvalidOperationException("Unexpected fixture directory");Directory.Delete(Folder,true);}
}
