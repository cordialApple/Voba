using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Voba.AI.Interpreter;
using Voba.Models;

namespace Voba.AI.Pipeline.Handlers
{
    public class GemmaIdeationHandler : RecipePipelineHandler
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletion;

        public GemmaIdeationHandler(Kernel kernel)
        {
            _kernel = kernel;
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        }

        public override async Task HandleAsync(RecipeGenerationContext context)
        {
            // 1. Get the precise, multi-line rules from your Interpreter
            IRestrictionExpression expression = RestrictionParser.Parse(context.DietaryRestrictions);
            string ruleBlock = expression.Interpret();

            // 2. Build the prompt using the RAW Interpreter output, and ask for 5 ideas
            string prompt = $@"
                STRICT RULES — READ FIRST:
                {ruleBlock}

                You are a chef. Generate exactly 5 recipe ideas that STRICTLY obey ALL rules above.
                Budget: ${context.TargetBudget}. Servings: {context.ServingSize}.

                Every single ingredient in every recipe MUST comply with the rules above.
                Do NOT include any FORBIDDEN ingredient under any circumstances.

                Reply ONLY with a raw JSON array — no markdown, no explanation, no extra text.
                [{{""Name"":""Dish Name"",""Ingredients"":[""ingredient 1"",""ingredient 2""],""EstimatedCost"":0.00}}]".Trim();

            context.DebugPrompt = prompt;

            // 3. Call Gemma 
            var candidates = await CallGemmaAsync(prompt);

            // 4. Extract the exact forbidden phrases from the Interpreter output
            var forbiddenPhrases = GetForbiddenPhrases(ruleBlock);

            // 5. The Cull: Aggressively execute any recipe that broke the rules
            var passing = candidates
                .Where(c => !HasForbiddenIngredient(c, forbiddenPhrases))
                .GroupBy(c => c.Name) // Prevent duplicates
                .Select(g => g.First())
                .Take(2) // We only want to show 2 to the user
                .ToList();

            context.ProposedOptions = passing;

            await base.HandleAsync(context);
        }

        // ── Helper Methods ──────────────────────────────────────────────────

        private static HashSet<string> GetForbiddenPhrases(string ruleBlock)
        {
            var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = ruleBlock.Split('\n');

            foreach (var line in lines)
            {
                var idx = line.IndexOf("FORBIDDEN:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;

                var forbiddenStr = line[(idx + 10)..];

                foreach (var part in forbiddenStr.Split(','))
                {
                    var phrase = part.Trim(' ', '.', '\n', '(', ')');

                    // Drop meta-phrases that describe categories rather than ingredients
                    if (phrase.StartsWith("any ", StringComparison.OrdinalIgnoreCase) ||
                        phrase.StartsWith("all ", StringComparison.OrdinalIgnoreCase) ||
                        phrase.Contains("species", StringComparison.OrdinalIgnoreCase) ||
                        phrase.Contains("animal", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Strip leading "all "/"any " prefix on real ingredients
                    if (phrase.StartsWith("all ", StringComparison.OrdinalIgnoreCase))
                        phrase = phrase[4..].Trim();

                    if (!string.IsNullOrWhiteSpace(phrase))
                        phrases.Add(phrase);
                }
            }
            return phrases;
        }

        private static bool HasForbiddenIngredient(RecipeOption recipe, HashSet<string> forbiddenPhrases)
        {
            if (forbiddenPhrases.Count == 0) return false;

            // If ANY ingredient contains ANY forbidden phrase, the whole recipe is garbage
            return recipe.Ingredients.Any(ingredient =>
                forbiddenPhrases.Any(forbidden =>
                    ingredient.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
        }

        private async Task<List<RecipeOption>> CallGemmaAsync(string prompt)
        {
            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            var response = await _chatCompletion.GetChatMessageContentAsync(history, kernel: _kernel);

            string rawJson = (response.Content ?? "[]")
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            int start = rawJson.IndexOf('[');
            int end = rawJson.LastIndexOf(']');

            if (start == -1 || end == -1 || end < start)
                return new List<RecipeOption>();

            rawJson = rawJson[start..(end + 1)];

            try
            {
                return JsonSerializer.Deserialize<List<RecipeOption>>(rawJson) ?? new List<RecipeOption>();
            }
            catch (JsonException)
            {
                return new List<RecipeOption>();
            }
        }
    }
}