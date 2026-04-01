using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Immutable snapshot of a single unit's complete state.
    /// </summary>
    public struct UnitSnapshot
    {
        public int UnitNbr;
        public UnitType UnitType;
        public int OwnerAgentNbr;
        public Position GridPosition;
        public float Health;
        public bool IsBuilt;
        public UnitAction CurrentAction;
        public float PathProgress;
        public int PathIndex;
        public int PathCount;
        public float TrainTimer;
        public UnitType TrainTarget;
        public float BuildTimer;
        public UnitType BuildTarget;
        public Position BuildSite;
        public bool BuildPlaced;
        public int GatherMineNbr;
        public int GatherBaseNbr;
        public GatherPhase GatherPhase;
        public float MiningTimer;
        public int AttackTargetNbr;
        public int RepairBuildingNbr;
        public int HealTargetNbr;
        public float Mana;
        public int LocalAvoidWaitTicks;

        internal static UnitSnapshot FromSimUnit(SimUnit u)
        {
            return new UnitSnapshot
            {
                UnitNbr = u.UnitNbr,
                UnitType = u.UnitType,
                OwnerAgentNbr = u.OwnerAgentNbr,
                GridPosition = u.GridPosition,
                Health = u.Health,
                IsBuilt = u.IsBuilt,
                CurrentAction = u.CurrentAction,
                PathProgress = u.PathProgress,
                PathIndex = u.PathIndex,
                PathCount = u.Path != null ? u.Path.Count : -1,
                TrainTimer = u.TrainTimer,
                TrainTarget = u.TrainTarget,
                BuildTimer = u.BuildTimer,
                BuildTarget = u.BuildTarget,
                BuildSite = u.BuildSite,
                BuildPlaced = u.BuildPlaced,
                GatherMineNbr = u.GatherMineNbr,
                GatherBaseNbr = u.GatherBaseNbr,
                GatherPhase = u.GatherPhase,
                MiningTimer = u.MiningTimer,
                AttackTargetNbr = u.AttackTargetNbr,
                RepairBuildingNbr = u.RepairBuildingNbr,
                HealTargetNbr = u.HealTargetNbr,
                Mana = u.Mana,
                LocalAvoidWaitTicks = u.LocalAvoidWaitTicks
            };
        }
    }

    /// <summary>
    /// Immutable snapshot of the entire game state at a single tick.
    /// Supports field-level diff for diagnosing exactly what changed.
    /// </summary>
    public class StateSnapshot
    {
        public int CurrentTick { get; }
        public int Gold0 { get; }
        public int Gold1 { get; }
        public int NextUnitNbr { get; }
        public IReadOnlyDictionary<int, UnitSnapshot> Units { get; }

        private StateSnapshot(int currentTick, int gold0, int gold1, int nextUnitNbr,
            Dictionary<int, UnitSnapshot> units)
        {
            CurrentTick = currentTick;
            Gold0 = gold0;
            Gold1 = gold1;
            NextUnitNbr = nextUnitNbr;
            Units = units;
        }

        /// <summary>
        /// Capture a snapshot of the current game state.
        /// </summary>
        public static StateSnapshot Capture(SimGame game)
        {
            var units = new Dictionary<int, UnitSnapshot>();
            foreach (var kvp in game.Units)
            {
                units[kvp.Key] = UnitSnapshot.FromSimUnit(kvp.Value);
            }

            return new StateSnapshot(
                game.CurrentTick,
                game.Gold[0],
                game.Gold[1],
                game.NextUnitNbr,
                units);
        }

        /// <summary>
        /// Produce a human-readable diff between two snapshots.
        /// Returns empty string if snapshots are identical.
        /// </summary>
        public static string Diff(StateSnapshot a, StateSnapshot b)
        {
            var sb = new StringBuilder();

            // Global state
            if (a.CurrentTick != b.CurrentTick)
                sb.AppendLine($"  CurrentTick: {a.CurrentTick} -> {b.CurrentTick}");
            if (a.Gold0 != b.Gold0)
                sb.AppendLine($"  Gold[0]: {a.Gold0} -> {b.Gold0}");
            if (a.Gold1 != b.Gold1)
                sb.AppendLine($"  Gold[1]: {a.Gold1} -> {b.Gold1}");
            if (a.NextUnitNbr != b.NextUnitNbr)
                sb.AppendLine($"  NextUnitNbr: {a.NextUnitNbr} -> {b.NextUnitNbr}");

            // Units only in A
            var allKeys = new HashSet<int>(a.Units.Keys);
            allKeys.UnionWith(b.Units.Keys);

            foreach (int key in allKeys.OrderBy(k => k))
            {
                bool inA = a.Units.ContainsKey(key);
                bool inB = b.Units.ContainsKey(key);

                if (inA && !inB)
                {
                    sb.AppendLine($"  Unit {key} ({a.Units[key].UnitType}): present in A only");
                    continue;
                }
                if (!inA && inB)
                {
                    sb.AppendLine($"  Unit {key} ({b.Units[key].UnitType}): present in B only");
                    continue;
                }

                var ua = a.Units[key];
                var ub = b.Units[key];
                var unitDiffs = DiffUnit(ua, ub);
                if (unitDiffs.Length > 0)
                {
                    sb.AppendLine($"  Unit {key} ({ua.UnitType}):");
                    sb.Append(unitDiffs);
                }
            }

            return sb.ToString();
        }

        private static string DiffUnit(UnitSnapshot a, UnitSnapshot b)
        {
            var sb = new StringBuilder();

            void Check<T>(string name, T va, T vb)
            {
                if (!EqualityComparer<T>.Default.Equals(va, vb))
                    sb.AppendLine($"    {name}: {va} -> {vb}");
            }

            Check("GridPosition", a.GridPosition, b.GridPosition);
            Check("Health", a.Health, b.Health);
            Check("IsBuilt", a.IsBuilt, b.IsBuilt);
            Check("CurrentAction", a.CurrentAction, b.CurrentAction);
            Check("PathProgress", a.PathProgress, b.PathProgress);
            Check("PathIndex", a.PathIndex, b.PathIndex);
            Check("PathCount", a.PathCount, b.PathCount);
            Check("TrainTimer", a.TrainTimer, b.TrainTimer);
            Check("TrainTarget", a.TrainTarget, b.TrainTarget);
            Check("BuildTimer", a.BuildTimer, b.BuildTimer);
            Check("BuildTarget", a.BuildTarget, b.BuildTarget);
            Check("BuildSite", a.BuildSite, b.BuildSite);
            Check("BuildPlaced", a.BuildPlaced, b.BuildPlaced);
            Check("GatherMineNbr", a.GatherMineNbr, b.GatherMineNbr);
            Check("GatherBaseNbr", a.GatherBaseNbr, b.GatherBaseNbr);
            Check("GatherPhase", a.GatherPhase, b.GatherPhase);
            Check("MiningTimer", a.MiningTimer, b.MiningTimer);
            Check("AttackTargetNbr", a.AttackTargetNbr, b.AttackTargetNbr);
            Check("RepairBuildingNbr", a.RepairBuildingNbr, b.RepairBuildingNbr);
            Check("HealTargetNbr", a.HealTargetNbr, b.HealTargetNbr);
            Check("Mana", a.Mana, b.Mana);
            Check("LocalAvoidWaitTicks", a.LocalAvoidWaitTicks, b.LocalAvoidWaitTicks);

            return sb.ToString();
        }
    }
}
