using System.Threading.Tasks;
using Voba.Models;

namespace Voba.AI.Pipeline
{
    // Every step in the Gemma AI process (like checking rules, brainstorming ideas, and writing steps) inherits from this.
    public abstract class RecipePipelineHandler
    {
        // Holds the reference to the next specific step in the AI pipeline.
        protected RecipePipelineHandler? _nextHandler;

        // Links one handler to the next so the app can build a continuous sequence of AI tasks.
        // Returns the next handler to allow easy, single-line setup when building the chain.
        public RecipePipelineHandler SetNext(RecipePipelineHandler nextHandler)
        {
            _nextHandler = nextHandler;
            return nextHandler;
        }

        // Pushes the recipe data down the chain to the next handler in line.
        // Stops passing data forward if a previous handler marks the recipe as completely finished.
        public virtual async Task HandleAsync(RecipeGenerationContext context)
        {
            if (_nextHandler != null && !context.IsHandled)
            {
                await _nextHandler.HandleAsync(context);
            }
        }
    }
}