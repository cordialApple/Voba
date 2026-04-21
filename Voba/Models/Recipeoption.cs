using System.Collections.Generic;

namespace Voba.Models
{
    public class RecipeOption
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Ingredients { get; set; } = new();
        public decimal EstimatedCost { get; set; }
        public decimal TotalCost { get; set; }
    }
}
