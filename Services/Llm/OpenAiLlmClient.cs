using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TransparentCaptureApp.Models;

namespace TransparentCaptureApp.Services.Llm;

public sealed class OpenAiLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public OpenAiLlmClient(HttpClient httpClient, string apiKey, string model)
    {
        _httpClient = httpClient;
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<TranscriptionResult> TranscribeImageAsync(
        string imagePath,
        string prompt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return Error("OpenAI APIキーが設定されていません。");
        }

        var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(imagePath, cancellationToken));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/responses");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        var body = new
        {
            model = _model,
            input = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "input_text", text = prompt },
                        new { type = "input_image", image_url = $"data:image/png;base64,{base64}" }
                    }
                }
            }
        };

        request.Content = CreateJsonContent(body);
        return await SendAsync(request, cancellationToken);
    }

    private async Task<TranscriptionResult> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Error($"OpenAI APIエラー: {(int)response.StatusCode} {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var text = ExtractOutputText(doc.RootElement);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Error("OpenAI API応答から文字起こし結果を取得できませんでした。");
            }

            return Success(text.Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error(ex.Message);
        }
    }

    private static string ExtractOutputText(JsonElement root)
    {
        if (root.TryGetProperty("output_text", out var outputText))
        {
            return outputText.GetString() ?? "";
        }

        if (!root.TryGetProperty("output", out var output) || output.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var builder = new StringBuilder();
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text))
                {
                    builder.AppendLine(text.GetString());
                }
            }
        }

        return builder.ToString();
    }

    private static StringContent CreateJsonContent(object body)
    {
        var json = JsonSerializer.Serialize(body);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private TranscriptionResult Success(string text) => new()
    {
        IsSuccess = true,
        Text = text,
        ProviderName = "OpenAI",
        ModelName = _model
    };

    private TranscriptionResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message,
        ProviderName = "OpenAI",
        ModelName = _model
    };
}
