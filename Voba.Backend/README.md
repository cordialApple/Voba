# Voba backend

Run the API on loopback with `scripts/Invoke-VobaDemo.ps1 -Task Backend` on `http://127.0.0.1:5057`. For manual startup, use `dotnet run --project Voba.Backend/Voba.Backend.csproj -- --urls http://127.0.0.1:5057` and set these process environment variables first:

Open `http://127.0.0.1:5057/demo` for the same-origin browser recipe workbench. It covers registration, login, generation, selection, save, saved recipe reopen, and logout. Tokens live only in browser memory. The protected helper uses `VobaDemoTests`; standalone startup defaults to `Voba` unless `VOBA_MONGO_DATABASE` is set. The API walkthrough is `npm --prefix Voba.Web.Tests run walkthrough` after `npm --prefix Voba.Web.Tests ci` while the backend runs. Browser tests use `npm --prefix Voba.Web.Tests test` and installed Edge.

- `VOBA_MONGO_CONNECTION_STRING`: server Mongo URI. Required.
- `VOBA_JWT_SECRET`: stable Base64 key decoding to at least 32 bytes. Required; keep outside Git. Changing it invalidates all access tokens.
- `VOBA_MONGO_DATABASE`: database name; defaults to `Voba`.
- `VOBA_OLLAMA_ENDPOINT`: Ollama URL; defaults to `http://127.0.0.1:11434`.
- `VOBA_OLLAMA_MODEL`: model name; defaults to `gemma3:4b`.
- `VOBA_ENRICHMENT_MODE`: `fake` or `real`; defaults to `fake`.
- `VOBA_SPOONACULAR_API_KEY`: required only for `real` mode.

The desktop needs only `VOBA_API_BASE_URL` (default `http://127.0.0.1:5057`) and user bearer tokens. Use `scripts/Invoke-VobaDemo.ps1 -Task App` in another process. Mongo URI, JWT signing key, Ollama connection, and Spoonacular key stay in the backend process. Remote API URLs require HTTPS.

Auth routes: `POST /api/auth/register`, `/login`, `/refresh`, `/logout`. Generation routes: `POST /api/generation/options`, then `POST /api/generation/drafts/{draftId}/select`. Recipe routes: `POST /api/recipes`, `GET /api/recipes`, `GET /api/recipes/{id}`, `DELETE /api/recipes/{id}`. Protected routes validate the access token and active Mongo session on every request. Register returns an account; call login separately for tokens. Save needs the `draftId` and `draftVersion` from the selected full recipe. Old versions return `409`.

Generation input bounds: budget above zero through 1,000,000 USD; servings 1 through 1,000; up to 32 restrictions, each at most 200 characters; cuisine at most 200 characters. HTTP request body limit: 16 KiB. Drafts expire after one hour. Cache entries expire after 24 hours; a real source entry keeps authority over synthetic/estimate entries.

Run offline tests with `dotnet test Voba.Backend.Tests/Voba.Backend.Tests.csproj`. Run the opt-in Atlas HTTP acceptance suite through `scripts/Invoke-VobaDemo.ps1 -Task Acceptance`; it creates and drops only its own GUID-named collections in `VobaDemoTests`. That suite uses real auth and Mongo stores with a synthetic generator. Run `scripts/Invoke-VobaDemo.ps1 -Task ModelAcceptance` for actual `gemma3:4b` over Ollama, Kestrel, and Atlas. It generates vegan options and full instructions, saves a recipe, then verifies both model phases are served from Mongo cache after server restart. This second suite can take several minutes. Both suites use synthetic cost and nutrition; neither calls Spoonacular. `scripts/Invoke-VobaDemo.ps1 -Task Seed` installs two repeatable dummy accounts and synthetic recipes in the test database; the seed command refuses non-test database names.
