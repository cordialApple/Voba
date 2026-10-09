using System.Net;
using System.Net.Sockets;
using System.Text;
using spoonacular.Client;
using Voba.Services;
using Voba.Spoonacular;
using Voba.Models;
using Xunit;

namespace Voba.Core.Tests;

public sealed class SpoonacularContractTests
{
    private const string IngredientJson = """
        {"id":1,"original":"1 cup rice","originalName":"rice","name":"rice","amount":1,"unit":"cup","unitShort":"cup","unitLong":"cup","possibleUnits":[],"estimatedCost":{"value":250,"unit":"US Cents"},"consistency":"solid","shoppingListUnits":[],"aisle":"","image":"","meta":[],"nutrition":{"nutrients":[{"name":"Calories","amount":200,"unit":"kcal","percentOfDailyNeeds":10},{"name":"Protein","amount":10,"unit":"g","percentOfDailyNeeds":20},{"name":"Fat","amount":0,"unit":"g","percentOfDailyNeeds":0},{"name":"Carbohydrates","amount":0,"unit":"g","percentOfDailyNeeds":0}],"properties":[],"caloricBreakdown":{"percentProtein":20,"percentFat":10,"percentCarbs":70},"weightPerServing":{"amount":1,"unit":"cup"}},"categoryPath":[]}
        """;

    [Fact]
    public async Task Parse_ingredients_sends_form_and_maps_real_cost_and_nutrition()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var response = $"[{IngredientJson}]";
        var exchange = RespondAsync(listener, response);
        var configuration = new Configuration { BasePath = $"http://127.0.0.1:{port}" };
        configuration.ApiKey["x-api-key"] = "test-key";
        var service = new SpoonacularEnrichmentService(new SpoonacularService(configuration));

        var enrichment = await service.EnrichAsync(["1 cup rice"], 2);
        var request = await exchange;

        Assert.StartsWith("POST /recipes/parseIngredients", request.Headers);
        Assert.Contains("x-api-key: test-key", request.Headers, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ingredientList=1%20cup%20rice", request.Body);
        Assert.Contains("servings=2", request.Body);
        Assert.Contains("includeNutrition=true", request.Body, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(enrichment);
        Assert.Equal(2.5m, enrichment!.TotalCostUsd);
        Assert.Equal(1.25m, enrichment.CostPerServingUsd);
        Assert.Equal(100m, enrichment.Nutrition.Calories);
        Assert.Equal(5m, enrichment.Nutrition.ProteinGrams);
    }

    [Fact]
    public async Task Partial_sdk_prices_keep_nutrition_but_not_a_real_cost_claim()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var unpriced = IngredientJson.Replace("\"estimatedCost\":{\"value\":250,\"unit\":\"US Cents\"}",
            "\"estimatedCost\":null", StringComparison.Ordinal);
        var exchange = RespondAsync(listener, $"[{IngredientJson},{unpriced}]");
        var configuration = new Configuration { BasePath = $"http://127.0.0.1:{port}" };
        configuration.ApiKey["x-api-key"] = "test-key";
        var provider = new SpoonacularEnrichmentService(new SpoonacularService(configuration));
        var option = new RecipeOption { EstimatedCost = 4m, Ingredients = ["1 cup rice", "1 cup beans"] };

        await RecipeGenerationPolicy.EnrichAsync(option, 2, provider);
        var request = await exchange;

        Assert.Contains("%0A", request.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(8m, option.TotalCost);
        Assert.Equal(RecipeDataSource.Estimate, option.DataSource);
        Assert.Equal(RecipeDataSource.Real, option.NutritionSource);
        Assert.Equal(200m, option.Nutrition!.Calories);
    }

    [Fact]
    public async Task Missing_sdk_ingredient_keeps_both_sources_as_estimates()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var exchange = RespondAsync(listener, $"[{IngredientJson}]");
        var configuration = new Configuration { BasePath = $"http://127.0.0.1:{port}" };
        configuration.ApiKey["x-api-key"] = "test-key";
        var provider = new SpoonacularEnrichmentService(new SpoonacularService(configuration));
        var option = new RecipeOption { EstimatedCost = 4m, Ingredients = ["1 cup rice", "1 cup beans"] };

        await RecipeGenerationPolicy.EnrichAsync(option, 2, provider);
        await exchange;

        Assert.Equal(8m, option.TotalCost);
        Assert.Equal(RecipeDataSource.Estimate, option.DataSource);
        Assert.Equal(RecipeDataSource.Estimate, option.NutritionSource);
        Assert.Null(option.Nutrition);
    }

    [Fact]
    public async Task Unsupported_macro_unit_keeps_price_real_but_nutrition_unverified()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var payload = IngredientJson.Replace("\"name\":\"Protein\",\"amount\":10,\"unit\":\"g\"",
            "\"name\":\"Protein\",\"amount\":10,\"unit\":\"ounces\"", StringComparison.Ordinal);
        var exchange = RespondAsync(listener, $"[{payload}]");
        var configuration = new Configuration { BasePath = $"http://127.0.0.1:{port}" };
        configuration.ApiKey["x-api-key"] = "test-key";
        var provider = new SpoonacularEnrichmentService(new SpoonacularService(configuration));
        var option = new RecipeOption { EstimatedCost = 4m, Ingredients = ["1 cup rice"] };

        await RecipeGenerationPolicy.EnrichAsync(option, 2, provider);
        await exchange;

        Assert.Equal(2.5m, option.TotalCost);
        Assert.Equal(RecipeDataSource.Real, option.DataSource);
        Assert.Equal(RecipeDataSource.Estimate, option.NutritionSource);
        Assert.Null(option.Nutrition);
    }

    [Fact]
    public async Task Provider_failure_keeps_cost_and_nutrition_as_estimates()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var exchange = RespondAsync(listener, "{\"message\":\"quota exceeded\"}", 503);
        var configuration = new Configuration { BasePath = $"http://127.0.0.1:{port}" };
        configuration.ApiKey["x-api-key"] = "test-key";
        var provider = new SpoonacularEnrichmentService(new SpoonacularService(configuration));
        var option = new RecipeOption { EstimatedCost = 4m, Ingredients = ["1 cup rice"] };

        await RecipeGenerationPolicy.EnrichAsync(option, 2, provider);
        await exchange;

        Assert.Equal(8m, option.TotalCost);
        Assert.Equal(RecipeDataSource.Estimate, option.DataSource);
        Assert.Equal(RecipeDataSource.Estimate, option.NutritionSource);
        Assert.Null(option.Nutrition);
    }

    private static async Task<(string Headers, string Body)> RespondAsync(TcpListener listener, string response,
        int statusCode = 200)
    {
        using var client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(10));
        await using var stream = client.GetStream();
        var headerBytes = new List<byte>();
        var oneByte = new byte[1];
        while (headerBytes.Count < 16384)
        {
            if (await stream.ReadAsync(oneByte) == 0)
                throw new EndOfStreamException();
            headerBytes.Add(oneByte[0]);
            if (headerBytes.Count >= 4 && headerBytes[^4] == 13 && headerBytes[^3] == 10 &&
                headerBytes[^2] == 13 && headerBytes[^1] == 10)
                break;
        }
        var headers = Encoding.ASCII.GetString(headerBytes.ToArray());
        var contentLengthLine = headers.Split("\r\n", StringSplitOptions.RemoveEmptyEntries)
            .Single(line => line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
        var contentLength = int.Parse(contentLengthLine.Split(':', 2)[1].Trim());
        var bodyBytes = new byte[contentLength];
        await stream.ReadExactlyAsync(bodyBytes);
        var bytes = Encoding.UTF8.GetBytes(response);
        var responseHeaders = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {statusCode} Response\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(responseHeaders);
        await stream.WriteAsync(bytes);
        return (headers, Encoding.UTF8.GetString(bodyBytes));
    }
}
