using System.Collections.Generic;
using AgentSDK;

namespace BalanceRunner.Telemetry
{
    /// <summary>
    /// A point-in-time snapshot of one agent's state, sampled periodically during a match.
    /// </summary>
    public class TimelineSnapshot
    {
        /// <summary>Tick when this snapshot was taken.</summary>
        public int Tick { get; set; }

        /// <summary>Current gold balance.</summary>
        public int Gold { get; set; }

        /// <summary>Cumulative gold mined so far.</summary>
        public int GoldMined { get; set; }

        /// <summary>Cumulative gold spent so far.</summary>
        public int GoldSpent { get; set; }

        /// <summary>Number of pawns alive.</summary>
        public int PawnCount { get; set; }

        /// <summary>Total army value (sum of COST for living military units).</summary>
        public int ArmyValue { get; set; }

        /// <summary>Count of living military units by type.</summary>
        public Dictionary<UnitType, int> UnitCounts { get; set; } = new Dictionary<UnitType, int>();

        /// <summary>Total current HP of all owned units.</summary>
        public float TotalHp { get; set; }

        /// <summary>Total max HP of all owned units (for HP% calculation).</summary>
        public float TotalMaxHp { get; set; }

        /// <summary>Cumulative enemy gold value killed so far.</summary>
        public int EnemyGoldKilled { get; set; }

        /// <summary>Cumulative own gold value lost so far.</summary>
        public int OwnGoldLost { get; set; }
    }

    /// <summary>
    /// A milestone event that occurred at a specific tick.
    /// </summary>
    public class MilestoneEvent
    {
        /// <summary>Tick when the event occurred.</summary>
        public int Tick { get; set; }

        /// <summary>Type of milestone.</summary>
        public string Type { get; set; }

        /// <summary>Description of what happened.</summary>
        public string Description { get; set; }
    }

    /// <summary>
    /// Complete timeline data for one agent in one match.
    /// </summary>
    public class AgentTimeline
    {
        /// <summary>Periodic snapshots sampled every N ticks.</summary>
        public List<TimelineSnapshot> Snapshots { get; set; } = new List<TimelineSnapshot>();

        /// <summary>Milestone events (buildings completed, first attack, peak army, etc.).</summary>
        public List<MilestoneEvent> Milestones { get; set; } = new List<MilestoneEvent>();
    }
}
