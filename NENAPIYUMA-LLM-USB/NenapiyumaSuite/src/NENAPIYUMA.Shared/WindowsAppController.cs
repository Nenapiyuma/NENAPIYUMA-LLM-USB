using System.Diagnostics;

namespace NENAPIYUMA.Shared;

public sealed record AllowedDesktopApp(string Id, string DisplayName, string LaunchTarget, string[] ProcessNames, bool CanClose);

public static class WindowsAppController
{
    private static readonly AllowedDesktopApp[] Apps =
    {
        new("notepad", "Notepad", "notepad.exe", new[] { "notepad" }, true),
        new("calculator", "Calculator", "calc.exe", new[] { "CalculatorApp", "calc" }, true),
        new("paint", "Paint", "mspaint.exe", new[] { "mspaint" }, true),
        new("edge", "Microsoft Edge", "msedge.exe", new[] { "msedge" }, false),
        new("explorer", "File Explorer", "explorer.exe", new[] { "explorer" }, false)
    };

    public static IReadOnlyList<AllowedDesktopApp> GetAllowedApps() => Apps;

    public static AllowedDesktopApp? Find(string id) =>
        Apps.FirstOrDefault(app => app.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

    public static void Launch(string id)
    {
        var app = Find(id) ?? throw new InvalidOperationException("That app is not in the approved allow-list.");
        var started = Process.Start(new ProcessStartInfo
        {
            FileName = app.LaunchTarget,
            UseShellExecute = true,
            WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        });
        if (started is null)
            throw new InvalidOperationException($"Windows did not start {app.DisplayName}.");
        started.Dispose();
    }

    public static int RequestClose(string id)
    {
        var app = Find(id) ?? throw new InvalidOperationException("That app is not in the approved allow-list.");
        if (!app.CanClose)
            throw new InvalidOperationException($"{app.DisplayName} is launch-only in this build; closing it is disabled to avoid disrupting Windows.");

        var processes = app.ProcessNames
            .SelectMany(Process.GetProcessesByName)
            .Where(process =>
            {
                try { return !process.HasExited && process.MainWindowHandle != IntPtr.Zero; }
                catch (InvalidOperationException) { return false; }
            })
            .GroupBy(process => process.Id)
            .Select(group => group.First())
            .ToList();

        if (processes.Count == 0)
            return 0;

        var requested = 0;
        foreach (var process in processes)
        {
            try
            {
                if (!process.HasExited && process.CloseMainWindow())
                    requested++;
            }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); }
        }
        return requested;
    }
}
