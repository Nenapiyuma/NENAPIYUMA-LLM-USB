using System.Net.Http.Json;
using System.Text.Json;

namespace NENAPIYUMA.Shared;

public sealed class ImageGenerationService
{
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };
    private readonly string _endpoint;

    public ImageGenerationService(string endpoint = "http://127.0.0.1:8188")
    {
        _endpoint = endpoint.TrimEnd('/');
        if (!Uri.TryCreate(_endpoint, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !IPAddressIsLoopback(uri.Host))
            throw new ArgumentException("For privacy, image generation must use a local ComfyUI endpoint on this PC.", nameof(endpoint));
    }

    public async Task<string> GenerateAsync(string prompt, string outputPath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new ArgumentException("Describe the image first.", nameof(prompt));
        if (prompt.Length > 4000)
            throw new ArgumentException("Image prompt is limited to 4,000 characters.", nameof(prompt));

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(TimeSpan.FromMinutes(12));
        var token = linked.Token;

        var checkpoint = await DetectCheckpointAsync(token);
        var workflow = new Dictionary<string, object>
        {
            ["4"] = new { class_type = "CheckpointLoaderSimple", inputs = new { ckpt_name = checkpoint } },
            ["6"] = new { class_type = "CLIPTextEncode", inputs = new { text = prompt, clip = new object[] { "4", 1 } } },
            ["7"] = new { class_type = "CLIPTextEncode", inputs = new { text = "low quality, blurry, distorted, watermark, unreadable text", clip = new object[] { "4", 1 } } },
            ["5"] = new { class_type = "EmptyLatentImage", inputs = new { width = 768, height = 768, batch_size = 1 } },
            ["3"] = new { class_type = "KSampler", inputs = new { seed = Random.Shared.NextInt64(1, long.MaxValue), steps = 20, cfg = 7.0, sampler_name = "euler", scheduler = "normal", denoise = 1.0, model = new object[] { "4", 0 }, positive = new object[] { "6", 0 }, negative = new object[] { "7", 0 }, latent_image = new object[] { "5", 0 } } },
            ["8"] = new { class_type = "VAEDecode", inputs = new { samples = new object[] { "3", 0 }, vae = new object[] { "4", 2 } } },
            ["9"] = new { class_type = "SaveImage", inputs = new { filename_prefix = "NENAPIYUMA" , images = new object[] { "8", 0 } } }
        };

        using var submit = await Http.PostAsJsonAsync(_endpoint + "/prompt", new { prompt = workflow, client_id = Guid.NewGuid().ToString("N") }, token);
        var submitBody = await submit.Content.ReadAsStringAsync(token);
        if (!submit.IsSuccessStatusCode)
            throw new InvalidOperationException($"ComfyUI rejected the image workflow (HTTP {(int)submit.StatusCode}). Check that ComfyUI is running and its checkpoint is compatible. Details: {Limit(submitBody, 700)}");

        using var submitJson = JsonDocument.Parse(submitBody);
        if (!submitJson.RootElement.TryGetProperty("prompt_id", out var promptIdElement))
            throw new InvalidDataException("ComfyUI did not return a prompt_id. The workflow may be incompatible with this ComfyUI version.");
        var promptId = promptIdElement.GetString();
        if (string.IsNullOrWhiteSpace(promptId))
            throw new InvalidDataException("ComfyUI returned an empty prompt_id.");

        var deadline = DateTimeOffset.UtcNow.AddMinutes(11);
        while (DateTimeOffset.UtcNow < deadline)
        {
            token.ThrowIfCancellationRequested();
            await Task.Delay(1200, token);
            using var historyResponse = await Http.GetAsync(_endpoint + "/history/" + Uri.EscapeDataString(promptId), token);
            if (!historyResponse.IsSuccessStatusCode)
                continue;

            using var historyJson = JsonDocument.Parse(await historyResponse.Content.ReadAsStringAsync(token));
            if (!historyJson.RootElement.TryGetProperty(promptId, out var result))
                continue;
            if (!result.TryGetProperty("outputs", out var outputs))
                continue;

            foreach (var node in outputs.EnumerateObject())
            {
                if (!node.Value.TryGetProperty("images", out var images) || images.GetArrayLength() == 0)
                    continue;
                var image = images[0];
                var filename = image.GetProperty("filename").GetString();
                var subfolder = image.TryGetProperty("subfolder", out var sub) ? sub.GetString() : "";
                var type = image.TryGetProperty("type", out var t) ? t.GetString() : "output";
                if (string.IsNullOrWhiteSpace(filename))
                    continue;

                var viewUrl = _endpoint + "/view?filename=" + Uri.EscapeDataString(filename) +
                    "&subfolder=" + Uri.EscapeDataString(subfolder ?? "") +
                    "&type=" + Uri.EscapeDataString(type ?? "output");
                using var imageResponse = await Http.GetAsync(viewUrl, token);
                imageResponse.EnsureSuccessStatusCode();
                var bytes = await imageResponse.Content.ReadAsByteArrayAsync(token);
                var pngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
                if (bytes.Length < 8 || bytes.Length > 100_000_000 || !bytes.AsSpan(0, 8).SequenceEqual(pngSignature))
                    throw new InvalidDataException("ComfyUI did not return a valid PNG image. Check the workflow output and ComfyUI logs.");
                var fullPath = Path.GetFullPath(outputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllBytesAsync(fullPath, bytes, token);
                return fullPath;
            }
        }

        throw new TimeoutException("Image generation did not finish within 11 minutes. Check GPU/RAM, checkpoint compatibility, and ComfyUI console logs.");
    }

    private async Task<string> DetectCheckpointAsync(CancellationToken cancellationToken)
    {
        using var response = await Http.GetAsync(_endpoint + "/object_info/CheckpointLoaderSimple", cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("CheckpointLoaderSimple", out var loader) ||
            !loader.TryGetProperty("input", out var input) ||
            !input.TryGetProperty("required", out var required) ||
            !required.TryGetProperty("ckpt_name", out var checkpoint) ||
            checkpoint.ValueKind != JsonValueKind.Array ||
            checkpoint.GetArrayLength() == 0 ||
            checkpoint[0].ValueKind != JsonValueKind.Array ||
            checkpoint[0].GetArrayLength() == 0)
            throw new InvalidOperationException("ComfyUI is running but has no available checkpoint. Add a compatible image checkpoint to ComfyUI/models/checkpoints and restart/reload ComfyUI.");

        var first = checkpoint[0][0].GetString();
        return string.IsNullOrWhiteSpace(first)
            ? throw new InvalidOperationException("ComfyUI did not report a usable image checkpoint.")
            : first;
    }

    private static bool IPAddressIsLoopback(string host) =>
        host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
        System.Net.IPAddress.TryParse(host, out var address) && System.Net.IPAddress.IsLoopback(address);

    private static string Limit(string text, int max) => text.Length <= max ? text : text[..max];
}
