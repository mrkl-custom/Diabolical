using System.IO;

namespace Diabolical.Services;

/// <summary>
/// Locates the repo root (marked by Diabolical.sln) so services can find files that live
/// alongside the source tree — data/characters, appsettings.local.json — rather than in
/// whatever bin/ output directory the app happens to be running from.
/// </summary>
internal static class RepoPaths
{
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Diabolical.sln")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? AppContext.BaseDirectory;
    }
}
