using System.Threading.Tasks;
using Voba.Models;

namespace Voba.AI.Pipeline
{
    public abstract class RecipePipelineHandler
    {
        protected RecipePipelineHandler? _nextHandler;

        public RecipePipelineHandler SetNext(RecipePipelineHandler nextHandler)
        {
            _nextHandler = nextHandler;
            return nextHandler;
        }

        public virtual async Task HandleAsync(RecipeGenerationContext context)
        {
            if (_nextHandler != null && !context.IsHandled)
            {
                await _nextHandler.HandleAsync(context);
            }
        }
    }
}