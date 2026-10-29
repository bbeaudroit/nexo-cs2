namespace CsEsp;
// Resolves installation resources independently from writable user data.
// Environment lookup is performed by the caller, keeping this helper deterministic.
internal sealed record RuntimePaths(string ResourceRoot,string DataRoot)
{
    internal string SettingsPath=>Path.Combine(DataRoot,"esp-settings.json");
    internal string SessionLogPath=>Path.Combine(DataRoot,"nexo-session.log");
    internal static RuntimePaths Resolve(string engineDirectory,string? dataRoot=null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(engineDirectory);
        string resources=Path.GetFullPath(Path.Combine(engineDirectory,".."));
        string data=string.IsNullOrWhiteSpace(dataRoot)?resources:Path.GetFullPath(dataRoot);
        Directory.CreateDirectory(data);
        return new(resources,data);
    }
}
