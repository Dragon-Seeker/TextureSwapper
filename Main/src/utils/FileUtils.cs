using System;
using System.IO;
using System.Threading.Tasks;
using io.wispforest.textureswapper.api;

namespace io.wispforest.textureswapper.utils;

public class FileUtils {
    
    public static string? getParentDirectory(string path, int backTrackAmount = 1) {
        var dirName = path;

        for (int i = 0; i < backTrackAmount; i++) {
            dirName = Path.GetDirectoryName(dirName);
        }
        
        return Path.GetFileName(dirName);
    }
    
    public static void createFileFromBytes(byte[] data, string filePath) {
        if (data.Length == 0 || string.IsNullOrEmpty(filePath)) return;
        
        try {
            var directoryPath = Path.GetDirectoryName(filePath);
            
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllBytes(filePath, data);
        } catch (Exception ex) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to create file for the given path: {filePath}"));
        }
    }

    public static async Task<byte[]?> loadDataFromFile(FileLookupHelper helper) {
        return await loadDataFromFile(helper.primaryPattern, helper.refreshFileAccess) ?? await loadDataFromFile(helper.secondaryPattern, helper.refreshFileAccess);
    }

    public static async Task<byte[]?> loadDataFromFile(FilePattern pattern, bool refreshFileAccess = false) {
        return await loadDataFromFile(pattern.directory, pattern.filePattern, refreshFileAccess);
    }

    public static async Task<byte[]?> loadDataFromFile(string directory, string filePattern, bool refreshFileAccess = false) {
        if (!string.IsNullOrEmpty(directory) && !string.IsNullOrEmpty(filePattern) && Directory.Exists(directory)) {
            try {
                var files = Directory.GetFiles(directory, filePattern);

                if (files.Length > 0) {
                    var filePath = files[0];

                    var result =  await File.ReadAllBytesAsync(filePath);
                    
                    if (refreshFileAccess) File.SetLastAccessTime(filePath, DateTime.Now);

                    return result;
                }
            } catch (Exception ex) {
                Plugin.logIfDebugging(source => source.LogError($"Unable to read file for the given path [{Path.Combine(directory, filePattern)}]: {ex}"));
            }
        }

        return null;
    }

    public static void deleteOldFiles(string folderPath, int daysOld) {
        if (!Directory.Exists(folderPath)) return;
        
        foreach (var directory in Directory.GetDirectories(folderPath)) {
            deleteOldFiles(directory, daysOld);
        }

        try {
            var cutoffDate = DateTime.Now.AddDays(-daysOld);
            var files = Directory.GetFiles(folderPath);

            foreach (var filePath in files) {
                var fileInfo = new FileInfo(filePath);
                
                if (fileInfo.LastAccessTime >= cutoffDate) continue;
                
                try {
                    File.Delete(filePath);
                    Plugin.logIfDebugging(source => source.LogInfo($"Deleted the given old cached file: {filePath}"));
                } catch (Exception deleteEx) {
                    Plugin.logIfDebugging(source => source.LogError($"Unable to delete the given old cached file {filePath}: {deleteEx}"));
                }
            }
        } catch (Exception ex) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to process the given temp directory for deletion {folderPath}: {ex}"));
        }
    }
}

public class FilePattern(string directory, string filePattern) {
    public string directory { get; } = directory;
    public string filePattern { get; } = filePattern;
}

public class FileLookupHelper(string directory, string name, string filePattern, bool refreshFileAccess = false) {
    public FilePattern primaryPattern => new(directory, filePattern);
    
    public FilePattern secondaryPattern => new(directory, $"{name}.*");

    public string filePath => Path.Combine(directory, filePattern);

    public bool refreshFileAccess { get; } = refreshFileAccess;
}