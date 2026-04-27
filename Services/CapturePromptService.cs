using TransparentCaptureApp.Models;

namespace TransparentCaptureApp.Services;

public static class CapturePromptService
{
    public static string BuildPrompt(AppSettings settings, CaptureMode mode)
    {
        var basePrompt = settings.TranscriptionPrompt.Trim();
        return mode switch
        {
            CaptureMode.Transcription => basePrompt,
            CaptureMode.TranslateToJapanese =>
                $"{basePrompt}\n\n" +
                "出力は以下の構成にしてください。\n" +
                "1. [文字起こし] として、画像内の文字を原文のまま文字起こししてください。\n" +
                "2. [日本語訳] として、文字起こし内容を自然な日本語に翻訳してください。\n" +
                "説明や補足は、指定した見出し以外には追加しないでください。",
            CaptureMode.ExplainTerms =>
                $"{basePrompt}\n\n" +
                "出力は以下の構成にしてください。\n" +
                "1. [文字起こし] として、画像内の文字を原文のまま文字起こししてください。\n" +
                "2. [漢字の読み方] として、読みにくい漢字や固有名詞の読み方を一覧にしてください。\n" +
                "3. [難しい単語の説明] として、専門用語、難語、略語を簡潔に説明してください。\n" +
                "該当する項目がない場合は「該当なし」と書いてください。",
            CaptureMode.ExplainImage =>
                $"{basePrompt}\n\n" +
                "出力は以下の構成にしてください。\n" +
                "1. [文字起こし] として、画像内の文字を原文のまま文字起こししてください。\n" +
                "2. [画像の解説] として、文字以外に写っている図、表、画面構成、状態、文脈を簡潔に説明してください。\n" +
                "文字起こしだけでは分からない視覚情報を中心にしてください。",
            _ => basePrompt
        };
    }

    public static string GetFilePrefix(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Transcription => "transcript",
            CaptureMode.TranslateToJapanese => "translate",
            CaptureMode.ExplainTerms => "terms",
            CaptureMode.ExplainImage => "image_explanation",
            _ => "transcript"
        };
    }

    public static string GetDisplayName(CaptureMode mode)
    {
        return mode switch
        {
            CaptureMode.Transcription => "文字起こし",
            CaptureMode.TranslateToJapanese => "日本語に翻訳",
            CaptureMode.ExplainTerms => "語句の解説",
            CaptureMode.ExplainImage => "画像の解説",
            _ => "文字起こし"
        };
    }
}
