using Voba.Client;
using Voba.Contracts;

namespace Voba.Pages;

public partial class Forum : ContentPage
{
    private readonly IVobaApiClient _api;

    public Forum(IVobaApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnGenerateClicked(object sender, EventArgs e)
    {
        if (!decimal.TryParse(BudgetInput.Text, out var budget) || budget is <= 0 or > 1_000_000 ||
            !int.TryParse(ServingsInput.Text, out var servings) || servings is <= 0 or > 1000)
        {
            ErrorLabel.Text = "Enter a budget up to $1,000,000 and 1–1,000 servings.";
            ErrorLabel.IsVisible = true;
            return;
        }

        ErrorLabel.IsVisible = false;
        GenerateButton.IsEnabled = false;
        Spinner.IsRunning = true;
        Spinner.IsVisible = true;

        try
        {
            var restrictions = new List<string>();
            var dietMap = new Dictionary<CheckBox, string>
            {
                { ChkVegan, "Vegan" },
                { ChkVegetarian, "Vegetarian" },
                { ChkKeto, "Keto" },
                { ChkPaleo, "Paleo" },
                { ChkGlutenFree, "Gluten-Free" },
                { ChkDairyFree, "Dairy-Free" },
                { ChkHalal, "Halal" },
                { ChkKosher, "Kosher" }
            };
            foreach (var (checkbox, name) in dietMap)
                if (checkbox.IsChecked)
                    restrictions.Add(name);
            if (!string.IsNullOrWhiteSpace(AllergyInput.Text))
                restrictions.AddRange(AllergyInput.Text
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(value => value.Trim())
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

            var request = new GenerationOptionsRequest(budget, servings, restrictions,
                string.IsNullOrWhiteSpace(CuisineInput.Text) ? null : CuisineInput.Text.Trim());
            var options = await _api.GetOptionsAsync(request);
            if (options.Options.Count == 0)
            {
                ErrorLabel.Text = "No recipes returned. Try adjusting your budget or restrictions.";
                ErrorLabel.IsVisible = true;
                return;
            }

            await Shell.Current.GoToAsync(nameof(RecipeSelect),
                new Dictionary<string, object> { ["Options"] = options, ["Servings"] = servings });
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = $"Something went wrong: {ex.Message}";
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            Spinner.IsRunning = false;
            Spinner.IsVisible = false;
            GenerateButton.IsEnabled = true;
        }
    }

    private async void OnBackClicked(object sender, EventArgs e) =>
        await Shell.Current.GoToAsync(nameof(Home));
}
