using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MarketIntelligence.DataCollection.Services;
using MarketIntelligence.DataProcessing.Services;
using MarketIntelligence.Storage.Services;
using MarketIntelligence.Analysis.Services;
using MarketIntelligence.DataCollection.Interfaces;
using MarketIntelligence.DataProcessing.Interfaces;
using MarketIntelligence.Storage.Interfaces;
using MarketIntelligence.Analysis.Interfaces;

namespace MarketIntelligence.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            var app = host.Services.GetRequiredService<MarketIntelligenceApp>();

            try
            {
                await app.RunAsync();
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Application error: {ex.Message}");
            }
        }

        static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    // Register services
                    services.AddHttpClient<ITwitterScraper, TwitterScraper>();
                    services.AddSingleton<ITextProcessor, TextProcessor>();
                    services.AddSingleton<IDataStorage>(sp =>
                        new ParquetStorage("./data"));
                    services.AddSingleton<ISignalGenerator, SignalGenerator>();
                    services.AddSingleton<IVisualizationService>(sp =>
                        new VisualizationService("./output"));
                    services.AddSingleton<MarketIntelligenceApp>();

                    // Configure logging
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                });
    }
}