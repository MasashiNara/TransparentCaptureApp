using System;
using System.IO;

namespace TransparentCaptureApp.Models;

public sealed class AppSettings
{
    public string SaveDirectory { get; set; } = GetDefaultSaveDirectory();
    public string LogFilePath { get; set; } = Path.Combine(GetDefaultSaveDirectory(), "app.log.txt");
    public LlmProviderType LlmProvider { get; set; } = LlmProviderType.OpenAi;
    public string OpenAiModel { get; set; } = "gpt-5.4-mini";
    public string AnthropicModel { get; set; } = "claude-sonnet-4-20250514";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string OllamaModel { get; set; } = "gemma3";
    public string LlamaCppUrl { get; set; } = "http://localhost:8080";
    public string LlamaCppModel { get; set; } = "llama.cpp";
    public string TranscriptionPrompt { get; set; } =
        "画像内に含まれる文字をできるだけ正確に文字起こししてください。\n" +
        "レイアウトが分かる範囲で改行を維持してください。\n" +
        "説明や補足は追加せず、文字起こし結果のみを返してください。";
    public bool SaveCaptureImage { get; set; } = true;
    public bool DeleteImageAfterSuccessfulTranscription { get; set; }
    public bool SaveTranscriptText { get; set; } = true;
    public bool OpenTranscriptAfterSave { get; set; } = true;

    public static string GetDefaultSaveDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "TransparentCapture");
    }
}
