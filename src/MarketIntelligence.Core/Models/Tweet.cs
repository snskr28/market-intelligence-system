using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MarketIntelligence.Core.Models
{
    public class Tweet
    {
        public string Id { get; set; }
        public string Username { get; set; }
        public DateTime Timestamp { get; set; }
        public string Content { get; set; }
        public int Likes { get; set; }
        public int Retweets { get; set; }
        public int Replies { get; set; }
        public List<string> Hashtags { get; set; } = new();
        public List<string> Mentions { get; set; } = new();
        public string Language { get; set; }
        public Dictionary<string, double> Features { get; set; } = new();
    }
}
