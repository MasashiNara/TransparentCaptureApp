using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TransparentCaptureApp.Services.Llm;

public sealed class LlamaCppServerStatusService
{
    private readonly HttpClient _httpClient;

    public LlamaCppServerStatusService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<LlamaCppServerStatus> CheckVisionSupportAsync(
        string baseUrl,
        string model,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return new LlamaCppServerStatus
            {
                IsReachable = false,
                SupportsVision = false,
                Message = "llama.cpp接続URLが入力されていません。"
            };
        }

        try
        {
            var normalizedBaseUrl = NormalizeBaseUrl(baseUrl);
            var json = await GetPropsJsonAsync(normalizedBaseUrl, model, cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var supportsVision = SupportsVision(doc.RootElement);

            return new LlamaCppServerStatus
            {
                IsReachable = true,
                SupportsVision = supportsVision,
                Message = supportsVision
                    ? "llama.cpp server は画像入力に対応しています。"
                    : "llama.cpp server に接続できましたが、画像入力対応は確認できませんでした。"
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return new LlamaCppServerStatus
            {
                IsReachable = false,
                SupportsVision = false,
                Message = $"llama.cpp server に接続できませんでした: {ex.Message}"
            };
        }
    }

    private async Task<string> GetPropsJsonAsync(
        string normalizedBaseUrl,
        string model,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(model))
        {
            var modelUrl = $"{normalizedBaseUrl}/props?model={Uri.EscapeDataString(model)}";
            var modelResponse = await _httpClient.GetAsync(modelUrl, cancellationToken);
            if (modelResponse.IsSuccessStatusCode)
            {
                return await modelResponse.Content.ReadAsStringAsync(cancellationToken);
            }
        }

        var response = await _httpClient.GetAsync($"{normalizedBaseUrl}/props", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        var normalized = baseUrl.TrimEnd('/');
        return normalized.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? normalized[..^3]
            : normalized;
    }

    private static bool SupportsVision(JsonElement root)
    {
        if (!root.TryGetProperty("modalities", out var modalities))
        {
            return false;
        }

        if (modalities.ValueKind == JsonValueKind.Object &&
            modalities.TryGetProperty("vision", out var vision))
        {
            return vision.ValueKind == JsonValueKind.True ||
                (vision.ValueKind == JsonValueKind.String &&
                 bool.TryParse(vision.GetString(), out var parsed) &&
                 parsed);
        }

        if (modalities.ValueKind == JsonValueKind.Array)
        {
            foreach (var modality in modalities.EnumerateArray())
            {
                if (modality.ValueKind == JsonValueKind.String &&
                    string.Equals(modality.GetString(), "vision", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
