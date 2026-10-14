using System.Text.Json;

namespace NENAPIYUMA.Shared;

public sealed class ConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string FilePath { get; }

    public ConversationStore(string filePath)
    {
        FilePath = Path.GetFullPath(filePath);
    }

    public List<ChatMessage> Load()
    {
        if (!File.Exists(FilePath))
            return new List<ChatMessage>();

        var content = File.ReadAllText(FilePath);
        return JsonSerializer.Deserialize<List<ChatMessage>>(content, JsonOptions) ?? new List<ChatMessage>();
    }

    public void Save(IEnumerable<ChatMessage> messages)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("The chat history path has no parent directory.");

        Directory.CreateDirectory(directory);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(messages.ToList(), JsonOptions));
        File.Move(temporary, FilePath, true);
    }

    public List<ChatMessage> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Load();

        return Load()
            .Where(message => message.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void ExportTo(string destinationPath)
    {
        var destination = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(destination, JsonSerializer.Serialize(Load(), JsonOptions));
    }

    public static List<ChatMessage> ReadBackup(string sourcePath)
    {
        var content = File.ReadAllText(sourcePath);
        var result = JsonSerializer.Deserialize<List<ChatMessage>>(content, JsonOptions);
        if (result is null)
            throw new InvalidDataException("This file does not contain a valid NENAPIYUMA chat backup.");
        if (result.Any(message => message.Role is not ("user" or "assistant" or "system")))
            throw new InvalidDataException("The backup contains an unsupported message role.");
        return result;
    }
}
