using System;
using System.Collections.Generic;
using MarketIntelligence.Analysis.Services;
using MarketIntelligence.Core.Models;
using Xunit;
using static System.Net.Mime.MediaTypeNames;

namespace MarketIntelligence.Tests
{
    public class SignalGeneratorTests
    {
        private readonly SignalGenerator _signalGenerator;

        public SignalGeneratorTests()
        {
            _signalGenerator = new SignalGenerator();
        }

        [Fact]
        public void GenerateSignals_WithBullishTweets_ReturnsBullishSignal()
        {
            // Arrange
            var tweets = new List<Tweet>();
            for (int i = 0; i < 20; i++)
            {
                tweets.Add(new Tweet
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = $"user{i}",
                    Content = "NIFTY looking very bullish today! Buy buy buy!",
                    Timestamp = DateTime.UtcNow.AddMinutes(-i),
                    Likes = 100,
                    Retweets = 50,
                    Features = new Dictionary<string, double>
                    {
                        ["bullish_keywords"] = 3,
                        ["bearish_keywords"] = 0,
                        ["engagement_score"] = 2.5
                    }
                });
            }

            // Act
            var signals = _signalGenerator.GenerateSignals(tweets);

            // Assert
            Assert.NotEmpty(signals);
            var niftySignal = signals.Find(s => s.Symbol == "NIFTY");
            Assert.NotNull(niftySignal);
            Assert.Equal(SignalType.Bullish, niftySignal.Type);
            Assert.True(niftySignal.SentimentScore > 0);
        }

        [Fact]
        public void GenerateSignals_WithMixedSentiment_ReturnsNeutralSignal()
        {
            // Arrange
            var tweets = new List<Tweet>();
            for (int i = 0; i < 10; i++)
            {
                tweets.Add(CreateTweet("SENSEX", i % 2 == 0 ? "bullish" : "bearish"));
            }

            // Act
            var signals = _signalGenerator.GenerateSignals(tweets);

            // Assert
            var sensexSignal = signals.Find(s => s.Symbol == "SENSEX");
            Assert.NotNull(sensexSignal);
            Assert.Equal(SignalType.Neutral, sensexSignal.Type);
        }

        [Fact]
        public void GenerateSignals_WithBearishTweets_ReturnsBearishSignal()
        {
            // Arrange
            var tweets = new List<Tweet>();
            for (int i = 0; i < 20; i++)
            {
                tweets.Add(new Tweet
                {
                    Id = Guid.NewGuid().ToString(),
                    Username = $"user{i}",
                    Content = "NIFTY looking very bearish today! Sell sell sell!",
                    Timestamp = DateTime.UtcNow.AddMinutes(-i),
                    Likes = 100,
                    Retweets = 50,
                    Features = new Dictionary<string, double>
                    {
                        ["bullish_keywords"] = 0,
                        ["bearish_keywords"] = 3,
                        ["engagement_score"] = 2.5
                    }
                });
            }

            // Act
            var signals = _signalGenerator.GenerateSignals(tweets);

            // Assert
            Assert.NotEmpty(signals);
            var niftySignal = signals.Find(s => s.Symbol == "NIFTY");
            Assert.NotNull(niftySignal);
            Assert.Equal(SignalType.Bearish, niftySignal.Type);
            Assert.True(niftySignal.SentimentScore < 0);
        }

        private Tweet CreateTweet(string symbol, string sentiment)
        {
            return new Tweet
            {
                Id = Guid.NewGuid().ToString(),
                Username = "testuser",
                Content = $"{symbol} looking {sentiment}",
                Timestamp = DateTime.UtcNow,
                Features = new Dictionary<string, double>
                {
                    ["bullish_keywords"] = sentiment == "bullish" ? 1 : 0,
                    ["bearish_keywords"] = sentiment == "bearish" ? 1 : 0,
                    ["engagement_score"] = 1.0
                }
            };
        }
    }
}