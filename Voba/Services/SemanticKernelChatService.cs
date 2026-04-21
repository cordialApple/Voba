using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Voba.Services
{
    public class SemanticKernelChatService : IAiChatService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletion;
        private readonly ChatHistory _chatHistory;

        public SemanticKernelChatService(Kernel kernel)
        {
            _kernel = kernel;
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();
            _chatHistory = new ChatHistory();

        }

        public async Task<string> GetResponseAsync(string userInput)
        {
            _chatHistory.AddUserMessage(userInput);
            var response = await _chatCompletion.GetChatMessageContentAsync(_chatHistory, kernel: _kernel);
            string safeResponse = response.Content ?? "Error: The model returned an empty response.";
            _chatHistory.AddAssistantMessage(safeResponse);
            return safeResponse;
        }
    }
}
