using System.Text.RegularExpressions;

namespace Knovault.Infrastructure.Files;

public static class FilenameTitleCleaner
{
    public static string Clean(string filePath)
    {
        var name = Path.GetFileNameWithoutExtension(filePath);
        name = name.Replace('_', ' ').Replace('.', ' ');
        name = Regex.Replace(name, @"\s+", " ").Trim();
        return string.IsNullOrWhiteSpace(name) ? "Untitled" : name;
    }
}
