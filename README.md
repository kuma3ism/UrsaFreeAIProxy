# UrsaFreeAIProxy
Combine multiple free Gemini API keys into one powerful AI endpoint

By registering multiple free API keys, requests are distributed across them in round-robin order. The more keys you add, the longer it takes to hit the rate limit — giving you more headroom for continuous use.

> **Note: This proxy is designed specifically for the Gemini API. Other providers (OpenAI, Anthropic, etc.) are not supported.**

---

## What is this?

Bundles multiple free Gemini API keys into a single OpenAI-compatible endpoint running on `localhost:9090`.

```
Any OpenAI-compatible client → localhost:9090 → Gemini API (free tier)
```

Works with any tool that supports a custom OpenAI API base URL — not just Continue.

---

## Compatible clients

| Client | How to connect |
| ------ | -------------- |
| **Continue** (VS Code) | Set `provider: openai` and `apiBase: http://localhost:9090/v1` in `config.yaml` |
| **Cursor** | Settings → Models → OpenAI-compatible → set base URL to `http://localhost:9090/v1` |
| **Open WebUI** | Add a new OpenAI connection with base URL `http://localhost:9090/v1` |
| **Obsidian Copilot plugin** | Set the API base to `http://localhost:9090/v1` |
| **LangChain / LlamaIndex** | Pass `base_url="http://localhost:9090/v1"` to the OpenAI client |
| **Any OpenAI SDK** | Set `base_url` (Python) or `baseURL` (Node.js) to `http://localhost:9090/v1` |

---

## Features

### Round-robin key rotation

Register multiple Gemini API keys obtained for free from [Google AI Studio](https://aistudio.google.com/api-keys). Each incoming request is handled by the next key in rotation. Adding more keys effectively raises the throughput ceiling — with 3 keys at 15 RPM each, you get up to 45 requests per minute in practice.

### Smart rate limit management

The Gemini free tier enforces a limit such as 15 requests per minute (15 RPM) per key. This proxy tracks usage per key automatically and inserts a wait when a key is about to hit its limit, so the caller never sees a rate limit error under normal conditions. If a 429 is returned anyway, the proxy immediately falls over to the next available key and retries without surfacing the error.

### OpenAI-compatible endpoint

The proxy starts on `localhost:9090` and exposes a `/v1/chat/completions` endpoint that speaks the OpenAI chat format. This means any OpenAI-compatible client can use Gemini transparently, with no special configuration required on the client side.

---

## Setup

### Prerequisites

- .NET 8.0 or later (Windows / Mac)
- Gemini API key(s) — get them for free at [Google AI Studio](https://aistudio.google.com)

> ⚠️ **Windows users: Run your terminal as Administrator**  
> On Windows, please launch your terminal with administrator privileges before running the commands below.

### 1. Edit appsettings.json

> ⚠️ **Warning: appsettings.json is not included in .gitignore by default.**  
> Be careful not to commit this file to your repository as it contains API keys.  
> Use environment variables (see below) as an alternative, or add `appsettings.json` to `.gitignore`.

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
    "Port": 9090
  }
}
```

A single key works fine. Multiple keys are distributed via round-robin.

### 2. Start the server

```bash
dotnet run --project src/UrsaFreeAIProxy.csproj
```

On startup, you will see output like this:

```
🚀 UrsaFreeAIProxy starting...
✅ Configuration loaded
   Model: gemini-3.5-flash
   API Keys: 2 key(s) loaded
   key[0]: ...CwulQ
   key[1]: ...XUD4
   Rate Limit: 15 requests/minute/key
   Effective Limit: 30 requests/minute
   Server Port: 9090
🚀 Server started on http://localhost:9090
📝 Model: gemini-3.5-flash
✅ Ready to serve CONTINUE agent requests
```

#### Options

| Option          | Description                                                    |
| --------------- | -------------------------------------------------------------- |
| `--test`        | Sends one test request per key to verify connectivity          |
| `--debug`       | Enables debug logging (shows detailed request information)     |
| `--port <num>`  | Start on a specific port (e.g. `--port 9090`)                  |

```bash
# Verify connectivity
dotnet run --project src/UrsaFreeAIProxy.csproj -- --test

# Start in debug mode
dotnet run --project src/UrsaFreeAIProxy.csproj -- --debug
```

`--test` sends one test request to each API key and reports success / 429 / failure per key, along with the estimated req/min (`number of keys × MaxRequestsPerMinute`).

### 3. Configure your client

#### Continue

Add the following to your Continue config file (`config.yaml`):

```yaml
models:
  - name: Gemini Flash (Local)
    provider: openai
    apiBase: http://localhost:9090/v1
    apiKey: dummy
    model: dummy
```

---

## Reading the logs

Normal operation:

```
→→→ Continue: POST /v1/chat/completions
=>=>=> [gemini-3.5-flash]: 14 messages (stream=True)
💬 User: "I want to replace UrsaButton's OnClick with IUrsaButtonAction..."
📡 [gemini-3.5-flash] [3/15 req/min] via key[2](...f82zL3RQ)
✅ [gemini-3.5-flash] response via key[2](...f82zL3RQ) (1873ms)
<=<=<= [gemini-3.5-flash]: 3347 tokens
🤖 Reply: "To implement IUrsaButtonAction..."
←←← Continue: stream sent (104 chars)
```

When rate limit is reached:

```
⏳ Rate limit reached for key[0](...H42CwulQ) (15/15). Waiting for slot...
📡 [gemini-3.5-flash] [1/15 req/min] via key[0](...H42CwulQ)
✅ [gemini-3.5-flash] response via key[0](...H42CwulQ) (923ms)
```

When a 429 is returned (auto-switching to the next key):

```
⚠️  429 on key[2](...) - switching to next key (1/5)
📡 [gemini-3.5-flash] [3/15 req/min] via key[3](...4ULeurRg)
✅ [gemini-3.5-flash] response via key[3](...4ULeurRg) (1102ms)
```

---

## Rate limiting

The default free tier limit for Gemini Flash is **15 req/min**.  
Set `MaxRequestsPerMinute` in `appsettings.json` to match the actual limit of the model you are using.

When multiple keys are registered, this proxy tracks the request count **per key** independently.

If a key returns 429, the proxy immediately switches to the next key without waiting.  
If all keys return 429, the proxy returns `500`.

---

## Endpoints

| Method | Path                   | Description                                      |
| ------ | ---------------------- | ------------------------------------------------ |
| `GET`  | `/health`              | Health check. Returns `{"status":"ok"}`          |
| `GET`  | `/v1/models`           | List available models (for Continue)             |
| `POST` | `/v1/chat/completions` | OpenAI-compatible chat endpoint (main)           |
| `POST` | `/chat`                | Simple chat endpoint (legacy)                    |

---

## Troubleshooting

**Frequent 429 errors**  
→ Set `MaxRequestsPerMinute` 1–2 lower than the actual limit. Requests near the rolling window boundary can still get rejected.

**Slow responses**  
→ Normal behavior when waiting for a rate limit slot. If you see `⏳ Waiting...` in the logs, the proxy is holding until a slot opens.

**Continue shows an error**  
→ Make sure the server is running. Run `curl http://localhost:9090/health` — if it responds, the server is up.

**`--test` shows all keys failing (DAILY_FREE_TIER)**  
→ The daily free tier quota has been exhausted. This quota is sometimes shared across projects under the same Google account. Wait until the next day or add keys from a different account.

---

## License

MIT

Copyright (c) 2026 kuma3ism
