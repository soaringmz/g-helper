using System.Text.RegularExpressions;

namespace GHelper.Input;

public static class CommandSanitizer
{
    private static readonly Regex AllowedCommandPattern = new(@"^[\w\s\.\-:/\\]+$");

    public static bool IsCommandAllowed(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return false;
        return AllowedCommandPattern.IsMatch(command) && command.IndexOfAny(new[] { '&', '|', '>', '<', ';' }) == -1;
    }
}
