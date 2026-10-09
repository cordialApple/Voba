# Voba backend

Run the API on loopback with `dotnet run --project Voba.Backend/Voba.Backend.csproj -- --urls http://127.0.0.1:5058`. Set these process environment variables first:

- `VOBA_MONGO_CONNECTION_STRING`: server Mongo URI. Required.
- `VOBA_JWT_SECRET`: stable Base64 key decoding to at least 32 bytes. Required; keep outside Git. Changing it invalidates all access tokens.
- `VOBA_MONGO_DATABASE`: database name; defaults to `Voba`.
- `VOBA_OLLAMA_ENDPOINT`: Ollama URL; defaults to `http://127.0.0.1:11434`.
- `VOBA_OLLAMA_MODEL`: model name; defaults to `gemma3:4b`.
- `VOBA_ENRICHMENT_MODE`: `fake` or `real`; defaults to `fake`.
- `VOBA_SPOONACULAR_API_KEY`: required only for `real` mode.

The desktop needs only the API base URL and user bearer tokens. Mongo URI, JWT signing key, Ollama connection, and Spoonacular key stay in the backend process.

Auth routes: `POST /api/auth/register`, `/login`, `/refresh`, `/logout`. Generation routes: `POST /api/generation/options`, then `POST /api/generation/drafts/{draftId}/select`. Recipe routes: `POST /api/recipes`, `GET /api/recipes`, `GET /api/recipes/{id}`, `DELETE /api/recipes/{id}`. Protected routes validate the access token and active Mongo session on every request. Register returns an account; call login separately for tokens. Save needs the `draftId` and `draftVersion` from the selected full recipe. Old versions return `409`.

Generation input bounds: budget above zero through 1,000,000 USD; servings 1 through 1,000; up to 32 restrictions, each at most 200 characters; cuisine at most 200 characters. HTTP request body limit: 16 KiB. Drafts expire after one hour. Cache entries expire after 24 hours; a real source entry keeps authority over synthetic/estimate entries.

Run offline tests with `dotnet test Voba.Backend.Tests/Voba.Backend.Tests.csproj`. Run the opt-in Atlas HTTP acceptance suite through `scripts/Invoke-VobaDemo.ps1 -Task Acceptance`; it creates and drops only its own GUID-named collections in `VobaDemoTests`. That suite uses real auth and Mongo stores with a synthetic generator, so it costs no Spoonacular calls. `scripts/Invoke-VobaDemo.ps1 -Task Seed` installs two repeatable dummy accounts and synthetic recipes in the test database; the seed command refuses non-test database names.
