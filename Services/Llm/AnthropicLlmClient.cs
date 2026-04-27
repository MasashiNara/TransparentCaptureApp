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

public sealed class AnthropicLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public AnthropicLlmClient(HttpClient httpClient, string apiKey, string model)
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
            return Error("Anthropic APIキーが設定されていません。");
        }

        var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(imagePath, cancellationToken));
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        request.Headers.Add("x-api-key", _apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var body = new
        {
            model = _model,
            max_tokens = 4096,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = "image/png",
                                data = base64
                            }
                        },
                        new { type = "text", text = prompt }
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
                return Error($"Anthropic APIエラー: {(int)response.StatusCode} {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var text = ExtractText(doc.RootElement);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Error("Anthropic API応答から文字起こし結果を取得できませんでした。");
            }

            return Success(text.Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error(ex.Message);
        }
    }

    private static string ExtractText(JsonElement root)
    {
        if (!root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return "";
        }

        var builder = new StringBuilder();
        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("type", out var type) &&
                type.GetString() == "text" &&
                item.TryGetProperty("text", out var text))
            {
                builder.AppendLine(text.GetString());
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
        ProviderName = "Anthropic",
        ModelName = _model
    };

    private TranscriptionResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message,
        ProviderName = "Anthropic",
        ModelName = _model
    };
}
