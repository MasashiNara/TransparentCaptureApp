namespace TransparentCaptureApp.Services.Llm;

public sealed class LlamaCppServerStatus
{
    public bool IsReachable { get; init; }
    public bool SupportsVision { get; init; }
    public string Message { get; init; } = "";
}
