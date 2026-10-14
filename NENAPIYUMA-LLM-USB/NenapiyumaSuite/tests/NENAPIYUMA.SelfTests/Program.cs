using NENAPIYUMA.Shared;

var failures = new List<string>();
var passed = 0;

void Check(bool condition, string name)
{
    if (condition)
    {
        Console.WriteLine($"[PASS] {name}");
        passed++;
    }
    else
    {
        Console.WriteLine($"[FAIL] {name}");
        failures.Add(name);
    }
}

var routeCases = new[]
{
    ("Explain photosynthesis", TaskKind.Chat),
    ("Write a Python program to add two numbers", TaskKind.Coding),
    ("Generate an image of a mountain", TaskKind.ImageGeneration),
    ("Generate background music", TaskKind.AudioGeneration),
    ("Open Notepad", TaskKind.ComputerControl)
};
foreach (var (input, expected) in routeCases)
    Check(TaskRouter.Route(input).Kind == expected, $"task routing: {input}");

Check(SafeCommandPolicy.TryGetLaunchTarget("Open Notepad", out var notepad) && notepad == "notepad.exe",
    "allow-listed Notepad launch");
Check(SafeCommandPolicy.TryGetLaunchTarget("Open Calculator", out var calculator) && calculator == "calc.exe",
    "allow-listed Calculator launch");
Check(!SafeCommandPolicy.TryGetLaunchTarget("Open PowerShell and run a command", out _),
    "arbitrary shell is blocked");
Check(!SafeCommandPolicy.IsAllowedExecutable("cmd.exe"), "cmd.exe is not allow-listed");

using (var cancelledClient = new LlamaServerClient())
using (var cancelledSource = new CancellationTokenSource())
{
    cancelledSource.Cancel();
    try
    {
        await cancelledClient.CompleteAsync(Array.Empty<ChatMessage>(), "test", 100, 0.7, cancelledSource.Token);
        Check(false, "generation cancellation");
    }
    catch (OperationCanceledException)
    {
        Check(true, "generation cancellation");
    }
}

using (var unreadyClient = new LlamaServerClient())
{
    try
    {
        await unreadyClient.CompleteAsync(Array.Empty<ChatMessage>(), "test", 100, 0.7, CancellationToken.None);
        Check(false, "missing server returns a useful error");
    }
    catch (InvalidOperationException)
    {
        Check(true, "missing server returns a useful error");
    }
}

var temp = Path.Combine(Path.GetTempPath(), "nenapiyuma-selftest-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    var emptyDiscovery = ModelLocator.Discover(temp, "models", "tools");
    Check(!emptyDiscovery.HasModel && !emptyDiscovery.CanRun, "missing model is detected without crashing");

    var models = Path.Combine(temp, "models");
    Directory.CreateDirectory(models);
    var smallModel = Path.Combine(models, "incomplete.gguf");
    File.WriteAllText(smallModel, "not a model");
    Check(!ModelLocator.IsValidModelFile(smallModel), "short/incomplete GGUF is rejected");

    var validModel = Path.Combine(models, "Qwen2.5-3B-Instruct-Q4_K_M.gguf");
    using (var stream = new FileStream(validModel, FileMode.CreateNew, FileAccess.Write))
        stream.SetLength(ModelLocator.MinimumModelBytes + 100);
    Check(ModelLocator.IsValidModelFile(validModel), "large GGUF file passes minimum size validation");

    var tools = Path.Combine(temp, "tools");
    Directory.CreateDirectory(tools);
    File.WriteAllText(Path.Combine(tools, "llama-server.exe"), "test runtime marker");
    var discovered = ModelLocator.Discover(temp, "models", "tools");
    Check(discovered.HasModel && discovered.HasRuntime && discovered.CanRun, "model/runtime paths are discovered automatically");

    var historyPath = Path.Combine(temp, "data", "chats.json");
    var store = new ConversationStore(historyPath);
    var messages = new List<ChatMessage>
    {
        new("user", "ආයුබෝවන්", DateTimeOffset.UtcNow),
        new("assistant", "ඔබට උදව් කළ හැක.", DateTimeOffset.UtcNow)
    };
    store.Save(messages);
    Check(store.Load().Count == 2, "conversation history round trip");
    Check(store.Search("උදව්").Count == 1, "conversation history search");
    var backupPath = Path.Combine(temp, "backup.json");
    store.ExportTo(backupPath);
    Check(ConversationStore.ReadBackup(backupPath).Count == 2, "chat backup export/import parsing");

    var settings = new SuiteSettings { ChatPort = 8080, StudioPort = 8081, ContextSize = 4096 };
    var configRoot = Path.Combine(temp, "settings");
    settings.Save(configRoot);
    var loadedSettings = SuiteSettings.Load(configRoot);
    Check(loadedSettings.ChatPort == 8080 && loadedSettings.StudioPort == 8081,
        "settings storage round trip");
}
catch (Exception ex)
{
    failures.Add("unexpected test exception: " + ex);
    Console.WriteLine("[FAIL] unexpected test exception: " + ex.Message);
}
finally
{
    try { Directory.Delete(temp, recursive: true); } catch { }
}

Console.WriteLine();
Console.WriteLine($"Tests: {passed} passed, {failures.Count} failed.");
if (failures.Count > 0)
{
    foreach (var failure in failures) Console.WriteLine(" - " + failure);
    Environment.ExitCode = 1;
}
