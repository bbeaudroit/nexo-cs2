using CsEsp;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Numerics;

internal static class VisualChecks
{
    internal static void Run()
    {
        void Require(bool condition,string message){if(!condition)throw new Exception(message);}
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..",".."));
        string output=Path.Combine(root,"work");Directory.CreateDirectory(output);
        var config=(new EspSettings{ChamsEnabled=true,ChamsOpacity=35,ChamsOutline=true,ChamsOutlineWidth=2,EspMaxDistance=999,EspLayout=new(){NameX=999,NameY=-999}}).Normalize();
        Require(config.EspMaxDistance==500&&config.EspLayout.NameX==150&&config.EspLayout.NameY==-150,"Native range/layout limits failed");
        var layout=(new EspLayout{NameX=20,NameY=-10}).Offset("Name",new(10,20,100,200));Require(layout==new Vector2(20,-20),"Layout must scale relative to box dimensions");
        var keys=(new EspSettings{MenuKey=45,ToggleKey=119,PrivacyKey=45}).Normalize();Require(keys.PrivacyKey==0,"Privacy hotkey must not collide with menu");
        Vector3 origin=new(-47,155,0);var bones=Enumerable.Repeat(origin+new Vector3(0,0,38),23).ToArray();
        void Joint(int i,float x,float z)=>bones[i]=origin+new Vector3(x,0,z);
        Joint(0,0,0);Joint(1,0,36);Joint(2,0,42);Joint(4,0,50);Joint(5,0,57);Joint(6,0,64);Joint(8,-7,56);Joint(9,-12,54);Joint(10,-17,43);Joint(11,-14,36);Joint(12,7,56);Joint(13,13,54);Joint(14,20,44);Joint(15,14,38);Joint(17,-7,34);Joint(18,-8,20);Joint(19,-9,2);Joint(20,7,34);Joint(21,8,20);Joint(22,10,2);
        var player=new Player(origin,84,3,"Fixture",7,30,bones,24);float[] matrix=[1,0,0,0,0,0,1.77778f,-113.7779f,0,0,1,0,0,1,0,0];
        Require(SensitivityConfig.Parse("\"convars\"\n{\n\t\"sensitivity\" \"1.230000\"\n}")==1.23f,"Read the exact sensitivity convar");
        foreach(string invalid in new[]{"\"zoom_sensitivity_ratio\" \"2\"","\"sensitivity\" \"NaN\"","\"sensitivity\" \"0\"","\"sensitivity\" \"99\"","\"sensitivity\" \"1,2\"","\"sensitivity\" \"1\"\n\"sensitivity\" \"2\""})Require(SensitivityConfig.Parse(invalid)==null,"Reject invalid/ambiguous sensitivity");
        Require(SensitivityConfig.Resolve(true,null,1.17f)==1.17f&&SensitivityConfig.Resolve(false,2,1.17f)==1.17f&&SensitivityConfig.Resolve(true,2,1.17f)==2,"Sensitivity manual fallback / auto resolution");
        string localModel=Path.Combine(root,"local-assets","sas.glb");
        if(!File.Exists(localModel)){Console.WriteLine("Visual checks: layout, hotkeys and sensitivity passed. SKIP SAS render checks: import your own CS2 model first; Valve assets are not included.");return;}
        using var renderer=new MeshChams(localModel);
        using var image=new Bitmap(1280,720,PixelFormat.Format32bppArgb);using var graphics=Graphics.FromImage(image);graphics.SmoothingMode=SmoothingMode.AntiAlias;
        renderer.Draw(graphics,player,matrix,1280,720,config);graphics.Flush();
        Require(renderer.Status=="External SAS mesh · approximate pose","Mesh render failed: "+renderer.Status);
        int filled=0,translucent=0,px=-1,py=-1;
        for(int y=0;y<720;y+=2)for(int x=0;x<1280;x+=2){var c=image.GetPixel(x,y);if(c.A>0){filled++;if(c.A<255)translucent++;if(px<0&&x>6&&x<1274&&y>6&&y<714&&c.A is >50 and <110&&new[]{(-5,0),(5,0),(0,-5),(0,5)}.All(d=>image.GetPixel(x+d.Item1,y+d.Item2).A is >50 and <110)){px=x;py=y;}}}
        Require(filled>500&&translucent>200,"Mesh must render a nonempty translucent silhouette");
        image.Save(Path.Combine(output,"mesh-chams-fixture.png"),ImageFormat.Png);
        graphics.Clear(Color.Transparent);renderer.Draw(graphics,player,matrix,1280,720,config with{ChamsOpacity=0});graphics.Flush();
        image.Save(Path.Combine(output,"mesh-contour-fixture.png"),ImageFormat.Png);
        Require(px>=0&&image.GetPixel(px,py).A==0,"Contour must not fill the interior");
        graphics.Clear(Color.Transparent);renderer.Draw(graphics,player,matrix,1280,720,config with{ChamsSupersampling=true});graphics.Flush();
        image.Save(Path.Combine(output,"mesh-smooth-current.png"),ImageFormat.Png);
        using(var invisible=new MeshChams("missing.glb")){invisible.Draw(graphics,player,matrix,1280,720,config with{ChamsOpacity=0,ChamsOutline=false});Require(invisible.Status=="External mesh not loaded","Invisible chams must skip loading / processing the mesh");}
        // Reusing a larger backing bitmap must not leave pixels behind on a smaller viewport.
        graphics.Clear(Color.Transparent);renderer.Draw(graphics,player,matrix,640,360,config);graphics.Flush();
        for(int y=360;y<720;y+=19)for(int x=0;x<1280;x+=19)Require(image.GetPixel(x,y).A==0,"Cached raster leaked outside the current viewport");
        var times=new List<double>();for(int i=0;i<12;i++){graphics.Clear(Color.Transparent);long start=Stopwatch.GetTimestamp();renderer.Draw(graphics,player,matrix,1280,720,config);times.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);}times.Sort();
        Console.WriteLine($"Visual checks: native layout/range/hotkey limits, SAS mesh fill and empty contour interior passed. Synthetic single-player render median={times[6]:F2} ms; max={times[^1]:F2} ms. Not a live-game FPS measurement.");
        File.WriteAllText(Path.Combine(output,"visual-0.7-result.json"),System.Text.Json.JsonSerializer.Serialize(new{status=renderer.Status,filled,translucent,medianMs=times[6],maxMs=times[^1],liveGame=false}));
    }
}
