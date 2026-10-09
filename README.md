# Voba

.NET MAUI recipe demo. Gemma through local Ollama makes meal options and cooking steps. MongoDB stores users, saved recipes, and generation cache. Enrichment adds cost and nutrition from demo data or live Spoonacular.

Original demo:

<img width="800" height="430" alt="Original Voba recipe demo" src="https://github.com/user-attachments/assets/5915231c-eda0-4742-80a8-f7e60dacfbba" />

## Run on Windows

Need .NET 9 MAUI workload, Ollama, `gemma3:4b`, and reachable MongoDB. Atlas works; set connection string through environment. No secret belongs in Git.

```powershell
ollama pull gemma3:4b
$env:VOBA_MONGO_CONNECTION_STRING = '<your MongoDB connection string>'
$env:VOBA_MONGO_DATABASE = 'VobaDemo'
$env:VOBA_ENRICHMENT_MODE = 'fake'
dotnet build Voba/Voba.csproj -f net9.0-windows10.0.19041.0
dotnet build Voba/Voba.csproj -t:Run -f net9.0-windows10.0.19041.0
```

Start Ollama server if one not already running. Defaults: endpoint `http://localhost:11434`, model `gemma3:4b`, enrichment `fake`. Override with `VOBA_OLLAMA_ENDPOINT` and `VOBA_OLLAMA_MODEL`. Unknown enrichment mode fails at startup.

For live cost and nutrition, set `VOBA_ENRICHMENT_MODE=real` and `VOBA_SPOONACULAR_API_KEY`. Live mode makes Spoonacular requests and may consume API quota. When provider fails, cost falls back to Gemma estimate and source label says so. Nutrition may be unavailable. Recipe instructions always come from Gemma.

`VOBA_JWT_SECRET` optional for local demo. If supplied, use Base64 for at least 32 random bytes. Without it, app makes random signing key each process; old tokens fail after restart. MongoDB unavailable? Login/sign-up shows connection error. Local Mongo URI is fallback only when `VOBA_MONGO_CONNECTION_STRING` absent.

## Cache and flow

Budget and servings must be positive. Options above total budget or without usable cost stay hidden. Request key includes budget, servings, cuisine, restrictions, model, and prompt version. Full recipe key also includes selected ingredients, price, and nutrition. Repeated options and selected recipe requests skip Gemma and enrichment while cache entry valid. Real source can replace demo source; demo source cannot replace real source, even after real entry expires. Live mode does not reuse demo data. Cost and nutrition source labels appear on recipe cards and saved recipes.

Sign up or log in, generate options, select one, then save it. Saved recipes belong to signed-in user. Sign out clears current app session.

## Verify

```powershell
dotnet test Voba.Core.Tests/Voba.Core.Tests.csproj
dotnet test Voba.Persistence.Tests/Voba.Persistence.Tests.csproj
dotnet test Voba.AppData.Tests/Voba.AppData.Tests.csproj
dotnet build Voba/Voba.csproj -f net9.0-windows10.0.19041.0
dotnet run --project Voba.ModelSmoke/Voba.ModelSmoke.csproj
```

`Voba.ModelSmoke` uses local Ollama and fake enrichment through app generation handlers; no MongoDB or Spoonacular call. `Voba.Persistence.IntegrationTests` needs `VOBA_TEST_MONGO_URI`. It uses `VobaDemoTests` by default, or `VOBA_TEST_MONGO_DATABASE` when set, and creates and removes only its own GUID-named collections. Unit tests need no Mongo server or Spoonacular key. Demo generation still needs Ollama. Synthetic enrichment means local demo prices and nutrition, not real market data. Diet and allergy handling filters model output but cannot guarantee medical safety.
