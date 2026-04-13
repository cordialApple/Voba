namespace Voba.Models
{
    public class GroceryListBuilder
    {
        private string _userId = string.Empty;
        private decimal _budget;
        private List<Ingredient> _items = new();
        private decimal _estimatedCost;

        public GroceryListBuilder ForUser(string userId)
        {
            _userId = userId;
            return this;
        }

        public GroceryListBuilder WithBudget(decimal budget)
        {
            _budget = budget;
            return this;
        }

        public GroceryListBuilder WithItems(IEnumerable<Ingredient> items)
        {
            _items = items.ToList();
            return this;
        }

        public GroceryListBuilder WithEstimatedCost(decimal cost)
        {
            _estimatedCost = cost;
            return this;
        }

        public GroceryList Build()
        {
            if (string.IsNullOrWhiteSpace(_userId))
                throw new InvalidOperationException("UserId is required.");

            // GroceryList budget must be positive — zero means no active shopping intent.
            // Note: User.Budget allows zero (user exists but hasn't set a budget yet).
            if (_budget <= 0)
                throw new InvalidOperationException("Budget must be positive.");

            return new GroceryList(_userId, _budget, _items, _estimatedCost);
        }
    }
}
