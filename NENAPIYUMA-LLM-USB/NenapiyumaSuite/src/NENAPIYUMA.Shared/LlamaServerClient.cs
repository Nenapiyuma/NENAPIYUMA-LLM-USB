using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace NENAPIYUMA.Shared;

public sealed class LlamaServerClient : IDisposable
{
    private readonly HttpClient _http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly object _logLock = new();
    private readonly Queue<string> _recentOutput = new();
    private Process? _process;
    private int _port;
    private bool _ready;

    public bool IsReady => _ready && _process is { HasExited: false };
    public int Port => _port;
    public long ServerWorkingSetBytes
    {
        get
        {
            try { return _process is { HasExited: false } ? _process.WorkingSet64 : 0; }
            catch { return 0; }
        }
    }

    public async Task StartAsync(ModelDiscovery discovery, int port, int contextSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsReady && _port == port)
            return;

        Stop();
        if (!discovery.CanRun)
            throw new InvalidOperationException(discovery.StatusMessage);
        if (port is < 1024 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Local AI port must be between 1024 and 65535.");

        EnsurePortAvailable(port);
        _port = port;
        var startInfo = new ProcessStartInfo
        {
            FileName = discovery.RuntimePath!,
            WorkingDirectory = Path.GetDirectoryName(discovery.RuntimePath!)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add("-m");
        startInfo.ArgumentList.Add(discovery.ModelPath!);
        startInfo.ArgumentList.Add("--host");
        startInfo.ArgumentList.Add("127.0.0.1");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(port.ToString());
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add(Math.Clamp(contextSize, 512, 32768).ToString());
        startInfo.ArgumentList.Add("--n-gpu-layers");
        startInfo.ArgumentList.Add("0");

        try
        {
            _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Windows could not start llama-server.exe.");
            _process.OutputDataReceived += (_, e) => CaptureOutput(e.Data);
            _process.ErrorDataReceived += (_, e) => CaptureOutput(e.Data);
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();

            var deadline = DateTimeOffset.UtcNow.AddSeconds(75);
            while (DateTimeOffset.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_process.HasExited)
                    throw new InvalidOperationException($"llama-server.exe exited with code {_process.ExitCode}. {RecentOutput()}");

                using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                probeTimeout.CancelAfter(TimeSpan.FromSeconds(2));
                try
                {
                    using var response = await _http.GetAsync($"http://127.0.0.1:{port}/health", probeTimeout.Token);
                    if (response.IsSuccessStatusCode)
                    {
                        _ready = true;
                        return;
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // A single health check timed out; keep waiting for model loading.
                }
                catch (HttpRequestException)
                {
                    // The server is still starting.
                }

                await Task.Delay(400, cancellationToken);
            }

            throw new TimeoutException($"Local AI did not become ready within 75 seconds. {RecentOutput()}");
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public async Task<string> CompleteAsync(
        IReadOnlyList<ChatMessage> history,
        string systemInstruction,
        int maxTokens,
        double temperature,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsReady)
            throw new InvalidOperationException("The local AI server is not ready.");

        using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        requestTimeout.CancelAfter(TimeSpan.FromMinutes(4));

        var messages = new List<Dictionary<string, string>>
        {
            new() { ["role"] = "system", ["content"] = systemInstruction }
        };
        messages.AddRange(history
            .Where(message => message.Role is "user" or "assistant")
            .TakeLast(18)
            .Select(message => new Dictionary<string, string>
            {
                ["role"] = message.Role,
                ["content"] = message.Content
            }));

        var payload = new
        {
            model = "local-model",
            messages,
            temperature = Math.Clamp(temperature, 0.0, 2.0),
            max_tokens = Math.Clamp(maxTokens, 32, 4096),
            stream = false
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"http://127.0.0.1:{_port}/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, requestTimeout.Token);
        var json = await response.Content.ReadAsStringAsync(requestTimeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            var detail = json.Length > 900 ? json[..900] : json;
            throw new InvalidOperationException($"Local model returned HTTP {(int)response.StatusCode}: {detail}");
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            throw new InvalidDataException("The local model returned no answer.");

        var message = choices[0].GetProperty("message");
        if (message.TryGetProperty("content", out var content))
        {
            var answer = content.GetString();
            if (!string.IsNullOrWhiteSpace(answer))
                return answer.Trim();
        }

        throw new InvalidDataException("The local model response did not contain text. Check model/runtime compatibility.");
    }

    public void Stop()
    {
        _ready = false;
        var process = _process;
        _process = null;
        if (process is null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(3000);
            }
        }
        catch (InvalidOperationException) { }
        catch (System.ComponentModel.Win32Exception) { }
        finally
        {
            process.Dispose();
        }
    }

    public void Dispose()
    {
        Stop();
        _http.Dispose();
    }

    private static void EnsurePortAvailable(int port)
    {
        var listener = new TcpListener(IPAddress.Loopback, port);
        try
        {
            listener.Start();
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"Local AI port {port} is already in use. Change the app port in config\\appsettings.json and restart.",
                ex);
        }
        finally
        {
            listener.Stop();
        }
    }

    private void CaptureOutput(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        lock (_logLock)
        {
            _recentOutput.Enqueue(line);
            while (_recentOutput.Count > 20)
                _recentOutput.Dequeue();
        }
    }

    private string RecentOutput()
    {
        lock (_logLock)
            return string.Join(Environment.NewLine, _recentOutput);
    }
}
