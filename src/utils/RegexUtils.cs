using System.Text.RegularExpressions;

namespace io.wispforest.textureswapper.utils;

public class RegexUtils {
    public static Regex parseRegexWithFlags(string regexStringWithFlags) {
        var regexPartPattern = new Regex("""(".*")""");

        var match = regexPartPattern.Match(regexStringWithFlags);

        if (!match.Success) return new Regex(regexStringWithFlags);
        
        var pattern = match.Value;

        var stringOptions = regexStringWithFlags.Replace(pattern, "");
        
        var options = RegexOptions.None;
        
        if (stringOptions.Contains("i")) options |= RegexOptions.IgnoreCase;
        if (stringOptions.Contains("m")) options |= RegexOptions.Multiline;
        
        return new Regex(pattern.TrimStart('"').TrimEnd('"'), options);
    }
}