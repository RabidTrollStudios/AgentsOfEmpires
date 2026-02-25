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
    public class EngineBalanceTests
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

        #region Economy: Worker Scaling

        [Fact]
        public void WorkerScaling()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ECONOMY: Worker Scaling ===");
            sb.AppendLine("  Workers | Gold@500t | Gold@1000t | Gold@2000t | Gold/tick");
            sb.AppendLine("  --------+-----------+------------+------------+---------");

            foreach (int workerCap in new[] { 1, 2, 3, 5, 8, 10 })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 0)
                    .WithUnit(0, UnitType.WORKER, new Position(5, 5))
                    .WithMine(new Position(10, 5), health: 50000)
                    .WithAgent(0, new PureEconomyAgent(workerCap))
                    .WithAgent(1, new DoNothingAgent())
                    .Build();

                game.InitializeMatch();
                game.InitializeRound();

                int startGold = game.GetGold(0);

                game.Run(500);
                int gold500 = game.GetGold(0) - startGold;

                game.Run(500);
                int gold1000 = game.GetGold(0) - startGold;

                game.Run(1000);
                int gold2000 = game.GetGold(0) - startGold;

                float goldPerTick = gold2000 / 2000f;

                sb.AppendLine($"  {workerCap,7} | {gold500,9} | {gold1000,10} | {gold2000,10} | {goldPerTick,8:F2}");
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Economy: Refinery Impact

        [Fact]
        public void RefineryImpact()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ECONOMY: Refinery Impact (5 workers) ===");
            sb.AppendLine("  Tick | No Refinery | With Refinery | Delta");
            sb.AppendLine("  -----+-------------+---------------+------");

            // Run without refinery
            var gameNoRef = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 1000)
                .WithGold(1, 0)
                .WithUnit(0, UnitType.WORKER, new Position(5, 5))
                .WithMine(new Position(10, 5), health: 50000)
                .WithAgent(0, new PureEconomyAgent(5))
                .WithAgent(1, new DoNothingAgent())
                .Build();

            // Run with refinery
            var gameRef = new SimGameBuilder()
                .WithMapSize(30, 30)
                .WithGold(0, 1000)
                .WithGold(1, 0)
                .WithUnit(0, UnitType.WORKER, new Position(5, 5))
                .WithMine(new Position(10, 5), health: 50000)
                .WithAgent(0, new RefineryEconomyAgent(5))
                .WithAgent(1, new DoNothingAgent())
                .Build();

            gameNoRef.InitializeMatch();
            gameNoRef.InitializeRound();
            gameRef.InitializeMatch();
            gameRef.InitializeRound();

            int startNoRef = gameNoRef.GetGold(0);
            int startRef = gameRef.GetGold(0);

            foreach (int checkpoint in new[] { 250, 500, 750, 1000, 1500, 2000, 3000 })
            {
                int ticksToRun = checkpoint - gameNoRef.CurrentTick;
                if (ticksToRun > 0)
                {
                    gameNoRef.Run(ticksToRun);
                    gameRef.Run(ticksToRun);
                }

                int goldNoRef = gameNoRef.GetGold(0) - startNoRef;
                int goldWithRef = gameRef.GetGold(0) - startRef;
                int delta = goldWithRef - goldNoRef;

                sb.AppendLine($"  {checkpoint,4} | {goldNoRef,11} | {goldWithRef,13} | {delta,5}");
            }

            _output.WriteLine(sb.ToString());
            _output.WriteLine($"  Refinery cost: {GameConstants.COST[UnitType.REFINERY]}g + Barracks: {GameConstants.COST[UnitType.BARRACKS]}g = {GameConstants.COST[UnitType.REFINERY] + GameConstants.COST[UnitType.BARRACKS]}g total infrastructure");
        }

        #endregion

        #region Economy: Build Timeline

        [Fact]
        public void BuildTimeline()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BUILD TIMELINE: Time to key milestones (3 workers, starting with 1 worker + 1000g) ===");

            foreach (UnitType trainType in new[] { UnitType.SOLDIER, UnitType.ARCHER })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 0)
                    .WithUnit(0, UnitType.WORKER, new Position(5, 5))
                    .WithMine(new Position(10, 5), health: 50000)
                    .WithAgent(0, new BuildTimelineAgent(3, trainType))
                    .WithAgent(1, new DoNothingAgent())
                    .Build();

                game.InitializeMatch();
                game.InitializeRound();

                int tickBase = -1;
                int tickBarracks = -1;
                int tickFirstUnit = -1;

                for (int t = 0; t < 5000; t++)
                {
                    game.Run(1);

                    if (tickBase < 0 && game.GetUnitsByType(0, UnitType.BASE).Any(u => u.IsBuilt))
                        tickBase = game.CurrentTick;

                    if (tickBarracks < 0 && game.GetUnitsByType(0, UnitType.BARRACKS).Any(u => u.IsBuilt))
                        tickBarracks = game.CurrentTick;

                    if (tickFirstUnit < 0)
                    {
                        var units = game.GetUnitsByType(0, trainType);
                        if (units.Count > 0)
                            tickFirstUnit = game.CurrentTick;
                    }

                    if (tickBarracks >= 0 && tickFirstUnit >= 0)
                        break;
                }

                sb.AppendLine($"  {trainType}: Base built @ tick {tickBase}, Barracks built @ tick {tickBarracks}, First {trainType} @ tick {tickFirstUnit}");
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Theoretical: DPS/HP Table

        [Fact]
        public void TheoreticalDpsHpTable()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== THEORETICAL DPS/HP TABLE ===");
            sb.AppendLine("  Unit    | Cost | HP   | DMG  | DPS/gold | HP/gold | Range | Speed");
            sb.AppendLine("  --------+------+------+------+----------+---------+-------+------");

            foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER })
            {
                float cost = GameConstants.COST[ut];
                float hp = GameConstants.HEALTH[ut];
                float dmg = GameConstants.BASE_DAMAGE[ut];
                float range = GameConstants.ATTACK_RANGE[ut];
                float speed = GameConstants.MOVEMENT_SPEED[ut];

                float dpsPerGold = dmg / cost;
                float hpPerGold = hp / cost;

                sb.AppendLine($"  {ut,-7} | {cost,4:F0} | {hp,4:F0} | {dmg,4:F0} | {dpsPerGold,8:F3}  | {hpPerGold,7:F1}  | {range,5:F1} | {speed:F2}");
            }

            sb.AppendLine();
            sb.AppendLine("  === DAMAGE MULTIPLIERS (attacker vs defender) ===");
            sb.AppendLine("  Attacker | vs Soldier | vs Archer | vs Building");
            sb.AppendLine("  ---------+------------+-----------+------------");
            foreach (UnitType at in new[] { UnitType.SOLDIER, UnitType.ARCHER })
            {
                float vsSol = GameConstants.DamageMultiplier(at, UnitType.SOLDIER);
                float vsArc = GameConstants.DamageMultiplier(at, UnitType.ARCHER);
                float vsBld = GameConstants.DamageMultiplier(at, UnitType.BASE);
                sb.AppendLine($"  {at,-8} | {vsSol,10:F2}x | {vsArc,9:F2}x | {vsBld,10:F2}x");
            }

            sb.AppendLine();
            sb.AppendLine("  === EFFECTIVE TTK (seconds, accounting for armor) ===");
            sb.AppendLine("  Attacker | Kill Soldier      | Kill Archer");
            sb.AppendLine("  ---------+-------------------+-------------------");
            foreach (UnitType at in new[] { UnitType.SOLDIER, UnitType.ARCHER })
            {
                float baseDmg = GameConstants.BASE_DAMAGE[at];
                float effVsSol = baseDmg * GameConstants.DamageMultiplier(at, UnitType.SOLDIER);
                float effVsArc = baseDmg * GameConstants.DamageMultiplier(at, UnitType.ARCHER);
                float ttkSol = GameConstants.HEALTH[UnitType.SOLDIER] / effVsSol;
                float ttkArc = GameConstants.HEALTH[UnitType.ARCHER] / effVsArc;
                sb.AppendLine($"  {at,-8} | {ttkSol,5:F1}s ({effVsSol,4:F0} eDPS) | {ttkArc,5:F1}s ({effVsArc,4:F0} eDPS)");
            }

            sb.AppendLine();
            sb.AppendLine("  Building  | Cost | HP   | Build Time Mult | Train Time (Sol/Arc)");
            sb.AppendLine("  ----------+------+------+-----------------+---------------------");
            foreach (UnitType ut in new[] { UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
            {
                float cost = GameConstants.COST[ut];
                float hp = GameConstants.HEALTH[ut];
                float buildTime = GameConstants.CREATION_TIME_MULTIPLIER[ut];
                string trainInfo = ut == UnitType.BARRACKS
                    ? $"Sol={GameConstants.CREATION_TIME_MULTIPLIER[UnitType.SOLDIER]:F0} / Arc={GameConstants.CREATION_TIME_MULTIPLIER[UnitType.ARCHER]:F0}"
                    : ut == UnitType.BASE
                        ? $"Worker={GameConstants.CREATION_TIME_MULTIPLIER[UnitType.WORKER]:F0}"
                        : "N/A";
                sb.AppendLine($"  {ut,-9} | {cost,4:F0} | {hp,4:F0} | {buildTime,15:F1} | {trainInfo}");
            }

            sb.AppendLine();
            sb.AppendLine($"  Worker cost: {GameConstants.COST[UnitType.WORKER]}g  |  Mining capacity: {GameConstants.MINING_CAPACITY[UnitType.WORKER]}g/trip  |  Refinery boost: {GameConstants.MINING_BOOST}x");

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Helper: DoNothingAgent

        private class DoNothingAgent : PlanningAgentBase
        {
            public override void InitializeMatch() { }
            public override void Update(IGameState state, IAgentActions actions) { }
        }

        #endregion

        #region Test Agents: Scenario-Specific

        /// <summary>
        /// Rushes military ASAP: builds barracks immediately, trains one unit type,
        /// attacks with all military when threshold reached. Minimal economy (2 workers).
        /// </summary>
        private class RushAgent : PlanningAgentBase
        {
            private readonly UnitType _trainType;
            private readonly int _attackThreshold;
            private bool _attacking;

            public RushAgent(UnitType trainType, int attackThreshold)
            {
                _trainType = trainType;
                _attackThreshold = attackThreshold;
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

                // Train up to 2 workers
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.WORKER]
                        && myWorkers.Count < 2)
                    {
                        actions.Train(baseNbr, UnitType.WORKER);
                    }
                }

                // Build barracks ASAP
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

                // Attack when threshold reached
                var military = _trainType == UnitType.SOLDIER ? mySoldiers : myArchers;
                if (military.Count >= _attackThreshold)
                    _attacking = true;

                if (_attacking)
                {
                    int? target = FindClosestEnemy(state);
                    if (target.HasValue)
                    {
                        foreach (int unitNbr in military)
                        {
                            var info = state.GetUnit(unitNbr);
                            if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                                actions.Attack(unitNbr, target.Value);
                        }
                    }
                }

                // Gather with non-builder workers
                if (mainBaseNbr >= 0 && mainMineNbr >= 0)
                {
                    foreach (int worker in myWorkers)
                    {
                        if (worker == builderNbr) continue;
                        var info = state.GetUnit(worker);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                            actions.Gather(worker, mainMineNbr, mainBaseNbr);
                    }
                }
            }

            private int? FindClosestEnemy(IGameState state)
            {
                int? best = null;
                foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                                UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
                {
                    foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    {
                        var enemyInfo = state.GetUnit(enemyNbr);
                        if (!enemyInfo.HasValue) continue;
                        best = best ?? enemyNbr;
                    }
                }
                return best;
            }
        }

        /// <summary>
        /// Greedy economy agent: trains many workers, builds refinery, delays military.
        /// Used to test whether eco-boom is punishable by rushes.
        /// </summary>
        private class GreedyEconomyAgent : PlanningAgentBase
        {
            private readonly int _maxWorkers;
            private readonly int _militaryThreshold;
            private int _builderNbr;
            private bool _attacking;

            public GreedyEconomyAgent(int maxWorkers, int militaryThreshold)
            {
                _maxWorkers = maxWorkers;
                _militaryThreshold = militaryThreshold;
            }

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

                // Build barracks then refinery
                if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                    BuildStructure(UnitType.BARRACKS, state, actions);
                else if (myRefineries.Count == 0 && HasBuiltUnit(myBases, state) && HasBuiltUnit(myBarracks, state))
                    BuildStructure(UnitType.REFINERY, state, actions);

                // Train soldiers once workers are saturated
                if (myWorkers.Count >= _maxWorkers)
                {
                    foreach (int barracksNbr in myBarracks)
                    {
                        var info = state.GetUnit(barracksNbr);
                        if (info.HasValue && info.Value.IsBuilt
                            && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.SOLDIER])
                        {
                            actions.Train(barracksNbr, UnitType.SOLDIER);
                        }
                    }
                }

                // Attack when military threshold reached
                if (mySoldiers.Count + myArchers.Count >= _militaryThreshold)
                    _attacking = true;

                if (_attacking)
                {
                    foreach (var unitList in new[] { mySoldiers, myArchers })
                    {
                        foreach (int unitNbr in unitList)
                        {
                            var info = state.GetUnit(unitNbr);
                            if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                            {
                                int? target = FindAnyEnemy(state);
                                if (target.HasValue) actions.Attack(unitNbr, target.Value);
                            }
                        }
                    }
                }

                // Gather
                if (mainBaseNbr >= 0 && mainMineNbr >= 0)
                {
                    foreach (int worker in myWorkers)
                    {
                        if (worker == _builderNbr) continue;
                        var info = state.GetUnit(worker);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                            actions.Gather(worker, mainMineNbr, mainBaseNbr);
                    }
                }
            }

            private void BuildStructure(UnitType type, IGameState state, IAgentActions actions)
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

            private int? FindAnyEnemy(IGameState state)
            {
                foreach (UnitType ut in new[] { UnitType.SOLDIER, UnitType.ARCHER, UnitType.WORKER,
                                                UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
                {
                    foreach (int enemyNbr in state.GetEnemyUnits(ut))
                        return enemyNbr;
                }
                return null;
            }
        }

        #endregion

        #region Mixed Army Compositions

        [Fact]
        public void MixedArmy_VsPureSoldiers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MIXED ARMY vs PURE SOLDIERS (Focus Fire, Equal Gold) ===");
            sb.AppendLine("  Gold | Mix (Sol+Arc)    | Pure Soldiers | Winner | Survivors | HP%    | Ticks");
            sb.AppendLine("  -----+------------------+---------------+--------+-----------+--------+------");

            foreach (int gold in new[] { 400, 600, 800, 1000, 1200, 1600 })
            {
                // Mixed: split gold roughly 50/50 between soldiers and archers
                int mixSoldiers = (gold / 2) / (int)GameConstants.COST[UnitType.SOLDIER];
                int mixArchers = (gold / 2) / (int)GameConstants.COST[UnitType.ARCHER];
                int pureSoldiers = gold / (int)GameConstants.COST[UnitType.SOLDIER];

                var result = RunCombatWithAgents(
                    agent0SoldierCount: mixSoldiers, agent0ArcherCount: mixArchers,
                    agent1SoldierCount: pureSoldiers, agent1ArcherCount: 0,
                    new FocusFireAgent(), new FocusFireAgent());

                string winner = result.WinnerAgent == 0 ? "Mixed" :
                                result.WinnerAgent == 1 ? "Soldier" : "Draw";
                int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                sb.AppendLine($"  {gold,4} | {mixSoldiers}S + {mixArchers}A ({mixSoldiers * 100 + mixArchers * 80}g) | {pureSoldiers,13} | {winner,-6} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
            }

            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public void MixedArmy_VsPureArchers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MIXED ARMY vs PURE ARCHERS (Focus Fire, Equal Gold) ===");
            sb.AppendLine("  Gold | Mix (Sol+Arc)    | Pure Archers | Winner | Survivors | HP%    | Ticks");
            sb.AppendLine("  -----+------------------+--------------+--------+-----------+--------+------");

            foreach (int gold in new[] { 400, 600, 800, 1000, 1200, 1600 })
            {
                int mixSoldiers = (gold / 2) / (int)GameConstants.COST[UnitType.SOLDIER];
                int mixArchers = (gold / 2) / (int)GameConstants.COST[UnitType.ARCHER];
                int pureArchers = gold / (int)GameConstants.COST[UnitType.ARCHER];

                var result = RunCombatWithAgents(
                    agent0SoldierCount: mixSoldiers, agent0ArcherCount: mixArchers,
                    agent1SoldierCount: 0, agent1ArcherCount: pureArchers,
                    new FocusFireAgent(), new FocusFireAgent());

                string winner = result.WinnerAgent == 0 ? "Mixed" :
                                result.WinnerAgent == 1 ? "Archer" : "Draw";
                int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                sb.AppendLine($"  {gold,4} | {mixSoldiers}S + {mixArchers}A ({mixSoldiers * 100 + mixArchers * 80}g) | {pureArchers,12} | {winner,-6} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
            }

            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public void MixedArmy_OptimalRatio()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MIXED ARMY OPTIMAL RATIO: 1000g budget, vary soldier:archer split ===");
            sb.AppendLine("  vs 10 Pure Soldiers (1000g):");
            sb.AppendLine("  Soldiers | Archers | Gold Used | Winner | Survivors | HP%    | Ticks");
            sb.AppendLine("  ---------+---------+-----------+--------+-----------+--------+------");

            // Test various ratios against pure soldiers
            var ratios = new[] { (10, 0), (8, 2), (6, 5), (5, 6), (4, 7), (2, 10), (0, 12) };
            foreach (var (sol, arc) in ratios)
            {
                int goldUsed = sol * (int)GameConstants.COST[UnitType.SOLDIER] + arc * (int)GameConstants.COST[UnitType.ARCHER];
                var result = RunCombatWithAgents(
                    agent0SoldierCount: sol, agent0ArcherCount: arc,
                    agent1SoldierCount: 10, agent1ArcherCount: 0,
                    new FocusFireAgent(), new FocusFireAgent());

                string winner = result.WinnerAgent == 0 ? "Mix" :
                                result.WinnerAgent == 1 ? "Soldier" : "Draw";
                int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                sb.AppendLine($"  {sol,8} | {arc,7} | {goldUsed,9} | {winner,-6} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
            }

            sb.AppendLine();
            sb.AppendLine("  vs 12 Pure Archers (960g):");
            sb.AppendLine("  Soldiers | Archers | Gold Used | Winner | Survivors | HP%    | Ticks");
            sb.AppendLine("  ---------+---------+-----------+--------+-----------+--------+------");

            foreach (var (sol, arc) in ratios)
            {
                int goldUsed = sol * (int)GameConstants.COST[UnitType.SOLDIER] + arc * (int)GameConstants.COST[UnitType.ARCHER];
                var result = RunCombatWithAgents(
                    agent0SoldierCount: sol, agent0ArcherCount: arc,
                    agent1SoldierCount: 0, agent1ArcherCount: 12,
                    new FocusFireAgent(), new FocusFireAgent());

                string winner = result.WinnerAgent == 0 ? "Mix" :
                                result.WinnerAgent == 1 ? "Archer" : "Draw";
                int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                sb.AppendLine($"  {sol,8} | {arc,7} | {goldUsed,9} | {winner,-6} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Rush Timing

        [Fact]
        public void RushTiming_SoldierRushVsEconomy()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RUSH TIMING: Soldier Rush (2-worker) vs Greedy Economy ===");
            sb.AppendLine("  Testing rush with attack thresholds of 2, 3, 4 soldiers");
            sb.AppendLine("  vs eco-boom with 5 workers (delays military)");
            sb.AppendLine();

            foreach (int rushThreshold in new[] { 2, 3, 4 })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(40, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 1000)
                    // Agent 0: rusher
                    .WithUnit(0, UnitType.WORKER, new Position(2, 5))
                    .WithMine(new Position(8, 5), health: 50000)
                    // Agent 1: eco player on the other side
                    .WithUnit(1, UnitType.WORKER, new Position(30, 5))
                    .WithMine(new Position(24, 5), health: 50000)
                    .WithAgent(0, new RushAgent(UnitType.SOLDIER, rushThreshold))
                    .WithAgent(1, new GreedyEconomyAgent(5, 3))
                    .Build();

                game.InitializeMatch();
                game.InitializeRound();

                // Run until one side is eliminated (all units dead)
                game.RunUntil(g =>
                    CountAllUnits(g, 0) == 0 || CountAllUnits(g, 1) == 0,
                    10000);

                int base0 = game.GetUnitsByType(0, UnitType.BASE).Count;
                int base1 = game.GetUnitsByType(1, UnitType.BASE).Count;
                int sol0 = game.GetUnitsByType(0, UnitType.SOLDIER).Count;
                int sol1 = game.GetUnitsByType(1, UnitType.SOLDIER).Count;
                int arc1 = game.GetUnitsByType(1, UnitType.ARCHER).Count;
                int workers0 = game.GetUnitsByType(0, UnitType.WORKER).Count;
                int workers1 = game.GetUnitsByType(1, UnitType.WORKER).Count;

                int total0 = CountAllUnits(game, 0);
                int total1 = CountAllUnits(game, 1);
                string winner = total0 == 0 ? "Eco" : total1 == 0 ? "Rush" : "Timeout";

                sb.AppendLine($"  Rush@{rushThreshold} soldiers:");
                sb.AppendLine($"    Result: {winner} wins @ tick {game.CurrentTick}");
                sb.AppendLine($"    Rusher:  {sol0} soldiers, {workers0} workers, base={base0}, gold={game.GetGold(0)}");
                sb.AppendLine($"    Eco:     {sol1} soldiers, {arc1} archers, {workers1} workers, base={base1}, gold={game.GetGold(1)}");
                sb.AppendLine();
            }

            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public void RushTiming_ArcherRushVsEconomy()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RUSH TIMING: Archer Rush (2-worker) vs Greedy Economy ===");
            sb.AppendLine();

            foreach (int rushThreshold in new[] { 2, 3, 4 })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(40, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 1000)
                    .WithUnit(0, UnitType.WORKER, new Position(2, 5))
                    .WithMine(new Position(8, 5), health: 50000)
                    .WithUnit(1, UnitType.WORKER, new Position(30, 5))
                    .WithMine(new Position(24, 5), health: 50000)
                    .WithAgent(0, new RushAgent(UnitType.ARCHER, rushThreshold))
                    .WithAgent(1, new GreedyEconomyAgent(5, 3))
                    .Build();

                game.InitializeMatch();
                game.InitializeRound();

                game.RunUntil(g =>
                    CountAllUnits(g, 0) == 0 || CountAllUnits(g, 1) == 0,
                    10000);

                int base0 = game.GetUnitsByType(0, UnitType.BASE).Count;
                int base1 = game.GetUnitsByType(1, UnitType.BASE).Count;
                int arc0 = game.GetUnitsByType(0, UnitType.ARCHER).Count;
                int sol1 = game.GetUnitsByType(1, UnitType.SOLDIER).Count;
                int arc1 = game.GetUnitsByType(1, UnitType.ARCHER).Count;
                int workers0 = game.GetUnitsByType(0, UnitType.WORKER).Count;
                int workers1 = game.GetUnitsByType(1, UnitType.WORKER).Count;

                int total0 = CountAllUnits(game, 0);
                int total1 = CountAllUnits(game, 1);
                string winner = total0 == 0 ? "Eco" : total1 == 0 ? "Rush" : "Timeout";

                sb.AppendLine($"  Rush@{rushThreshold} archers:");
                sb.AppendLine($"    Result: {winner} wins @ tick {game.CurrentTick}");
                sb.AppendLine($"    Rusher:  {arc0} archers, {workers0} workers, base={base0}, gold={game.GetGold(0)}");
                sb.AppendLine($"    Eco:     {sol1} soldiers, {arc1} archers, {workers1} workers, base={base1}, gold={game.GetGold(1)}");
                sb.AppendLine();
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Economy vs Military Tradeoff

        [Fact]
        public void EcoVsMilitary_WorkerCountImpact()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ECONOMY vs MILITARY: Varying eco worker count vs 2-worker soldier rush ===");
            sb.AppendLine("  Eco invests in workers before military; rusher goes straight to barracks");
            sb.AppendLine();

            foreach (int ecoWorkers in new[] { 3, 5, 8 })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(40, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 1000)
                    // Rusher (agent 0)
                    .WithUnit(0, UnitType.WORKER, new Position(2, 5))
                    .WithMine(new Position(8, 5), health: 50000)
                    // Eco player (agent 1)
                    .WithUnit(1, UnitType.WORKER, new Position(30, 5))
                    .WithMine(new Position(24, 5), health: 50000)
                    .WithAgent(0, new RushAgent(UnitType.SOLDIER, 3))
                    .WithAgent(1, new GreedyEconomyAgent(ecoWorkers, 3))
                    .Build();

                game.InitializeMatch();
                game.InitializeRound();

                game.RunUntil(g =>
                    CountAllUnits(g, 0) == 0 || CountAllUnits(g, 1) == 0,
                    10000);

                int base0 = game.GetUnitsByType(0, UnitType.BASE).Count;
                int base1 = game.GetUnitsByType(1, UnitType.BASE).Count;
                int sol0 = game.GetUnitsByType(0, UnitType.SOLDIER).Count;
                int sol1 = game.GetUnitsByType(1, UnitType.SOLDIER).Count;
                int workers1 = game.GetUnitsByType(1, UnitType.WORKER).Count;

                int total0 = CountAllUnits(game, 0);
                int total1 = CountAllUnits(game, 1);
                string winner = total0 == 0 ? "Eco" : total1 == 0 ? "Rush" : "Timeout";

                sb.AppendLine($"  Eco with {ecoWorkers} workers vs 3-soldier rush:");
                sb.AppendLine($"    Result: {winner} wins @ tick {game.CurrentTick}");
                sb.AppendLine($"    Rusher:  {sol0} soldiers, base={base0}, gold={game.GetGold(0)}");
                sb.AppendLine($"    Eco:     {sol1} soldiers, {workers1} workers, base={base1}, gold={game.GetGold(1)}");
                sb.AppendLine();
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Building Durability

        [Fact]
        public void BuildingDurability_TimeToDestroy()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BUILDING DURABILITY: Time to destroy buildings ===");
            sb.AppendLine("  Attackers       | Target   | HP   | Ticks to Destroy | Seconds");
            sb.AppendLine("  ----------------+----------+------+------------------+--------");

            var attackConfigs = new[]
            {
                (solCount: 1, arcCount: 0, label: "1 Soldier"),
                (solCount: 2, arcCount: 0, label: "2 Soldiers"),
                (solCount: 3, arcCount: 0, label: "3 Soldiers"),
                (solCount: 5, arcCount: 0, label: "5 Soldiers"),
                (solCount: 0, arcCount: 1, label: "1 Archer"),
                (solCount: 0, arcCount: 2, label: "2 Archers"),
                (solCount: 0, arcCount: 3, label: "3 Archers"),
                (solCount: 0, arcCount: 5, label: "5 Archers"),
                (solCount: 2, arcCount: 3, label: "2S + 3A Mix"),
            };

            foreach (UnitType buildingType in new[] { UnitType.BASE, UnitType.BARRACKS, UnitType.REFINERY })
            {
                foreach (var cfg in attackConfigs)
                {
                    var builder = new SimGameBuilder()
                        .WithMapSize(30, 30)
                        .WithGold(0, 0)
                        .WithGold(1, 0)
                        .WithAgent(0, new AttackClosestAgent())
                        .WithAgent(1, new DoNothingAgent());

                    // Place building at center
                    builder.WithUnit(1, buildingType, new Position(15, 15), isBuilt: true);

                    // Place attackers adjacent to building
                    for (int i = 0; i < cfg.solCount; i++)
                        builder.WithUnit(0, UnitType.SOLDIER, new Position(12, 13 + i));
                    for (int i = 0; i < cfg.arcCount; i++)
                        builder.WithUnit(0, UnitType.ARCHER, new Position(10, 13 + i));

                    var game = builder.Build();
                    game.InitializeMatch();
                    game.InitializeRound();

                    game.RunUntil(g => g.GetUnitsByType(1, buildingType).Count == 0, 5000);

                    int ticks = game.CurrentTick;
                    float seconds = ticks * 0.05f;
                    float hp = GameConstants.HEALTH[buildingType];

                    sb.AppendLine($"  {cfg.label,-15} | {buildingType,-8} | {hp,4:F0} | {ticks,16} | {seconds,6:F1}s");
                }
                sb.AppendLine();
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Mine Depletion

        [Fact]
        public void MineDepletion_TimingByWorkerCount()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MINE DEPLETION: Time to exhaust mine by worker count ===");
            sb.AppendLine("  Workers | Mine HP | Depletion Tick | Seconds | Gold Collected | Gold/tick");
            sb.AppendLine("  --------+---------+----------------+---------+----------------+----------");

            foreach (int mineHp in new[] { 2000, 5000 })
            {
                foreach (int workerCount in new[] { 1, 2, 3, 5, 8 })
                {
                    var builder = new SimGameBuilder()
                        .WithMapSize(30, 30)
                        .WithGold(0, 500)
                        .WithGold(1, 0)
                        .WithMine(new Position(10, 5), health: mineHp)
                        .WithAgent(0, new PureGatherAgent())
                        .WithAgent(1, new DoNothingAgent());

                    // Pre-place workers (skip training delay) — place near base
                    for (int i = 0; i < workerCount; i++)
                        builder.WithUnit(0, UnitType.WORKER, new Position(7, 5 + i));

                    var game = builder.Build();
                    game.InitializeMatch();
                    game.InitializeRound();

                    int mineNbr = -1;
                    foreach (var u in game.GetUnitsByType(-1, UnitType.MINE))
                        mineNbr = u.UnitNbr;

                    game.RunUntil(g =>
                    {
                        var mineUnit = g.GetUnit(mineNbr);
                        return mineUnit == null || mineUnit.Health <= 0;
                    }, 10000);

                    int depletionTick = game.CurrentTick;
                    float seconds = depletionTick * 0.05f;
                    int goldCollected = game.GetGold(0);
                    float goldPerTick = depletionTick > 0 ? (float)goldCollected / depletionTick : 0;

                    sb.AppendLine($"  {workerCount,7} | {mineHp,7} | {depletionTick,14} | {seconds,7:F1} | {goldCollected,14} | {goldPerTick,8:F2}");
                }
                sb.AppendLine();
            }

            _output.WriteLine(sb.ToString());
        }

        /// <summary>
        /// Simple agent that just gathers with all workers. Used for mine depletion tests
        /// where workers are pre-placed and we want no training/building overhead.
        /// </summary>
        private class PureGatherAgent : PlanningAgentBase
        {
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

        #endregion

        #region Worker Harassment

        [Fact]
        public void WorkerHarassment_SoldierVsWorkers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WORKER HARASSMENT: Soldiers raiding workers ===");
            sb.AppendLine("  Soldiers | Workers | Workers Killed | Ticks | Soldiers Surviving");
            sb.AppendLine("  ---------+---------+----------------+-------+-------------------");

            foreach (int soldierCount in new[] { 1, 2, 3 })
            {
                foreach (int workerCount in new[] { 3, 5, 8 })
                {
                    var builder = new SimGameBuilder()
                        .WithMapSize(30, 30)
                        .WithGold(0, 0)
                        .WithGold(1, 0)
                        .WithAgent(0, new AttackClosestAgent())
                        .WithAgent(1, new DoNothingAgent());  // Workers don't fight back

                    // Soldiers at one side
                    for (int i = 0; i < soldierCount; i++)
                        builder.WithUnit(0, UnitType.SOLDIER, new Position(5, 13 + i));

                    // Workers clustered near a mine
                    for (int i = 0; i < workerCount; i++)
                        builder.WithUnit(1, UnitType.WORKER, new Position(15, 12 + i));

                    var game = builder.Build();
                    game.InitializeMatch();
                    game.InitializeRound();

                    game.RunUntil(g => g.GetUnitsByType(1, UnitType.WORKER).Count == 0, 2000);

                    int workersLeft = game.GetUnitsByType(1, UnitType.WORKER).Count;
                    int workersKilled = workerCount - workersLeft;
                    int soldiersLeft = game.GetUnitsByType(0, UnitType.SOLDIER).Count;

                    sb.AppendLine($"  {soldierCount,8} | {workerCount,7} | {workersKilled,14} | {game.CurrentTick,5} | {soldiersLeft,18}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("  === GOLD COST ANALYSIS ===");
            float soldierCost = GameConstants.COST[UnitType.SOLDIER];
            float workerCost = GameConstants.COST[UnitType.WORKER];
            sb.AppendLine($"  1 Soldier ({soldierCost}g) kills workers ({workerCost}g each):");
            sb.AppendLine($"  - Break even at {soldierCost / workerCost:F0} workers killed");
            sb.AppendLine($"  - Plus lost mining time while workers are dead");

            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public void WorkerHarassment_ArcherVsWorkers()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== WORKER HARASSMENT: Archers raiding workers ===");
            sb.AppendLine("  Archers | Workers | Workers Killed | Ticks | Archers Surviving");
            sb.AppendLine("  --------+---------+----------------+-------+------------------");

            foreach (int archerCount in new[] { 1, 2, 3 })
            {
                foreach (int workerCount in new[] { 3, 5, 8 })
                {
                    var builder = new SimGameBuilder()
                        .WithMapSize(30, 30)
                        .WithGold(0, 0)
                        .WithGold(1, 0)
                        .WithAgent(0, new AttackClosestAgent())
                        .WithAgent(1, new DoNothingAgent());

                    for (int i = 0; i < archerCount; i++)
                        builder.WithUnit(0, UnitType.ARCHER, new Position(5, 13 + i));

                    for (int i = 0; i < workerCount; i++)
                        builder.WithUnit(1, UnitType.WORKER, new Position(15, 12 + i));

                    var game = builder.Build();
                    game.InitializeMatch();
                    game.InitializeRound();

                    game.RunUntil(g => g.GetUnitsByType(1, UnitType.WORKER).Count == 0, 2000);

                    int workersLeft = game.GetUnitsByType(1, UnitType.WORKER).Count;
                    int workersKilled = workerCount - workersLeft;
                    int archersLeft = game.GetUnitsByType(0, UnitType.ARCHER).Count;

                    sb.AppendLine($"  {archerCount,7} | {workerCount,7} | {workersKilled,14} | {game.CurrentTick,5} | {archersLeft,17}");
                }
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion
    }
}
