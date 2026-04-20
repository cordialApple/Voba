using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace spoonacular
{
    /// <summary>
    /// Holds the global configuration and secrets for external API services.
    /// </summary>
    public static class ApiSettings
    {
        /// <summary>
        /// The API key required to authenticate requests to Spoonacular.
        /// </summary>
        public static string SpoonacularApiKey { get; set; } = "0f5055e31d424cc9bf30775ed374468f";
    }
}