using NENAPIYUMA.Shared;

namespace NENAPIYUMA.Chat;

internal sealed class ChatForm : Form
{
    private const string SystemInstruction =
        "You are NENAPIYUMA Chat, a helpful local offline assistant. Answer in Sinhala when the user writes Sinhala and in English when the user writes English. Help with coding and explain code clearly. Be honest about uncertainty and never invent current facts. Do not claim to use the internet.";

    private readonly string _appDirectory = AppContext.BaseDirectory;
    private readonly SuiteSettings _settings;
    private readonly ConversationStore _store;
    private readonly LlamaServerClient _server = new();
    private readonly List<ChatMessage> _history = new();
    private ModelDiscovery _discovery;
    private readonly RichTextBox _transcript = new();
    private readonly TextBox _input = new();
    private readonly TextBox _searchBox = new();
    private readonly Label _status = new();
    private readonly Label _modelLabel = new();
    private readonly Button _send = new();
    private readonly Button _stop = new();
    private readonly CheckBox _automation = new();
    private bool _working;
    private CancellationTokenSource? _generation;

    public ChatForm()
    {
        Text = "NENAPIYUMA Chat";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 540);
        Size = new Size(1040, 760);
        BackColor = Color.FromArgb(13, 20, 34);
        ForeColor = Color.FromArgb(230, 238, 250);
        Font = new Font("Segoe UI", 10F);

        _settings = SuiteSettings.Load(_appDirectory);
        _discovery = ModelLocator.Discover(_appDirectory, _settings.ModelDirectory, _settings.RuntimeDirectory);
        var dataDirectory = Path.Combine(_appDirectory, "data");
        Directory.CreateDirectory(dataDirectory);
        _store = new ConversationStore(Path.Combine(dataDirectory, "chat-history.json"));

        try { _history.AddRange(_store.Load()); }
        catch (Exception ex) { MessageBox.Show(this, "Chat history could not be read. A new session will start.\n\n" + ex.Message, "NENAPIYUMA Chat", MessageBoxButtons.OK, MessageBoxIcon.Warning); }

        BuildUi();
        RenderMessages(_history);
        UpdateButtons();
        Shown += async (_, _) =>
        {
            if (_discovery.CanRun)
                await EnsureModelAsync(showError: false);
            else
                SetStatus(_discovery.StatusMessage);
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 126));
        Controls.Add(layout);

        var header = new Panel { Dock = DockStyle.Fill };
        var title = new Label { Text = "NENAPIYUMA Chat", Font = new Font("Segoe UI Semibold", 20F), ForeColor = Color.FromArgb(116, 189, 255), AutoSize = true, Location = new Point(2, 0) };
        var subtitle = new Label { Text = "Local-first Sinhala + English chat and coding", Font = new Font("Segoe UI", 9.5F), ForeColor = Color.FromArgb(165, 179, 199), AutoSize = true, Location = new Point(4, 34) };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);
        layout.Controls.Add(header, 0, 0);

        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true, BackColor = BackColor, Padding = new Padding(0, 4, 0, 0) };
        _status.Text = "Checking local model...";
        _status.AutoSize = true;
        _status.Margin = new Padding(2, 8, 12, 0);
        _status.ForeColor = Color.FromArgb(255, 212, 128);
        toolbar.Controls.Add(_status);
        _modelLabel.Text = "Port: 127.0.0.1:" + _settings.ChatPort;
        _modelLabel.AutoSize = true;
        _modelLabel.Margin = new Padding(5, 8, 10, 0);
        _modelLabel.ForeColor = Color.FromArgb(165, 179, 199);
        toolbar.Controls.Add(_modelLabel);

        _searchBox.Width = 180;
        _searchBox.PlaceholderText = "Search this history…";
        StyleInput(_searchBox);
        toolbar.Controls.Add(_searchBox);
        var searchButton = MakeButton("Search", false);
        searchButton.Click += (_, _) => SearchHistory();
        toolbar.Controls.Add(searchButton);
        var showAllButton = MakeButton("Show chat", false);
        showAllButton.Click += (_, _) => RenderMessages(_history);
        toolbar.Controls.Add(showAllButton);
        var exportButton = MakeButton("Export", false);
        exportButton.Click += (_, _) => ExportHistory();
        toolbar.Controls.Add(exportButton);
        var importButton = MakeButton("Import", false);
        importButton.Click += (_, _) => ImportHistory();
        toolbar.Controls.Add(importButton);
        var clearButton = MakeButton("Clear", false);
        clearButton.Click += (_, _) => ClearHistory();
        clearButton.Name = "clearChatButton";
        toolbar.Controls.Add(clearButton);

        var imageButton = MakeButton("Generate image", false);
        imageButton.Click += async (_, _) => await FeatureTools.GenerateImageAsync(this, _settings, SetStatus);
        toolbar.Controls.Add(imageButton);
        var audioButton = MakeButton("Generate audio", false);
        audioButton.Click += async (_, _) => await FeatureTools.GenerateAudioAsync(this, SetStatus);
        toolbar.Controls.Add(audioButton);
        _automation.Text = "PC control OFF";
        _automation.AutoSize = true;
        _automation.Checked = false;
        _automation.ForeColor = Color.FromArgb(230, 230, 240);
        _automation.Margin = new Padding(6, 8, 6, 0);
        _automation.CheckedChanged += (_, _) => _automation.Text = _automation.Checked ? "PC control ON" : "PC control OFF";
        toolbar.Controls.Add(_automation);
        var pcButton = MakeButton("Control PC…", false);
        pcButton.Click += (_, _) => FeatureTools.ShowComputerControl(this, _automation.Checked, LogAction, SetStatus);
        toolbar.Controls.Add(pcButton);
        layout.Controls.Add(toolbar, 0, 1);

        _transcript.Dock = DockStyle.Fill;
        _transcript.ReadOnly = true;
        _transcript.BorderStyle = BorderStyle.FixedSingle;
        _transcript.BackColor = Color.FromArgb(20, 30, 48);
        _transcript.ForeColor = Color.FromArgb(225, 235, 248);
        _transcript.Font = new Font("Segoe UI", 10.5F);
        _transcript.DetectUrls = true;
        _transcript.WordWrap = true;
        _transcript.Padding = new Padding(10);
        layout.Controls.Add(_transcript, 0, 2);

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Padding = new Padding(0, 8, 0, 0), BackColor = BackColor };
        footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
        _input.Multiline = true;
        _input.AcceptsReturn = true;
        _input.AcceptsTab = true;
        _input.Dock = DockStyle.Fill;
        _input.ScrollBars = ScrollBars.Vertical;
        _input.PlaceholderText = "ඔබගේ ප්‍රශ්නය මෙහි ලියන්න… / Type your message… (Enter to send, Shift+Enter for a new line)";
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
        _stop.Text = "Stop generation";
        StyleButton(_stop, false);
        _stop.Click += (_, _) => _generation?.Cancel();
        buttons.Controls.Add(_stop);
        footer.Controls.Add(buttons, 0, 1);
        layout.Controls.Add(footer, 0, 3);
    }

    private async Task<bool> EnsureModelAsync(bool showError)
    {
        _discovery = ModelLocator.Discover(_appDirectory, _settings.ModelDirectory, _settings.RuntimeDirectory);
        if (!_discovery.CanRun)
        {
            SetStatus(_discovery.StatusMessage);
            if (showError)
                MessageBox.Show(this,
                    _discovery.StatusMessage + "\n\nNo model is downloaded automatically. Keep the model and runtime in the documented folders, then try again.",
                    "Local AI is not ready", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }

        if (_server.IsReady)
            return true;

        SetStatus("Starting local AI model…");
        _modelLabel.Text = Path.GetFileName(_discovery.ModelPath) + "  ·  127.0.0.1:" + _settings.ChatPort;
        try
        {
            await _server.StartAsync(_discovery, _settings.ChatPort, _settings.ContextSize, CancellationToken.None);
            SetStatus("AI ready · local inference");
            return true;
        }
        catch (Exception ex)
        {
            SetStatus("AI unavailable — check model/runtime/port");
            if (showError)
                MessageBox.Show(this, ex.Message, "NENAPIYUMA local AI error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                await FeatureTools.GenerateImageAsync(this, _settings, SetStatus, text);
                return;
            }
            if (route.Kind == TaskKind.AudioGeneration)
            {
                await FeatureTools.GenerateAudioAsync(this, SetStatus, text);
                return;
            }
            if (route.Kind == TaskKind.ComputerControl)
            {
                FeatureTools.HandleControlRequest(this, text, _automation.Checked, LogAction, SetStatus);
                return;
            }

            if (!await EnsureModelAsync(showError: true))
                return;

            _generation = new CancellationTokenSource();
            SetStatus("Thinking…");
            var answer = await _server.CompleteAsync(_history, SystemInstruction, 1000, 0.7, _generation.Token);
            _history.Add(new ChatMessage("assistant", answer, DateTimeOffset.Now));
            SaveHistory();
            RenderMessages(_history);
            SetStatus("AI ready · local inference");
        }
        catch (OperationCanceledException)
        {
            SetStatus("Generation stopped.");
            RenderMessages(_history);
        }
        catch (Exception ex)
        {
            SetStatus("Request failed — see error");
            MessageBox.Show(this, ex.Message, "Local AI request failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _generation?.Dispose();
            _generation = null;
            _working = false;
            UpdateButtons();
        }
    }

    private void SearchHistory()
    {
        var query = _searchBox.Text.Trim();
        if (query.Length == 0)
        {
            RenderMessages(_history);
            SetStatus("Enter a word or phrase to search this saved conversation.");
            return;
        }

        try
        {
            var matches = _store.Search(query);
            RenderMessages(matches);
            SetStatus($"{matches.Count} matching message(s) in the saved conversation.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Search failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ExportHistory()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "Export NENAPIYUMA chat backup",
            Filter = "JSON chat backup (*.json)|*.json",
            FileName = "nenapiyuma-chat-backup.json"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            SaveHistory();
            _store.ExportTo(dialog.FileName);
            SetStatus("Chat backup exported.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ImportHistory()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Import NENAPIYUMA chat backup",
            Filter = "JSON chat backup (*.json)|*.json|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        if (MessageBox.Show(this, "Importing a backup replaces the conversation currently shown. Continue?",
                "Import chat backup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        try
        {
            var imported = ConversationStore.ReadBackup(dialog.FileName);
            _history.Clear();
            _history.AddRange(imported);
            SaveHistory();
            RenderMessages(_history);
            SetStatus("Chat backup imported.");
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Import failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ClearHistory()
    {
        if (_working)
            return;
        if (MessageBox.Show(this, "Clear the saved conversation on this drive?", "Clear chat",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        _history.Clear();
        SaveHistory();
        RenderMessages(_history);
        SetStatus("Chat cleared.");
    }

    private void SaveHistory()
    {
        try { _store.Save(_history); }
        catch (Exception ex) { SetStatus("Could not save chat history: " + ex.Message); }
    }

    private void LogAction(string result, string detail)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(_appDirectory, "data"));
            File.AppendAllText(Path.Combine(_appDirectory, "data", "action-log.txt"),
                $"{DateTimeOffset.Now:O}\t{result}\t{detail}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private void RenderMessages(IEnumerable<ChatMessage> messages)
    {
        _transcript.SuspendLayout();
        _transcript.Clear();
        foreach (var message in messages)
        {
            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.SelectionColor = message.Role == "user"
                ? Color.FromArgb(119, 194, 255)
                : Color.FromArgb(135, 222, 174);
            _transcript.SelectionFont = new Font("Segoe UI Semibold", 10.5F);
            _transcript.AppendText(message.Role == "user" ? "You\n" : "NENAPIYUMA\n");
            _transcript.SelectionStart = _transcript.TextLength;
            _transcript.SelectionColor = Color.FromArgb(225, 235, 248);
            _transcript.SelectionFont = new Font("Segoe UI", 10.5F);
            _transcript.AppendText(message.Content + "\n\n");
        }
        _transcript.SelectionStart = _transcript.TextLength;
        _transcript.ScrollToCaret();
        _transcript.ResumeLayout();
    }

    private void SetStatus(string text)
    {
        _status.Text = text;
        _status.ForeColor = text.Contains("ready", StringComparison.OrdinalIgnoreCase)
            ? Color.FromArgb(135, 222, 174)
            : text.Contains("missing", StringComparison.OrdinalIgnoreCase) || text.Contains("unavailable", StringComparison.OrdinalIgnoreCase) || text.Contains("failed", StringComparison.OrdinalIgnoreCase)
                ? Color.FromArgb(255, 160, 150)
                : Color.FromArgb(255, 212, 128);
    }

    private void UpdateButtons()
    {
        _send.Enabled = !_working;
        _stop.Enabled = _working;
    }

    private static void StyleInput(TextBox box)
    {
        box.BackColor = Color.FromArgb(25, 37, 58);
        box.ForeColor = Color.FromArgb(232, 240, 250);
        box.BorderStyle = BorderStyle.FixedSingle;
        box.Font = new Font("Segoe UI", 10.5F);
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button { Text = text, AutoSize = true, Height = 31, FlatStyle = FlatStyle.Flat, Margin = new Padding(3), Padding = new Padding(10, 2, 10, 2) };
        StyleButton(button, primary);
        return button;
    }

    private static void StyleButton(Button button, bool primary)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(63, 91, 128);
        button.BackColor = primary ? Color.FromArgb(40, 124, 216) : Color.FromArgb(34, 49, 73);
        button.ForeColor = Color.White;
        button.Font = new Font("Segoe UI Semibold", 9.5F);
        button.Cursor = Cursors.Hand;
        button.Padding = new Padding(9, 2, 9, 2);
        button.Margin = new Padding(4, 3, 4, 3);
    }
}
