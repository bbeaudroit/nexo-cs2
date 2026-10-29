namespace CsEsp;
internal static class Program
{
    static IEnumerable<Control> All(Control parent)=>parent.Controls.Cast<Control>().SelectMany(c=>new[]{c}.Concat(All(c)));
    [STAThread]
    static int Main(string[] args)
    {
        StreamWriter? sessionLog=null;
        try
        {
            if(args.Contains("--installation-id")){Console.WriteLine(InstallationIdentity.Get());return 0;}
            if(args.Contains("--capture-check"))
            {
                ApplicationConfiguration.Initialize();
                using var surface=new Form{Width=400,Height=200,FormBorderStyle=FormBorderStyle.None,BackColor=Color.Black,TransparencyKey=Color.Black,ShowInTaskbar=false};
                surface.Show();Application.DoEvents();bool applied=Native.SetWindowDisplayAffinity(surface.Handle,0x11);int error=applied?0:System.Runtime.InteropServices.Marshal.GetLastWin32Error();bool queried=Native.GetWindowDisplayAffinity(surface.Handle,out uint affinity);
                Console.WriteLine($"Color-key capture: applied={applied}; queried={queried}; affinity={affinity}; error={error}");surface.Close();return applied&&queried&&affinity==0x11?0:1;
            }
            if(args.Contains("--export-ui-defaults"))
            {
                string target=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","desktop-ui","defaults.json"));Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.WriteAllText(target,System.Text.Json.JsonSerializer.Serialize(new{settings=new EspSettings(),aim=new AimProfile()},new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));return 0;
            }
            if(args.Contains("--web-check"))
            {
                using var radar=new WebRadar(()=>Frame.Empty);radar.Start().GetAwaiter().GetResult();using var client=new HttpClient();
                var page=client.GetAsync(radar.Link).GetAwaiter().GetResult();if(!page.IsSuccessStatusCode)throw new Exception("No se pudo abrir la pagina privada");
                var data=client.GetStringAsync(radar.Link+"data").GetAwaiter().GetResult();if(!data.Contains("\"players\""))throw new Exception("Respuesta de radar invalida");
                var denied=client.GetAsync($"http://127.0.0.1:{radar.Port}/sin-token/data").GetAwaiter().GetResult();if(denied.StatusCode!=System.Net.HttpStatusCode.NotFound)throw new Exception("El radar acepta solicitudes sin token");
                using var interrupted=new WebRadar(()=>Frame.Empty,250);var starting=interrupted.Start();interrupted.Dispose();try{starting.GetAwaiter().GetResult();throw new Exception("El arranque no se cancelo");}catch(OperationCanceledException){}
                if(interrupted.Port!=0)throw new Exception("Quedo un servidor iniciado tras detenerlo");
                using var interruptedShare=new WebRadar(()=>Frame.Empty,250);var sharing=interruptedShare.ShareInternet("no-existe.exe");interruptedShare.Dispose();try{sharing.GetAwaiter().GetResult();throw new Exception("El enlace no se cancelo");}catch(OperationCanceledException){}
                Console.WriteLine("5 comprobaciones de radar web correctas, incluida cancelacion durante arranque; no se publico ningun tunel.");return 0;
            }
            if(args.Contains("--input-check"))
            {
                if(System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>()!=40||System.Runtime.InteropServices.Marshal.OffsetOf<Native.Input>(nameof(Native.Input.Flags)).ToInt32()!=20)throw new Exception("Estructura Win32 de entrada incorrecta");
                Console.WriteLine("Estructura Win32 verificada; no se genero entrada de mouse.");return 0;
            }
            if(args.Contains("--menu-preview")||args.Contains("--render-menu")||args.Contains("--ui-check")||string.Equals(Path.GetFileName(Environment.ProcessPath),"MenuPreview.exe",StringComparison.OrdinalIgnoreCase))
            {
                ApplicationConfiguration.Initialize();EspSettings preview=new();using var testMenu=new SettingsMenu(()=>preview,v=>preview=v,true);
                if(args.Contains("--ui-check"))
                {
                    testMenu.Show();Application.DoEvents();if(!testMenu.Visible)throw new Exception("El menu no se abre");
                    var controls=All(testMenu).ToArray();var skeleton=controls.OfType<CheckBox>().Single(c=>c.Text=="Esqueleto");skeleton.Checked=false;if(preview.Skeleton)throw new Exception("El control de esqueleto no actualiza la configuracion");
                    var size=controls.OfType<NumericUpDown>().Single(c=>c.AccessibleName=="Tamaño de texto");size.Value=12;if(preview.FontSize!=12)throw new Exception("El tamaño de texto no actualiza la configuracion");
                    var tabs=controls.OfType<TabControl>().Single();for(int i=0;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;Application.DoEvents();if(!tabs.TabPages[i].Visible)throw new Exception("Pestaña no visible");}
                    testMenu.Hide();if(testMenu.Visible)throw new Exception("El menu no se cierra");
                    Console.WriteLine("Menu: apertura, cierre, seis secciones, checkbox y tamaño de texto verificados.");return 0;
                }
                if(args.Contains("--render-menu"))
                {
                    testMenu.Show();Application.DoEvents();string output=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","work"));Directory.CreateDirectory(output);
                    var tabs=All(testMenu).OfType<TabControl>().Single();
                    for(int i=0;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;Application.DoEvents();using var bitmap=new Bitmap(testMenu.Width,testMenu.Height);testMenu.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save(Path.Combine(output,$"menu-{i}.png"));}
                    Console.WriteLine($"Menu renderizado en {output}");return 0;
                }
                Application.Run(testMenu);return 0;
            }
            using var license=new LicenseGuard();
            if(args.Contains("--license-check")){Console.WriteLine("Licencia del motor validada.");return 0;}
            var paths=RuntimePaths.Resolve(AppContext.BaseDirectory,Environment.GetEnvironmentVariable("NEXO_DATA_ROOT"));
            string root=paths.ResourceRoot;
            sessionLog=args.Length==0?new StreamWriter(paths.SessionLogPath,false){AutoFlush=true}:null;
            if(sessionLog is not null){Console.SetOut(sessionLog);Console.SetError(sessionLog);}
            string folder=args.FirstOrDefault(a=>!a.StartsWith("--"))??Path.Combine(root,"dumps","manual-01");
            using var game=new GameReader(folder);
            if(args.Contains("--diagnose"))
            {
                game.ProbeBones=args.Contains("--probe-bones");
                var frame=game.Sample(true,args.Contains("--world"),args.Contains("--world"));Console.WriteLine(frame.Status);
                game.ProbeBones=false;
                Console.WriteLine($"Lecturas fallidas durante sondeo: {game.FailedReads}");
                Console.WriteLine($"Matriz finita: {frame.Matrix.All(float.IsFinite)}; enemigos vivos: {frame.Players.Length}; esqueletos validos: {frame.BonesValid}; lectura: {frame.ReadMs:F2} ms");
                foreach(var p in frame.Players)Console.WriteLine($"Enemigo: HP={p.Health}; arma={WeaponNames.Label(p.Weapon)}; municion={p.Ammo}; nombre leido={!string.IsNullOrWhiteSpace(p.Name)}; huesos={p.Bones.Length}; distancia={p.Distance:F0}m");
                Console.WriteLine($"Local vivo={frame.Local.Alive}; arma={WeaponNames.Label(frame.Local.Weapon)}; recoil valido={frame.Local.RecoilValid}; espectadores={frame.Spectators.Length}; objetos={frame.World.Length}; daño atribuible={frame.Local.DamageValid}; impactos registrados={frame.Local.TotalHits}; FOV={frame.Local.Fov}; sensibilidad de mira={frame.Local.SensitivityScale}");
                if(args.Contains("--world"))Console.WriteLine(game.WorldDiagnostics);
                foreach(var item in frame.World.Take(12))Console.WriteLine($"Mundo: {item.Kind} · {item.Name}; reloj validado={item.Remaining>=0}");
                var timings=new List<double>();for(int i=0;i<100;i++)timings.Add(game.Sample().ReadMs);timings.Sort();Console.WriteLine($"100 lecturas: mediana {timings[50]:F2}ms; p95 {timings[95]:F2}ms");
                return 0;
            }
            ApplicationConfiguration.Initialize();
            if(args.Contains("--bridge-check"))
            {
                string file=Path.Combine(root,"work","bridge-check-settings.json");new EspSettings().Save(file);
                using var sample=new Overlay(game,file){ExternalUi=true,ControlTest=true};using var server=new UiBridge();_ = sample.Handle;server.Start(sample,false).GetAwaiter().GetResult();
                using var watchdog=new System.Windows.Forms.Timer{Interval=8000};watchdog.Tick+=(_,_)=>sample.Close();watchdog.Start();
                var checks=Task.Run(async()=>
                {
                    try
                    {
                        using var client=new HttpClient{BaseAddress=new Uri($"http://127.0.0.1:{server.Port}")};
                        if((await client.GetAsync("/state")).StatusCode!=System.Net.HttpStatusCode.Unauthorized)throw new Exception("El puente acepta solicitudes sin token");
                        client.DefaultRequestHeaders.Add("X-Nexo-Token",server.Token);
                        using var body=new StringContent(System.Text.Json.JsonSerializer.Serialize(new EspSettings{GlowStrength=63}),System.Text.Encoding.UTF8,"application/json");
                        var response=await client.PutAsync("/settings",body);response.EnsureSuccessStatusCode();using var data=System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                        if(!data.RootElement.GetProperty("settingsSaved").GetBoolean()||data.RootElement.GetProperty("settings").GetProperty("GlowStrength").GetInt32()!=63)throw new Exception("No se aplicó el ajuste en el hilo de interfaz");
                        if(!data.RootElement.GetProperty("overlayCaptureProtected").GetBoolean())throw new Exception("Overlay capture exclusion was not applied and confirmed by Windows");
                        var state=await client.GetStringAsync("/state");if(!state.Contains("\"menuRequested\":true"))throw new Exception("El menú no bloquea la entrada al arrancar");
                        async Task<string> Command(string action,string argument){using var payload=new StringContent(System.Text.Json.JsonSerializer.Serialize(new{Action=action,Argument=argument}),System.Text.Encoding.UTF8,"application/json");var result=await client.PostAsync("/command",payload);result.EnsureSuccessStatusCode();return System.Text.Json.JsonSerializer.Deserialize<string>(await result.Content.ReadAsStringAsync())!;}
                        await Command("ui-visible","0");using(var hidden=System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/state"))){if(!hidden.RootElement.GetProperty("menuRequested").GetBoolean())throw new Exception("La visibilidad real sobreescribió la intención de abrir");}
                        var revision=await Command("ui-request","0");await Command("ui-visible","1");using(var delayed=System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/state"))){if(delayed.RootElement.GetProperty("menuRequested").GetBoolean()||delayed.RootElement.GetProperty("menuVersion").GetInt64().ToString()!=revision)throw new Exception("Una confirmación atrasada reabrió el menú");}
                        if(await Command("ui-request","0")!=revision)throw new Exception("La intención de menú no es idempotente");
                        await Command("ui-request","1");
                        using var before=System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/state"));long firstSample=before.RootElement.GetProperty("sampleAt").GetInt64();
                        for(int i=0;i<20;i++)
                        {
                            bool protect=i%2==1;using var captureSettings=new StringContent(System.Text.Json.JsonSerializer.Serialize(new EspSettings{ExcludeFromCapture=protect}),System.Text.Encoding.UTF8,"application/json");
                            using var updated=await client.PutAsync("/settings",captureSettings);updated.EnsureSuccessStatusCode();
                            using var captureState=System.Text.Json.JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
                            if(captureState.RootElement.GetProperty("overlayCaptureProtected").GetBoolean()!=protect)throw new Exception("Capture toggle was not confirmed by Windows");
                        }
                        await Task.Delay(150);using var after=System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/state"));if(after.RootElement.GetProperty("sampleAt").GetInt64()<=firstSample||after.RootElement.GetProperty("ageMs").GetDouble()>200)throw new Exception("Capture toggles stalled the reader");
                        Console.WriteLine("Puente Electron/motor verificado: autenticación, persistencia, intención de menú versionada y confirmaciones atrasadas.");
                        Console.WriteLine("20 capture toggles confirmed; sampling remains fresh after HWND recreation. Automated input blocked.");
                    }
                    finally{sample.BeginInvoke((Action)(()=>sample.Close()));}
                });
                Application.Run(sample);checks.GetAwaiter().GetResult();return 0;
            }
            if(args.Contains("--persistence-check"))
            {
                string file=Path.Combine(root,"work","save-denied-"+Guid.NewGuid()+".json");new EspSettings().Save(file);
                try
                {
                    File.SetAttributes(file,FileAttributes.ReadOnly);using var sample=new Overlay(game,file);sample.ControlUpdate(sample.ControlSettings with{GlowStrength=61});
                    using var state=System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(sample.ControlState()));
                    if(state.RootElement.GetProperty("settingsSaved").GetBoolean()||sample.ControlSettings.GlowStrength!=61)throw new Exception("No se detectó la escritura denegada");
                    Console.WriteLine("Escritura denegada detectada: cambios activos y estado Sin guardar informado.");return 0;
                }
                finally{File.SetAttributes(file,FileAttributes.Normal);File.Delete(file);if(File.Exists(file+".tmp"))File.Delete(file+".tmp");}
            }
            if(args.Contains("--render-overlay")){using var sample=new Overlay(game,Path.Combine(root,"esp-settings.json"));sample.RenderDemo(Path.Combine(root,"work"));return 0;}
            Native.timeBeginPeriod(1);
            try
            {
                if(args.Contains("--startup-check"))
                {
                    string testFile=Path.Combine(root,"work","startup-settings.json");Directory.CreateDirectory(Path.GetDirectoryName(testFile)!);new EspSettings().Save(testFile);
                    using var testOverlay=new Overlay(game,testFile){Opacity=0};using var timer=new System.Windows.Forms.Timer{Interval=1500};Frame sample=Frame.Empty;
                    timer.Tick+=(_,_)=>{timer.Stop();sample=testOverlay.Latest;testOverlay.Close();};timer.Start();Application.Run(testOverlay);
                    if(sample.Timestamp==Frame.Empty.Timestamp)throw new Exception("El lector no produjo datos al arrancar");
                    Console.WriteLine($"Arranque y cierre del lector verificados; muestra con {sample.Players.Length} enemigos. Entradas automaticas desactivadas.");return 0;
                }
                using var overlay=new Overlay(game,paths.SettingsPath,resourceRoot:root){ExternalUi=args.Contains("--ui-bridge"),ControlTest=args.Contains("--control-test")};
                using var bridge=overlay.ExternalUi?new UiBridge():null;
                if(bridge is not null){_ = overlay.Handle;bridge.Start(overlay).GetAwaiter().GetResult();}
                license.Monitor();Application.Run(overlay);return 0;
            }
            finally{Native.timeEndPeriod(1);}
        }
        catch(Exception ex){Console.Error.WriteLine(ex.Message);if(args.Length==0)MessageBox.Show(ex.Message,"Nexo",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}
        finally{sessionLog?.Dispose();}
    }
}
