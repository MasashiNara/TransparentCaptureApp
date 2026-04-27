# TransparentCaptureApp

Windows 11 用の透明オーバーレイ型キャプチャ文字起こしアプリです。

## 開発環境

- Windows 11
- Visual Studio Community 2022 または .NET SDK
- .NET 8 SDK

## 実行

```powershell
dotnet run --project .\TransparentCaptureApp.csproj
```

## 単体 exe 作成

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

出力先:

```text
bin\Release\net8.0-windows\win-x64\publish\TransparentCaptureApp.exe
```

## LLM プロバイダ

設定画面で以下を選択できます。

- OpenAi
- Anthropic
- Ollama
- LlamaCpp

llama.cpp server を使う場合は、`llama-server` を画像入力対応モデルで起動し、設定画面で接続 URL とモデル名を指定します。

既定値:

```text
URL: http://localhost:8080
Model: llama.cpp
```

llama.cpp server へは OpenAI 互換の `POST /v1/chat/completions` で画像を送信します。

設定画面の `画像対応チェック` ボタンで、llama.cpp server の `GET /props` を呼び出し、`modalities.vision` が `true` か確認できます。
