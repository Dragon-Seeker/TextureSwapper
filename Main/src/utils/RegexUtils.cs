using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using io.wispforest.endec;
using io.wispforest.endec.impl;

namespace io.wispforest.textureswapper.utils;

public class RegexUtils {
    
    public static readonly StructEndec<RegexRawData> RAW_DATA_ENDEC = StructEndecBuilder.of(
        endec.Endec.STRING.fieldOf<RegexRawData>("regex", data => data.rawPattern),
        endec.Endec.STRING.listOf().optionalFieldOf<RegexRawData>("flags", data => data.options, () => []),
        (s, list) => new RegexRawData(s, list)
    );
    
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

    public static RegexOptions fromString(string str) {
        return str switch { 
                "IgnoreCase" or "i"                       => RegexOptions.IgnoreCase, 
                "Multiline" or "m"                        => RegexOptions.Multiline, 
                "ExplicitCapture" or "ec" or "n"          => RegexOptions.ExplicitCapture, 
                "Compiled" or "c"                         => RegexOptions.Compiled, 
                "Singleline" or "s"                       => RegexOptions.Singleline, 
                "IgnorePatternWhitespace" or "ipw" or "x" => RegexOptions.IgnorePatternWhitespace, 
                "RightToLeft" or "rtl" or "r"             => RegexOptions.RightToLeft, 
                "ECMAScript" or "ecmas"                   => RegexOptions.ECMAScript, 
                "CultureInvariant" or "ci"                => RegexOptions.CultureInvariant,
                _                                         => RegexOptions.None
        };
    }
}


public class RegexRawData(string rawPattern, IList<string> options) {
    public string rawPattern { get; } = rawPattern;
    public IList<string> options { get; } = options;

    public Regex toRegex() {
        var parsedOptions = this.options
                .Select(RegexUtils.fromString)
                .Aggregate(RegexOptions.None, (current, regexOption) => current | regexOption);
        
        var regexPartPattern = new Regex("""(".*")""");

        var match = regexPartPattern.Match(rawPattern);

        if (!match.Success) return new Regex(rawPattern);
        
        var pattern = match.Value;

        var stringOptions = pattern.Replace(pattern, "");
        
        if (stringOptions.Contains("i")) parsedOptions |= RegexOptions.IgnoreCase;
        if (stringOptions.Contains("m")) parsedOptions |= RegexOptions.Multiline;
        
        return new Regex(pattern.TrimStart('"').TrimEnd('"'), parsedOptions);
    }
}