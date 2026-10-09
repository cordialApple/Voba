using Voba.Models;

namespace Voba.Backend;

public interface IRecipeGenerator
{
    Task GenerateOptionsAsync(RecipeGenerationContext context, CancellationToken cancellationToken);

    Task GenerateFullAsync(RecipeGenerationContext context, CancellationToken cancellationToken);
}
