using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace CsEsp;

// Reads the active Steam user's saved CS2 convars. Never writes game config.
internal sealed class SensitivityConfig
{
    long checkedAt;
    internal float? Value {get;private set;}
    internal string Status {get;private set;}="Waiting for saved CS2 config";
    internal static float? Parse(string text)
    {
        var matches=Regex.Matches(text,@"(?m)^\s*""sensitivity""\s*""([^""\r\n]+)""",RegexOptions.CultureInvariant);
        if(matches.Count!=1)return null;
        return float.TryParse(matches[0].Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out float value)&&float.IsFinite(value)&&value is >=.01f and <=20?value:null;
    }
    internal void Refresh()
    {
        long now=Stopwatch.GetTimestamp();if(checkedAt!=0&&Stopwatch.GetElapsedTime(checkedAt,now).TotalSeconds<2)return;checkedAt=now;
        Value=null;
        try
        {
            string? steam=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath",null) as string;
            object? user=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam\ActiveProcess","ActiveUser",null);
            if(string.IsNullOrWhiteSpace(steam)||user is not int id||id==0){Status="Active Steam account unavailable · manual fallback";return;}
            string folder=Path.Combine(steam,"userdata",unchecked((uint)id).ToString(CultureInfo.InvariantCulture),"730","local","cfg");
            // Slot 0 is the primary local player. Do not guess another account or slot.
            string file=Path.Combine(folder,"cs2_user_convars_0_slot0.vcfg");
            var info=new FileInfo(file);if(!info.Exists||info.Length>2*1024*1024){Status="Saved CS2 config unavailable · manual fallback";return;}
            using var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var reader=new StreamReader(stream);Value=Parse(reader.ReadToEnd());
            Status=Value.HasValue?"Auto · saved CS2 config (refreshes every 2 s)":"Saved sensitivity invalid · manual fallback";
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {Status="Cannot read saved CS2 config · manual fallback";}
    }
    internal static float Resolve(bool automatic,float? detected,float manual)=>automatic&&detected is >=.01f and <=20?detected.Value:manual;
}
