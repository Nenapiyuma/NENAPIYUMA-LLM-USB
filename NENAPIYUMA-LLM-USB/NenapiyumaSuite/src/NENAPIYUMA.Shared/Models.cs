namespace NENAPIYUMA.Shared;

public sealed record ChatMessage(string Role, string Content, DateTimeOffset Timestamp);

public enum TaskKind
{
    Chat,
    Coding,
    ImageGeneration,
    AudioGeneration,
    ComputerControl
}

public sealed record TaskRoute(TaskKind Kind, string SystemInstruction)
{
    public bool RequiresLocalLanguageModel => Kind is TaskKind.Chat or TaskKind.Coding;
}

public sealed record ModelDiscovery(string? ModelPath, string? RuntimePath)
{
    public bool HasModel => ModelLocator.IsValidModelFile(ModelPath);
    public bool HasRuntime => !string.IsNullOrWhiteSpace(RuntimePath) && File.Exists(RuntimePath);
    public bool CanRun => HasModel && HasRuntime;

    public string StatusMessage
    {
        get
        {
            if (!HasModel)
                return "Local GGUF model missing or incomplete. Place a compatible .gguf file in models\\.";
            if (!HasRuntime)
                return "llama-server.exe is missing. Add a Windows x64 llama.cpp runtime and its required DLL files in tools\\.";
            return "Local model and llama.cpp runtime found. Ready to start.";
        }
    }
}
