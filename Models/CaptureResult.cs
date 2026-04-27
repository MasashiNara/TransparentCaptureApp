namespace TransparentCaptureApp.Models;

public sealed class CaptureResult
{
    public required string ImagePath { get; init; }
    public required int X { get; init; }
    public required int Y { get; init; }
    public required int Width { get; init; }
    public required int Height { get; init; }
}
