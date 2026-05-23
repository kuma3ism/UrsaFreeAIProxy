# JeminiLateUse

Gemini 3.5 Flash Free版のレート制限（1分5回）に対応したローカルプロバイダー

**CONTINUE から Gemini を無制限に使える！** 🚀

## 📋 概要

このアプリケーションは、Gemini 3.5 Flash Free版を CONTINUE エディタから使用する際に、1分5回のリクエスト制限を超えないようにリクエストを遅延させます。

### 🎯 対応要件
- ✅ CONTINUE統合対応（OpenAI互換API）
- ✅ APIキー設定機能（環境変数 or 設定ファイル）
- ✅ モデル指定機能
- ✅ レート制限自動管理（1分5回）
- ✅ HTTP サーバー（ローカル）
- ✅ 詳細ログ出力
- ✅ ユニットテスト実装

---

## 🚀 クイックスタート

### 1️⃣ 前提条件
- .NET 8.0 以上がインストール済み
- Gemini API キーを取得済み ([Google AI Studio](https://aistudio.google.com))

### 2️⃣ セットアップ

#### 方法A: 環境変数を使用
```bash
export GEMINI_API_KEY="your-gemini-api-key-here"
export GEMINI_MODEL="gemini-1.5-flash"  # Optional
export SERVER_PORT=8080                  # Optional
```

#### 方法B: 設定ファイルを使用（推奨）

`appsettings.json` を編集：
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "Gemini": {
    "ApiKey": "your-gemini-api-key-here",
    "Model": "gemini-1.5-flash",
    "MaxRequestsPerMinute": 5
  },
  "Server": {
    "Port": 8080
  }
}
```

### 3️⃣ ビルド

```bash
# リポジトリをクローン
git clone https://github.com/kuma3ism/JeminiLateUse.git
cd JeminiLateUse

# ビルド
dotnet build
```

### 4️⃣ 実行

```bash
dotnet run --project src/JeminiLateUse.csproj
```

サーバーが起動します：
```
🚀 Server started on http://localhost:8080
📝 Model: gemini-1.5-flash
✅ Ready to serve CONTINUE agent requests
```

---

## 🔌 CONTINUE 統合（推奨方法）

### CONTINUE 設定ファイルを編集

ファイル: `~/.continue/config.json`（または `~/.continuerc.json`）

```json
{
  "models": [
    {
      "title": "Gemini Flash (Local)",
      "provider": "openai",
      "apiBase": "http://localhost:8080/v1",
      "apiKey": "dummy"
    }
  ]
}
```

### 使い方
1. JeminiLateUse サーバーを起動したままにする
2. CONTINUE で「Gemini Flash (Local)」を選択
3. エージェント機能を利用可能

---

## 📡 API エンドポイント

### OpenAI互換エンドポイント（推奨）

#### POST `/v1/chat/completions`

CONTINUE で使用するメインエンドポイント

**リクエスト:**
```bash
curl -X POST http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {
        "role": "user",
        "content": "こんにちは"
      }
    ]
  }'
```

**レスポンス:**
```json
{
  "id": "chatcmpl-xyz",
  "object": "chat.completion",
  "created": 1716550000,
  "model": "gemini-1.5-flash",
  "choices": [
    {
      "index": 0,
      "message": {
        "role": "assistant",
        "content": "こんにちは！何かお手伝いできることはありますか？"
      },
      "finish_reason": "stop"
    }
  ],
  "usage": {
    "prompt_tokens": 5,
    "completion_tokens": 15,
    "total_tokens": 20
  }
}
```

### カスタムエンドポイント

#### POST `/chat`（後方互換性）

**リクエスト:**
```bash
curl -X POST http://localhost:8080/chat \
  -H "Content-Type: application/json" \
  -d '{"message": "こんにちは"}'
```

**レスポンス:**
```json
{
  "response": "こんにちは！何かお手伝いできることはありますか？",
  "rateLimit": 2
}
```

### ユーティリティエンドポイント

#### GET `/health`
サーバーの状態確認

```bash
curl http://localhost:8080/health
```

**レスポンス:**
```json
{
  "status": "ok",
  "model": "gemini-1.5-flash"
}
```

#### GET `/v1/models`
利用可能なモデル一覧

```bash
curl http://localhost:8080/v1/models
```

**レスポンス:**
```json
{
  "object": "list",
  "data": [
    {
      "id": "gemini-1.5-flash",
      "object": "model",
      "owned_by": "google",
      "permission": []
    }
  ]
}
```

---

## ⚙️ 設定オプション

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",  // Debug | Information | Warning | Error
      "Microsoft": "Warning"
    }
  },
  "Gemini": {
    "ApiKey": "your-api-key",              // 必須
    "Model": "gemini-1.5-flash",           // Optional
    "MaxRequestsPerMinute": 5              // Optional (default: 5)
  },
  "Server": {
    "Port": 8080                           // Optional (default: 8080)
  }
}
```

### 環境変数の優先度

1. 環境変数 > 2. appsettings.json > 3. デフォルト値

```bash
# 環境変数で上書き
GEMINI_API_KEY="override-key" dotnet run --project src/JeminiLateUse.csproj
```

---

## 🧪 テスト

ユニットテストを実行：

```bash
# 全テスト実行
dotnet test tests/JeminiLateUse.Tests.csproj

# 特定のテストのみ実行
dotnet test tests/JeminiLateUse.Tests.csproj --filter RateLimiterTests

# 詳細出力
dotnet test tests/JeminiLateUse.Tests.csproj -v normal
```

### テスト内容
- **RateLimiterTests**: レート制限ロジックの検証
- **GeminiConfigTests**: 設定バリデーションの検証
- **ConfigurationManagerTests**: 設定ファイル読み込みの検証

---

## 📊 レート制限の仕組み

```
┌─────────────────────────────────────────┐
│ 1分間に最大5回のリクエスト               │
└─────────────────────────────────────────┘

リクエスト1 ✅ (0ms)
リクエスト2 ✅ (0ms)
リクエスト3 ✅ (0ms)
リクエスト4 ✅ (0ms)
リクエスト5 ✅ (0ms)
リクエスト6 ⏳ (60秒待機) → リクエスト1が1分経過後に実行 ✅
```

実装の詳細：
- 各リクエストのタイムスタンプを記録
- 1分以内のリクエスト数をカウント
- 制限に達した場合、古いリクエストが1分経過するまで待機
- 自動的に古いタイムスタンプを削除

---

## 🎨 ログ出力

### ログレベル

- **Debug** 🔍：詳細情報（開発時）
- **Information** ℹ️：通常動作ログ
- **Warning** ⚠️：注意情報
- **Error** ❌：エラーメッセージ

### 開発環境でデバッグログを有効化

```bash
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/JeminiLateUse.csproj
```

### ログ例

```
[2024-05-23 11:58:34.123] ℹ️ [Startup] 🚀 JeminiLateUse starting...
[2024-05-23 11:58:34.456] ℹ️ [Startup] ✅ Configuration loaded
[2024-05-23 11:58:34.789] ℹ️ [Startup] 📝 Model: gemini-1.5-flash
[2024-05-23 11:58:35.012] 🔍 [GeminiProvider] Sending message: こんにちは...
[2024-05-23 11:58:35.234] 🔍 [GeminiProvider] Rate limit status: 1/5
[2024-05-23 11:58:36.567] ℹ️ [GeminiProvider] Gemini API call successful (1333ms)
```

---

## 🏗️ アーキテクチャ

```
src/
├── Program.cs                        # エントリーポイント
├── Config/
│   ├── GeminiConfig.cs              # Gemini設定クラス
│   ├── AppSettings.cs               # アプリケーション設定
│   └── ConfigurationManager.cs      # 設定管理
├── Logging/
│   └── LoggerFactory.cs             # ログ出力管理
├── RateLimit/
│   └── RateLimiter.cs               # レート制限実装
├── Provider/
│   └── GeminiProvider.cs            # Gemini API通信
└── Server/
    └── ContinueIntegrationServer.cs # HTTPサーバー

tests/
├── Config/
│   ├── GeminiConfigTests.cs
│   └── ConfigurationManagerTests.cs
└── RateLimiter/
    └── RateLimiterTests.cs
```

---

## 🔧 トラブルシューティング

### エラー: "GEMINI_API_KEY is not set"
**原因**: APIキーが設定されていない

**解決方法:**
```bash
# 方法1: 環境変数で設定
export GEMINI_API_KEY="your-key"

# 方法2: appsettings.jsonに設定
# "Gemini": { "ApiKey": "your-key" }
```

### エラー: "Failed to call Gemini API"
**原因**: 
- APIキーが無効
- ネットワーク接続がない
- Gemini APIが利用不可

**解決方法:**
- APIキーを確認
- インターネット接続を確認
- ログレベルを Debug に設定して詳細を確認

### パフォーマンス問題
**症状**: リクエストが遅い

**原因**: レート制限に達している（正常な動作）

**解決方法:**
- リクエスト間隔を広げる
- 複数の Gemini モデルキーで複数サーバーを起動

---

## 📝 ライセンス

MIT License

---

## 🤝 貢献

バグ報告や機能提案は GitHub Issues で！

---

## 💡 使用例

### 例1: CONTINUE で コード生成

1. サーバーを起動
2. CONTINUE で「Gemini Flash (Local)」を選択
3. エージェント機能を使用してコード生成を依頼

```
ユーザー: "C# で Fibonacci 関数を書いて"
Gemini: "以下は Fibonacci 関数です：
public int Fibonacci(int n) { ... }"
```

### 例2: cURL でテスト

```bash
# サーバー起動
dotnet run --project src/JeminiLateUse.csproj

# 別のターミナルでテスト
curl -X POST http://localhost:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "messages": [
      {"role": "user", "content": "Hello!"}
    ]
  }'
```

---

**Happy coding! 🎉**
