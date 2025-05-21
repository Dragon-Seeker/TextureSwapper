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
        
        var name = Path.GetFileName(dirName);
        
        return name;
    }
    
    public static void createFileFromBytes(byte[] data, string filePath) {
        if (data.Length == 0 || string.IsNullOrEmpty(filePath)) return;
        
        try {
            string directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath)) {
                Directory.CreateDirectory(directoryPath);
            }

            File.WriteAllBytes(filePath, data);
        } catch (Exception ex) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to create file for the given path: {filePath}"));
        }
    }

    public static async Task<byte[]?> loadDataFromFile(FileLookupHelper helper) {
        var value = await loadDataFromFile(helper.getPrimaryPattern());

        if (value is null) {
            value = await loadDataFromFile(helper.getSecoundaryPattern());
        }

        return value;
    }

    public static async Task<byte[]?> loadDataFromFile((string directory, string filePattern) tuple) {
        return await loadDataFromFile(tuple.directory, tuple.filePattern);
    }

    public static async Task<byte[]?> loadDataFromFile(string directory, string filePattern) {
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory) && !string.IsNullOrEmpty(filePattern) ) {
            try {
                var files = Directory.GetFiles(directory, filePattern);

                if (files.Length > 0) {
                    var filePath = files[0];

                    return await File.ReadAllBytesAsync(filePath);
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

            foreach (string filePath in files) {
                var fileInfo = new FileInfo(filePath);
                
                if (fileInfo.LastAccessTime >= cutoffDate) continue;
                
                try {
                    File.Delete(filePath);
                    Plugin.logIfDebugging(source => source.LogError($"Deleted the given old cached file: {filePath}"));
                } catch (Exception deleteEx) {
                    Plugin.logIfDebugging(source => source.LogError($"Unable to delete the given old cached file {filePath}: {deleteEx}"));
                }
            }
        } catch (Exception ex) {
            Plugin.logIfDebugging(source => source.LogError($"Unable to process the given temp directory for deletion {folderPath}: {ex}"));
        }
    }
}

public class FileLookupHelper(string directory, string name, string filePattern) {
    public (string directory, string filePattern) getPrimaryPattern() {
        return new(directory, filePattern);
    }
    
    public (string directory, string filePattern) getSecoundaryPattern() {
        return new(directory, $"{name}.*");
    }

    public string getFilePath() {
        return Path.Combine(directory, filePattern);
    }
}