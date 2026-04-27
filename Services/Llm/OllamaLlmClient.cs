using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TransparentCaptureApp.Models;

namespace TransparentCaptureApp.Services.Llm;

public sealed class OllamaLlmClient : ILlmClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string _model;

    public OllamaLlmClient(HttpClient httpClient, string baseUrl, string model)
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
            return Error("Ollamaの接続URLまたはモデル名が設定されていません。");
        }

        var base64 = Convert.ToBase64String(await File.ReadAllBytesAsync(imagePath, cancellationToken));
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/generate");
        var body = new
        {
            model = _model,
            prompt,
            images = new[] { base64 },
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
                return Error($"Ollama APIエラー: {(int)response.StatusCode} {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var text = doc.RootElement.TryGetProperty("response", out var responseText)
                ? responseText.GetString()
                : "";
            if (string.IsNullOrWhiteSpace(text))
            {
                return Error("Ollama API応答から文字起こし結果を取得できませんでした。");
            }

            return Success(text.Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Error(ex.Message);
        }
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
        ProviderName = "Ollama",
        ModelName = _model
    };

    private TranscriptionResult Error(string message) => new()
    {
        IsSuccess = false,
        ErrorMessage = message,
        ProviderName = "Ollama",
        ModelName = _model
    };
}
