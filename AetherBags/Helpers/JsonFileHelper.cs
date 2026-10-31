using Dalamud.Plugin.Services;
using System;
using System.IO;
using System.Text.Json;
using Dalamud.Utility;

namespace AetherBags.Helpers;

public static class JsonFileHelper {
    private static readonly JsonSerializerOptions SerializerOptions = new() {
        WriteIndented = true,
        IncludeFields = true,
    };

    public static T LoadFile<T>(string filePath, Func<string, T?>? deserialize = null) where T : new() {
        string fileText;
        try {
            fileText = File.ReadAllText(filePath);
        }
        catch (FileNotFoundException) {
            var newFile = new T();
            WriteFile(newFile, filePath);
            return newFile;
        }

        try {
            return (deserialize is null
                ? JsonSerializer.Deserialize<T>(fileText, SerializerOptions)
                : deserialize(fileText)) ?? throw new JsonException("The configuration contains null.");
        }
        catch (Exception e) {
            var backupPath = $"{filePath}.invalid-{Guid.NewGuid():N}";
            File.Copy(filePath, backupPath);
            IPluginLog.Get().Error(e, $"Error loading {filePath}. Original file preserved at {backupPath}; creating defaults.");
        }

        var replacement = new T();
        WriteFile(replacement, filePath);
        return replacement;
    }

    public static void SaveFile<T>(T? file, string filePath) {
        try {
            if (file is null) {
                IPluginLog.Get().Error("Null file provided.");
                return;
            }

            WriteFile(file, filePath);
        }
        catch (Exception e) {
            IPluginLog.Get().Error(e, $"Error trying to save file {filePath}");
        }
    }

    private static void WriteFile<T>(T file, string filePath) {
        var fileText = JsonSerializer.Serialize(file, file!.GetType(), SerializerOptions);
        FilesystemUtil.WriteAllTextSafe(filePath, fileText);
    }

    public static FileInfo GetFileInfo(params string[] path) {
        var directory = Plugin.PluginInterface.ConfigDirectory;

        for (var index = 0; index < path.Length - 1; index++) {
            directory = new DirectoryInfo(Path.Combine(directory.FullName, path[index]));
            if (!directory.Exists) {
                directory.Create();
            }
        }

        return new FileInfo(Path.Combine(directory.FullName, path[^1]));
    }
}
