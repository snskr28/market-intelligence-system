using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using MarketIntelligence.Core.Models;
using MarketIntelligence.DataProcessing.Interfaces;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace MarketIntelligence.DataProcessing.Services
{
    public class TextProcessor : ITextProcessor
    {
        private readonly MLContext _mlContext;
        private ITransformer _tfidfModel;

        public TextProcessor()
        {
            _mlContext = new MLContext(seed: 0);
        }

        public void ProcessTweets(List<Tweet> tweets)
        {
            // Initialize the TF-IDF model if we haven't already and we have data
            if (_tfidfModel == null && tweets.Any())
            {
                _tfidfModel = BuildTfidfModel(tweets);
            }

            foreach (var tweet in tweets)
            {
                tweet.Content = CleanText(tweet.Content);
                var features = ExtractFeatures(tweet);
                tweet.Features = features;
            }
        }

        private string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Remove URLs
            text = Regex.Replace(text, @"http[s]?://[^\s]+", " ");

            // Remove special characters but keep Indian language characters
            text = Regex.Replace(text, @"[^\w\s\u0900-\u097F]", " ");

            // Remove extra whitespace
            text = Regex.Replace(text, @"\s+", " ").Trim();

            return text.ToLowerInvariant();
        }

        private Dictionary<string, double> ExtractFeatures(Tweet tweet)
        {
            var features = new Dictionary<string, double>();

            // Basic features
            features["length"] = tweet.Content.Length;
            features["word_count"] = tweet.Content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            features["hashtag_count"] = tweet.Hashtags.Count;
            features["mention_count"] = tweet.Mentions.Count;

            // Engagement features
            features["engagement_score"] = CalculateEngagementScore(tweet);

            // Time-based features
            features["hour_of_day"] = tweet.Timestamp.Hour;
            features["day_of_week"] = (int)tweet.Timestamp.DayOfWeek;

            // Market-specific features
            features["bullish_keywords"] = CountKeywords(tweet.Content, BullishKeywords);
            features["bearish_keywords"] = CountKeywords(tweet.Content, BearishKeywords);

            // TF-IDF features - only if model is available
            if (_tfidfModel != null)
            {
                var tfidfFeatures = GetTfidfFeatures(tweet.Content);
                foreach (var (key, value) in tfidfFeatures)
                {
                    features[$"tfidf_{key}"] = value;
                }
            }

            return features;
        }

        private double CalculateEngagementScore(Tweet tweet)
        {
            var total = tweet.Likes + tweet.Retweets * 2 + tweet.Replies * 1.5;
            return Math.Log10(total + 1);
        }

        private int CountKeywords(string text, string[] keywords)
        {
            return keywords.Count(keyword =>
                text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private Dictionary<string, double> GetTfidfFeatures(string text)
        {
            // Simplified TF-IDF implementation using basic frequency
            var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (!words.Any()) return new Dictionary<string, double>();

            var wordFreq = words.GroupBy(w => w)
                .ToDictionary(g => g.Key, g => (double)g.Count() / words.Length);

            return wordFreq.Take(10).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private ITransformer BuildTfidfModel(List<Tweet> tweets)
        {
            try
            {
                // Create sample data from the actual tweets
                var textData = tweets.Where(t => !string.IsNullOrWhiteSpace(t.Content))
                                   .Take(Math.Min(100, tweets.Count)) // Limit sample size for performance
                                   .Select(t => new TextData { Text = CleanText(t.Content) })
                                   .ToList();

                // Only proceed if we have actual text data
                if (!textData.Any() || textData.All(t => string.IsNullOrWhiteSpace(t.Text)))
                {
                    return null;
                }

                var dataView = _mlContext.Data.LoadFromEnumerable(textData);

                // Build the TF-IDF pipeline
                var pipeline = _mlContext.Transforms.Text
                    .FeaturizeText("TfidfFeatures", "Text");

                return pipeline.Fit(dataView);
            }
            catch (Exception ex)
            {
                // Log the exception
                Console.WriteLine($"Failed to build TF-IDF model: {ex.Message}");
                return null;
            }
        }

        private static readonly string[] BullishKeywords =
        {
            "buy", "long", "bullish", "up", "gain", "profit", "moon", "rocket",
            "खरीदें", "तेजी", "लाभ", "ऊपर"
        };

        private static readonly string[] BearishKeywords =
        {
            "sell", "short", "bearish", "down", "loss", "crash", "dump",
            "बेचें", "मंदी", "नुकसान", "नीचे"
        };

        private class TextData
        {
            [LoadColumn(0)]
            public string Text { get; set; }
        }
    }
}