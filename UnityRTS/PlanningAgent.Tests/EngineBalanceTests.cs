using System.Collections.Generic;
using System.Linq;
using System.Text;
using AgentSDK;
using AgentTestHarness;
using Xunit;
using Xunit.Abstractions;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// Engine balance evaluation tests. Uses maximally simple "dumb" agents
    /// to isolate game constant effects from agent tactics.
    /// All combat tests run until all units of one side are destroyed.
    /// </summary>
    public partial class EngineBalanceTests
    {
        private readonly ITestOutputHelper _output;

        public EngineBalanceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        #region Test Agents

        /// <summary>
        /// Attacks the closest enemy each tick. No economy, no building, no micro.
        /// Used for pure combat tests with pre-spawned units.
        /// </summary>
        private class AttackClosestAgent : PlanningAgentBase
        {
            public override void InitializeMatch() { }

            public override void Update(IGameState state, IAgentActions actions)
            {
                UpdateGameState(state);
                foreach (var unitList in new[] { mySoldiers, myArchers, myWorkers })
                {
                    foreach (int unitNbr in unitList)
                    {
                        var info = state.GetUnit(unitNbr);
                        if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;

                        int? target = FindClosest(unitNbr, state);
                        if (target.HasValue)
                            actions.Attack(unitNbr, target.Value);
                    }
                }
            }

            private int? FindClosest(int attackerNbr, IGameState state)
            {
                var attackerInfo = state.GetUnit(attackerNbr);
                if (!attackerInfo.HasValue) return null;
                Position pos = attackerInfo.Value.CenterPosition;

                float bestDist = float.MaxValue;
                int? best = null;

                foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                                UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
                {
                    foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    {
                        var enemyInfo = state.GetUnit(enemyNbr);
                        if (!enemyInfo.HasValue) continue;
                        float dist = Position.Distance(pos, enemyInfo.Value.CenterPosition);
                        if (dist < bestDist) { bestDist = dist; best = enemyNbr; }
                    }
                }
                return best;
            }
        }

        /// <summary>
        /// All units focus fire on the same target (lowest HP enemy).
        /// Maximizes kills by concentrating damage. Reveals army-scale dynamics
        /// that AttackClosestAgent misses (it creates N parallel 1v1 duels).
        /// </summary>
        private class FocusFireAgent : PlanningAgentBase
        {
            public override void InitializeMatch() { }

            public override void Update(IGameState state, IAgentActions actions)
            {
                UpdateGameState(state);

                int? focusTarget = FindLowestHpEnemy(state);
                if (!focusTarget.HasValue) return;

                foreach (var unitList in new[] { mySoldiers, myArchers, myWorkers })
                {
                    foreach (int unitNbr in unitList)
                    {
                        var info = state.GetUnit(unitNbr);
                        if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;
                        actions.Attack(unitNbr, focusTarget.Value);
                    }
                }
            }

            private int? FindLowestHpEnemy(IGameState state)
            {
                float lowestHp = float.MaxValue;
                int? best = null;

                foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                                UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
                {
                    foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    {
                        var enemyInfo = state.GetUnit(enemyNbr);
                        if (!enemyInfo.HasValue) continue;
                        if (enemyInfo.Value.Health < lowestHp)
                        {
                            lowestHp = enemyInfo.Value.Health;
                            best = enemyNbr;
                        }
                    }
                }
                return best;
            }
        }

        /// <summary>
        /// Pure economy agent: trains up to N workers (configurable), gathers gold.
        /// No military, no barracks. Used for economy rate tests.
        /// </summary>
        private class PureEconomyAgent : PlanningAgentBase
        {
            private readonly int _maxWorkers;
            public PureEconomyAgent(int maxWorkers) { _maxWorkers = maxWorkers; }

            public override void InitializeMatch() { }

            public override void Update(IGameState state, IAgentActions actions)
            {
                UpdateGameState(state);
                mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;
                mainMineNbr = mines.Count > 0 ? mines[0] : -1;

                // Build base if none exists
                if (myBases.Count == 0)
                {
                    foreach (int worker in myWorkers)
                    {
                        var info = state.GetUnit(worker);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.BASE])
                        {
                            foreach (Position pos in buildPositions)
                            {
                                if (state.IsBoundedAreaBuildable(UnitType.BASE, pos))
                                {
                                    actions.Build(worker, pos, UnitType.BASE);
                                    return;
                                }
                            }
                        }
                    }
                    return;
                }

                // Train workers up to cap
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.WORKER]
                        && myWorkers.Count < _maxWorkers)
                    {
                        actions.Train(baseNbr, UnitType.WORKER);
                    }
                }

                // Gather
                if (mainBaseNbr < 0 || mainMineNbr < 0) return;
                var mineInfo = state.GetUnit(mainMineNbr);
                if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) return;

                foreach (int worker in myWorkers)
                {
                    var info = state.GetUnit(worker);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                        actions.Gather(worker, mainMineNbr, mainBaseNbr);
                }
            }
        }

        /// <summary>
        /// Economy agent that also builds barracks + refinery. Used to measure
        /// refinery's 2x mining boost relative to its cost.
        /// </summary>
        private class RefineryEconomyAgent : PlanningAgentBase
        {
            private readonly int _maxWorkers;
            private int _builderNbr;
            public RefineryEconomyAgent(int maxWorkers) { _maxWorkers = maxWorkers; }

            public override void InitializeMatch() { }

            public override void Update(IGameState state, IAgentActions actions)
            {
                UpdateGameState(state);
                mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;
                mainMineNbr = mines.Count > 0 ? mines[0] : -1;
                _builderNbr = -1;

                // Build base if none exists
                if (myBases.Count == 0)
                {
                    foreach (int worker in myWorkers)
                    {
                        var info = state.GetUnit(worker);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.BASE])
                        {
                            foreach (Position pos in buildPositions)
                            {
                                if (state.IsBoundedAreaBuildable(UnitType.BASE, pos))
                                {
                                    actions.Build(worker, pos, UnitType.BASE);
                                    return;
                                }
                            }
                        }
                    }
                    return;
                }

                // Train workers
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.WORKER]
                        && myWorkers.Count < _maxWorkers)
                    {
                        actions.Train(baseNbr, UnitType.WORKER);
                    }
                }

                // Build barracks then refinery
                if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                    BuildAny(UnitType.BARRACKS, state, actions);
                else if (myRefineries.Count == 0 && HasBuiltUnit(myBases, state) && HasBuiltUnit(myBarracks, state))
                    BuildAny(UnitType.REFINERY, state, actions);

                // Gather (skip builder to avoid overwriting build command)
                if (mainBaseNbr < 0 || mainMineNbr < 0) return;
                var mineInfo = state.GetUnit(mainMineNbr);
                if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) return;

                foreach (int worker in myWorkers)
                {
                    if (worker == _builderNbr) continue;
                    var info = state.GetUnit(worker);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                        actions.Gather(worker, mainMineNbr, mainBaseNbr);
                }
            }

            private void BuildAny(UnitType type, IGameState state, IAgentActions actions)
            {
                foreach (int worker in myWorkers)
                {
                    var info = state.GetUnit(worker);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[type])
                    {
                        foreach (Position pos in buildPositions)
                        {
                            if (state.IsBoundedAreaBuildable(type, pos))
                            {
                                actions.Build(worker, pos, type);
                                _builderNbr = worker;
                                return;
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Builds base, barracks, trains N workers, then trains a single unit type.
        /// Used for build timeline measurements.
        /// </summary>
        private class BuildTimelineAgent : PlanningAgentBase
        {
            private readonly int _maxWorkers;
            private readonly UnitType _trainType;
            public BuildTimelineAgent(int maxWorkers, UnitType trainType)
            {
                _maxWorkers = maxWorkers;
                _trainType = trainType;
            }

            public override void InitializeMatch() { }

            public override void Update(IGameState state, IAgentActions actions)
            {
                UpdateGameState(state);
                mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;
                mainMineNbr = mines.Count > 0 ? mines[0] : -1;

                // Build base if none exists
                if (myBases.Count == 0)
                {
                    foreach (int worker in myWorkers)
                    {
                        var info = state.GetUnit(worker);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.BASE])
                        {
                            foreach (Position pos in buildPositions)
                            {
                                if (state.IsBoundedAreaBuildable(UnitType.BASE, pos))
                                {
                                    actions.Build(worker, pos, UnitType.BASE);
                                    return;
                                }
                            }
                        }
                    }
                    return;
                }

                int builderNbr = -1;

                // Train workers
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.WORKER]
                        && myWorkers.Count < _maxWorkers)
                    {
                        actions.Train(baseNbr, UnitType.WORKER);
                    }
                }

                // Build barracks
                if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                {
                    foreach (int worker in myWorkers)
                    {
                        var info = state.GetUnit(worker);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
                        {
                            foreach (Position pos in buildPositions)
                            {
                                if (state.IsBoundedAreaBuildable(UnitType.BARRACKS, pos))
                                {
                                    actions.Build(worker, pos, UnitType.BARRACKS);
                                    builderNbr = worker;
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }

                // Train military
                foreach (int barracksNbr in myBarracks)
                {
                    var info = state.GetUnit(barracksNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[_trainType])
                    {
                        actions.Train(barracksNbr, _trainType);
                    }
                }

                // Gather (skip builder to avoid overwriting build command)
                if (mainBaseNbr < 0 || mainMineNbr < 0) return;
                var mineInfo = state.GetUnit(mainMineNbr);
                if (!mineInfo.HasValue || mineInfo.Value.Health <= 0) return;

                foreach (int worker in myWorkers)
                {
                    if (worker == builderNbr) continue;
                    var info = state.GetUnit(worker);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                        actions.Gather(worker, mainMineNbr, mainBaseNbr);
                }
            }
        }

        #endregion

        #region Helpers

        private struct MatchResult
        {
            public int WinnerAgent;   // 0, 1, or -1 for draw/timeout
            public int TicksElapsed;
            public int Agent0Units;
            public int Agent1Units;
            public float Agent0HpPercent;
            public float Agent1HpPercent;
        }

        private int CountAllUnits(SimGame game, int agent)
        {
            int count = 0;
            foreach (UnitType ut in new[] { UnitType.WORKER, UnitType.SOLDIER, UnitType.ARCHER,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
            {
                count += game.GetUnitsByType(agent, ut).Count;
            }
            return count;
        }

        private float GetTotalHpPercent(SimGame game, int agent)
        {
            float totalHp = 0;
            float totalMaxHp = 0;
            foreach (UnitType ut in new[] { UnitType.WORKER, UnitType.SOLDIER, UnitType.ARCHER,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
            {
                foreach (var unit in game.GetUnitsByType(agent, ut))
                {
                    totalHp += unit.Health;
                    totalMaxHp += GameConstants.HEALTH[ut];
                }
            }
            return totalMaxHp > 0 ? (totalHp / totalMaxHp * 100f) : 0f;
        }

        /// <summary>
        /// Run a pure combat scenario with pre-spawned units. No economy, no buildings.
        /// Uses AttackClosestAgent for both sides by default.
        /// </summary>
        private MatchResult RunCombat(int agent0SoldierCount, int agent0ArcherCount,
                                       int agent1SoldierCount, int agent1ArcherCount,
                                       float startDistance = 15f, int maxTicks = 3000)
        {
            return RunCombatWithAgents(agent0SoldierCount, agent0ArcherCount,
                agent1SoldierCount, agent1ArcherCount,
                new AttackClosestAgent(), new AttackClosestAgent(),
                startDistance, maxTicks);
        }

        /// <summary>
        /// Run a pure combat scenario with custom agents and pre-spawned units.
        /// </summary>
        private MatchResult RunCombatWithAgents(int agent0SoldierCount, int agent0ArcherCount,
                                       int agent1SoldierCount, int agent1ArcherCount,
                                       IPlanningAgent agent0, IPlanningAgent agent1,
                                       float startDistance = 15f, int maxTicks = 3000)
        {
            var builder = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 0)
                .WithGold(1, 0)
                .WithAgent(0, agent0)
                .WithAgent(1, agent1);

            // Place agent 0 units on the left
            int centerY = 15;
            int agent0X = 5;
            int agent1X = agent0X + (int)startDistance;

            for (int i = 0; i < agent0SoldierCount; i++)
                builder.WithUnit(0, UnitType.SOLDIER, new Position(agent0X, centerY - agent0SoldierCount / 2 + i));
            for (int i = 0; i < agent0ArcherCount; i++)
                builder.WithUnit(0, UnitType.ARCHER, new Position(agent0X, centerY - agent0ArcherCount / 2 + i + agent0SoldierCount));

            // Place agent 1 units on the right
            for (int i = 0; i < agent1SoldierCount; i++)
                builder.WithUnit(1, UnitType.SOLDIER, new Position(agent1X, centerY - agent1SoldierCount / 2 + i));
            for (int i = 0; i < agent1ArcherCount; i++)
                builder.WithUnit(1, UnitType.ARCHER, new Position(agent1X, centerY - agent1ArcherCount / 2 + i + agent1SoldierCount));

            var game = builder.Build();
            game.InitializeMatch();
            game.InitializeRound();

            game.RunUntil(g =>
                CountAllUnits(g, 0) == 0 || CountAllUnits(g, 1) == 0,
                maxTicks);

            int units0 = CountAllUnits(game, 0);
            int units1 = CountAllUnits(game, 1);

            int winner;
            if (units0 == 0 && units1 > 0) winner = 1;
            else if (units1 == 0 && units0 > 0) winner = 0;
            else if (units0 > units1) winner = 0;
            else if (units1 > units0) winner = 1;
            else winner = -1;

            return new MatchResult
            {
                WinnerAgent = winner,
                TicksElapsed = game.CurrentTick,
                Agent0Units = units0,
                Agent1Units = units1,
                Agent0HpPercent = GetTotalHpPercent(game, 0),
                Agent1HpPercent = GetTotalHpPercent(game, 1),
            };
        }

        #endregion

        #region Combat Balance: Equal Count

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(8)]
        [InlineData(12)]
        public void EqualCount_SoldiersVsArchers(int count)
        {
            var result = RunCombat(
                agent0SoldierCount: count, agent0ArcherCount: 0,
                agent1SoldierCount: 0, agent1ArcherCount: count);

            string winner = result.WinnerAgent == 0 ? "Soldier" :
                            result.WinnerAgent == 1 ? "Archer" : "Draw";
            int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
            float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

            _output.WriteLine($"=== EQUAL COUNT: {count}v{count} ===");
            _output.WriteLine($"  Winner: {winner}  Survivors: {survivors}  HP%: {hpPct:F1}%  Ticks: {result.TicksElapsed}");
        }

        #endregion

        #region Combat Balance: Equal Gold

        [Theory]
        [InlineData(300)]
        [InlineData(600)]
        [InlineData(900)]
        [InlineData(1200)]
        [InlineData(1800)]
        public void EqualGold_SoldiersVsArchers(int gold)
        {
            int soldierCount = gold / (int)GameConstants.COST[UnitType.SOLDIER];
            int archerCount = gold / (int)GameConstants.COST[UnitType.ARCHER];

            var result = RunCombat(
                agent0SoldierCount: soldierCount, agent0ArcherCount: 0,
                agent1SoldierCount: 0, agent1ArcherCount: archerCount);

            string winner = result.WinnerAgent == 0 ? "Soldier" :
                            result.WinnerAgent == 1 ? "Archer" : "Draw";
            int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
            float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

            _output.WriteLine($"=== EQUAL GOLD: {gold}g ({soldierCount} soldiers vs {archerCount} archers) ===");
            _output.WriteLine($"  Winner: {winner}  Survivors: {survivors}  HP%: {hpPct:F1}%  Ticks: {result.TicksElapsed}");
        }

        #endregion

        #region Combat Balance: Break-Even Ratio

        [Fact]
        public void BreakEvenRatio_SoldiersVsArchers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BREAK-EVEN RATIO: Soldiers vs Archers ===");
            sb.AppendLine("  Soldiers | Archers | Winner   | Survivors | HP%    | Ticks");
            sb.AppendLine("  ---------+---------+----------+-----------+--------+------");

            for (int soldiers = 1; soldiers <= 8; soldiers++)
            {
                for (int archers = 1; archers <= 8; archers++)
                {
                    var result = RunCombat(
                        agent0SoldierCount: soldiers, agent0ArcherCount: 0,
                        agent1SoldierCount: 0, agent1ArcherCount: archers);

                    string winner = result.WinnerAgent == 0 ? "Soldier" :
                                    result.WinnerAgent == 1 ? "Archer" : "Draw";
                    int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                    float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                    sb.AppendLine($"  {soldiers,8} | {archers,7} | {winner,-8} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
                }
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Combat Balance: Range Advantage

        [Theory]
        [InlineData(3)]
        [InlineData(8)]
        [InlineData(15)]
        [InlineData(25)]
        public void RangeAdvantage_VaryingDistance(int distance)
        {
            // 6 soldiers (600g) vs 4 archers (600g) at varying distances
            var result = RunCombat(
                agent0SoldierCount: 6, agent0ArcherCount: 0,
                agent1SoldierCount: 0, agent1ArcherCount: 4,
                startDistance: distance);

            string winner = result.WinnerAgent == 0 ? "Soldier" :
                            result.WinnerAgent == 1 ? "Archer" : "Draw";
            int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
            float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

            _output.WriteLine($"=== RANGE: Distance={distance} (6 soldiers vs 4 archers) ===");
            _output.WriteLine($"  Winner: {winner}  Survivors: {survivors}  HP%: {hpPct:F1}%  Ticks: {result.TicksElapsed}");
        }

        #endregion

        #region Focus Fire: Equal Count

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(5)]
        [InlineData(8)]
        [InlineData(12)]
        public void FocusFire_EqualCount_SoldiersVsArchers(int count)
        {
            var result = RunCombatWithAgents(
                agent0SoldierCount: count, agent0ArcherCount: 0,
                agent1SoldierCount: 0, agent1ArcherCount: count,
                new FocusFireAgent(), new FocusFireAgent());

            string winner = result.WinnerAgent == 0 ? "Soldier" :
                            result.WinnerAgent == 1 ? "Archer" : "Draw";
            int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
            float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

            _output.WriteLine($"=== FOCUS FIRE EQUAL COUNT: {count}v{count} ===");
            _output.WriteLine($"  Winner: {winner}  Survivors: {survivors}  HP%: {hpPct:F1}%  Ticks: {result.TicksElapsed}");
        }

        #endregion

        #region Focus Fire: Equal Gold

        [Theory]
        [InlineData(300)]
        [InlineData(600)]
        [InlineData(900)]
        [InlineData(1200)]
        [InlineData(1800)]
        public void FocusFire_EqualGold_SoldiersVsArchers(int gold)
        {
            int soldierCount = gold / (int)GameConstants.COST[UnitType.SOLDIER];
            int archerCount = gold / (int)GameConstants.COST[UnitType.ARCHER];

            var result = RunCombatWithAgents(
                agent0SoldierCount: soldierCount, agent0ArcherCount: 0,
                agent1SoldierCount: 0, agent1ArcherCount: archerCount,
                new FocusFireAgent(), new FocusFireAgent());

            string winner = result.WinnerAgent == 0 ? "Soldier" :
                            result.WinnerAgent == 1 ? "Archer" : "Draw";
            int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
            float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

            _output.WriteLine($"=== FOCUS FIRE EQUAL GOLD: {gold}g ({soldierCount} soldiers vs {archerCount} archers) ===");
            _output.WriteLine($"  Winner: {winner}  Survivors: {survivors}  HP%: {hpPct:F1}%  Ticks: {result.TicksElapsed}");
        }

        #endregion

        #region Focus Fire: Break-Even Ratio

        [Fact]
        public void FocusFire_BreakEvenRatio_SoldiersVsArchers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== FOCUS FIRE BREAK-EVEN RATIO: Soldiers vs Archers ===");
            sb.AppendLine("  Soldiers | Archers | Winner   | Survivors | HP%    | Ticks");
            sb.AppendLine("  ---------+---------+----------+-----------+--------+------");

            for (int soldiers = 1; soldiers <= 8; soldiers++)
            {
                for (int archers = 1; archers <= 8; archers++)
                {
                    var result = RunCombatWithAgents(
                        agent0SoldierCount: soldiers, agent0ArcherCount: 0,
                        agent1SoldierCount: 0, agent1ArcherCount: archers,
                        new FocusFireAgent(), new FocusFireAgent());

                    string winner = result.WinnerAgent == 0 ? "Soldier" :
                                    result.WinnerAgent == 1 ? "Archer" : "Draw";
                    int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                    float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                    sb.AppendLine($"  {soldiers,8} | {archers,7} | {winner,-8} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
                }
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Focus Fire: Range Advantage

        [Theory]
        [InlineData(3)]
        [InlineData(8)]
        [InlineData(15)]
        [InlineData(25)]
        public void FocusFire_RangeAdvantage_VaryingDistance(int distance)
        {
            // 6 soldiers (600g) vs 7 archers (560g) at varying distances — both focus fire
            int soldierCount = 6;
            int archerCount = 7;
            var result = RunCombatWithAgents(
                agent0SoldierCount: soldierCount, agent0ArcherCount: 0,
                agent1SoldierCount: 0, agent1ArcherCount: archerCount,
                new FocusFireAgent(), new FocusFireAgent(),
                startDistance: distance);

            string winner = result.WinnerAgent == 0 ? "Soldier" :
                            result.WinnerAgent == 1 ? "Archer" : "Draw";
            int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
            float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

            _output.WriteLine($"=== FOCUS FIRE RANGE: Distance={distance} ({soldierCount} soldiers vs {archerCount} archers) ===");
            _output.WriteLine($"  Winner: {winner}  Survivors: {survivors}  HP%: {hpPct:F1}%  Ticks: {result.TicksElapsed}");
        }

        #endregion

        #region Helper: DoNothingAgent

        private class DoNothingAgent : PlanningAgentBase
        {
            public override void InitializeMatch() { }
            public override void Update(IGameState state, IAgentActions actions) { }
        }

        #endregion
    }
}
