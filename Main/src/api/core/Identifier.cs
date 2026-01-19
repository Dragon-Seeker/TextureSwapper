using System;
using System.Text.RegularExpressions;
using io.wispforest.endec;
using io.wispforest.textureswapper.utils;

namespace io.wispforest.textureswapper.api.core;

public class Identifier {
    public static readonly Endec<Identifier> ENDEC = Endec.STRING.xmap(of, id => id.ToString());
    
    public readonly string Namespace;
    public readonly string Path;
    
    private Identifier(string ns, string path) {
        if (!isValidNamespace(ns)) throw new ArgumentException($"Identifier Namespace is not valid: {ns}");
        if (!IsValidPath(path)) throw new ArgumentException($"Identifier Path is not valid: {path}");
        
        Namespace = ns;
        Path = path;
    }

    public static Identifier of(string Namespace, string Path) => new (Namespace, Path);
    
    public static Identifier of(string value) {
        if (!isValidIdentifier(value)) throw new ArgumentException($"Invalid identifier value: {value}");
        
        var parts = value.Split(":");
        
        return new Identifier(parts[0], parts[1]);
    }

    public static Identifier ofDomain(string Namespace, string domain, string suffix = "") => of(Namespace, UriUtils.sanitizeName(domain) + suffix);

    public static Identifier ofUri(string uri, string unknownHostType = "unknown") {
        var domain = UriUtils.sanitizeName(UriUtils.getDomain(uri)) ?? unknownHostType;
        
        // TODO: FIND IF IT IS REQUIRED TO GET THE ABSOLUTE PATH TO DEAL WITH MORE ODD URLS INSTEAD OF FILE NAME GET
        //var path = UriUtils.sanitizeName(UriUtils.getURI(uri).AbsolutePath);
        
        var path = UriUtils.sanitizeName(System.IO.Path.GetFileNameWithoutExtension(uri));

        if (path is null) {
            throw new NullReferenceException($"Unable to handle the given uri to an ID: {uri}");
        }
        
        return of(domain, path);
    }

    public override bool Equals(object? obj) {
        if (obj is null) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj is not Identifier other) return false;
        return other.Namespace == Namespace && other.Path == Path;
    }
    
    public override int GetHashCode() => (Namespace, Path).GetHashCode();

    public override string ToString() => $"{Namespace}:{Path}";
    
    private static readonly Regex FULL_PATTERN = RegexUtils.parseRegexWithFlags("^[^:\\s]+:[^:\\s]+$");
    
    private static readonly Regex NAMESPACE_PATTERN = RegexUtils.parseRegexWithFlags("^[^:\\s]+$");
    private static readonly Regex PATH_PATTERN = RegexUtils.parseRegexWithFlags("^[^:\\s]+$");
    
    public static bool isValidIdentifier(string input) {
        return !string.IsNullOrEmpty(input) 
            ? FULL_PATTERN.IsMatch(input)
            : throw new NullReferenceException("A given input string could not be convert to a valid identifier as it was null or empty!");
    }
    
    public static bool isValidNamespace(string input) {
        return !string.IsNullOrEmpty(input) 
            ? NAMESPACE_PATTERN.IsMatch(input)
            : throw new NullReferenceException("A given input string could not be convert to a valid namespace as it was null or empty!");
    }
    
    public static bool IsValidPath(string input) {
        return !string.IsNullOrEmpty(input) 
            ? NAMESPACE_PATTERN.IsMatch(input)
            : throw new NullReferenceException("A given input string could not be convert to a valid path as it was null or empty!");
    }
}