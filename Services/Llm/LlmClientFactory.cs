using System.Net.Http;
using TransparentCaptureApp.Models;

namespace TransparentCaptureApp.Services.Llm;

public sealed class LlmClientFactory
{
    private readonly SecretService _secretService;
    private readonly HttpClient _httpClient;

    public LlmClientFactory(SecretService secretService, HttpClient httpClient)
    {
        _secretService = secretService;
        _httpClient = httpClient;
    }

    public ILlmClient Create(AppSettings settings)
    {
        return settings.LlmProvider switch
        {
            LlmProviderType.OpenAi => new OpenAiLlmClient(
                _httpClient,
                _secretService.GetSecret(SecretKeys.OpenAiApiKey),
                settings.OpenAiModel),
            LlmProviderType.Anthropic => new AnthropicLlmClient(
                _httpClient,
                _secretService.GetSecret(SecretKeys.AnthropicApiKey),
                settings.AnthropicModel),
            LlmProviderType.Ollama => new OllamaLlmClient(
                _httpClient,
                settings.OllamaUrl,
                settings.OllamaModel),
            LlmProviderType.LlamaCpp => new LlamaCppLlmClient(
                _httpClient,
                settings.LlamaCppUrl,
                settings.LlamaCppModel),
            _ => throw new System.InvalidOperationException("未対応のLLMプロバイダです。")
        };
    }
}
