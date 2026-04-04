using System;
using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using AgentTestHarness;
using BalanceRunner.Telemetry;

namespace BalanceRunner.Runner
{
    /// <summary>
    /// Non-invasive observer that wraps a SimGame and collects per-agent
    /// telemetry by comparing state snapshots before and after each tick.
    /// Does not modify SimGame behavior — uses only the public/internal query API.
    /// </summary>
    internal class TelemetryCollector
    {
        private readonly SimGame _game;
        private readonly AgentMatchStats[] _stats;

        // Snapshot state for diff detection
        private Dictionary<int, UnitSnapshot> _previousUnits = new Dictionary<int, UnitSnapshot>();
        private int[] _previousGold = new int[2];

        // Timeline sampling
        private const int SAMPLE_INTERVAL = 50;
        private int[] _cumulativeEnemyKilled = new int[2];
        private int[] _cumulativeOwnLost = new int[2];
        private HashSet<UnitType>[] _buildingsCompleted = new HashSet<UnitType>[]
        {
            new HashSet<UnitType>(), new HashSet<UnitType>()
        };
        private int[] _peakArmyValueSoFar = new int[2];

        private static readonly UnitType[] MilitaryTypes = new[]
        {
            UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER
        };

        private static readonly UnitType[] BuildingTypes = new[]
        {
            UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER, UnitType.MONASTERY
        };

        public TelemetryCollector(SimGame game)
        {
            _game = game;
            _stats = new AgentMatchStats[] { new AgentMatchStats(), new AgentMatchStats() };

            // Take initial snapshot
            SnapshotState();
        }

        /// <summary>Get accumulated stats for an agent.</summary>
        public AgentMatchStats GetStats(int agentNbr) => _stats[agentNbr];

        /// <summary>
        /// Advance one tick and collect telemetry from the state diff.
        /// </summary>
        public void TickAndCollect()
        {
            var unitsBefore = _previousUnits;
            int[] goldBefore = { _previousGold[0], _previousGold[1] };

            _game.Tick();

            var unitsAfter = SnapshotCurrentUnits();
            int currentTick = _game.CurrentTick;

            // Detect births (new unit IDs not in previous snapshot)
            foreach (var kvp in unitsAfter)
            {
                if (!unitsBefore.ContainsKey(kvp.Key))
                {
                    int owner = kvp.Value.OwnerAgentNbr;
                    if (owner < 0 || owner > 1) continue;

                    UnitType type = kvp.Value.UnitType;
                    _stats[owner].RecordProduction(type);
                    _stats[owner].GoldSpent += (int)GameConstants.COST[type];

                    // Track first military unit
                    if (_stats[owner].FirstMilitaryTick < 0 && IsMilitary(type))
                        _stats[owner].FirstMilitaryTick = currentTick;
                }
            }

            // Detect deaths (unit IDs in previous but not in current)
            foreach (var kvp in unitsBefore)
            {
                if (!unitsAfter.ContainsKey(kvp.Key))
                {
                    int owner = kvp.Value.OwnerAgentNbr;
                    if (owner < 0 || owner > 1) continue;

                    UnitType deadType = kvp.Value.UnitType;
                    _stats[owner].RecordLoss(deadType);

                    int deadValue = (int)GameConstants.COST[deadType];
                    _cumulativeOwnLost[owner] += deadValue;

                    // Track first kill (enemy died)
                    int killer = 1 - owner;
                    _cumulativeEnemyKilled[killer] += deadValue;
                    if (_stats[killer].FirstKillTick < 0)
                        _stats[killer].FirstKillTick = currentTick;
                }
            }

            // Detect building completions (milestone events)
            foreach (var kvp in unitsAfter)
            {
                int owner = kvp.Value.OwnerAgentNbr;
                if (owner < 0 || owner > 1) continue;
                UnitType type = kvp.Value.UnitType;

                if (Array.IndexOf(BuildingTypes, type) >= 0
                    && !_buildingsCompleted[owner].Contains(type))
                {
                    // Check if it just became built (wasn't in previous or wasn't built)
                    if (unitsBefore.TryGetValue(kvp.Key, out var prev))
                    {
                        // Building existed before — skip (was already counted at birth)
                    }
                    else
                    {
                        // New building appeared
                        _buildingsCompleted[owner].Add(type);
                        _stats[owner].Timeline.Milestones.Add(new MilestoneEvent
                        {
                            Tick = currentTick,
                            Type = "BuildingStarted",
                            Description = $"{type} construction started"
                        });
                    }
                }
            }

            // Detect first attack (unit transitioned to ATTACK action)
            foreach (var kvp in unitsAfter)
            {
                int owner = kvp.Value.OwnerAgentNbr;
                if (owner < 0 || owner > 1) continue;
                if (_stats[owner].FirstAttackTick >= 0) continue;

                if (kvp.Value.CurrentAction == UnitAction.ATTACK)
                {
                    if (!unitsBefore.TryGetValue(kvp.Key, out var prev)
                        || prev.CurrentAction != UnitAction.ATTACK)
                    {
                        _stats[owner].FirstAttackTick = currentTick;
                    }
                }
            }

            // Track gold mined (positive gold delta not explained by unit deaths refunding)
            for (int a = 0; a < 2; a++)
            {
                int currentGold = _game.GetGold(a);
                int goldDelta = currentGold - goldBefore[a];
                // Gold increases come from mining; decreases from training/building.
                // We already track spending via births. Approximate mining as positive deltas.
                if (goldDelta > 0)
                    _stats[a].GoldMined += goldDelta;
            }

            // Track peak army value + milestone
            for (int a = 0; a < 2; a++)
            {
                int armyValue = 0;
                foreach (var kvp in unitsAfter)
                {
                    if (kvp.Value.OwnerAgentNbr == a && IsMilitary(kvp.Value.UnitType))
                        armyValue += (int)GameConstants.COST[kvp.Value.UnitType];
                }
                if (armyValue > _stats[a].PeakArmyValue)
                {
                    _stats[a].PeakArmyValue = armyValue;
                    _peakArmyValueSoFar[a] = armyValue;
                }
            }

            // Sample timeline snapshot every N ticks
            if (currentTick % SAMPLE_INTERVAL == 0)
            {
                for (int a = 0; a < 2; a++)
                    RecordTimelineSnapshot(a, currentTick, unitsAfter);
            }

            // Update snapshot for next tick
            _previousUnits = unitsAfter;
            _previousGold[0] = _game.GetGold(0);
            _previousGold[1] = _game.GetGold(1);
        }

        /// <summary>
        /// FinalizeStats stats with end-of-match data (surviving units, final gold, HP%).
        /// Call after the match loop completes.
        /// </summary>
        public void FinalizeStats()
        {
            for (int a = 0; a < 2; a++)
            {
                _stats[a].FinalGold = _game.GetGold(a);

                float totalHp = 0;
                float totalMaxHp = 0;

                foreach (var unit in _game.Units.Values)
                {
                    if (unit.OwnerAgentNbr != a) continue;

                    UnitType type = unit.UnitType;
                    if (!_stats[a].SurvivingUnits.ContainsKey(type))
                        _stats[a].SurvivingUnits[type] = 0;
                    _stats[a].SurvivingUnits[type]++;

                    totalHp += unit.Health;
                    totalMaxHp += GameConstants.HEALTH[type];
                }

                _stats[a].SurvivingHpPercent = totalMaxHp > 0 ? totalHp / totalMaxHp * 100f : 0f;

                // Add key milestones
                if (_stats[a].FirstMilitaryTick >= 0)
                    _stats[a].Timeline.Milestones.Add(new MilestoneEvent
                    {
                        Tick = _stats[a].FirstMilitaryTick,
                        Type = "FirstMilitary",
                        Description = "First military unit trained"
                    });
                if (_stats[a].FirstAttackTick >= 0)
                    _stats[a].Timeline.Milestones.Add(new MilestoneEvent
                    {
                        Tick = _stats[a].FirstAttackTick,
                        Type = "FirstAttack",
                        Description = "First attack launched"
                    });
                if (_stats[a].FirstKillTick >= 0)
                    _stats[a].Timeline.Milestones.Add(new MilestoneEvent
                    {
                        Tick = _stats[a].FirstKillTick,
                        Type = "FirstKill",
                        Description = "First enemy unit killed"
                    });

                // Sort milestones by tick
                _stats[a].Timeline.Milestones.Sort((x, y) => x.Tick.CompareTo(y.Tick));

                // Record final snapshot
                RecordTimelineSnapshot(a, _game.CurrentTick, SnapshotCurrentUnits());
            }
        }

        private void RecordTimelineSnapshot(int agentNbr, int tick,
            Dictionary<int, UnitSnapshot> units)
        {
            var snapshot = new TimelineSnapshot
            {
                Tick = tick,
                Gold = _game.GetGold(agentNbr),
                GoldMined = _stats[agentNbr].GoldMined,
                GoldSpent = _stats[agentNbr].GoldSpent,
                EnemyGoldKilled = _cumulativeEnemyKilled[agentNbr],
                OwnGoldLost = _cumulativeOwnLost[agentNbr]
            };

            int pawnCount = 0;
            int armyValue = 0;
            float totalHp = 0;
            float totalMaxHp = 0;

            foreach (var kvp in units)
            {
                if (kvp.Value.OwnerAgentNbr != agentNbr) continue;

                UnitType type = kvp.Value.UnitType;

                if (type == UnitType.PAWN)
                    pawnCount++;

                if (IsMilitary(type))
                {
                    armyValue += (int)GameConstants.COST[type];
                    if (!snapshot.UnitCounts.ContainsKey(type))
                        snapshot.UnitCounts[type] = 0;
                    snapshot.UnitCounts[type]++;
                }

                if (type == UnitType.MONK)
                {
                    if (!snapshot.UnitCounts.ContainsKey(type))
                        snapshot.UnitCounts[type] = 0;
                    snapshot.UnitCounts[type]++;
                }

                totalHp += kvp.Value.Health;
                totalMaxHp += GameConstants.HEALTH[type];
            }

            snapshot.PawnCount = pawnCount;
            snapshot.ArmyValue = armyValue;
            snapshot.TotalHp = totalHp;
            snapshot.TotalMaxHp = totalMaxHp;

            _stats[agentNbr].Timeline.Snapshots.Add(snapshot);
        }

        private void SnapshotState()
        {
            _previousUnits = SnapshotCurrentUnits();
            _previousGold[0] = _game.GetGold(0);
            _previousGold[1] = _game.GetGold(1);
        }

        private Dictionary<int, UnitSnapshot> SnapshotCurrentUnits()
        {
            var snapshot = new Dictionary<int, UnitSnapshot>();
            foreach (var kvp in _game.Units)
            {
                var u = kvp.Value;
                snapshot[kvp.Key] = new UnitSnapshot
                {
                    UnitType = u.UnitType,
                    OwnerAgentNbr = u.OwnerAgentNbr,
                    CurrentAction = u.CurrentAction,
                    Health = u.Health
                };
            }
            return snapshot;
        }

        private static bool IsMilitary(UnitType type)
        {
            return type == UnitType.WARRIOR || type == UnitType.ARCHER || type == UnitType.LANCER;
        }

        private struct UnitSnapshot
        {
            public UnitType UnitType;
            public int OwnerAgentNbr;
            public UnitAction CurrentAction;
            public float Health;
        }
    }
}
