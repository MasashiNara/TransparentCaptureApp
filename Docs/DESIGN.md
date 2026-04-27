# 透明キャプチャ文字起こしアプリ 詳細設計書

## 1. 前提

本書は `SPEC.md` を実装に落とすための詳細設計を定義する。

初期実装は以下を前提とする。

- OS: Windows 11
- 実装方式: C# / .NET / WPF
- 配布形式: 単体 exe
- LLM プロバイダ: OpenAI API、Anthropic API、Ollama、llama.cpp server
- キャプチャ対象: メインウィンドウの透明領域全体
- ウィンドウ: 常に最前面
- 複数モニター: 初期版から必須対応

## 2. アプリ構成

### 2.1 プロジェクト構成

```text
TransparentCaptureApp
├─ App.xaml
├─ App.xaml.cs
├─ MainWindow.xaml
├─ MainWindow.xaml.cs
├─ SettingsWindow.xaml
├─ SettingsWindow.xaml.cs
├─ Models
│  ├─ AppSettings.cs
│  ├─ CaptureResult.cs
│  ├─ LlmProviderType.cs
│  └─ TranscriptionResult.cs
├─ Services
│  ├─ CaptureService.cs
│  ├─ FileService.cs
│  ├─ LogService.cs
│  ├─ SettingsService.cs
│  ├─ SecretService.cs
│  └─ Llm
│     ├─ ILlmClient.cs
│     ├─ LlmClientFactory.cs
│     ├─ OpenAiLlmClient.cs
│     ├─ AnthropicLlmClient.cs
│     ├─ OllamaLlmClient.cs
│     └─ LlamaCppLlmClient.cs
└─ Utilities
   ├─ DateTimeProvider.cs
   ├─ PathUtility.cs
   └─ WindowCoordinateUtility.cs
```

### 2.2 レイヤー方針

- UI 層は WPF Window とイベントハンドラを担当する。
- キャプチャ、保存、ログ、設定、LLM 通信は Service 層に分離する。
- LLM プロバイダごとの差分は `ILlmClient` 実装に閉じ込める。
- API キーなどの秘密情報は `SecretService` で保存・復元する。

## 3. 画面設計

### 3.1 メインウィンドウ

#### 3.1.1 表示仕様

メインウィンドウは透明オーバーレイとして表示する。

- 枠線あり
- 上部に操作バーあり
- 操作バー以外の内部領域は透明
- 常に最前面
- リサイズ可能
- 移動可能

初期サイズ:

- 幅: 900 px
- 高さ: 500 px

最小サイズ:

- 幅: 320 px
- 高さ: 180 px

操作バー高さ:

- 40 px

枠線:

- 2 px

#### 3.1.2 ボタン

操作バーに以下のボタンを配置する。

| ボタン | 処理 |
| --- | --- |
| 設定 | 設定画面を開く |
| キャプチャ | 透明領域をキャプチャし、文字起こしを実行する |
| ログ | ログファイルを既定アプリで開く |
| 保存先を開く | 保存先フォルダを Explorer で開く |

#### 3.1.3 キャプチャ中の UI

キャプチャ処理中は以下の状態にする。

- キャプチャボタンを無効化する。
- 操作バーに処理中ステータスを表示する。
- UI スレッドをブロックしない。

処理中ステータス例:

- `キャプチャ中...`
- `文字起こし中...`
- `保存しました`
- `エラー`

### 3.2 設定画面

#### 3.2.1 表示項目

設定画面には以下を配置する。

| 項目 | 入力形式 | 初期値 |
| --- | --- | --- |
| 保存先フォルダ | テキストボックス + 参照ボタン | `Documents\TransparentCapture` |
| LLM プロバイダ | コンボボックス | `OpenAI` |
| OpenAI API キー | パスワード入力 | 空 |
| OpenAI モデル名 | テキストボックス | `gpt-5.4-mini` |
| Anthropic API キー | パスワード入力 | 空 |
| Anthropic モデル名 | テキストボックス | `claude-sonnet-4-20250514` |
| Ollama 接続 URL | テキストボックス | `http://localhost:11434` |
| Ollama モデル名 | テキストボックス | `gemma3` |
| llama.cpp 接続 URL | テキストボックス | `http://localhost:8080` |
| llama.cpp モデル名 | テキストボックス | `llama.cpp` |
| llama.cpp 画像入力対応チェック | ボタン | - |
| 文字起こしプロンプト | 複数行テキストボックス | 初期プロンプト |
| キャプチャ画像を保存する | チェックボックス | ON |
| 文字起こし成功後に画像を削除する | チェックボックス | OFF |
| テキストファイルを保存する | チェックボックス | ON |
| テキスト保存後に自動で開く | チェックボックス | ON |
| ログファイル保存先 | テキストボックス + 参照ボタン | 保存先フォルダ |

#### 3.2.2 ボタン

| ボタン | 処理 |
| --- | --- |
| 保存 | 入力値を検証して保存し、設定画面を閉じる |
| キャンセル | 変更を破棄して閉じる |
| 画像対応チェック | llama.cpp server の画像入力対応状態を確認する |

#### 3.2.3 入力検証

設定保存時に以下を検証する。

- 保存先フォルダが空でないこと
- ログファイル保存先が空でないこと
- 選択中プロバイダに応じた必須項目が入力されていること
- Ollama 接続 URL が URL として解釈可能であること
- llama.cpp 接続 URL が URL として解釈可能であること
- プロンプトが空でないこと

プロバイダ別の必須項目:

| プロバイダ | 必須項目 |
| --- | --- |
| OpenAI | OpenAI API キー、OpenAI モデル名 |
| Anthropic | Anthropic API キー、Anthropic モデル名 |
| Ollama | Ollama 接続 URL、Ollama モデル名 |
| llama.cpp | llama.cpp 接続 URL、llama.cpp モデル名 |

## 4. 設定データ設計

### 4.1 保存場所

設定ファイルはユーザーごとのアプリデータに保存する。

```text
%AppData%\TransparentCaptureApp\settings.json
```

ログファイルの既定保存先:

```text
Documents\TransparentCapture\app.log.txt
```

### 4.2 AppSettings

```csharp
public sealed class AppSettings
{
    public string SaveDirectory { get; set; }
    public string LogFilePath { get; set; }
    public LlmProviderType LlmProvider { get; set; }
    public string OpenAiModel { get; set; }
    public string AnthropicModel { get; set; }
    public string OllamaUrl { get; set; }
    public string OllamaModel { get; set; }
    public string LlamaCppUrl { get; set; }
    public string LlamaCppModel { get; set; }
    public string TranscriptionPrompt { get; set; }
    public bool SaveCaptureImage { get; set; }
    public bool DeleteImageAfterSuccessfulTranscription { get; set; }
    public bool SaveTranscriptText { get; set; }
    public bool OpenTranscriptAfterSave { get; set; }
}
```

API キーは `settings.json` に直接保存しない。

### 4.3 LlmProviderType

```csharp
public enum LlmProviderType
{
    OpenAi,
    Anthropic,
    Ollama,
    LlamaCpp
}
```

### 4.4 秘密情報

以下は `SecretService` で保存する。

- OpenAI API キー
- Anthropic API キー

初期実装では Windows DPAPI を使い、現在の Windows ユーザーのみ復号できる形で保存する。

保存場所:

```text
%AppData%\TransparentCaptureApp\secrets.dat
```

## 5. ファイル出力設計

### 5.1 出力ファイル名

キャプチャ画像:

```text
capture_yyyyMMdd_HHmmss.png
```

文字起こしテキスト:

```text
transcript_yyyyMMdd_HHmmss.txt
```

同一秒に複数回実行された場合は連番を付ける。

```text
capture_yyyyMMdd_HHmmss_001.png
transcript_yyyyMMdd_HHmmss_001.txt
```

### 5.2 文字コード

文字起こしテキストとログは UTF-8 で保存する。

### 5.3 画像削除

以下をすべて満たす場合、保存済み画像を削除する。

- `SaveCaptureImage` が `false`、または `DeleteImageAfterSuccessfulTranscription` が `true`
- LLM 文字起こしが成功している
- テキスト保存が有効な場合、文字起こしテキスト保存が成功している

LLM 文字起こしまたはテキスト保存に失敗した場合、画像は削除しない。

## 6. キャプチャ設計

### 6.1 キャプチャ範囲

キャプチャ範囲はメインウィンドウ内の透明領域全体とする。

透明領域は以下で計算する。

- 左: ウィンドウ左端 + 枠線幅
- 上: ウィンドウ上端 + 枠線幅 + 操作バー高さ
- 幅: ウィンドウ幅 - 枠線幅 * 2
- 高さ: ウィンドウ高さ - 操作バー高さ - 枠線幅 * 2

WPF の論理ピクセルと実画面ピクセルの差を DPI スケールで補正する。

### 6.2 複数モニター

Windows の仮想スクリーン座標を使用する。

対応条件:

- メインモニター以外にウィンドウがある場合もキャプチャできること
- 左側や上側に配置されたモニターの負の座標に対応すること
- モニターごとの DPI 差異を考慮すること

### 6.3 キャプチャ時のウィンドウ除外

キャプチャ対象にアプリ自身の操作バーや枠線を含めないため、透明領域のみを座標指定してキャプチャする。

透明領域上にマウスカーソルがある場合、カーソルを画像に含めるかは初期版では OS の標準キャプチャ挙動に従う。

## 7. LLM 通信設計

### 7.1 共通インターフェース

```csharp
public interface ILlmClient
{
    Task<TranscriptionResult> TranscribeImageAsync(
        string imagePath,
        string prompt,
        CancellationToken cancellationToken);
}
```

### 7.2 TranscriptionResult

```csharp
public sealed class TranscriptionResult
{
    public bool IsSuccess { get; set; }
    public string Text { get; set; }
    public string ErrorMessage { get; set; }
    public string ProviderName { get; set; }
    public string ModelName { get; set; }
}
```

### 7.3 OpenAI

OpenAI API キーと OpenAI モデル名を使う。

画像は PNG を Base64 化し、画像入力対応の API に送信する。

タイムアウト:

- 既定 120 秒

### 7.4 Anthropic

Anthropic API キーと Anthropic モデル名を使う。

画像は PNG を Base64 化し、画像入力対応の Messages API に送信する。

タイムアウト:

- 既定 120 秒

### 7.5 Ollama

Ollama 接続 URL と Ollama モデル名を使う。

既定 URL:

```text
http://localhost:11434
```

画像は Base64 化し、Ollama の画像入力対応 API に送信する。

タイムアウト:

- 既定 180 秒

### 7.6 llama.cpp server

llama.cpp 接続 URL と llama.cpp モデル名を使う。

既定 URL:

```text
http://localhost:8080
```

画像は PNG を Base64 化し、OpenAI 互換の Chat Completions API に送信する。

送信先:

```text
POST /v1/chat/completions
```

画像入力は `image_url` の data URL 形式を使う。

タイムアウト:

- 既定 180 秒

### 7.7 llama.cpp 画像入力対応チェック

設定画面の「画像対応チェック」ボタン押下時、llama.cpp server の `GET /props` を呼び出す。

モデル名が入力されている場合は、router mode を考慮して以下を優先する。

```text
GET /props?model=(model_name)
```

上記が失敗した場合は以下にフォールバックする。

```text
GET /props
```

応答 JSON の `modalities.vision` が `true` の場合、画像入力対応と判定する。

判定例:

```json
{
  "modalities": {
    "vision": true
  }
}
```

タイムアウト:

- 既定 20 秒

## 8. キャプチャ実行フロー

```text
ユーザーが「キャプチャ」を押す
  ↓
設定を読み込む
  ↓
保存先フォルダを作成または確認
  ↓
透明領域の画面座標を計算
  ↓
スクリーンショットを PNG 保存
  ↓
選択中プロバイダの LLM クライアントを生成
  ↓
画像とプロンプトを送信
  ↓
文字起こし結果を取得
  ↓
TXT 保存
  ↓
必要に応じて TXT を開く
  ↓
設定に応じて PNG を削除
  ↓
ログ出力
```

エラー発生時は以下を行う。

- 処理を中断する。
- 失敗原因をログに出力する。
- ユーザーに短いエラーメッセージを表示する。
- キャプチャ済み画像がある場合、原則削除しない。

## 9. ログ設計

### 9.1 出力形式

1 行 1 イベントで出力する。

```text
yyyy-MM-dd HH:mm:ss.fff [LEVEL] message
```

例:

```text
2026-04-26 21:30:12.123 [INFO] Capture started.
2026-04-26 21:30:13.456 [INFO] Capture image saved: C:\Users\...\capture_20260426_213012.png
2026-04-26 21:30:20.789 [ERROR] LLM request failed: timeout.
```

### 9.2 ログレベル

- INFO
- WARN
- ERROR

### 9.3 ローテーション

初期版ではログローテーションは行わない。

ログファイルが肥大化した場合の対策は後回しとする。

## 10. エラー表示設計

ユーザー向けエラーメッセージは短く表示し、詳細はログに出力する。

| 状況 | 表示メッセージ |
| --- | --- |
| API キー未設定 | `APIキーが設定されていません。設定画面を確認してください。` |
| 保存先に書き込めない | `保存先に書き込めません。保存先設定を確認してください。` |
| キャプチャ失敗 | `キャプチャに失敗しました。` |
| LLM 通信失敗 | `文字起こしに失敗しました。ログを確認してください。` |
| Ollama 接続失敗 | `Ollamaに接続できません。接続URLと起動状態を確認してください。` |
| llama.cpp 接続失敗 | `llama.cppに接続できません。接続URLと起動状態を確認してください。` |

## 11. 単体 exe 配布

初期版は .NET の publish 機能で単体 exe を作成する。

想定コマンド:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```

配布物:

```text
TransparentCaptureApp.exe
```

## 12. テスト観点

### 12.1 画面

- 起動時に透明領域が表示されること
- 操作バーと枠線だけが見えること
- ウィンドウを移動できること
- ウィンドウをリサイズできること
- 常に最前面で表示されること

### 12.2 キャプチャ

- 透明領域だけがキャプチャされること
- 操作バーがキャプチャに含まれないこと
- 枠線がキャプチャに含まれないこと
- メインモニターで正しく保存されること
- サブモニターで正しく保存されること
- 負の座標にあるモニターで正しく保存されること
- DPI スケーリング 100% 以外でも範囲がずれないこと

### 12.3 設定

- 設定保存後、再起動しても復元されること
- API キーが平文で設定ファイルに残らないこと
- プロンプトを編集できること
- テキスト自動表示を ON/OFF できること

### 12.4 LLM

- OpenAI API で文字起こしできること
- Anthropic API で文字起こしできること
- Ollama で文字起こしできること
- llama.cpp server で文字起こしできること
- API キー未設定時に適切なエラーになること
- Ollama 未起動時に適切なエラーになること
- llama.cpp server 未起動時に適切なエラーになること
- llama.cpp 画像入力対応チェックで `modalities.vision` を判定できること

### 12.5 ファイル

- PNG が指定フォルダに保存されること
- TXT が指定フォルダに保存されること
- TXT が UTF-8 で保存されること
- 設定により TXT が自動で開くこと
- 設定により文字起こし成功後に PNG が削除されること
- 「保存先を開く」で Explorer が開くこと
- 「ログ」でログファイルが開くこと

## 13. 後続検討

初期版の完成後に以下を検討する。

- ホットキー対応
- キャプチャ前カウントダウン
- キャプチャ履歴
- アプリ内プレビュー
- アプリ内テキスト表示
- Markdown 出力
- ログローテーション
- インストーラー
- 自動アップデート
