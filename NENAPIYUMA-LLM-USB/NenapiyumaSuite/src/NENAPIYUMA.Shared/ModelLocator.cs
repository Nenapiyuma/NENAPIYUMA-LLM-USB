namespace NENAPIYUMA.Shared;

public static class ModelLocator
{
    public const long MinimumModelBytes = 10_000_000;

    public static ModelDiscovery Discover(string appDirectory, string? configuredModelDirectory = "models", string? configuredRuntimeDirectory = "tools")
    {
        var fullAppDirectory = Path.GetFullPath(appDirectory);
        var modelDirectories = CandidateDirectories(fullAppDirectory, configuredModelDirectory, "models");
        var runtimeDirectories = CandidateDirectories(fullAppDirectory, configuredRuntimeDirectory, "tools");

        string? modelPath = null;
        foreach (var directory in modelDirectories)
        {
            try
            {
                if (!Directory.Exists(directory))
                    continue;

                var models = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                    .Where(path => Path.GetExtension(path).Equals(".gguf", StringComparison.OrdinalIgnoreCase))
                    .Where(IsValidModelFile)
                    .ToList();

                if (models.Count == 0)
                    continue;

                modelPath = models
                    .OrderBy(path => Priority(Path.GetFileName(path)))
                    .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .First();
                break;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Continue scanning other known model locations.
            }
        }

        string? runtimePath = null;
        foreach (var directory in runtimeDirectories)
        {
            var candidate = Path.Combine(directory, "llama-server.exe");
            try
            {
                if (File.Exists(candidate) && new FileInfo(candidate).Length > 0)
                {
                    runtimePath = candidate;
                    break;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Continue scanning other known runtime locations.
            }
        }

        return new ModelDiscovery(modelPath, runtimePath);
    }

    public static bool IsValidModelFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.GetExtension(path).Equals(".gguf", StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            return File.Exists(path) && new FileInfo(path).Length >= MinimumModelBytes;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return false;
        }
    }

    private static int Priority(string fileName)
    {
        if (fileName.Equals("helalm-4b.Q8_0.gguf", StringComparison.OrdinalIgnoreCase)) return 0;
        if (fileName.Contains("Qwen2.5-3B", StringComparison.OrdinalIgnoreCase)) return 1;
        if (fileName.Contains("Qwen2.5-1.5B", StringComparison.OrdinalIgnoreCase)) return 2;
        return 10;
    }

    private static IEnumerable<string> CandidateDirectories(string appDirectory, string? configuredPath, string defaultFolderName)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            var configured = Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.GetFullPath(Path.Combine(appDirectory, configuredPath));
            if (Directory.Exists(configured) && seen.Add(configured))
                yield return configured;
        }

        var current = new DirectoryInfo(appDirectory);
        for (var depth = 0; current is not null && depth < 5; depth++, current = current.Parent)
        {
            var candidates = new[]
            {
                Path.Combine(current.FullName, defaultFolderName),
                Path.Combine(current.FullName, "NENAPIYUMA-Chat", defaultFolderName),
                Path.Combine(current.FullName, "NENAPIYUMA-Studio", defaultFolderName),
                Path.Combine(current.FullName, "NENAPIYUMA-LLM-USB", defaultFolderName)
            };

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate) && seen.Add(candidate))
                    yield return candidate;
            }
        }
    }
}
