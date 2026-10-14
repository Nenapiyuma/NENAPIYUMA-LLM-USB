using System.Text.Json;

namespace NENAPIYUMA.Shared;

public sealed class SuiteSettings
{
    public string ModelDirectory { get; set; } = "models";
    public string RuntimeDirectory { get; set; } = "tools";
    public int ChatPort { get; set; } = 8080;
    public int StudioPort { get; set; } = 8081;
    public int ContextSize { get; set; } = 4096;
    public bool AllowComputerAutomation { get; set; } = false;
    public string ImageGenerationEndpoint { get; set; } = "http://127.0.0.1:8188";

    public static SuiteSettings Load(string appDirectory)
    {
        var path = Path.Combine(appDirectory, "config", "appsettings.json");
        if (!File.Exists(path))
            return new SuiteSettings();

        try
        {
            var settings = JsonSerializer.Deserialize<SuiteSettings>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new SuiteSettings();

            if (settings.ChatPort is < 1024 or > 65535) settings.ChatPort = 8080;
            if (settings.StudioPort is < 1024 or > 65535) settings.StudioPort = 8081;
            if (settings.ContextSize is < 512 or > 32768) settings.ContextSize = 4096;
            settings.ModelDirectory = string.IsNullOrWhiteSpace(settings.ModelDirectory) ? "models" : settings.ModelDirectory;
            settings.RuntimeDirectory = string.IsNullOrWhiteSpace(settings.RuntimeDirectory) ? "tools" : settings.RuntimeDirectory;
            settings.ImageGenerationEndpoint = string.IsNullOrWhiteSpace(settings.ImageGenerationEndpoint)
                ? "http://127.0.0.1:8188"
                : settings.ImageGenerationEndpoint.TrimEnd('/');
            return settings;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return new SuiteSettings();
        }
    }

    public void Save(string appDirectory)
    {
        var directory = Path.Combine(appDirectory, "config");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "appsettings.json");
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(tempPath, path, true);
    }
}
