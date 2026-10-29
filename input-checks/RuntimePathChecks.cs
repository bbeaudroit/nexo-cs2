using CsEsp;
internal static class RuntimePathChecks
{
    internal static void Run()
    {
        int passed=0;void Check(bool value,string message){if(!value)throw new Exception(message);passed++;}
        string temporary=Path.Combine(Path.GetTempPath(),"nexo-paths-"+Guid.NewGuid().ToString("N"));
        try{
            string resources=Path.Combine(temporary,"resources"),engine=Path.Combine(resources,"engine");
            var portable=RuntimePaths.Resolve(engine);
            Check(portable.ResourceRoot==Path.GetFullPath(resources)&&portable.DataRoot==portable.ResourceRoot,"Portable roots must share resource parent");
            Check(Directory.Exists(portable.DataRoot),"Resolving runtime paths must prepare writable data directory");
            string data=Path.Combine(temporary,"profile","data");var installed=RuntimePaths.Resolve(engine,data);
            Check(installed.ResourceRoot==Path.GetFullPath(resources)&&installed.DataRoot==Path.GetFullPath(data),"Installed resources and user data must stay separate");
            Check(Directory.Exists(data),"Custom data directory must be created");
            Check(installed.SettingsPath==Path.Combine(data,"esp-settings.json"),"Settings must live in data root");
            Check(installed.SessionLogPath==Path.Combine(data,"nexo-session.log"),"Session log must live in data root");
            bool rejected=false;try{RuntimePaths.Resolve(" ");}catch(ArgumentException){rejected=true;}Check(rejected,"Empty resource roots must be rejected");
        }finally{if(Directory.Exists(temporary))Directory.Delete(temporary,true);}
        Console.WriteLine($"{passed} runtime path checks passed using isolated temporary directories.");
    }
}
