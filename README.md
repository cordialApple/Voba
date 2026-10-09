# Voba

.NET MAUI recipe demo. Gemma through local Ollama makes meal options and cooking steps. MongoDB stores users, saved recipes, and generation cache. Enrichment adds cost and nutrition from demo data or live Spoonacular.

Original demo:

<img width="800" height="430" alt="Original Voba recipe demo" src="https://github.com/user-attachments/assets/5915231c-eda0-4742-80a8-f7e60dacfbba" />

## Run on Windows

Need .NET 9 MAUI workload, Ollama, `gemma3:4b`, and reachable MongoDB. The backend holds MongoDB, JWT, and provider credentials. The desktop holds only the API URL and user tokens. No secret belongs in Git.

```powershell
ollama pull gemma3:4b
.\scripts\Invoke-VobaDemo.ps1 -Task Seed
.\scripts\Invoke-VobaDemo.ps1 -Task Backend
```

In a second PowerShell window:

```powershell
.\scripts\Invoke-VobaDemo.ps1 -Task App
```

The helper needs the protected Atlas credential file described below. It runs backend and app as separate processes, uses `VobaDemoTests` for seeded dummy accounts, and keeps secrets out of the desktop process. [Seed accounts and test data](Voba.Seed/README.md) list the repeatable dummy login. For a different MongoDB, set `VOBA_MONGO_CONNECTION_STRING`, `VOBA_MONGO_DATABASE`, and a stable Base64 `VOBA_JWT_SECRET` of at least 32 random bytes in the backend process, then start [the backend](Voba.Backend/README.md). Start the desktop with `VOBA_API_BASE_URL` pointing to that backend. Remote API URLs require HTTPS.

Start Ollama server if one not already running. Backend defaults: endpoint `http://localhost:11434`, model `gemma3:4b`, enrichment `fake`. Override with `VOBA_OLLAMA_ENDPOINT` and `VOBA_OLLAMA_MODEL` in the backend process. Unknown enrichment mode fails at startup.

For live cost and nutrition, set `VOBA_ENRICHMENT_MODE=real` and `VOBA_SPOONACULAR_API_KEY`. Live mode makes Spoonacular requests and may consume API quota. When provider fails, cost falls back to Gemma estimate and source label says so. Nutrition may be unavailable. Recipe instructions always come from Gemma.

`VOBA_JWT_SECRET` is required for backend startup. The demo helper makes one stable, protected key outside Git. MongoDB unavailable? Login and sign-up show a backend error.

## Cache and flow

Budget and servings must be positive. Options above total budget or without usable cost stay hidden. Request key includes budget, servings, cuisine, restrictions, model, and prompt version. Full recipe key also includes selected ingredients, price, and nutrition. Repeated options and selected recipe requests skip Gemma and enrichment while cache entry valid. Real source can replace demo source; demo source cannot replace real source, even after real entry expires. Live mode does not reuse demo data. Cost and nutrition source labels appear on recipe cards and saved recipes.

Sign up or log in, generate options, select one, then save it. Backend-owned drafts and authenticated routes keep saved recipes bound to the signed-in user. Sign out revokes the server session and clears local tokens.

## Verify

```powershell
dotnet test Voba.Core.Tests/Voba.Core.Tests.csproj
dotnet test Voba.Persistence.Tests/Voba.Persistence.Tests.csproj
dotnet test Voba.AppData.Tests/Voba.AppData.Tests.csproj
dotnet test Voba.Backend.Tests/Voba.Backend.Tests.csproj
dotnet test Voba.Client.Tests/Voba.Client.Tests.csproj
dotnet test Voba.Seed.Tests/Voba.Seed.Tests.csproj
dotnet build Voba/Voba.csproj -f net9.0-windows10.0.19041.0
```

Run `dotnet run --project Voba.ModelSmoke/Voba.ModelSmoke.csproj` for local Ollama and fake enrichment. With `VOBA_TEST_MONGO_URI`, it also checks live sign-up, login, generation cache, recipe save/reload, source precedence, and logout in `VOBA_TEST_MONGO_DATABASE` (default `VobaDemoTests`). It deletes its GUID user, auth data, saved recipe, and cache collection. Test collections and indexes remain.

`Voba.Persistence.IntegrationTests` needs `VOBA_TEST_MONGO_URI`; it creates and removes only its own GUID-named collections in that test database. Unit tests need no Mongo server or Spoonacular key. Demo generation still needs Ollama. Synthetic enrichment means local demo prices and nutrition, not real market data. Diet and allergy handling filters model output but cannot guarantee medical safety.

On configured Windows machine, `scripts/Invoke-VobaDemo.ps1` reads `%LOCALAPPDATA%\Voba\demo-credentials.dpapi`, protected for current Windows user and stored outside Git. It passes MongoDB credentials to child process through environment, then restores previous values. File must already exist from scoped Atlas access setup.

```powershell
.\scripts\Invoke-VobaDemo.ps1 -Task Integration
.\scripts\Invoke-VobaDemo.ps1 -Task Smoke
.\scripts\Invoke-VobaDemo.ps1 -Task Acceptance
```
