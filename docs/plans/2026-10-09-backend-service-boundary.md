# Backend Service Boundary Implementation Plan

**Goal:** Put auth, generation, enrichment, cache, and recipe ownership behind an HTTP API.

**Architecture:** `Voba.Contracts` holds HTTP DTOs. `Voba.Backend` owns routes and generation. `Voba.Core` holds draft contract and existing cache coordinator. Mongo stores accounts, sessions, drafts, recipes, and cache. MAUI becomes HTTP client.

**Tech Stack:** .NET 9 ASP.NET Core, Semantic Kernel with Ollama, MongoDB Driver, xUnit.

1. Write failing contract/draft tests. Add DTOs and draft types. Run green.
2. Write failing HTTP auth and ownership tests: two accounts, invalid/expired token, foreign draft and recipe denial. Add routes against session-backed auth and scoped stores. Run green.
3. Write failing generation tests: option and full draft lifecycle, validation, server-owned source and costs, cache after restart, provider failure, cancellation. Add generation service with Gemma, enrichment, coordinator, and drafts. Run green.
4. Run opt-in live Mongo/Ollama smoke through local HTTP with isolated test data. Keep external calls out of default tests.
5. Run Core, Persistence, Backend tests and Windows build. Simplify changed code. Review diff. Commit owned paths only after parent review.
