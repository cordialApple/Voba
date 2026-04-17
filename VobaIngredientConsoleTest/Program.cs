using Voba.api;

var cleanedIngredientPrompt = new List<string>
{
    "chicken breast",
    "thai basil",
    "garlic",
    "soy sauce",
    "fish sauce",
    "brown sugar",
    "red chili",
    "vegetable oil"
};

var service = new SpoonacularService();

Console.WriteLine("Ingredient Pricing Console Test");
Console.WriteLine();

decimal totalEstimatedCost = 0;

foreach (var ingredientName in cleanedIngredientPrompt)
{
    var searchResult = await service.IngredientSearch(ingredientName, number: 1);
    var matchedIngredient = searchResult?.Results?.FirstOrDefault();

    if (matchedIngredient == null)
    {
        Console.WriteLine($"{ingredientName} -> no ingredient match found");
        continue;
    }

    var ingredientInformation = await service.GetIngredientInformation(matchedIngredient.Id);
    if (ingredientInformation == null)
    {
        Console.WriteLine($"{ingredientName} -> matched ID {matchedIngredient.Id}, but ingredient information could not be retrieved");
        continue;
    }

    var estimatedCostValue = ingredientInformation.EstimatedCost?.Value ?? 0;
    var estimatedCostUnit = ingredientInformation.EstimatedCost?.Unit ?? "unknown unit";

    totalEstimatedCost += estimatedCostValue;

    Console.WriteLine(
        $"{ingredientName} -> {ingredientInformation.Name} (ID: {ingredientInformation.Id}) = {estimatedCostValue} {estimatedCostUnit}");
}

Console.WriteLine();
Console.WriteLine($"Total Estimated Cost: {totalEstimatedCost}");
