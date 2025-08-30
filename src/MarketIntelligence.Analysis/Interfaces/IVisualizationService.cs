using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarketIntelligence.Core.Models;

namespace MarketIntelligence.Analysis.Interfaces
{
    public interface IVisualizationService
    {
        void GenerateSignalPlots(List<MarketSignal> signals);
    }
}
