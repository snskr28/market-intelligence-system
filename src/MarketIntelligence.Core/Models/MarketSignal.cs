using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketIntelligence.Core.Models
{
    public class MarketSignal
    {
        public DateTime Timestamp { get; set; }
        public string Symbol { get; set; }
        public double SentimentScore { get; set; }
        public double VolumeScore { get; set; }
        public double MomentumScore { get; set; }
        public double CompositeScore { get; set; }
        public double Confidence { get; set; }
        public SignalType Type { get; set; }
    }

    public enum SignalType
    {
        Bullish,
        Bearish,
        Neutral
    }
}
