using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarketIntelligence.Analysis.Interfaces;
using MarketIntelligence.Core.Models;
using ScottPlot;

namespace MarketIntelligence.Analysis.Services
{
    public class VisualizationService : IVisualizationService
    {
        private readonly string _outputPath;

        public VisualizationService(string outputPath)
        {
            _outputPath = outputPath;
            Directory.CreateDirectory(_outputPath);
        }

        public void GenerateSignalPlots(List<MarketSignal> signals)
        {
            // Group signals by symbol
            var signalsBySymbol = signals.GroupBy(s => s.Symbol);

            foreach (var group in signalsBySymbol)
            {
                GenerateSymbolPlot(group.Key, group.ToList());
            }

            GenerateCompositeDashboard(signals);
        }

        private void GenerateSymbolPlot(string symbol, List<MarketSignal> signals)
        {
            ScottPlot.Plot plt = new();
            plt.Title($"{symbol} Market Signals");
            plt.Axes.Bottom.Label.Text = "Time";
            plt.Axes.Left.Label.Text = "Signal Score";

            // Use streaming approach for large datasets
            var timestamps = signals.Select(s => s.Timestamp.ToOADate()).ToArray();
            var sentiments = signals.Select(s => s.SentimentScore).ToArray();
            var composites = signals.Select(s => s.CompositeScore).ToArray();

            // Sample data if too large
            if (signals.Count > 1000)
            {
                var sampleIndices = GetSampleIndices(signals.Count, 1000);
                timestamps = sampleIndices.Select(i => timestamps[i]).ToArray();
                sentiments = sampleIndices.Select(i => sentiments[i]).ToArray();
                composites = sampleIndices.Select(i => composites[i]).ToArray();
            }

            var sentimentScatter = plt.Add.Scatter(timestamps, sentiments);
            sentimentScatter.LegendText = "Sentiment";
            sentimentScatter.MarkerSize = 3;

            var compositeScatter = plt.Add.Scatter(timestamps, composites);
            compositeScatter.LegendText = "Composite";
            compositeScatter.MarkerSize = 3;

            // Add signal regions
            AddSignalRegions(plt, signals);

            plt.ShowLegend();
            plt.SavePng(Path.Combine(_outputPath, $"{symbol}_signals.png"), 800, 600);
        }

        private void GenerateCompositeDashboard(List<MarketSignal> signals)
        {
            ScottPlot.Plot plt = new();
            plt.Title("Market Intelligence Dashboard");

            // Create subplots effect
            var symbols = signals.Select(s => s.Symbol).Distinct().ToArray();
            var symbolData = symbols.Select(s =>
            {
                var symbolSignals = signals.Where(sig => sig.Symbol == s).ToList();
                return new
                {
                    Symbol = s,
                    AvgSentiment = symbolSignals.Average(sig => sig.SentimentScore),
                    AvgVolume = symbolSignals.Average(sig => sig.VolumeScore),
                    SignalCount = symbolSignals.Count,
                    BullishCount = symbolSignals.Count(sig => sig.Type == SignalType.Bullish),
                    BearishCount = symbolSignals.Count(sig => sig.Type == SignalType.Bearish)
                };
            }).ToArray();

            // Bar chart for signal distribution
            var positions = Enumerable.Range(0, symbols.Length).Select(i => (double)i).ToArray();
            var bullishCounts = symbolData.Select(d => (double)d.BullishCount).ToArray();
            var bearishCounts = symbolData.Select(d => (double)d.BearishCount).ToArray();

            var bullishBars = plt.Add.Bars(positions, bullishCounts);
            bullishBars.LegendText = "Bullish";
            bullishBars.Color = ScottPlot.Colors.Green;

            var bearishBars = plt.Add.Bars(positions, bearishCounts.Select(v => -v).ToArray());
            bearishBars.LegendText = "Bearish";
            bearishBars.Color = ScottPlot.Colors.Red;

            plt.Axes.Bottom.SetTicks(positions, symbols);
            plt.ShowLegend();
            plt.SavePng(Path.Combine(_outputPath, "market_dashboard.png"), 1200, 800);
        }

        private void AddSignalRegions(ScottPlot.Plot plt, List<MarketSignal> signals)
        {
            var bullishRegions = GetContinuousRegions(signals, SignalType.Bullish);
            var bearishRegions = GetContinuousRegions(signals, SignalType.Bearish);

            foreach (var region in bullishRegions)
            {
                var span = plt.Add.VerticalSpan(region.Start.ToOADate(), region.End.ToOADate());
                span.FillColor = ScottPlot.Color.FromHex("#1e90ff").WithAlpha(30);
            }

            foreach (var region in bearishRegions)
            {
                var span = plt.Add.VerticalSpan(region.Start.ToOADate(), region.End.ToOADate());
                span.FillColor = ScottPlot.Color.FromHex("#ff0000").WithAlpha(30);
            }
        }

        private List<(DateTime Start, DateTime End)> GetContinuousRegions(
            List<MarketSignal> signals, SignalType type)
        {
            var regions = new List<(DateTime Start, DateTime End)>();
            DateTime? regionStart = null;

            foreach (var signal in signals.OrderBy(s => s.Timestamp))
            {
                if (signal.Type == type && regionStart == null)
                {
                    regionStart = signal.Timestamp;
                }
                else if (signal.Type != type && regionStart != null)
                {
                    regions.Add((regionStart.Value, signal.Timestamp));
                    regionStart = null;
                }
            }

            if (regionStart != null && signals.Any())
            {
                regions.Add((regionStart.Value, signals.Max(s => s.Timestamp)));
            }

            return regions;
        }

        private int[] GetSampleIndices(int totalCount, int sampleSize)
        {
            var step = totalCount / sampleSize;
            return Enumerable.Range(0, sampleSize)
                .Select(i => Math.Min(i * step, totalCount - 1))
                .ToArray();
        }
    }
}