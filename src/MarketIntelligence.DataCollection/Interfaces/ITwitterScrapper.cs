using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketIntelligence.Core.Models;

namespace MarketIntelligence.DataCollection.Interfaces
{
    public interface ITwitterScraper
    {
        Task<List<Tweet>> ScrapeTweetsAsync(List<string> hashtags, int targetCount, CancellationToken cancellationToken);
    }
}
