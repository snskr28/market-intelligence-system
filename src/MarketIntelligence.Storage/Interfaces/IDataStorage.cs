using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketIntelligence.Core.Models;

namespace MarketIntelligence.Storage.Interfaces
{
    public interface IDataStorage
    {
        Task SaveTweetsAsync(List<Tweet> tweets, string fileName = null);
        Task<List<Tweet>> LoadTweetsAsync(string fileName);
        Task SaveSignalsAsync(List<MarketSignal> signals, string fileName = null);
    }
}
