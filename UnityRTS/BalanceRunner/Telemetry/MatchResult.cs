using AgentSDK;

namespace BalanceRunner.Telemetry
{
    /// <summary>
    /// Complete result of a single simulated match between two agents.
    /// Contains match metadata, outcome, and per-agent telemetry.
    /// </summary>
    public class MatchResult
    {
        /// <summary>Name of the agent in slot 0 (player 0).</summary>
        public string Agent0Name { get; set; }

        /// <summary>Name of the agent in slot 1 (player 1).</summary>
        public string Agent1Name { get; set; }

        /// <summary>Random seed used for map generation.</summary>
        public int Seed { get; set; }

        /// <summary>Map template used for this match.</summary>
        public MapTemplate MapTemplate { get; set; }

        /// <summary>Maximum frames allowed before timeout.</summary>
        public int FrameLimit { get; set; }

        /// <summary>Winner: 0 or 1 for the winning agent, -1 for draw/timeout.</summary>
        public int Winner { get; set; }

        /// <summary>Total frames elapsed when the match ended.</summary>
        public int DurationFrames { get; set; }

        /// <summary>How the match ended.</summary>
        public MatchEndReason EndReason { get; set; }

        /// <summary>Telemetry for agent 0.</summary>
        public AgentMatchStats Agent0Stats { get; set; }

        /// <summary>Telemetry for agent 1.</summary>
        public AgentMatchStats Agent1Stats { get; set; }

        /// <summary>Get stats for a specific agent slot.</summary>
        public AgentMatchStats GetStats(int agentNbr) =>
            agentNbr == 0 ? Agent0Stats : Agent1Stats;

        /// <summary>Get the name for a specific agent slot.</summary>
        public string GetAgentName(int agentNbr) =>
            agentNbr == 0 ? Agent0Name : Agent1Name;
    }

    /// <summary>
    /// How a match ended.
    /// </summary>
    public enum MatchEndReason
    {
        /// <summary>One side lost all units including base.</summary>
        Elimination,

        /// <summary>One side lost their base (but may have mobile units).</summary>
        BaseDestroyed,

        /// <summary>Frame limit reached; winner determined by score.</summary>
        Timeout,

        /// <summary>Both sides eliminated simultaneously or tied on score at timeout.</summary>
        Draw
    }
}
