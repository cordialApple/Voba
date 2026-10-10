# Headless localhost checks

Start the protected backend in one terminal:

```powershell
./scripts/Invoke-VobaDemo.ps1 -Task Backend
```

Run from `Voba.Web.Tests` in another terminal:

```powershell
npm ci
npm run test:unit
npm run test:ui
npm test
npm run walkthrough
```

`npm run test:ui` mocks HTTP and needs no backend. `npm test` uses installed Edge in a fresh headless context. It creates one dummy account and leaves one saved recipe for review. Screenshots and sanitized account/recipe details land in ignored `test-results`. `npm run walkthrough` uses the real HTTP API with a separate dummy account; its sanitized report lands in ignored `walkthrough-results`. Neither report includes tokens or passwords. Live checks use `http://127.0.0.1:5057` unless `VOBA_WEB_BASE_URL` is set.
