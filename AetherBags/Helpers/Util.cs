using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using AetherBags.Configuration;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace AetherBags.Helpers;

public static class Util
{
    private static readonly JsonSerializerOptions ConfigJsonOptions = new()
    {
        WriteIndented = true,
        IncludeFields = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string SerializeUIntSet(HashSet<uint> set)
        => string.Join(",", set.OrderBy(x => x));

    public static HashSet<uint> DeserializeUIntSet(string data)
        => data
            .Split([','], StringSplitOptions.RemoveEmptyEntries)
            .Select(s => uint.TryParse(s, out var val) ? val : (uint?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToHashSet();

    private static string CompressToBase64(string str)
        => Convert.ToBase64String(Dalamud.Utility.Util.CompressString(str));

    private static string DecompressFromBase64(string base64)
        => Dalamud.Utility.Util.DecompressString(Convert.FromBase64String(base64));

    public static string SerializeHashSet(HashSet<uint> hashSet)
        => CompressToBase64(SerializeUIntSet(hashSet));

    public static HashSet<uint> DeserializeHashSet(string input)
    {
        try
        {
            return DeserializeUIntSet(DecompressFromBase64(input));
        }
        catch
        {
            return new HashSet<uint>();
        }
    }

    public static string SerializeCompressed<T>(T value, JsonSerializerOptions? options = null)
    {
        var json = JsonSerializer.Serialize(value, options ?? ConfigJsonOptions);
        return CompressToBase64(json);
    }

    public static T? DeserializeCompressed<T>(string input, JsonSerializerOptions? options = null)
    {
        try
        {
            var json = DecompressFromBase64(input);
            return JsonSerializer.Deserialize<T>(json, options ?? ConfigJsonOptions);
        }
        catch
        {
            return default;
        }
    }

    public static string SerializeConfig(SystemConfiguration config)
        => SerializeCompressed(config, ConfigJsonOptions);

    public static SystemConfiguration? DeserializeConfig(string input)
    {
        try
        {
            var json = DecompressFromBase64(input);
            json = ConfigMigrator.Migrate(json, out _);
            var config = JsonSerializer.Deserialize<SystemConfiguration>(json, ConfigJsonOptions);
            config?.EnsureInitialized();
            return config;
        }
        catch
        {
            return default;
        }
    }

    public static void SaveConfig(SystemConfiguration config)
    {
        FileInfo file = JsonFileHelper.GetFileInfo(SystemConfiguration.FileName);
        JsonFileHelper.SaveFile(config, file.FullName);
        System.AetherBagsAPI?.API.RaiseConfigurationChanged();
    }

    private static SystemConfiguration LoadConfig()
    {
        FileInfo file = JsonFileHelper.GetFileInfo(SystemConfiguration.FileName);

        if (!file.Exists)
        {
            var newConfig = new SystemConfiguration();
            JsonFileHelper.SaveFile(newConfig, file.FullName);
            return newConfig;
        }

        SystemConfiguration config;
        try
        {
            var fileText = File.ReadAllText(file.FullName);
            var migratedText = ConfigMigrator.Migrate(fileText, out bool migrated);
            config = JsonSerializer.Deserialize<SystemConfiguration>(migratedText, ConfigJsonOptions) ?? new SystemConfiguration();
            if (migrated)
                JsonFileHelper.SaveFile(config, file.FullName);
        }
        catch (Exception e)
        {
            Services.Logger.Error(e, $"Error trying to load file {file.FullName}, creating a new one instead.");
            config = new SystemConfiguration();
            JsonFileHelper.SaveFile(config, file.FullName);
        }

        config.EnsureInitialized();
        return config;
    }

    public static SystemConfiguration LoadConfigOrDefault()
    {
        var config = LoadConfig();
        config.EnsureInitialized();
        return config;
    }

    public static SystemConfiguration ResetConfig()
        => new SystemConfiguration();

    public static bool LooksLikeRegex(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c is '.' or '*' or '+' or '?' or '[' or ']' or '(' or ')' or '\\' or '^' or '$' or '{' or '}' or '|')
                return true;
        }
        return false;
    }
}
