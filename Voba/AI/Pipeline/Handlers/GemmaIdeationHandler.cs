using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
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
            var history = new ChatHistory();

            string restrictions = context.DietaryRestrictions.Count > 0
                ? string.Join(", ", context.DietaryRestrictions)
                : "None";

            string prompt = $"Generate exactly 2 recipe ideas under ${context.TargetBudget} for {context.ServingSize} people. Dietary restrictions: {restrictions}. Return ONLY a raw JSON array of objects with 'Name' and a string array of 'Ingredients'.";

            history.AddUserMessage(prompt);

            var response = await _chatCompletion.GetChatMessageContentAsync(history, kernel: _kernel);
            string rawJson = response.Content ?? "[]";

            rawJson = rawJson.Replace("```json", "").Replace("```", "").Trim();

            var options = JsonSerializer.Deserialize<List<RecipeConcept>>(rawJson);

            if (options != null)
            {
                context.ProposedOptions = options;
            }

            await base.HandleAsync(context);
        }
    }
}