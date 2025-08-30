using System;
using System.Collections.Generic;
using System.Linq;
using MarketIntelligence.Analysis.Interfaces;
using MarketIntelligence.Core.Models;
using MathNet.Numerics.Statistics;

namespace MarketIntelligence.Analysis.Services
{
    public class SignalGenerator : ISignalGenerator
    {
        private readonly Dictionary<string, List<double>> _historicalScores;
        private readonly int _windowSize;

        public SignalGenerator(int windowSize = 100)
        {
            _windowSize = windowSize;
            _historicalScores = new Dictionary<string, List<double>>();
        }

        public List<MarketSignal> GenerateSignals(List<Tweet> tweets)
        {
            var signals = new List<MarketSignal>();
            var tweetsBySymbol = GroupTweetsBySymbol(tweets);

            foreach (var (symbol, symbolTweets) in tweetsBySymbol)
            {
                var signal = CalculateSignal(symbol, symbolTweets);
                if (signal != null)
                {
                    signals.Add(signal);
                    UpdateHistoricalScores(symbol, signal.CompositeScore);
                }
            }

            return signals;
        }

        private Dictionary<string, List<Tweet>> GroupTweetsBySymbol(List<Tweet> tweets)
        {
            var symbols = new Dictionary<string, List<Tweet>>();
            var marketSymbols = new[] { "NIFTY", "SENSEX", "BANKNIFTY" };

            foreach (var tweet in tweets)
            {
                foreach (var symbol in marketSymbols)
                {
                    if (tweet.Content.Contains(symbol, StringComparison.OrdinalIgnoreCase) ||
                        tweet.Hashtags.Any(h => h.Contains(symbol, StringComparison.OrdinalIgnoreCase)))
                    {
                        if (!symbols.ContainsKey(symbol))
                            symbols[symbol] = new List<Tweet>();

                        symbols[symbol].Add(tweet);
                    }
                }
            }

            return symbols;
        }

        private MarketSignal CalculateSignal(string symbol, List<Tweet> tweets)
        {
            if (tweets.Count < 10) // Minimum tweets required
                return null;

            var sentimentScore = CalculateSentimentScore(tweets);
            var volumeScore = CalculateVolumeScore(tweets);
            var momentumScore = CalculateMomentumScore(symbol, sentimentScore);
            var compositeScore = CalculateCompositeScore(sentimentScore, volumeScore, momentumScore);
            var confidence = CalculateConfidence(tweets, compositeScore);

            return new MarketSignal
            {
                Timestamp = DateTime.UtcNow,
                Symbol = symbol,
                SentimentScore = sentimentScore,
                VolumeScore = volumeScore,
                MomentumScore = momentumScore,
                CompositeScore = compositeScore,
                Confidence = confidence,
                Type = DetermineSignalType(compositeScore)
            };
        }

        private double CalculateSentimentScore(List<Tweet> tweets)
        {
            var scores = tweets.Select(t =>
            {
                var bullish = t.Features.GetValueOrDefault("bullish_keywords", 0);
                var bearish = t.Features.GetValueOrDefault("bearish_keywords", 0);
                var engagement = t.Features.GetValueOrDefault("engagement_score", 0);

                return (bullish - bearish) * (1 + engagement * 0.1);
            }).ToList();

            return scores.Count > 0 ? scores.Average() : 0;
        }

        private double CalculateVolumeScore(List<Tweet> tweets)
        {
            var recentTweets = tweets.Where(t => t.Timestamp > DateTime.UtcNow.AddHours(-1)).ToList();
            var olderTweets = tweets.Where(t => t.Timestamp <= DateTime.UtcNow.AddHours(-1)).ToList();

            if (olderTweets.Count == 0)
                return 1.0;

            var recentVolume = recentTweets.Count;
            var averageVolume = olderTweets.Count / Math.Max(1,
                (DateTime.UtcNow - olderTweets.Min(t => t.Timestamp)).TotalHours);

            return recentVolume / Math.Max(1, averageVolume);
        }

        private double CalculateMomentumScore(string symbol, double currentSentiment)
        {
            if (!_historicalScores.ContainsKey(symbol) || _historicalScores[symbol].Count < 2)
                return 0;

            var history = _historicalScores[symbol];
            var recentScores = history.TakeLast(Math.Min(10, history.Count)).ToList();

            if (recentScores.Count < 2)
                return 0;

            var momentum = (currentSentiment - recentScores.Average()) /
                          Math.Max(0.01, recentScores.StandardDeviation());

            return Math.Max(-3, Math.Min(3, momentum)); // Clip to [-3, 3]
        }

        private double CalculateCompositeScore(double sentiment, double volume, double momentum)
        {
            // Weighted combination
            var weights = new { Sentiment = 0.5, Volume = 0.2, Momentum = 0.3 };

            return sentiment * weights.Sentiment +
                   volume * weights.Volume +
                   momentum * weights.Momentum;
        }

        private double CalculateConfidence(List<Tweet> tweets, double compositeScore)
        {
            var factors = new List<double>();

            // Tweet volume factor
            factors.Add(Math.Min(1.0, tweets.Count / 100.0));

            // Engagement factor
            var avgEngagement = tweets.Average(t => t.Features.GetValueOrDefault("engagement_score", 0));
            factors.Add(Math.Min(1.0, avgEngagement / 5.0));

            // Score magnitude factor
            factors.Add(Math.Min(1.0, Math.Abs(compositeScore) / 2.0));

            // User diversity factor
            var uniqueUsers = tweets.Select(t => t.Username).Distinct().Count();
            factors.Add(Math.Min(1.0, uniqueUsers / (double)tweets.Count));

            return factors.Average();
        }

        private SignalType DetermineSignalType(double score)
        {
            if (score > 0.5) return SignalType.Bullish;
            if (score < -0.5) return SignalType.Bearish;
            return SignalType.Neutral;
        }

        private void UpdateHistoricalScores(string symbol, double score)
        {
            if (!_historicalScores.ContainsKey(symbol))
                _historicalScores[symbol] = new List<double>();

            _historicalScores[symbol].Add(score);

            // Keep only recent history
            if (_historicalScores[symbol].Count > _windowSize)
                _historicalScores[symbol].RemoveAt(0);
        }
    }
}