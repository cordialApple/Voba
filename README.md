# Voba — AI Recipe Generation

A .NET MAUI app that uses a local Gemma 3:4b model (via Ollama) to generate
budget-aware, dietary-safe recipe ideas entirely on localhost.

---

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [.NET MAUI workload](https://learn.microsoft.com/en-us/dotnet/maui/get-started/installation)
- [Ollama](https://ollama.com/) installed and running
- Gemma 3:4b pulled: `ollama pull gemma3:4b`

---

## Running locally

```bash
# 1. Start Ollama
ollama serve

# 2. Clone and restore
git clone https://github.com/YOUR_USERNAME/Voba.git
cd Voba
dotnet restore

# 3. Run
dotnet build -t:Run -f net9.0-windows10.0.19041.0
```

Connects to Ollama at `http://localhost:11434` by default.

---

## How it works

1. User sets budget, serving size, lifestyle diet checkboxes, and typed allergies
2. The Interpreter pattern translates restrictions into precise AI rule strings
3. Gemma generates 5 recipe concepts — violations are automatically culled
4. User selects a recipe and gets full step-by-step cooking instructions

---

## Project structure

Voba/
├── AI/
│   ├── Interpreter/        # Dietary restriction → rule string translation
│   └── Pipeline/Handlers/  # GemmaIdeationHandler, GemmaFullRecipeHandler
├── Models/                 # RecipeGenerationContext, RecipeOption
├── Services/               # SemanticKernelChatService
├── MainPage.xaml(.cs)      # Test UI (to be replaced)
└── MauiProgram.cs          # DI registration
