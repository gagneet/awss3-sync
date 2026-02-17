using Microsoft.Extensions.FileSystemGlobbing;

namespace FileSyncApp.Core.Services;

public class FileFilterService
{
    private readonly Matcher _matcher;

    public FileFilterService(IEnumerable<string> includes, IEnumerable<string> excludes)
    {
        _matcher = new Matcher();
        foreach (var include in includes) _matcher.AddInclude(include);
        foreach (var exclude in excludes) _matcher.AddExclude(exclude);
    }

    public bool IsMatch(string relativePath)
    {
        return _matcher.Match(relativePath).HasMatches;
    }
}
