using NENAPIYUMA.Shared;

namespace NENAPIYUMA.Chat;

internal static class FeatureTools
{
    public static async Task GenerateImageAsync(IWin32Window owner, SuiteSettings settings, Action<string> status, string? initialPrompt = null)
    {
        var prompt = PromptDialog.Show(owner, "Create an image", "Describe the image to generate. A local ComfyUI service and compatible checkpoint are required.", string.IsNullOrWhiteSpace(initialPrompt) ? "A peaceful Sri Lankan tea estate at sunrise, detailed landscape art" : initialPrompt);
        if (string.IsNullOrWhiteSpace(prompt)) return;
        using var save = new SaveFileDialog
        {
            Title = "Save generated image",
            Filter = "PNG image (*.png)|*.png",
            FileName = "NENAPIYUMA-image.png",
            AddExtension = true,
            DefaultExt = "png"
        };
        if (save.ShowDialog(owner) != DialogResult.OK) return;

        status("Generating image locally…");
        try
        {
            var generator = new ImageGenerationService(settings.ImageGenerationEndpoint);
            var path = await generator.GenerateAsync(prompt, save.FileName);
            status("Image generated and saved.");
            MessageBox.Show(owner, "Image generated successfully:\n" + path, "NENAPIYUMA Image Generator", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            status("Image generation failed.");
            MessageBox.Show(owner, "No image was saved.\n\n" + ex.Message + "\n\nStart ComfyUI on this PC (default: http://127.0.0.1:8188) and install a compatible checkpoint.", "Image generation unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public static async Task GenerateAudioAsync(IWin32Window owner, Action<string> status, string? initialPrompt = null)
    {
        var result = PromptDialog.ShowWithDuration(owner, "Create audio", "Describe a short musical idea. This is an offline procedural synthesizer, not a neural AI audio model.", string.IsNullOrWhiteSpace(initialPrompt) ? "Calm ambient music, soft piano, slow 72 BPM" : initialPrompt);
        if (result is null || string.IsNullOrWhiteSpace(result.Value.Prompt)) return;
        using var save = new SaveFileDialog
        {
            Title = "Save generated audio",
            Filter = "WAV audio (*.wav)|*.wav",
            FileName = "NENAPIYUMA-audio.wav",
            AddExtension = true,
            DefaultExt = "wav"
        };
        if (save.ShowDialog(owner) != DialogResult.OK) return;

        status("Creating audio offline…");
        try
        {
            var path = await AudioGenerationService.GenerateWavAsync(result.Value.Prompt, save.FileName, result.Value.DurationSeconds);
            status("Audio created and saved.");
            MessageBox.Show(owner, "WAV audio created successfully:\n" + path + "\n\nNote: this is procedural synthesized music/sound, not neural text-to-audio generation.", "NENAPIYUMA Audio Generator", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            status("Audio generation failed.");
            MessageBox.Show(owner, "Audio file was not created.\n\n" + ex.Message, "Audio generation failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static void ShowComputerControl(IWin32Window owner, bool enabled, Action<string, string> audit, Action<string> status)
    {
        if (!enabled)
        {
            MessageBox.Show(owner, "Computer control is OFF. Enable the computer-control checkbox first. Each action still requires a separate confirmation.", "Computer control disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            status("Computer control disabled");
            return;
        }

        using var dialog = new Form
        {
            Text = "Permission-gated PC control",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MinimizeBox = false,
            MaximizeBox = false,
            ClientSize = new Size(440, 220),
            BackColor = Color.FromArgb(20, 27, 43),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };
        var appLabel = new Label { Text = "Allowed app", AutoSize = true, Location = new Point(18, 18) };
        var appPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(18, 42), Width = 400 };
        var apps = WindowsAppController.GetAllowedApps().ToArray();
        appPicker.Items.AddRange(apps.Cast<object>().ToArray());
        appPicker.DisplayMember = nameof(AllowedDesktopApp.DisplayName);
        appPicker.SelectedIndex = 0;
        var actionLabel = new Label { Text = "Action", AutoSize = true, Location = new Point(18, 82) };
        var actionPicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(18, 106), Width = 400 };
        actionPicker.Items.AddRange(new object[] { "Open / launch", "Close gracefully" });
        actionPicker.SelectedIndex = 0;
        void UpdateActions()
        {
            actionPicker.Items.Clear();
            actionPicker.Items.Add("Open / launch");
            var selected = appPicker.SelectedItem as AllowedDesktopApp;
            actionPicker.Items.Add(selected?.CanClose == true ? "Close gracefully" : "Close not allowed for this app");
            actionPicker.SelectedIndex = 0;
        }
        appPicker.SelectedIndexChanged += (_, _) => UpdateActions();
        var approve = new Button { Text = "Continue to confirmation…", Location = new Point(214, 162), Width = 204, Height = 36, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Cancel", Location = new Point(18, 162), Width = 110, Height = 36, DialogResult = DialogResult.Cancel };
        dialog.Controls.AddRange(new Control[] { appLabel, appPicker, actionLabel, actionPicker, approve, cancel });
        dialog.AcceptButton = approve;
        dialog.CancelButton = cancel;
        if (dialog.ShowDialog(owner) != DialogResult.OK || appPicker.SelectedItem is not AllowedDesktopApp app) return;

        var closing = actionPicker.SelectedIndex == 1;
        if (closing && !app.CanClose)
        {
            MessageBox.Show(owner, "This app is launch-only in this build; closing it is disabled to avoid disrupting Windows.", "Action not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var warning = closing
            ? $"Request Windows to close {app.DisplayName}?\n\nSave your work first. Windows may show its normal unsaved-work prompt. NENAPIYUMA will not force-kill the app."
            : $"Allow NENAPIYUMA to launch {app.DisplayName}?\n\nOnly this allow-listed application will be launched; no shell command will run.";
        if (MessageBox.Show(owner, warning, "Confirm PC action", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            audit("Denied", app.DisplayName + (closing ? " close" : " launch"));
            status("PC action cancelled");
            return;
        }

        try
        {
            if (closing)
            {
                var count = WindowsAppController.RequestClose(app.Id);
                var message = count > 0
                    ? $"Windows received a graceful close request for {count} {app.DisplayName} window(s). Check the app for any save prompt."
                    : $"No visible {app.DisplayName} window was found to close.";
                MessageBox.Show(owner, message, "PC control result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                audit(count > 0 ? "Close requested" : "No window found", app.DisplayName);
                status(count > 0 ? "Graceful close requested" : "No visible window found");
            }
            else
            {
                WindowsAppController.Launch(app.Id);
                MessageBox.Show(owner, app.DisplayName + " was launched.", "PC control result", MessageBoxButtons.OK, MessageBoxIcon.Information);
                audit("Launched", app.DisplayName);
                status("Allowed app launched");
            }
        }
        catch (Exception ex)
        {
            audit("Failed", app.DisplayName + " · " + ex.Message);
            status("PC action failed");
            MessageBox.Show(owner, ex.Message, "PC control failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public static bool TryParseControlRequest(string request, out string appId, out bool closing)
    {
        var text = request.ToLowerInvariant();
        closing = ContainsAny(text, "close ", "quit ", "exit ", "close the ", "වහන්න", "වසා දමන්න", "වහලා දාන්න");
        var launching = ContainsAny(text, "open ", "launch ", "start ", "run ", "විවෘත කරන්න", "අරින්න", "ආරම්භ කරන්න");
        appId = string.Empty;
        if (!closing && !launching) return false;

        if (ContainsAny(text, "notepad", "නෝට්පෑඩ්", "නොට්පෑඩ්")) appId = "notepad";
        else if (ContainsAny(text, "calculator", "calc", "කැල්කියුලේටරය", "ගණකය")) appId = "calculator";
        else if (ContainsAny(text, "paint", "පේන්ට්")) appId = "paint";
        else if (ContainsAny(text, "edge", "browser", "බ්‍රවුසරය")) appId = "edge";
        else if (ContainsAny(text, "file explorer", "explorer", "ගොනු ගවේෂකය")) appId = "explorer";
        else return false;

        return true;
    }

    public static void HandleControlRequest(IWin32Window owner, string request, bool enabled, Action<string, string> audit, Action<string> status)
    {
        if (!enabled)
        {
            MessageBox.Show(owner, "Computer control is OFF. Enable the computer-control checkbox first. No action was performed.", "Computer control disabled", MessageBoxButtons.OK, MessageBoxIcon.Information);
            status("Computer control disabled");
            return;
        }
        if (!TryParseControlRequest(request, out var appId, out var closing) || WindowsAppController.Find(appId) is not { } app)
        {
            MessageBox.Show(owner, "I could not match that request to the safe allow-list. Use the PC Control button to select an allowed app and action.", "Unsupported PC action", MessageBoxButtons.OK, MessageBoxIcon.Information);
            status("Unsupported PC action");
            return;
        }
        if (closing && !app.CanClose)
        {
            MessageBox.Show(owner, app.DisplayName + " is launch-only in this build. No action was performed.", "Action not available", MessageBoxButtons.OK, MessageBoxIcon.Information);
            status("Close action blocked");
            return;
        }
        var text = closing
            ? $"Request Windows to close {app.DisplayName}? Save your work first. Windows may show its normal save prompt; the app will not be force-killed."
            : $"Allow NENAPIYUMA to launch {app.DisplayName}? Only this allow-listed application will be launched.";
        if (MessageBox.Show(owner, text, "Confirm PC action", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            audit("Denied", app.DisplayName);
            status("PC action cancelled");
            return;
        }
        try
        {
            if (closing)
            {
                var count = WindowsAppController.RequestClose(app.Id);
                audit(count > 0 ? "Close requested" : "No window found", app.DisplayName);
                status(count > 0 ? "Graceful close requested" : "No visible window found");
                MessageBox.Show(owner, count > 0 ? $"Windows received a close request for {count} window(s)." : "No visible app window was found.", "PC control result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                WindowsAppController.Launch(app.Id);
                audit("Launched", app.DisplayName);
                status("Allowed app launched");
                MessageBox.Show(owner, app.DisplayName + " was launched.", "PC control result", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            audit("Failed", app.DisplayName + " · " + ex.Message);
            status("PC action failed");
            MessageBox.Show(owner, ex.Message, "PC control failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool ContainsAny(string text, params string[] terms) =>
        terms.Any(term => text.Contains(term, StringComparison.OrdinalIgnoreCase));

    private sealed class PromptDialog : Form
    {
        private readonly TextBox _prompt = new();
        private readonly NumericUpDown? _duration;
        private PromptDialog(string title, string description, string initial, bool withDuration)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(560, withDuration ? 300 : 250);
            BackColor = Color.FromArgb(20, 27, 43);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F);
            var info = new Label { Text = description, Location = new Point(16, 14), Size = new Size(526, 46) };
            _prompt.Multiline = true;
            _prompt.AcceptsReturn = true;
            _prompt.ScrollBars = ScrollBars.Vertical;
            _prompt.Text = initial;
            _prompt.Location = new Point(16, 66);
            _prompt.Size = new Size(526, withDuration ? 118 : 128);
            _prompt.BackColor = Color.FromArgb(30, 39, 58);
            _prompt.ForeColor = Color.White;
            Controls.Add(info);
            Controls.Add(_prompt);
            if (withDuration)
            {
                Controls.Add(new Label { Text = "Duration (seconds)", AutoSize = true, Location = new Point(16, 198) });
                _duration = new NumericUpDown { Minimum = 5, Maximum = 60, Value = 20, Location = new Point(150, 194), Width = 80 };
                Controls.Add(_duration);
            }
            var ok = new Button { Text = "Continue", DialogResult = DialogResult.OK, Location = new Point(342, withDuration ? 244 : 205), Width = 94 };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(448, withDuration ? 244 : 205), Width = 94 };
            Controls.Add(ok);
            Controls.Add(cancel);
            AcceptButton = ok;
            CancelButton = cancel;
        }

        public static string? Show(IWin32Window owner, string title, string description, string initial)
        {
            using var dialog = new PromptDialog(title, description, initial, false);
            return dialog.ShowDialog(owner) == DialogResult.OK ? dialog._prompt.Text.Trim() : null;
        }

        public static (string Prompt, int DurationSeconds)? ShowWithDuration(IWin32Window owner, string title, string description, string initial)
        {
            using var dialog = new PromptDialog(title, description, initial, true);
            return dialog.ShowDialog(owner) == DialogResult.OK
                ? (dialog._prompt.Text.Trim(), (int)(dialog._duration?.Value ?? 20))
                : null;
        }
    }
}
