using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarketIntelligence.Analysis.Interfaces;
using MarketIntelligence.Analysis.Services;
using MarketIntelligence.Core.Models;
using MarketIntelligence.DataCollection.Interfaces;
using MarketIntelligence.DataCollection.Services;
using MarketIntelligence.DataProcessing.Interfaces;
using MarketIntelligence.DataProcessing.Services;
using MarketIntelligence.Storage.Interfaces;
using MarketIntelligence.Storage.Services;
using Microsoft.Extensions.Logging;

namespace MarketIntelligence.Console
{
    public class MarketIntelligenceApp
    {
        private readonly ITwitterScraper _scraper;
        private readonly ITextProcessor _textProcessor;
        private readonly IDataStorage _storage;
        private readonly ISignalGenerator _signalGenerator;
        private readonly IVisualizationService _visualization;
        private readonly ILogger<MarketIntelligenceApp> _logger;

        public MarketIntelligenceApp(
            ITwitterScraper scraper,
            ITextProcessor textProcessor,
            IDataStorage storage,
            ISignalGenerator signalGenerator,
            IVisualizationService visualization,
            ILogger<MarketIntelligenceApp> logger)
        {
            _scraper = scraper;
            _textProcessor = textProcessor;
            _storage = storage;
            _signalGenerator = signalGenerator;
            _visualization = visualization;
            _logger = logger;
        }

        public async Task RunAsync()
        {
            _logger.LogInformation("Starting Market Intelligence System");
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Step 1: Data Collection
                _logger.LogInformation("Starting data collection...");
                var hashtags = new List<string> { "nifty50", "sensex", "intraday", "banknifty" };
                var tweets = await CollectDataAsync(hashtags, 2000);
                _logger.LogInformation($"Collected {tweets.Count} tweets");

                // Step 2: Data Processing
                _logger.LogInformation("Processing tweets...");
                _textProcessor.ProcessTweets(tweets);
                _logger.LogInformation("Text processing completed");

                // Step 3: Storage
                _logger.LogInformation("Saving data to Parquet...");
                await _storage.SaveTweetsAsync(tweets);
                _logger.LogInformation("Data saved successfully");

                // Step 4: Signal Generation
                _logger.LogInformation("Generating market signals...");
                var signals = _signalGenerator.GenerateSignals(tweets);
                await _storage.SaveSignalsAsync(signals);
                _logger.LogInformation($"Generated {signals.Count} market signals");

                // Step 5: Visualization
                _logger.LogInformation("Creating visualizations...");
                _visualization.GenerateSignalPlots(signals);
                _logger.LogInformation("Visualizations saved to output folder");

                // Step 6: Summary Report
                GenerateSummaryReport(tweets, signals);

                stopwatch.Stop();
                _logger.LogInformation($"Total execution time: {stopwatch.Elapsed}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in market intelligence pipeline");
                throw;
            }
        }

        private async Task<List<Tweet>> CollectDataAsync(List<string> hashtags, int targetCount)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var tweets = await _scraper.ScrapeTweetsAsync(hashtags, targetCount, cts.Token);

            // For demo purposes, generate synthetic data if scraping fails
            if (tweets.Count < targetCount / 2)
            {
                _logger.LogWarning("Insufficient real data, generating synthetic tweets");
                tweets.AddRange(GenerateSyntheticTweets(targetCount - tweets.Count));
            }

            return tweets;
        }

        private List<Tweet> GenerateSyntheticTweets(int count)
        {
            var random = new Random();
            var tweets = new List<Tweet>();
            var templates = new[]
            {
                "#NIFTY50 looking bullish today! Target {0}",
                "Bearish on #SENSEX, support at {0}",
                "#BANKNIFTY intraday setup: Buy above {0}",
                "Market update: #NIFTY50 at {0}, momentum positive",
                "#Intraday tip: Sell #SENSEX below {0}"
            };

            for (int i = 0; i < count; i++)
            {
                var template = templates[random.Next(templates.Length)];
                var price = random.Next(15000, 20000);

                tweets.Add(new Tweet
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = $"trader_{random.Next(1000)}",
                    Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(1440)),
                    Content = string.Format(template, price),
                    Likes = random.Next(0, 100),
                    Retweets = random.Next(0, 50),
                    Replies = random.Next(0, 20),
                    Hashtags = new List<string> { "NIFTY50", "SENSEX", "BANKNIFTY" }
                        .Where(_ => random.NextDouble() > 0.5).ToList()
                });
            }

            return tweets;
        }

        private void GenerateSummaryReport(List<Tweet> tweets, List<MarketSignal> signals)
        {
            System.Console.WriteLine("\n=== Market Intelligence Summary ===");
            System.Console.WriteLine($"Total Tweets Analyzed: {tweets.Count}");
            System.Console.WriteLine($"Time Range: {tweets.Min(t => t.Timestamp)} to {tweets.Max(t => t.Timestamp)}");
            System.Console.WriteLine($"Unique Users: {tweets.Select(t => t.Username).Distinct().Count()}");
            System.Console.WriteLine($"\nTotal Signals Generated: {signals.Count}");

            foreach (var symbol in signals.Select(s => s.Symbol).Distinct())
            {
                var symbolSignals = signals.Where(s => s.Symbol == symbol).ToList();
                var latestSignal = symbolSignals.OrderByDescending(s => s.Timestamp).First();

                System.Console.WriteLine($"\n{symbol}:");
                System.Console.WriteLine($"  Latest Signal: {latestSignal.Type}");
                System.Console.WriteLine($"  Composite Score: {latestSignal.CompositeScore:F2}");
                System.Console.WriteLine($"  Confidence: {latestSignal.Confidence:P}");
            }
        }
    }
}