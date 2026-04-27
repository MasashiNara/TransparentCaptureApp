using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using TransparentCaptureApp.Models;
using TransparentCaptureApp.Services;
using TransparentCaptureApp.Services.Llm;
using TransparentCaptureApp.Utilities;

namespace TransparentCaptureApp;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService = new();
    private readonly SecretService _secretService = new();
    private readonly FileService _fileService = new();
    private readonly CaptureService _captureService = new();
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(180)
    };

    private AppSettings _settings;
    private LogService _logService;
    private bool _isCapturing;

    public MainWindow()
    {
        InitializeComponent();
        _settings = _settingsService.Load();
        _logService = new LogService(_settings.LogFilePath);
        _logService.Info("Application started.");
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(_settings, _secretService)
        {
            Owner = this
        };

        if (window.ShowDialog() == true)
        {
            _settings = window.Settings;
            _settingsService.Save(_settings);
            _logService.SetLogFilePath(_settings.LogFilePath);
            _logService.Info("Settings saved.");
            StatusTextBlock.Text = "設定を保存しました";
        }
    }

    private async void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCapturing)
        {
            return;
        }

        await CaptureAndTranscribeAsync();
    }

    private void LogButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            PathUtility.EnsureParentDirectory(_settings.LogFilePath);
            if (!File.Exists(_settings.LogFilePath))
            {
                File.WriteAllText(_settings.LogFilePath, "");
            }

            _fileService.OpenFile(_settings.LogFilePath);
        }
        catch (Exception ex)
        {
            _logService.Error(ex, "Failed to open log file");
            ShowError("ログファイルを開けませんでした。");
        }
    }

    private void OpenSaveDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _fileService.OpenDirectory(_settings.SaveDirectory);
        }
        catch (Exception ex)
        {
            _logService.Error(ex, "Failed to open save directory");
            ShowError("保存先フォルダを開けませんでした。");
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task CaptureAndTranscribeAsync()
    {
        _isCapturing = true;
        CaptureButton.IsEnabled = false;
        SettingsButton.IsEnabled = false;
        StatusTextBlock.Text = "キャプチャ中...";

        string? imagePath = null;
        try
        {
            _settings = _settingsService.Load();
            _logService.SetLogFilePath(_settings.LogFilePath);
            _fileService.EnsureDirectory(_settings.SaveDirectory);
            _logService.Info("Capture started.");

            var rect = WindowCoordinateUtility.ToDeviceRect(this, CaptureArea);
            imagePath = _fileService.CreateUniquePath(_settings.SaveDirectory, "capture", ".png", DateTime.Now);

            Hide();
            await Task.Delay(120);
            var captureResult = _captureService.CaptureToPng(rect, imagePath);
            Show();
            Activate();

            _logService.Info($"Capture image saved: {captureResult.ImagePath} ({captureResult.X},{captureResult.Y},{captureResult.Width},{captureResult.Height})");

            StatusTextBlock.Text = "文字起こし中...";
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(180));
            var factory = new LlmClientFactory(_secretService, _httpClient);
            var client = factory.Create(_settings);
            var result = await client.TranscribeImageAsync(imagePath, _settings.TranscriptionPrompt, cancellationTokenSource.Token);

            if (!result.IsSuccess)
            {
                _logService.Error($"LLM request failed: {result.ErrorMessage}");
                ShowError("文字起こしに失敗しました。ログを確認してください。");
                StatusTextBlock.Text = "エラー";
                return;
            }

            _logService.Info($"LLM response succeeded: {result.ProviderName} {result.ModelName}");

            if (_settings.SaveTranscriptText)
            {
                var transcriptPath = _fileService.CreateUniquePath(_settings.SaveDirectory, "transcript", ".txt", DateTime.Now);
                _fileService.SaveText(transcriptPath, result.Text);
                _logService.Info($"Transcript saved: {transcriptPath}");

                if (_settings.OpenTranscriptAfterSave)
                {
                    _fileService.OpenFile(transcriptPath);
                }
            }

            var shouldDeleteImage =
                (!_settings.SaveCaptureImage || _settings.DeleteImageAfterSuccessfulTranscription) &&
                File.Exists(imagePath);
            if (shouldDeleteImage)
            {
                File.Delete(imagePath);
                _logService.Info($"Capture image deleted: {imagePath}");
            }

            StatusTextBlock.Text = "保存しました";
        }
        catch (OperationCanceledException ex)
        {
            Show();
            Activate();
            _logService.Error(ex, "Capture or transcription timed out");
            ShowError("文字起こしがタイムアウトしました。");
            StatusTextBlock.Text = "エラー";
        }
        catch (Exception ex)
        {
            Show();
            Activate();
            _logService.Error(ex, "Capture failed");
            ShowError("キャプチャまたは文字起こしに失敗しました。");
            StatusTextBlock.Text = "エラー";
        }
        finally
        {
            if (!IsVisible)
            {
                Show();
            }

            _isCapturing = false;
            CaptureButton.IsEnabled = true;
            SettingsButton.IsEnabled = true;
        }
    }

    private void ShowError(string message)
    {
        System.Windows.MessageBox.Show(this, message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
