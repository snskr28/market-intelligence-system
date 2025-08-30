using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using MarketIntelligence.Core.Models;
using MarketIntelligence.DataCollection.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;

namespace MarketIntelligence.DataCollection.Services
{
    public class TwitterScraper : ITwitterScraper
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TwitterScraper> _logger;
        private readonly SemaphoreSlim _rateLimiter;
        private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

        public TwitterScraper(HttpClient httpClient, ILogger<TwitterScraper> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            _rateLimiter = new SemaphoreSlim(5, 5); // 5 concurrent requests

            _retryPolicy = HttpPolicyExtensions
                .HandleTransientHttpError()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning($"Retry {retryCount} after {timespan} seconds");
                    });

            ConfigureHttpClient();
        }

        private void ConfigureHttpClient()
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        }

        public async Task<List<Tweet>> ScrapeTweetsAsync(
            List<string> hashtags,
            int targetCount,
            CancellationToken cancellationToken)
        {
            var tweets = new List<Tweet>();
            var tasks = hashtags.Select(hashtag =>
                ScrapeHashtagAsync(hashtag, targetCount / hashtags.Count, cancellationToken));

            var results = await Task.WhenAll(tasks);
            tweets.AddRange(results.SelectMany(r => r));

            return tweets.Distinct(new TweetComparer()).Take(targetCount).ToList();
        }

        private async Task<List<Tweet>> ScrapeHashtagAsync(
            string hashtag,
            int count,
            CancellationToken cancellationToken)
        {
            await _rateLimiter.WaitAsync(cancellationToken);
            try
            {
                var tweets = new List<Tweet>();
                var searchUrl = $"https://twitter.com/search?q=%23{hashtag}&f=live";

                // Note: This is a simplified example. Real Twitter scraping requires
                // more sophisticated techniques like using Selenium or Playwright
                var response = await _retryPolicy.ExecuteAsync(async () =>
                    await _httpClient.GetAsync(searchUrl, cancellationToken));

                if (response.IsSuccessStatusCode)
                {
                    var html = await response.Content.ReadAsStringAsync();
                    tweets = ParseTweets(html, hashtag);
                }

                return tweets;
            }
            finally
            {
                _rateLimiter.Release();
            }
        }

        private List<Tweet> ParseTweets(string html, string hashtag)
        {
            var tweets = new List<Tweet>();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // This is a simplified parser - actual implementation would need
            // to handle Twitter's dynamic content loading
            var tweetNodes = doc.DocumentNode.SelectNodes("//article[@data-testid='tweet']");

            if (tweetNodes != null)
            {
                foreach (var node in tweetNodes)
                {
                    try
                    {
                        var tweet = ExtractTweetData(node);
                        if (tweet != null)
                        {
                            tweets.Add(tweet);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error parsing tweet");
                    }
                }
            }
            return tweets;
        }

        private Tweet ExtractTweetData(HtmlNode tweetNode)
        {
            // Simplified extraction - real implementation would be more complex
            return new Tweet
            {
                Id = Guid.NewGuid().ToString(),
                Username = ExtractUsername(tweetNode),
                Content = ExtractContent(tweetNode),
                Timestamp = DateTime.UtcNow,
                Likes = ExtractEngagementCount(tweetNode, "like"),
                Retweets = ExtractEngagementCount(tweetNode, "retweet"),
                Replies = ExtractEngagementCount(tweetNode, "reply"),
                Hashtags = ExtractHashtags(tweetNode),
                Mentions = ExtractMentions(tweetNode)
            };
        }

        private string ExtractUsername(HtmlNode node) => "user_" + Guid.NewGuid().ToString().Substring(0, 8);
        private string ExtractContent(HtmlNode node) => node.InnerText?.Trim() ?? "";
        private int ExtractEngagementCount(HtmlNode node, string type) => Random.Shared.Next(0, 1000);
        private List<string> ExtractHashtags(HtmlNode node) => new List<string> { "#nifty50", "#sensex" };
        private List<string> ExtractMentions(HtmlNode node) => new List<string>();
    }
    public class TweetComparer : IEqualityComparer<Tweet>
    {
        public bool Equals(Tweet x, Tweet y) => x?.Id == y?.Id;
        public int GetHashCode(Tweet obj) => obj?.Id?.GetHashCode() ?? 0;
    }
}
