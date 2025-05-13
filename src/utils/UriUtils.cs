using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Sirenix.Utilities;

namespace io.wispforest.textureswapper.utils;

public class UriUtils {
    public static Uri? getURI(string url) {
        if (!string.IsNullOrEmpty(url)) {
            try {
                return new Uri(url);
            } catch (UriFormatException) {
                if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                    try {
                        return new Uri("https://" + url);
                    } catch(UriFormatException) {}
                }
            }
        }

        return null;
    }
    
    public static string? getDomain(string url) {
        return getURI(url)?.Host;
    }
    
    public static string? sanitizeName(string? hostname) {
        if (string.IsNullOrEmpty(hostname)) return null;

        // Remove invalid characters and replace with underscores
        string sanitizedName = Regex.Replace(hostname, @"[^a-zA-Z0-9_\-\.]", "_");

        // Truncate if too long (adjust length as needed)
        int maxLength = 255; // Example max length
        if (sanitizedName.Length > maxLength) sanitizedName = sanitizedName.Substring(0, maxLength);

        // Avoid reserved names (Windows example)
        string[] reservedNames = { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
        if (Array.IndexOf(reservedNames, sanitizedName.ToUpper()) >= 0) {
            sanitizedName = "_" + sanitizedName; // Add an underscore to avoid conflict
        }

        return sanitizedName;
    }
}