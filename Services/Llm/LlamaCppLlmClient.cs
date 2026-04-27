using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TransparentCaptureApp.Models;

namespace TransparentCaptureApp.Services.Llm;

public sealed class LlamaCppLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public LlamaCppLlmClient(HttpClient httpClient, string baseUrl, string model)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    public async Task<TranscriptionResult> TranscribeImageAsync(
        string imagePath,
        string prompt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_baseUrl) || string.IsNullOrWhiteSpace(_model))
        {
            return Error("llama.cppの接続URLまたはモデル名が設定されていません。");
        }

        var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(imagePath, cancellationToken));
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl());
        var body = new
        {
            model = _model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:image/png;base64,{base64}"
                            }
                        }
                    }
                }
            },
            stream = false
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
                return Error($"llama.cpp APIエラー: {(int)response.StatusCode} {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var text = ExtractText(doc.RootElement);
            if (string.IsNullOrWhiteSpace(text))
            {
                return Error("llama.cpp API応答から文字起こし結果を取得できませんでした。");
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
        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return "";
        }

        var first = choices[0];
        if (first.TryGetProperty("message", out var message) &&
            message.TryGetProperty("content", out var content))
        {
            return content.GetString() ?? "";
        }

        if (first.TryGetProperty("text", out var text))
        {
            return text.GetString() ?? "";
        }

        return "";
    }

    private static StringContent CreateJsonContent(object body)
    {
        var json = JsonSerializer.Serialize(body);
        return new StringContent(json, Encoding.UTF8, "application/json");
    }

    private string BuildChatCompletionsUrl()
    {
        return _baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase)
            ? $"{_baseUrl}/chat/completions"
            : $"{_baseUrl}/v1/chat/completions";
    }

    private TranscriptionResult Success(string text) => new()
    {
        IsSuccess = true,
        Text = text,
        ProviderName = "llama.cpp",
        ModelName = _model
    };

    private TranscriptionResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message,
        ProviderName = "llama.cpp",
        ModelName = _model
    };
}
