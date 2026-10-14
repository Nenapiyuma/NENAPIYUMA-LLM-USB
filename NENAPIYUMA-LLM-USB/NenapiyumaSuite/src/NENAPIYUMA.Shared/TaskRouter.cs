namespace NENAPIYUMA.Shared;

public static class TaskRouter
{
    public static TaskRoute Route(string input)
    {
        var text = input.Trim().ToLowerInvariant();

        if (ContainsAny(text, "generate an image", "generate image", "draw me", "create an image", "image of", "make a picture", "picture of", "පින්තූරයක්", "රූපයක්", "චිත්‍රයක්", "ඡායාරූපයක්"))
            return new TaskRoute(TaskKind.ImageGeneration, "Image generation was requested.");

        if (ContainsAny(text, "generate audio", "generate music", "background music", "make a song", "text to speech", "audio file", "සංගීතය", "ගීතයක්", "ශ්‍රව්‍ය", "හඬ ගොනුව"))
            return new TaskRoute(TaskKind.AudioGeneration, "Audio generation was requested.");

        if (ContainsAny(text, "open notepad", "launch notepad", "start notepad", "open calculator", "launch calculator", "open calc", "විවෘත කරන්න", "අරින්න", "ආරම්භ කරන්න", "පරිගණකය පාලනය"))
            return new TaskRoute(TaskKind.ComputerControl, "Windows automation was requested.");

        if (ContainsAny(text, "write code", "write a program", "create a program", "debug", "function in ", "python", "javascript", "typescript", "c#", "c++", "sql", "powershell", "programming", "code for", "කෝඩ්", "කේතය", "ප්‍රෝග්‍රෑම්", "පයිතන්"))
            return new TaskRoute(TaskKind.Coding, "The user is asking for coding help. Provide accurate, runnable code when possible, mention assumptions, and include a brief explanation.");

        return new TaskRoute(TaskKind.Chat, "You are NENAPIYUMA, a helpful local assistant. Answer naturally in Sinhala when the user writes Sinhala and in English when the user writes English. Be honest about uncertainty and do not invent current facts.");
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}

public static class SafeCommandPolicy
{
    public static bool TryGetLaunchTarget(string request, out string executable)
    {
        executable = string.Empty;
        var text = request.ToLowerInvariant();

        var explicitLaunchIntent = ContainsAny(text, "open ", "open\t", "launch ", "start ", "run ", "විවෘත කරන්න", "අරින්න", "ආරම්භ කරන්න");
        if (!explicitLaunchIntent)
            return false;

        if (ContainsAny(text, "notepad", "නෝට්පෑඩ්", "නොට්පෑඩ්"))
        {
            executable = "notepad.exe";
            return true;
        }

        if (ContainsAny(text, "calculator", "calc", "කැල්කියුලේටරය", "ගණකය"))
        {
            executable = "calc.exe";
            return true;
        }

        return false;
    }

    public static bool IsAllowedExecutable(string executable) =>
        executable.Equals("notepad.exe", StringComparison.OrdinalIgnoreCase) ||
        executable.Equals("calc.exe", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));
}
