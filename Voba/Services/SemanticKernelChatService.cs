using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Voba.Services
{
    public class SemanticKernelChatService : IAiChatService
    {
        private readonly Kernel _kernel;
        private readonly IChatCompletionService _chatCompletion;
        private readonly ChatHistory _chatHistory;

        // The Kernel is automatically injected here by MauiProgram.cs
        public SemanticKernelChatService(Kernel kernel)
        {
            _kernel = kernel;

            // Extract the chat service that we configured to use Ollama
            _chatCompletion = _kernel.GetRequiredService<IChatCompletionService>();

            // Initialize an empty chat history (this allows the AI to remember the conversation)
            _chatHistory = new ChatHistory();

        }

        public async Task<string> GetResponseAsync(string userInput)
        {
            // 1. Add the user's new message to the history
            _chatHistory.AddUserMessage(userInput);

            // 2. Send the entire history to Gemma 3:4b via Ollama
            var response = await _chatCompletion.GetChatMessageContentAsync(_chatHistory, kernel: _kernel);

            // 3. Safely extract the content, providing a fallback if it is null
            string safeResponse = response.Content ?? "Error: The model returned an empty response.";

            // 4. Add Gemma's safe response to the history so it remembers it next time
            _chatHistory.AddAssistantMessage(safeResponse);

            return safeResponse;
        }
    }
}
