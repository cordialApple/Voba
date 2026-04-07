using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Voba
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

// MongoDB Client Setup
             var mongoClient = new MongoClient(Secrets.MongoConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(Secrets.MongoDatabaseName);

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
