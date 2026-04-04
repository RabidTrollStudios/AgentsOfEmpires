using System.Collections.Generic;
using AgentSDK;

namespace BalanceRunner.Telemetry
{
    /// <summary>
    /// Per-agent statistics collected during a single match.
    /// Tracks production, losses, economy, and key timing milestones.
    /// </summary>
    public class AgentMatchStats
    {
        /// <summary>Gold remaining at match end.</summary>
        public int FinalGold { get; set; }

        /// <summary>Total gold gained from mining (estimated from positive gold deltas minus refunds).</summary>
        public int GoldMined { get; set; }

        /// <summary>Total gold spent on units and buildings (sum of COST for each unit produced).</summary>
        public int GoldSpent { get; set; }

        /// <summary>Units produced during the match, by type.</summary>
        public Dictionary<UnitType, int> UnitsProduced { get; set; } = new Dictionary<UnitType, int>();

        /// <summary>Units lost (killed) during the match, by type.</summary>
        public Dictionary<UnitType, int> UnitsLost { get; set; } = new Dictionary<UnitType, int>();

        /// <summary>Maximum total army value (sum of COST for all living military units) at any tick.</summary>
        public int PeakArmyValue { get; set; }

        /// <summary>Tick when the first military unit (warrior, archer, lancer) finished training. -1 if never.</summary>
        public int FirstMilitaryTick { get; set; } = -1;

        /// <summary>Tick when the agent's first attack command was observed (unit entered ATTACK action). -1 if never.</summary>
        public int FirstAttackTick { get; set; } = -1;

        /// <summary>Tick when the agent first killed an enemy unit. -1 if never.</summary>
        public int FirstKillTick { get; set; } = -1;

        /// <summary>Count of surviving units at match end, by type.</summary>
        public Dictionary<UnitType, int> SurvivingUnits { get; set; } = new Dictionary<UnitType, int>();

        /// <summary>Total HP percentage of surviving units relative to their max HP.</summary>
        public float SurvivingHpPercent { get; set; }

        /// <summary>Timeline data: periodic snapshots and milestone events.</summary>
        public AgentTimeline Timeline { get; set; } = new AgentTimeline();

        /// <summary>Increment production count for a unit type.</summary>
        public void RecordProduction(UnitType type)
        {
            if (!UnitsProduced.ContainsKey(type))
                UnitsProduced[type] = 0;
            UnitsProduced[type]++;
        }

        /// <summary>Increment loss count for a unit type.</summary>
        public void RecordLoss(UnitType type)
        {
            if (!UnitsLost.ContainsKey(type))
                UnitsLost[type] = 0;
            UnitsLost[type]++;
        }

        /// <summary>Get production count for a type (0 if not produced).</summary>
        public int GetProduced(UnitType type)
        {
            return UnitsProduced.TryGetValue(type, out int count) ? count : 0;
        }

        /// <summary>Get loss count for a type (0 if none lost).</summary>
        public int GetLost(UnitType type)
        {
            return UnitsLost.TryGetValue(type, out int count) ? count : 0;
        }
    }
}
