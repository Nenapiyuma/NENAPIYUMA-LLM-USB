using System.Diagnostics;
using NENAPIYUMA.Shared;

namespace NENAPIYUMA.Studio;

internal sealed class StudioForm : Form
{
    private const string BaseSystemInstruction =
        "You are NENAPIYUMA Studio AI, a local-first assistant. Answer in Sinhala when the user writes Sinhala, and in English when the user writes English. Help with general questions and programming. Be accurate and clear. Do not invent up-to-date facts when offline. Do not claim that optional tools ran unless a real output was produced.";

    private readonly string _appDirectory = AppContext.BaseDirectory;
    private readonly SuiteSettings _settings;
    private readonly ConversationStore _store;
    private readonly string _dataDirectory;
    private readonly LlamaServerClient _server = new();
    private readonly List<ChatMessage> _history = new();
    private ModelDiscovery _discovery;
    private readonly RichTextBox _transcript = new();
    private readonly TextBox _input = new();
    private readonly TextBox _searchBox = new();
    private readonly Label _status = new();
    private readonly Label _resource = new();
    private readonly ComboBox _mode = new();
    private readonly CheckBox _automation = new();
    private readonly Button _send = new();
    private readonly Button _stop = new();
    private bool _working;
    private CancellationTokenSource? _generation;

    public StudioForm()
    {
        Text = "NENAPIYUMA Studio AI";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(820, 580);
        Size = new Size(1120, 800);
        BackColor = Color.FromArgb(14, 18, 32);
        ForeColor = Color.FromArgb(233, 239, 249);
        Font = new Font("Segoe UI", 10F);

        _settings = SuiteSettings.Load(_appDirectory);
        _discovery = ModelLocator.Discover(_appDirectory, _settings.ModelDirectory, _settings.RuntimeDirectory);
        _dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NENAPIYUMA", "Studio", "data");
        Directory.CreateDirectory(_dataDirectory);
        _store = new ConversationStore(Path.Combine(_dataDirectory, "chat-history.json"));
        try { _history.AddRange(_store.Load()); }
        catch (Exception ex) { MessageBox.Show(this, "Saved history could not be loaded.\n\n" + ex.Message, "NENAPIYUMA Studio AI", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        BuildUi();
        RenderMessages(_history);
        UpdateButtons();
        Shown += async (_, _) =>
        {
            if (_discovery.CanRun)
                await EnsureModelAsync(showError: false);
            else
                SetStatus(_discovery.StatusMessage);
            RefreshResourceStatus();
        };
        FormClosing += (_, _) =>
        {
            try { _store.Save(_history); } catch { }
            _generation?.Cancel();
            _server.Dispose();
            _generation?.Dispose();
        };
    }

    private void BuildUi()
    {
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(14), BackColor = BackColor };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "NENAPIYUMA Studio AI", Font = new Font("Segoe UI Semibold", 20F), ForeColor = Color.FromArgb(173, 148, 255), AutoSize = true, Location = new Point(2, 0) };
        var subtitle = new Label { Text = "One input · automatic task routing · local models", Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(167, 178, 200), AutoSize = true, Location = new Point(4, 35) };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        layout.Controls.Add(header, 0, 0);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = BackColor, Padding = new Padding(0, 3, 0, 0) };
        _status.Text = "Checking local AI…";
        _status.AutoSize = true;
        _status.Margin = new Padding(2, 9, 12, 0);
        _status.ForeColor = Color.FromArgb(255, 212, 128);
        toolbar.Controls.Add(_status);
        _resource.AutoSize = true;
        _resource.Margin = new Padding(5, 9, 10, 0);
        _resource.ForeColor = Color.FromArgb(167, 178, 200);
        _resource.Text = "Local model: checking";
        toolbar.Controls.Add(_resource);

        _mode.DropDownStyle = ComboBoxStyle.DropDownList;
        _mode.Items.AddRange(new object[] { "Thinking", "Spike" });
        _mode.SelectedIndex = 0;
        _mode.Width = 112;
        _mode.BackColor = Color.FromArgb(29, 37, 61);
        _mode.ForeColor = Color.White;
        _mode.Margin = new Padding(4, 3, 8, 3);
        toolbar.Controls.Add(_mode);

        _automation.Text = "Enable computer control (off by default)";
        _automation.AutoSize = true;
        _automation.Checked = false;
        _automation.ForeColor = Color.FromArgb(230, 230, 240);
        _automation.Margin = new Padding(6, 8, 6, 0);
        toolbar.Controls.Add(_automation);
        layout.Controls.Add(toolbar, 0, 1);

        _transcript.Dock = DockStyle.Fill;
        _transcript.ReadOnly = true;
        _transcript.BorderStyle = BorderStyle.FixedSingle;
        _transcript.BackColor = Color.FromArgb(21, 26, 45);
        _transcript.ForeColor = Color.FromArgb(231, 238, 248);
        _transcript.Font = new Font("Segoe UI", 10.5F);
        _transcript.DetectUrls = true;
        _transcript.WordWrap = true;
        layout.Controls.Add(_transcript, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 8, 0, 0), BackColor = BackColor };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        _input.Multiline = true;
        _input.AcceptsReturn = true;
        _input.AcceptsTab = true;
        _input.Dock = DockStyle.Fill;
        _input.ScrollBars = ScrollBars.Vertical;
        _input.PlaceholderText = "Ask a question, request code, or describe a task… / සිංහලෙන් හෝ English වලින් ලියන්න";
        StyleInput(_input);
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                e.SuppressKeyPress = true;
                _ = SendMessageAsync();
            }
        };
        footer.Controls.Add(_input, 0, 0);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.RightToLeft, BackColor = BackColor };
        _send.Text = "Send";
        StyleButton(_send, true);
        _send.Click += async (_, _) => await SendMessageAsync();
        buttons.Controls.Add(_send);
        _stop.Text = "Stop";
        StyleButton(_stop, false);
        _stop.Click += (_, _) => _generation?.Cancel();
        buttons.Controls.Add(_stop);

        var export = MakeButton("Export history", false);
        export.Click += (_, _) => ExportHistory();
        buttons.Controls.Add(export);
        var search = MakeButton("Search", false);
        search.Click += (_, _) => SearchHistory();
        buttons.Controls.Add(search);
        _searchBox.Width = 150;
        _searchBox.PlaceholderText = "Find in history";
        StyleInput(_searchBox);
        buttons.Controls.Add(_searchBox);
        var clear = MakeButton("Clear chat", false);
        clear.Click += (_, _) => ClearHistory();
        buttons.Controls.Add(clear);

        footer.Controls.Add(buttons, 0, 1);
        layout.Controls.Add(footer, 0, 3);
    }

    private async Task<bool> EnsureModelAsync(bool showError)
    {
        _discovery = ModelLocator.Discover(_appDirectory, _settings.ModelDirectory, _settings.RuntimeDirectory);
        if (!_discovery.CanRun)
        {
            SetStatus(_discovery.StatusMessage);
            RefreshResourceStatus();
            if (showError)
                MessageBox.Show(this,
                    _discovery.StatusMessage + "\n\nPlace a compatible GGUF model and the Windows llama.cpp runtime in the documented folders. No model is downloaded automatically.",
                    "Local AI is not ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (_server.IsReady)
            return true;

        SetStatus("Starting the local model…");
        try
        {
            await _server.StartAsync(_discovery, _settings.StudioPort, _settings.ContextSize, CancellationToken.None);
            SetStatus("AI ready · localhost only");
            RefreshResourceStatus();
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("AI unavailable — check model/runtime/port");
            RefreshResourceStatus();
            if (showError)
                MessageBox.Show(this, ex.Message, "NENAPIYUMA Studio AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }

    private async Task SendMessageAsync()
    {
        if (_working)
            return;

        var text = _input.Text.Trim();
        if (text.Length == 0)
            return;

        _working = true;
        UpdateButtons();
        try
        {
            var route = TaskRouter.Route(text);
            _input.Clear();
            _history.Add(new ChatMessage("user", text, DateTimeOffset.Now));
            SaveHistory();
            RenderMessages(_history);

            if (route.Kind == TaskKind.ImageGeneration)
            {
                AddAssistantMessage("Image generation is not configured in this build. No image was generated. Install a compatible local image model and its runtime before using this feature. / මෙම build එකේ image-generation module එක සකසා නැත.");
                SetStatus("Image module unavailable · no output created");
                return;
            }

            if (route.Kind == TaskKind.AudioGeneration)
            {
                AddAssistantMessage("Audio generation is not configured in this build. No audio file was created. Text-to-speech and music generation are different capabilities; a compatible local model/runtime must be installed for the chosen task. / මෙම build එකේ audio-generation module එක සකසා නැත.");
                SetStatus("Audio module unavailable · no output created");
                return;
            }

            if (route.Kind == TaskKind.ComputerControl)
            {
                HandleComputerControl(text);
                return;
            }

            if (!await EnsureModelAsync(showError: true))
                return;

            _generation = new CancellationTokenSource();
            var thinking = _mode.SelectedItem?.ToString() == "Thinking";
            var modeInstruction = thinking
                ? "Use a deliberate approach. Give a concise explanation of the method, assumptions, and checks. Do not claim access to hidden reasoning features."
                : "Prioritize a concise, fast answer. Avoid unnecessary detail.";
            var systemInstruction = BaseSystemInstruction + "\n" + route.SystemInstruction + "\n" + modeInstruction;
            SetStatus(route.Kind == TaskKind.Coding ? "Routing to local coding assistant…" : "Routing to local chat model…");
            var answer = await _server.CompleteAsync(_history, systemInstruction, thinking ? 1600 : 320, thinking ? 0.65 : 0.45, _generation.Token);
            AddAssistantMessage(answer);
            SetStatus($"AI ready · {(_mode.SelectedItem?.ToString() ?? "Thinking")} mode · local");
            RefreshResourceStatus();
        }
        catch (OperationCanceledException)
        {
            SetStatus("Generation stopped.");
        }
        catch (Exception ex)
        {
            SetStatus("Task failed — see error");
            MessageBox.Show(this, ex.Message, "NENAPIYUMA Studio AI", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _generation?.Dispose();
            _generation = null;
            _working = false;
            UpdateButtons();
            RefreshResourceStatus();
        }
    }

    private void HandleComputerControl(string request)
    {
        if (!_automation.Checked)
        {
            AddAssistantMessage("Windows computer control is off. Enable “Enable computer control” first, then send an explicit supported request. No action was performed.");
            SetStatus("Computer control disabled");
            return;
        }

        if (!SafeCommandPolicy.TryGetLaunchTarget(request, out var executable) || !SafeCommandPolicy.IsAllowedExecutable(executable))
        {
            AddAssistantMessage("For safety, this initial build can only open Notepad or Calculator. Other Windows actions and arbitrary shell commands are not implemented.");
            SetStatus("Action blocked by allow-list");
            return;
        }

        var choice = MessageBox.Show(this,
            $"Allow NENAPIYUMA Studio AI to open {executable}?\n\nOnly this allow-listed app will be launched; no shell command will run.",
            "Approve Windows action", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (choice != DialogResult.Yes)
        {
            AddAssistantMessage("Action cancelled. Nothing was launched.");
            SetStatus("Action cancelled");
            LogAction("Denied", executable);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = true,
                WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            });
            AddAssistantMessage($"Approved action completed: launched {executable}. No other system action was performed.");
            SetStatus("Allow-listed app launched");
            LogAction("Allowed", executable);
        }
        catch (Exception ex)
        {
            AddAssistantMessage($"Could not launch {executable}: {ex.Message}");
            SetStatus("Windows action failed");
            LogAction("Failed", executable + " · " + ex.Message);
        }
    }

    private void LogAction(string result, string detail)
    {
        try
        {
            Directory.CreateDirectory(_dataDirectory);
            File.AppendAllText(Path.Combine(_dataDirectory, "action-log.txt"),
                $"{DateTimeOffset.Now:O}\t{result}\t{detail}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void AddAssistantMessage(string text)
    {
        _history.Add(new ChatMessage("assistant", text, DateTimeOffset.Now));
        SaveHistory();
        RenderMessages(_history);
    }

    private void SearchHistory()
    {
        try
        {
            var query = _searchBox.Text.Trim();
            RenderMessages(query.Length == 0 ? _history : _store.Search(query));
            SetStatus(query.Length == 0 ? "Showing current conversation." : $"History search: {_store.Search(query).Count} match(es).");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "History search failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportHistory()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export NENAPIYUMA Studio history",
            Filter = "JSON chat backup (*.json)|*.json",
            FileName = "nenapiyuma-studio-backup.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            _store.Save(_history);
            _store.ExportTo(dialog.FileName);
            SetStatus("Conversation backup exported.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearHistory()
    {
        if (_working)
            return;
        if (MessageBox.Show(this, "Clear the saved Studio conversation?", "Clear chat",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _history.Clear();
        SaveHistory();
        RenderMessages(_history);
        SetStatus("Conversation cleared.");
    }

    private void SaveHistory()
    {
        try { _store.Save(_history); }
        catch (Exception ex) { SetStatus("Could not save history: " + ex.Message); }
    }

    private void RenderMessages(IEnumerable<ChatMessage> messages)
    {
        _transcript.SuspendLayout();
        _transcript.Clear();
        foreach (var message in messages)
        {
            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.SelectionColor = message.Role == "user"
                ? Color.FromArgb(135, 174, 255)
                : Color.FromArgb(133, 225, 188);
            _transcript.SelectionFont = new Font("Segoe UI Semibold", 10.5F);
            _transcript.AppendText(message.Role == "user" ? "You\n" : "NENAPIYUMA Studio AI\n");
            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.SelectionColor = Color.FromArgb(230, 237, 248);
            _transcript.SelectionFont = new Font("Segoe UI", 10.5F);
            _transcript.AppendText(message.Content + "\n\n");
        }
        _transcript.SelectionStart = _transcript.TextLength;
        _transcript.ScrollToCaret();
        _transcript.ResumeLayout();
    }

    private void RefreshResourceStatus()
    {
        var workingSet = _server.ServerWorkingSetBytes;
        var model = _discovery.HasModel ? Path.GetFileName(_discovery.ModelPath) : "model missing";
        var serverMemory = workingSet > 0 ? $" · server RAM {workingSet / (1024d * 1024d):N0} MB" : string.Empty;
        _resource.Text = model + serverMemory + $" · port {_settings.StudioPort}";
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
        _status.ForeColor = text.Contains("ready", StringComparison.OrdinalIgnoreCase) || text.Contains("launched", StringComparison.OrdinalIgnoreCase)
            ? Color.FromArgb(133, 225, 188)
            : text.Contains("missing", StringComparison.OrdinalIgnoreCase) || text.Contains("unavailable", StringComparison.OrdinalIgnoreCase) || text.Contains("failed", StringComparison.OrdinalIgnoreCase) || text.Contains("blocked", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(255, 159, 159)
                : Color.FromArgb(255, 211, 129);
    }

    private void UpdateButtons()
    {
        _send.Enabled = !_working;
        _stop.Enabled = _working;
        _mode.Enabled = !_working;
    }

    private static void StyleInput(TextBox box)
    {
        box.BackColor = Color.FromArgb(27, 32, 55);
        box.ForeColor = Color.FromArgb(233, 239, 249);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = new Font("Segoe UI", 10.5F);
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 31, FlatStyle = FlatStyle.Flat, Margin = new Padding(3), Padding = new Padding(8, 2, 8, 2) };
        StyleButton(button, primary);
        return button;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(79, 76, 131);
        button.BackColor = primary ? Color.FromArgb(106, 83, 204) : Color.FromArgb(39, 42, 69);
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(9, 2, 9, 2);
        button.Margin = new Padding(4, 3, 4, 3);
    }
}
