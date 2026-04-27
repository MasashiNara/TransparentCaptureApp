using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using TransparentCaptureApp.Models;
using TransparentCaptureApp.Services;
using TransparentCaptureApp.Services.Llm;
using WinForms = System.Windows.Forms;

namespace TransparentCaptureApp;

public partial class SettingsWindow : Window
{
    private readonly SecretService _secretService;
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public SettingsWindow(AppSettings settings, SecretService secretService)
    {
        InitializeComponent();
        Settings = Clone(settings);
        _secretService = secretService;
        LoadToControls();
    }

    public AppSettings Settings { get; private set; }

    private void LoadToControls()
    {
        SaveDirectoryTextBox.Text = Settings.SaveDirectory;
        SelectProvider(Settings.LlmProvider);
        OpenAiApiKeyPasswordBox.Password = _secretService.GetSecret(SecretKeys.OpenAiApiKey);
        OpenAiModelTextBox.Text = Settings.OpenAiModel;
        AnthropicApiKeyPasswordBox.Password = _secretService.GetSecret(SecretKeys.AnthropicApiKey);
        AnthropicModelTextBox.Text = Settings.AnthropicModel;
        OllamaUrlTextBox.Text = Settings.OllamaUrl;
        OllamaModelTextBox.Text = Settings.OllamaModel;
        LlamaCppUrlTextBox.Text = Settings.LlamaCppUrl;
        LlamaCppModelTextBox.Text = Settings.LlamaCppModel;
        PromptTextBox.Text = Settings.TranscriptionPrompt;
        SaveCaptureImageCheckBox.IsChecked = Settings.SaveCaptureImage;
        DeleteImageAfterSuccessCheckBox.IsChecked = Settings.DeleteImageAfterSuccessfulTranscription;
        SaveTranscriptTextCheckBox.IsChecked = Settings.SaveTranscriptText;
        OpenTranscriptAfterSaveCheckBox.IsChecked = Settings.OpenTranscriptAfterSave;
        LogFilePathTextBox.Text = Settings.LogFilePath;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var candidate = ReadFromControls();
        var validationError = Validate(candidate);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            System.Windows.MessageBox.Show(this, validationError, "設定エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings = candidate;
        _secretService.SetSecret(SecretKeys.OpenAiApiKey, OpenAiApiKeyPasswordBox.Password);
        _secretService.SetSecret(SecretKeys.AnthropicApiKey, AnthropicApiKeyPasswordBox.Password);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BrowseSaveDirectory_Click(object sender, RoutedEventArgs e)
    {
        var selected = SelectFolder(SaveDirectoryTextBox.Text);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SaveDirectoryTextBox.Text = selected;
        }
    }

    private void BrowseLogDirectory_Click(object sender, RoutedEventArgs e)
    {
        var currentDirectory = Path.GetDirectoryName(LogFilePathTextBox.Text);
        var selected = SelectFolder(currentDirectory ?? SaveDirectoryTextBox.Text);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            LogFilePathTextBox.Text = Path.Combine(selected, "app.log.txt");
        }
    }

    private async void CheckLlamaCppVisionButton_Click(object sender, RoutedEventArgs e)
    {
        await CheckLlamaCppVisionSupportAsync();
    }

    private async Task CheckLlamaCppVisionSupportAsync()
    {
        var service = new LlamaCppServerStatusService(_httpClient);
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        try
        {
            var status = await service.CheckVisionSupportAsync(
                LlamaCppUrlTextBox.Text.Trim(),
                LlamaCppModelTextBox.Text.Trim(),
                cancellationTokenSource.Token);

            var icon = status.SupportsVision ? MessageBoxImage.Information : MessageBoxImage.Warning;
            System.Windows.MessageBox.Show(this, status.Message, "llama.cpp画像対応チェック", MessageBoxButton.OK, icon);
        }
        catch (OperationCanceledException)
        {
            System.Windows.MessageBox.Show(
                this,
                "llama.cpp server への接続がタイムアウトしました。",
                "llama.cpp画像対応チェック",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static string SelectFolder(string initialDirectory)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            SelectedPath = Directory.Exists(initialDirectory) ? initialDirectory : AppSettings.GetDefaultSaveDirectory(),
            UseDescriptionForTitle = true,
            Description = "フォルダを選択してください"
        };

        return dialog.ShowDialog() == WinForms.DialogResult.OK ? dialog.SelectedPath : "";
    }

    private AppSettings ReadFromControls()
    {
        return new AppSettings
        {
            SaveDirectory = SaveDirectoryTextBox.Text.Trim(),
            LogFilePath = LogFilePathTextBox.Text.Trim(),
            LlmProvider = ReadProvider(),
            OpenAiModel = OpenAiModelTextBox.Text.Trim(),
            AnthropicModel = AnthropicModelTextBox.Text.Trim(),
            OllamaUrl = OllamaUrlTextBox.Text.Trim(),
            OllamaModel = OllamaModelTextBox.Text.Trim(),
            LlamaCppUrl = LlamaCppUrlTextBox.Text.Trim(),
            LlamaCppModel = LlamaCppModelTextBox.Text.Trim(),
            TranscriptionPrompt = PromptTextBox.Text.Trim(),
            SaveCaptureImage = SaveCaptureImageCheckBox.IsChecked == true,
            DeleteImageAfterSuccessfulTranscription = DeleteImageAfterSuccessCheckBox.IsChecked == true,
            SaveTranscriptText = SaveTranscriptTextCheckBox.IsChecked == true,
            OpenTranscriptAfterSave = OpenTranscriptAfterSaveCheckBox.IsChecked == true
        };
    }

    private string Validate(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.SaveDirectory))
        {
            return "保存先フォルダを入力してください。";
        }

        if (string.IsNullOrWhiteSpace(settings.LogFilePath))
        {
            return "ログファイル保存先を入力してください。";
        }

        if (string.IsNullOrWhiteSpace(settings.TranscriptionPrompt))
        {
            return "文字起こしプロンプトを入力してください。";
        }

        if (settings.LlmProvider == LlmProviderType.OpenAi &&
            (string.IsNullOrWhiteSpace(OpenAiApiKeyPasswordBox.Password) || string.IsNullOrWhiteSpace(settings.OpenAiModel)))
        {
            return "OpenAI APIキーとモデル名を入力してください。";
        }

        if (settings.LlmProvider == LlmProviderType.Anthropic &&
            (string.IsNullOrWhiteSpace(AnthropicApiKeyPasswordBox.Password) || string.IsNullOrWhiteSpace(settings.AnthropicModel)))
        {
            return "Anthropic APIキーとモデル名を入力してください。";
        }

        if (settings.LlmProvider == LlmProviderType.Ollama)
        {
            if (string.IsNullOrWhiteSpace(settings.OllamaModel))
            {
                return "Ollamaモデル名を入力してください。";
            }

            if (!Uri.TryCreate(settings.OllamaUrl, UriKind.Absolute, out _))
            {
                return "Ollama接続URLを正しいURL形式で入力してください。";
            }
        }

        if (settings.LlmProvider == LlmProviderType.LlamaCpp)
        {
            if (string.IsNullOrWhiteSpace(settings.LlamaCppModel))
            {
                return "llama.cppモデル名を入力してください。";
            }

            if (!Uri.TryCreate(settings.LlamaCppUrl, UriKind.Absolute, out _))
            {
                return "llama.cpp接続URLを正しいURL形式で入力してください。";
            }
        }

        return "";
    }

    private void SelectProvider(LlmProviderType provider)
    {
        foreach (var item in ProviderComboBox.Items.OfType<ComboBoxItem>())
        {
            if (string.Equals(item.Content?.ToString(), provider.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                ProviderComboBox.SelectedItem = item;
                return;
            }
        }

        ProviderComboBox.SelectedIndex = 0;
    }

    private LlmProviderType ReadProvider()
    {
        var selected = (ProviderComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
        return Enum.TryParse<LlmProviderType>(selected, out var provider) ? provider : LlmProviderType.OpenAi;
    }

    private static AppSettings Clone(AppSettings settings)
    {
        return new AppSettings
        {
            SaveDirectory = settings.SaveDirectory,
            LogFilePath = settings.LogFilePath,
            LlmProvider = settings.LlmProvider,
            OpenAiModel = settings.OpenAiModel,
            AnthropicModel = settings.AnthropicModel,
            OllamaUrl = settings.OllamaUrl,
            OllamaModel = settings.OllamaModel,
            LlamaCppUrl = settings.LlamaCppUrl,
            LlamaCppModel = settings.LlamaCppModel,
            TranscriptionPrompt = settings.TranscriptionPrompt,
            SaveCaptureImage = settings.SaveCaptureImage,
            DeleteImageAfterSuccessfulTranscription = settings.DeleteImageAfterSuccessfulTranscription,
            SaveTranscriptText = settings.SaveTranscriptText,
            OpenTranscriptAfterSave = settings.OpenTranscriptAfterSave
        };
    }
}
