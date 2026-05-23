namespace Knovault.Api;

public sealed class AppPaths
{
    public string DataRoot { get; }
    public string DbPath => Path.Combine(DataRoot, "knovault.db");
    public string CoversDir => Path.Combine(DataRoot, "covers");

    public AppPaths()
    {
        DataRoot = Environment.GetEnvironmentVariable("KNOVAULT_DATA")
            ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Knovault");
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(CoversDir);
    }
}
