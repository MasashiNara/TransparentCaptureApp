namespace TransparentCaptureApp.Models;

public sealed class TranscriptionResult
{
    public bool IsSuccess { get; set; }
    public string Text { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
    public string ProviderName { get; set; } = "";
    public string ModelName { get; set; } = "";
}
