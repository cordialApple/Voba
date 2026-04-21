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
    // Handles the recipe brainstorming phase in the Voba app pipeline.
    // Asks the local Gemma AI for meal ideas and strictly filters out any dangerous ingredients.
    public class GemmaIdeationHandler : RecipePipelineHandler
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletion;

        // Sets up the Semantic Kernel connection to the local Gemma model.
        public GemmaIdeationHandler(Kernel kernel)
        {
            _kernel = kernel;
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        }

        public override async Task HandleAsync(RecipeGenerationContext context)
        {
            // 1. Gets the detailed allergy and diet rules from the expression tree.
            IRestrictionExpression expression = RestrictionParser.Parse(context.DietaryRestrictions);
            string ruleBlock = expression.Interpret();

            // 2. Build the optional cuisine line — blank means no constraint.
            string cuisineLine = string.IsNullOrWhiteSpace(context.CuisinePreference)
                ? "Cuisine: Any — feel free to suggest diverse styles."
                : $"Cuisine: {context.CuisinePreference.Trim()} — ALL 5 recipes MUST be {context.CuisinePreference.Trim()} cuisine.";

            // 3. Builds a strict instruction prompt for the Gemma model.
            // Forces the AI to return exactly 5 ideas in a clean JSON format so the app can parse it easily.
            string prompt = $@"
            STRICT RULES — READ FIRST:
            {ruleBlock}
 
            You are a chef. Generate exactly 5 recipe ideas that STRICTLY obey ALL rules above.
            Budget: ${context.TargetBudget}. Servings: {context.ServingSize}.
            {cuisineLine}
 
            Every single ingredient in every recipe MUST comply with the rules above.
            Do NOT include any FORBIDDEN ingredient under any circumstances.
 
            Reply ONLY with a raw JSON array — no markdown, no explanation, no extra text.
            [{{""Name"":""Dish Name"",""Ingredients"":[""ingredient 1"",""ingredient 2""],""EstimatedCost"":0.00}}]".Trim();

            // Saves the prompt for debugging AI output later.
            context.DebugPrompt = prompt;

            // 4. Sends the prompt to the local Gemma model and gets the raw JSON back.
            var candidates = await CallGemmaAsync(prompt);

            // 5. Pulls out the exact list of banned ingredients from the rule block.
            var forbiddenPhrases = GetForbiddenPhrases(ruleBlock);

            // 6. The Cull: Aggressively deletes any AI recipe that accidentally includes a forbidden ingredient.
            var passing = candidates
                .Where(c => !HasForbiddenIngredient(c, forbiddenPhrases)) // Keep only safe recipes
                .GroupBy(c => c.Name) // Prevent exact duplicates from showing up
                .Select(g => g.First())
                .Take(2) // Takes only the first 2 safe options to display on the Voba front-end
                .ToList();

            // Passes the safe recipes to the next step in the pipeline.
            context.ProposedOptions = passing;

            await base.HandleAsync(context);
        }

        // ── Helper Methods ──────────────────────────────────────────────────

        // Reads through the rule text and builds a list of individual forbidden food items.
        private static HashSet<string> GetForbiddenPhrases(string ruleBlock)
        {
            var phrases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var lines = ruleBlock.Split('\n');

            foreach (var line in lines)
            {
                // Looks for the exact line where the forbidden list starts.
                var idx = line.IndexOf("FORBIDDEN:", StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;

                var forbiddenStr = line[(idx + 10)..];

                // Splits the comma-separated list into individual ingredients.
                foreach (var part in forbiddenStr.Split(','))
                {
                    var phrase = part.Trim(' ', '.', '\n', '(', ')');

                    // Drops broad category words because checking for "any animal" will accidentally block safe ingredients.
                    if (phrase.StartsWith("any ", StringComparison.OrdinalIgnoreCase) ||
                        phrase.StartsWith("all ", StringComparison.OrdinalIgnoreCase) ||
                        phrase.Contains("species", StringComparison.OrdinalIgnoreCase) ||
                        phrase.Contains("animal", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Removes prefixes from real ingredients (turns "all dairy" into "dairy").
                    if (phrase.StartsWith("all ", StringComparison.OrdinalIgnoreCase))
                        phrase = phrase[4..].Trim();

                    if (!string.IsNullOrWhiteSpace(phrase))
                        phrases.Add(phrase);
                }
            }
            return phrases;
        }

        // Checks every ingredient in an AI-generated recipe against the forbidden list.
        private static bool HasForbiddenIngredient(RecipeOption recipe, HashSet<string> forbiddenPhrases)
        {
            if (forbiddenPhrases.Count == 0) return false;

            // If even one ingredient contains a banned word, the whole recipe fails the safety check.
            return recipe.Ingredients.Any(ingredient =>
                forbiddenPhrases.Any(forbidden =>
                    ingredient.Contains(forbidden, StringComparison.OrdinalIgnoreCase)));
        }

        // Handles the actual communication with Gemma and formats the response.
        private async Task<List<RecipeOption>> CallGemmaAsync(string prompt)
        {
            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            // Requests the response from the local model.
            var response = await _chatCompletion.GetChatMessageContentAsync(history, kernel: _kernel);

            // Cleans up the AI's response to ensure it is valid JSON.
            // Strips out any markdown blocks (like ```json) that the model might add by mistake.
            string rawJson = (response.Content ?? "[]")
                .Replace("```json", "")
                .Replace("```", "")
                .Trim();

            // Locates the exact start and end of the JSON array.
            int start = rawJson.IndexOf('[');
            int end = rawJson.LastIndexOf(']');

            // Returns an empty list if the AI totally failed to output a JSON array.
            if (start == -1 || end == -1 || end < start)
                return new List<RecipeOption>();

            rawJson = rawJson[start..(end + 1)];

            // Attempts to convert the clean JSON string into a list of RecipeOption objects.
            try
            {
                return JsonSerializer.Deserialize<List<RecipeOption>>(rawJson) ?? new List<RecipeOption>();
            }
            catch (JsonException)
            {
                // Safely catches formatting errors if the AI hallucinated bad JSON structure.
                return new List<RecipeOption>();
            }
        }
    }
}