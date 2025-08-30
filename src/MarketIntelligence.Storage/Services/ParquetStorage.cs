using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using MarketIntelligence.Core.Models;
using MarketIntelligence.Storage.Interfaces;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace MarketIntelligence.Storage.Services
{
    public class ParquetStorage : IDataStorage
    {
        private readonly string _basePath;
        private readonly ParquetSchema _tweetSchema;
        private readonly ParquetSchema _signalSchema;

        public ParquetStorage(string basePath)
        {
            _basePath = basePath;
            Directory.CreateDirectory(_basePath);

            _tweetSchema = new ParquetSchema(
                new DataField<string>("Id"),
                new DataField<string>("Username"),
                new DataField<DateTime>("Timestamp"),
                new DataField<string>("Content"),
                new DataField<int>("Likes"),
                new DataField<int>("Retweets"),
                new DataField<int>("Replies"),
                new DataField<string>("Hashtags"),
                new DataField<string>("Mentions"),
                new DataField<string>("Language"),
                new DataField<double>("SentimentScore"),
                new DataField<double>("EngagementScore")
            );

            _signalSchema = new ParquetSchema(
                new DataField<DateTime>("Timestamp"),
                new DataField<string>("Symbol"),
                new DataField<double>("SentimentScore"),
                new DataField<double>("VolumeScore"),
                new DataField<double>("MomentumScore"),
                new DataField<double>("CompositeScore"),
                new DataField<double>("Confidence"),
                new DataField<string>("Type")
            );
        }

        public async Task SaveTweetsAsync(List<Tweet> tweets, string fileName = null)
        {
            fileName ??= $"tweets_{DateTime.UtcNow:yyyyMMdd_HHmmss}.parquet";
            var filePath = Path.Combine(_basePath, fileName);

            // Deduplicate tweets
            var uniqueTweets = tweets.GroupBy(t => t.Id).Select(g => g.First()).ToList();

            using var file = File.Create(filePath);
            using var writer = await ParquetWriter.CreateAsync(_tweetSchema, file);

            writer.CompressionMethod = CompressionMethod.Snappy;

            using var groupWriter = writer.CreateRowGroup();

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[0],
                uniqueTweets.Select(t => t.Id).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[1],
                uniqueTweets.Select(t => t.Username).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[2],
                uniqueTweets.Select(t => t.Timestamp).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[3],
                uniqueTweets.Select(t => t.Content).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[4],
                uniqueTweets.Select(t => t.Likes).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[5],
                uniqueTweets.Select(t => t.Retweets).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[6],
                uniqueTweets.Select(t => t.Replies).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[7],
                uniqueTweets.Select(t => string.Join(",", t.Hashtags)).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[8],
                uniqueTweets.Select(t => string.Join(",", t.Mentions)).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[9],
                uniqueTweets.Select(t => t.Language ?? "en").ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[10],
                uniqueTweets.Select(t => t.Features.GetValueOrDefault("sentiment_score", 0.0)).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _tweetSchema.DataFields[11],
                uniqueTweets.Select(t => t.Features.GetValueOrDefault("engagement_score", 0.0)).ToArray()));
        }

        public async Task<List<Tweet>> LoadTweetsAsync(string fileName)
        {
            var filePath = Path.Combine(_basePath, fileName);
            var tweets = new List<Tweet>();

            using var file = File.OpenRead(filePath);
            using var reader = await ParquetReader.CreateAsync(file);

            for (int i = 0; i < reader.RowGroupCount; i++)
            {
                using var groupReader = reader.OpenRowGroupReader(i);

                // Read columns without generic type arguments
                var ids = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[0])).Data.Cast<string>().ToArray();
                var usernames = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[1])).Data.Cast<string>().ToArray();
                var timestamps = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[2])).Data.Cast<DateTime>().ToArray();
                var contents = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[3])).Data.Cast<string>().ToArray();
                var likes = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[4])).Data.Cast<int>().ToArray();
                var retweets = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[5])).Data.Cast<int>().ToArray();
                var replies = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[6])).Data.Cast<int>().ToArray();
                var hashtags = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[7])).Data.Cast<string>().ToArray();
                var mentions = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[8])).Data.Cast<string>().ToArray();
                var languages = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[9])).Data.Cast<string>().ToArray();
                var sentimentScores = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[10])).Data.Cast<double>().ToArray();
                var engagementScores = (await groupReader.ReadColumnAsync(_tweetSchema.DataFields[11])).Data.Cast<double>().ToArray();

                for (int j = 0; j < ids.Length; j++)
                {
                    tweets.Add(new Tweet
                    {
                        Id = ids[j],
                        Username = usernames[j],
                        Timestamp = timestamps[j],
                        Content = contents[j],
                        Likes = likes[j],
                        Retweets = retweets[j],
                        Replies = replies[j],
                        Hashtags = string.IsNullOrEmpty(hashtags[j])
                            ? new List<string>()
                            : hashtags[j].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                        Mentions = string.IsNullOrEmpty(mentions[j])
                            ? new List<string>()
                            : mentions[j].Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                        Language = languages[j],
                        Features = new Dictionary<string, double>
                        {
                            ["sentiment_score"] = sentimentScores[j],
                            ["engagement_score"] = engagementScores[j]
                        }
                    });
                }
            }

            return tweets;
        }

        public async Task SaveSignalsAsync(List<MarketSignal> signals, string fileName = null)
        {
            fileName ??= $"signals_{DateTime.UtcNow:yyyyMMdd_HHmmss}.parquet";
            var filePath = Path.Combine(_basePath, fileName);

            using var file = File.Create(filePath);
            using var writer = await ParquetWriter.CreateAsync(_signalSchema, file);

            writer.CompressionMethod = CompressionMethod.Snappy;

            using var groupWriter = writer.CreateRowGroup();

            await groupWriter.WriteColumnAsync(new DataColumn(
                _signalSchema.DataFields[0],
                signals.Select(s => s.Timestamp).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _signalSchema.DataFields[1],
                signals.Select(s => s.Symbol).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _signalSchema.DataFields[2],
                signals.Select(s => s.SentimentScore).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _signalSchema.DataFields[3],
                signals.Select(s => s.VolumeScore).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _signalSchema.DataFields[4],
                signals.Select(s => s.MomentumScore).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _signalSchema.DataFields[5],
                signals.Select(s => s.CompositeScore).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _signalSchema.DataFields[6],
                signals.Select(s => s.Confidence).ToArray()));

            await groupWriter.WriteColumnAsync(new DataColumn(
                _signalSchema.DataFields[7],
                signals.Select(s => s.Type.ToString()).ToArray()));
        }
    }
}