using Voba.Services;

namespace Voba;

public partial class MainPage : ContentPage
{
    private readonly IAiChatService _aiChatService;

    public MainPage(IAiChatService aiChatService)
    {
        InitializeComponent();
        _aiChatService = aiChatService;
    }
}
