using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketIntelligence.Core.Models;

namespace MarketIntelligence.DataProcessing.Interfaces
{
    public interface ITextProcessor
    {
        void ProcessTweets(List<Tweet> tweets);
    }
}
