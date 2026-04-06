using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;

namespace Voba.Services
{
    public class SemanticKernelChatService : IAiChatService
    {
        private readonly Kernel _kernel;

        public SemanticKernelChatService(Kernel kernel)
        {
            _kernel = kernel;
        }

        public async Task<string> GetResponseAsync(string prompt)
        {
            try
            {
                var result = await _kernel.InvokePromptAsync(prompt);
                return result.ToString();
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }
}
