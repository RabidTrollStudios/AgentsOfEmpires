using System.Text;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    public partial class EngineBalanceTests
    {
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

                    // Pre-place workers (skip training delay)
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
