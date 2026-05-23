# JeminiLateUse

Gemini 3.5 Flash Free版のレート制限（1分5回）に対応したプロバイダー

## 概要

このアプリケーションは、Gemini 3.5 Flash Free版をCONTINUEエディタから使用する際に、1分5回のリクエスト制限を超えないようにリクエストを遅延させます。

## 機能

- ✅ CONTINUE統合対応
- ✅ APIキー設定機能
- ✅ モデル指定機能
- ✅ レート制限自動管理（1分5回）
- ✅ HTTP サーバー（ローカル）

## セットアップ

### 環境変数

```bash
export GEMINI_API_KEY="your-api-key-here"
export GEMINI_MODEL="gemini-1.5-flash"  # Optional, defaults to gemini-1.5-flash
export SERVER_PORT=8080                   # Optional, defaults to 8080
```

### ビルド

```bash
dotnet build
```

### 実行

```bash
dotnet run --project src/JeminiLateUse.csproj
```

サーバーは `http://localhost:8080` で起動します。

## API エンドポイント

### POST /chat

メッセージを送信してGemini APIから回答を取得します。

**リクエスト:**
```json
{
  "message": "こんにちは"
}
```

**レスポンス:**
```json
{
  "response": "こんにちは！何かお手伝いできることはありますか？",
  "rateLimit": 2
}
```

### GET /health

サーバーの状態確認

**レスポンス:**
```json
{
  "status": "ok"
}
```

## CONTINUE 統合

CONTINUE設定ファイル（`~/.continue/config.json`）に以下を追加：

```json
{
  "models": [
    {
      "title": "Gemini Flash (Local)",
      "provider": "openai",
      "model": "gpt-3.5-turbo",
      "apiBase": "http://localhost:8080",
      "apiKey": "dummy"
    }
  ]
}
```

## レート制限の仕組み

- リクエストを送信するたびにタイムスタンプを記録
- 1分間のリクエスト数が5回に達した場合、次のリクエストは自動的に待機
- 古いリクエストが1分経過したら、自動的に削除

## 実装詳細

- **RateLimiter**: リクエスト制限を管理するコアロジック
- **GeminiProvider**: Gemini APIとの通信を行う
- **ContinueIntegrationServer**: CONTINUEからのリクエストを受け取るHTTPサーバー
- **GeminiConfig**: 設定管理

## ライセンス

MIT
