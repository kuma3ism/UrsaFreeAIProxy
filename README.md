# UrsaFreeAIProxy

Gemini の無料 API キーを複数登録して利用制限を分散するローカルプロキシ。  
制限に達すると自動で待機してエラーを回避し、タスクを完了させる。  
Continue（VS Code の AI コーディング拡張）からの接続を想定しています。

---

## これは何？

複数の無料APIキーを束ね、Continueからは一つのAIを扱ってるように振舞います

```
VS Code (Continue) → localhost:8080 → Gemini API (無料)
```

---

## メリット
- **複数APIキーのラウンドロビン** — 複数のAPIキーを順番に使用します
- **レート制限を自動管理** — 制限に達したら自動で待機(15rpmが回復するまで60秒待機)
---

## セットアップ

### 前提

- .NET 8.0 以上（Windows、Mac）
- Gemini API キー（[Google AI Studio](https://aistudio.google.com) で無料取得）

### 1. appsettings.json を編集

> ⚠️ **注意: appsettings.json はデフォルトで .gitignore に含まれていません。**  
> API キーを含むこのファイルをリポジトリにコミットしないよう注意してください。  
> 環境変数（後述）で代替するか、`.gitignore` に `appsettings.json` を追加することを推奨します。

```json
{
  "Ai": {
    "ApiKeys": [
      "your-gemini-api-key-1",
      "your-gemini-api-key-2"
    ],
    "Model": "gemini-3.5-flash",
    "MaxRequestsPerMinute": 15
  },
  "Server": {
    "Port": 8080
  }
}
```

キーは 1 本でも動く。複数登録するとラウンドロビンで分散する。

### 2. 環境変数で設定する（オプション）

appsettings.json の代わりに環境変数でも設定できる。ファイルに書きたくない場合はこちらを使う。

| 環境変数                        | 内容                           | デフォルト          |
| ------------------------------- | ------------------------------ | ------------------- |
| `GEMINI_API_KEY`                | API キー（1 本のみ）           | —                   |
| `GEMINI_MODEL`                  | 使用するモデル名               | `gemini-1.5-flash`  |
| `GEMINI_MAX_REQUESTS_PER_MINUTE`| 1 キーあたりの上限 req/min     | `5`                 |
| `SERVER_PORT`                   | サーバーのポート番号           | `8080`              |

環境変数が設定されている場合、appsettings.json より優先される。`GEMINI_API_KEY` は appsettings.json の `ApiKeys` リストの先頭に追加される。

### 3. サーバーを起動
Windowsの場合は管理者権限が必要です

```bash
dotnet run --project src/UrsaFreeAIProxy.csproj
```

起動すると以下のような出力が表示される：

```
🚀 UrsaFreeAIProxy starting...
✅ Configuration loaded
   Model: gemini-2.0-flash
   API Keys: 2 key(s) loaded
   key[0]: ...CwulQ
   key[1]: ...XUD4
   Rate Limit: 15 requests/minute/key
   Effective Limit: 30 requests/minute
   Server Port: 8080
🚀 Server started on http://localhost:8080
📝 Model: gemini-2.0-flash
✅ Ready to serve CONTINUE agent requests
```

#### オプション

| オプション        | 内容                                            |
| ----------------- | ----------------------------------------------- |
| `--test`          | 各キーに 1 回ずつテストリクエストを送って疎通確認 |
| `--debug`         | デバッグログを有効化（詳細なリクエスト情報を表示）|
| `--port <番号>`   | ポートを指定して起動（例: `--port 9090`）        |

```bash
# 疎通確認
dotnet run --project src/UrsaFreeAIProxy.csproj -- --test

# デバッグモードで起動
dotnet run --project src/UrsaFreeAIProxy.csproj -- --debug
```

`--test` は各 API キーに 1 回ずつテストリクエストを送り、キーごとの成功 / 429 / 失敗と、`キー数 × MaxRequestsPerMinute` の見込み req/min を表示する。

### 4. Continue の設定

Continue の設定ファイル（`config.yaml`）に追加：

```yaml
models:
  - name: Gemini Flash (Local)
    provider: openai
    apiBase: http://localhost:8080/v1
    apiKey: dummy
    model: dummy
```

---

## ログの見方

通常時：

```
→→→ Continue: POST /v1/chat/completions
=>=>=> [gemini-2.0-flash]: 14 messages (stream=True)
💬 User: "UrsaButtonのOnClickをIUrsaButtonActionに差し替えたい..."
📡 [gemini-2.0-flash] [3/15 req/min] via key[2](...f82zL3RQ)
✅ [gemini-2.0-flash] response via key[2](...f82zL3RQ) (1873ms)
<=<=<= [gemini-2.0-flash]: 3347 tokens
🤖 Reply: "IUrsaButtonActionを実装するには..."
←←← Continue: stream sent (104 chars)
```

レート制限に達した時：

```
⏳ Rate limit reached for key[0](...H42CwulQ) (15/15). Waiting for slot...
📡 [gemini-2.0-flash] [1/15 req/min] via key[0](...H42CwulQ)
✅ [gemini-2.0-flash] response via key[0](...H42CwulQ) (923ms)
```

429 が返ってきた時（次のキーに自動切替）：

```
⚠️  429 on key[2](...) - switching to next key (1/5)
📡 [gemini-2.0-flash] [3/15 req/min] via key[3](...4ULeurRg)
✅ [gemini-2.0-flash] response via key[3](...4ULeurRg) (1102ms)
```

---

## レート制限について

Gemini Flash 無料枠のデフォルト制限は **15 req/min**。  
`appsettings.json` の `MaxRequestsPerMinute` をモデルの実際の制限に合わせて設定する。

キーを複数登録している場合、このプロキシ内のカウンターは**キーごと**に管理している。

1 本のキーが 429 を返した場合は、待機せずに次のキーへ切り替えて再試行する。  
全キーが 429 を返した場合は `500` を返す。

---

## エンドポイント

| メソッド | パス                   | 説明                                   |
| -------- | ---------------------- | -------------------------------------- |
| `GET`    | `/health`              | サーバー死活確認。`{"status":"ok"}` を返す |
| `GET`    | `/v1/models`           | 利用可能なモデル一覧（Continue 向け）  |
| `POST`   | `/v1/chat/completions` | OpenAI 互換チャット（Continue メイン） |
| `POST`   | `/chat`                | シンプルなチャット（後方互換用）       |

---

## トラブルシューティング

**429 エラーが頻発する**  
→ `MaxRequestsPerMinute` を実際の制限より 1〜2 低めに設定する。ローリングウィンドウの境界で弾かれることがある。

**応答が遅い**  
→ レート制限待機中の正常な動作。ログに `⏳ Waiting...` が出ていれば待機中。

**Continue がエラーになる**  
→ サーバーが起動しているか確認。`curl http://localhost:8080/health` でレスポンスが返れば正常。

**`--test` でキーが全滅 (DAILY_FREE_TIER)**  
→ 無料枠の日次クォータを使い切っている。同じ Google アカウントのプロジェクト間で共有されることがあるため、翌日まで待つか別アカウントのキーを追加する。

---

## ライセンス

MIT
