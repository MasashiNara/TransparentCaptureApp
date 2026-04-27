using System.Drawing;
using System.Drawing.Imaging;
using System.Windows;
using TransparentCaptureApp.Models;

namespace TransparentCaptureApp.Services;

public sealed class CaptureService
{
    public CaptureResult CaptureToPng(Int32Rect rect, string imagePath)
    {
        using var bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(rect.X, rect.Y, 0, 0, new System.Drawing.Size(rect.Width, rect.Height), CopyPixelOperation.SourceCopy);
        bitmap.Save(imagePath, ImageFormat.Png);

        return new CaptureResult
        {
            ImagePath = imagePath,
            X = rect.X,
            Y = rect.Y,
            Width = rect.Width,
            Height = rect.Height
        };
    }
}
