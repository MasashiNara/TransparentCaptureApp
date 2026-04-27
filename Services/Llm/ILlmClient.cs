using System.Threading;
using System.Threading.Tasks;
using TransparentCaptureApp.Models;

namespace TransparentCaptureApp.Services.Llm;

public interface ILlmClient
{
    Task<TranscriptionResult> TranscribeImageAsync(
        string imagePath,
        string prompt,
        CancellationToken cancellationToken);
}
