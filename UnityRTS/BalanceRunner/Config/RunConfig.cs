using System.Collections.Generic;
using AgentSDK;

namespace BalanceRunner.Config
{
    /// <summary>
    /// Configuration for a batch balance evaluation run.
    /// </summary>
    public class RunConfig
    {
        /// <summary>Agent names to include. Empty or null means "all".</summary>
        public List<string> Agents { get; set; } = new List<string>();

        /// <summary>Number of seeds per matchup ordering (default 5).</summary>
        public int SeedCount { get; set; } = 5;

        /// <summary>Maximum frames per match before timeout (default 5000).</summary>
        public int FrameLimit { get; set; } = 5000;

        /// <summary>Map template to use (default OpenField).</summary>
        public MapTemplate MapTemplate { get; set; } = MapTemplate.OpenField;

        /// <summary>Whether to run both seat orderings per matchup (default true).</summary>
        public bool BothSeatOrderings { get; set; } = true;
    }
}
