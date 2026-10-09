# Demo backend plan

1. Core cache contract and coordinator: canonical keys for options and selected recipe, 24-hour expiry, isolated payload copies, concurrent request single flight, source-aware promotion. Regression tests cover keys, hits, races, failures, expiry, mutation, and source precedence.
2. Mongo adapter: atomic source-aware store and read filter, offline tests plus opt-in isolated live tests. Real source retains authority after expiry.
3. App wiring: both generation stages use coordinator; trusted enrichment stamps cost and nutrition source; positive inputs and total budget filter; saved recipes retain source. Environment settings replace committed-secret requirement.
4. Auth and recovery: repository indexes initialize asynchronously on first use; login and sign-up show connection error and disable duplicate submit; sign out clears session.
5. Verify: run three offline suites, Windows MAUI build, and local Ollama handler smoke. With configured MongoDB and Ollama, smoke sign-up, generation, repeat cache hit, selected recipe, save, reload, and sign out. Live Spoonacular mode only when key and quota authorized.

Limits: Atlas connection string and credentials stay local. Unit tests verify cache behavior without database; full app flow needs configured MongoDB and Ollama. Fake enrichment produces demo cost and nutrition. Gemma provides recipe text in both modes.
