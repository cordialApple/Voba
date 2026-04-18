using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Voba.Services
{
    // Defines the standard rules for communicating with the AI model across the Voba
    public interface IAiChatService
    {
        Task<string> GetResponseAsync(string prompt);
    }
}