using System.Linq;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Voba.AI.Interpreter;
using Voba.Models;

namespace Voba.AI.Pipeline.Handlers
{
    /// <summary>
    /// CHAIN LINK — Pipeline 2, Step 2 (Full Recipe).
    ///
    /// Receives a selected, priced RecipeConcept and asks Gemma to
    /// generate full step-by-step cooking instructions.
    ///
    /// Uses the same Interpreter as GemmaIdeationHandler so that
    /// dietary rules are enforced consistently across both prompts —
    /// the same expression tree, the same rule text.
    /// </summary>
    public class GemmaFullRecipeHandler : RecipePipelineHandler
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletion;

        public GemmaFullRecipeHandler(Kernel kernel)
        {
            _kernel = kernel;
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
        }

        public override async Task HandleAsync(RecipeGenerationContext context)
        {
            if (context.SelectedOption == null)
                return;

            var recipe = context.SelectedOption;

            // ── Interpreter: same parse → same rules as ideation ─────────────
            IRestrictionExpression expression =
                RestrictionParser.Parse(context.DietaryRestrictions);

            string rules = expression.Interpret();

            string ingredientLines = string.Join("\n",
                recipe.Ingredients.Select(i => $"  - {i}"));

            string prompt = $@"
You are a professional chef writing a clear, home-cook-friendly recipe.

Dish: {recipe.Name}
Servings: {context.ServingSize}
Estimated cost: ${recipe.EstimatedCost:F2}

RULES TO FOLLOW EXACTLY:
{rules}

Ingredients already chosen:
{ingredientLines}

Write ONLY numbered step-by-step cooking instructions.
- Start directly with step 1. No title. No preamble.
- Include temperatures, timings, and key techniques.
- Do NOT repeat the ingredient list.".Trim();

            var history = new ChatHistory();
            history.AddUserMessage(prompt);

            var response = await _chatCompletion.GetChatMessageContentAsync(history, kernel: _kernel);

            context.FinalRecipe = new FullRecipe
            {
                Title = recipe.Name,
                Instructions = response.Content ?? "Could not generate instructions."
            };

            context.IsHandled = true;

            await base.HandleAsync(context);
        }
    }
}