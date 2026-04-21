using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Voba.Services
{
    public interface IAiChatService
    {
        Task<string> GetResponseAsync(string prompt);
    }
}
