namespace Voba.Contracts;

public sealed record RegisterRequest(string Email, string Username, string Password);

public sealed record RegisterResponse(string UserId, string Email, string Username);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthTokensResponse(string UserId, string AccessToken, string RefreshToken);

public sealed record GenerationOptionsRequest(
    decimal Budget,
    int Servings,
    IReadOnlyList<string> DietaryRestrictions,
    string? CuisinePreference);

public sealed record NutritionResponse(
    decimal Calories,
    decimal ProteinGrams,
    decimal FatGrams,
    decimal CarbGrams);

public sealed record RecipeOptionResponse(
    string OptionId,
    string Name,
    IReadOnlyList<string> Ingredients,
    decimal EstimatedCost,
    decimal TotalCost,
    NutritionResponse? Nutrition,
    string CostSource,
    string NutritionSource);

public sealed record GenerationOptionsResponse(
    string DraftId,
    IReadOnlyList<RecipeOptionResponse> Options);

public sealed record SelectRecipeRequest(string OptionId);

public sealed record FullRecipeResponse(
    string DraftId,
    long DraftVersion,
    string Title,
    string Instructions,
    RecipeOptionResponse SelectedOption,
    int Servings,
    decimal Budget,
    IReadOnlyList<string> DietaryRestrictions,
    string? CuisinePreference);

public sealed record SaveRecipeRequest(string DraftId, long DraftVersion);

public sealed record SavedIngredientResponse(string Name, decimal Amount, string Unit);

public sealed record SavedRecipeResponse(
    string Id,
    string Title,
    IReadOnlyList<SavedIngredientResponse> Ingredients,
    decimal TotalCost,
    string Instructions,
    NutritionResponse? Nutrition,
    string CostSource,
    string NutritionSource,
    DateTime SavedAtUtc);

public sealed record ApiError(string Code, string Message);
