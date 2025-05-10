using System;
using System.Text.RegularExpressions;
using io.wispforest;
using io.wispforest.impl;

namespace io.wispforest.textureswapper.utils;

public class Identifier {
    public static readonly Endec<Identifier> ENDEC = Endecs.STRING.xmap(
        s => Identifier.of(s), 
        identifier => identifier.toStringFormat());
    
    public readonly string Namespace;
    public readonly string Path;
    
    private Identifier(string ns, string path) {
        if (!IsValidNamespace(ns)) throw new ArgumentException($"Identifier Namespace is not valid: {ns}");
        if (!IsValidPath(path)) throw new ArgumentException($"Identifier Path is not valid: {path}");
        
        Namespace = ns;
        Path = path;
    }

    public static Identifier of(string Namespace, string Path) {
        return new Identifier(Namespace, Path);
    }
    
    public static Identifier of(string value) {
        if (!IsValidIdentifier(value)) {
            throw new ArgumentException($"Invalid identifier value: {value}");
        }
        
        var parts = value.Split(":");
        
        return new Identifier(parts[0], parts[1]);
    }

    public static Identifier ofUri(string uri) {
        var domain = UriUtils.getDomain(uri) ?? "local";
        
        var path = UriUtils.sanitizeName(System.IO.Path.GetFileNameWithoutExtension(uri));

        if (path is null) {
            throw new NullReferenceException($"Unable to handle the given uri to an ID: {uri}");
        }
        
        return of(domain, path);
    }
    
    public string toStringFormat() {
        return Namespace + ":" + Path;
    }

    public override bool Equals(object? obj) {
        if (this == obj) return true;
        if (obj is Identifier otherId) {
            return otherId.Namespace == Namespace && otherId.Path == Path;
        }

        return false;
    }
    
    public override int GetHashCode() {
        return (Namespace, Path).GetHashCode();
    }

    public override string ToString() {
        return $"Identifier({toStringFormat()})";
    }
    
    public static bool IsValidIdentifier(string input) {
        if (string.IsNullOrEmpty(input)) { // Empty or null strings are not valid.
            throw new NullReferenceException("A given string is null and cannot be converted to an Identifier");
        }

        string pattern = "^[^:\\s]+:[^:\\s]+$";
        return Regex.IsMatch(input, pattern);
    }
    
    public static bool IsValidNamespace(string input) {
        if (string.IsNullOrEmpty(input)) { // Empty or null strings are not valid.
            throw new NullReferenceException("A given string is null and is not a valid Namespace");
        }

        string pattern = "^[^:\\s]+$";
        return Regex.IsMatch(input, pattern);
    }
    
    public static bool IsValidPath(string input) {
        if (string.IsNullOrEmpty(input)) { // Empty or null strings are not valid.
            throw new NullReferenceException("A given string is null and is not a valid Path");
        }

        string pattern = "^[^:\\s]+$";
        return Regex.IsMatch(input, pattern);
    }
}