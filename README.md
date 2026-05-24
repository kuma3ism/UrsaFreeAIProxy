# UrsaFreeAIProxy

Gemini の無料 API キーを複数使って、Continue を無制限（登録するAPIの数に依存）に使い続けるためのローカルプロキシ。

１つのアカウントで複数のプロジェクトを作成してそれぞれAPIキーを発行してください

https://aistudio.google.com/api-keys

---

## これは何？

Continue（VS Code の AI コーディング拡張）は OpenAI 互換の API を話せる。  
このアプリはその口をローカルで受け、Gemini の無料 API に変換して流す。

```
VS Code (Continue) → localhost:8080 → Gemini API (無料)
```

---

## メリット

- **無料で使い続けられる** — Gemini 3.5 Flash の無料枠（15 req/min）をフルに活用
- **複数キーのラウンドロビン** — API キーを複数登録して429を回避、実効スループットを上げられる
- **レート制限を自動管理** — 制限に達したら自動で待機、Continue 側はエラーを気にしなくていい
- **ログで会話の流れが見える** — 何を聞いて何が返ってきたか、分間何回目かが一目でわかる

---

## セットアップ

### 前提

- .NET 8.0 以上
- Gemini API キー（[Google AI Studio](https://aistudio.google.com) で無料取得）

### 1. appsettings.json を編集

```json
{
  "Gemini": {
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

### 2. サーバーを起動

```bash
dotnet run --project src/JeminiLateUse.csproj
```

```
🚀 Server started on http://localhost:8080
📝 Model: gemini-3.5-flash
✅ Ready to serve CONTINUE agent requests
```

### 3. Continue の設定

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
=>=>=> Gemini: 14 messages (stream=True)
💬 User: "UrsaButtonのOnClickをIUrsaButtonActionに差し替えたい..."
📡 Calling Gemini API [3/15 req/min] via key[2](...f82zL3RQ)
✅ Response received via key[2](...f82zL3RQ) (1873ms)
<=<=<= Gemini: 3347 tokens
🤖 Reply: "IUrsaButtonActionを実装するには..."
←←← Continue: stream sent (104 chars)
```

レート制限に達した時：

```
⏳ Rate limit reached (15/15). Waiting for slot...
📡 Calling Gemini API [1/15 req/min] via key[0](...H42CwulQ)
✅ Response received ...
```

429 が返ってきた時（次のキーに自動切替）：

```
⚠️  429 on key[2](...) - switching to next key (1/5)
📡 Calling Gemini API [3/15 req/min] via key[3](...4ULeurRg)
✅ Response received ...
```

---

## レート制限について

Gemini 3.5 Flash 無料枠のデフォルト制限は **15 req/min**。  
`appsettings.json` の `MaxRequestsPerMinute` をモデルの実際の制限に合わせて設定する。

キーを複数登録している場合でも、このプロキシ内のカウンターは**全キー合算**で管理している。  
キーの数を増やしても req/min は上がらないが、429 が 1 本のキーに集中するのを防ぐ効果がある。

---

## トラブルシューティング

**429 エラーが頻発する**  
→ `MaxRequestsPerMinute` を実際の制限より 1〜2 低めに設定する。ローリングウィンドウの境界で弾かれることがある。

**応答が遅い**  
→ レート制限待機中の正常な動作。ログに `⏳ Waiting...` が出ていれば待機中。

**Continue がエラーになる**  
→ サーバーが起動しているか確認。`curl http://localhost:8080/health` でレスポンスが返れば正常。

---

## ライセンス

MIT
