using System.Text;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Opponent.Tests
{
    public partial class EngineBalanceTests
    {
        #region Test Agents: Scenario-Specific

        /// <summary>
        /// Rushes military ASAP: builds barracks immediately, trains one unit type,
        /// attacks with all military when threshold reached. Minimal economy (2 pawns).
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
                    foreach (int pawn in myPawns)
                    {
                        var info = state.GetUnit(pawn);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.BASE])
                        {
                            foreach (Position pos in buildPositions)
                            {
                                if (state.IsBoundedAreaBuildable(UnitType.BASE, pos))
                                {
                                    actions.Build(pawn, pos, UnitType.BASE);
                                    return;
                                }
                            }
                        }
                    }
                    return;
                }

                int builderNbr = -1;

                // Train up to 2 pawns
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.PAWN]
                        && myPawns.Count < 2)
                    {
                        actions.Train(baseNbr, UnitType.PAWN);
                    }
                }

                // Build barracks ASAP
                if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                {
                    foreach (int pawn in myPawns)
                    {
                        var info = state.GetUnit(pawn);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.BARRACKS])
                        {
                            foreach (Position pos in buildPositions)
                            {
                                if (state.IsBoundedAreaBuildable(UnitType.BARRACKS, pos))
                                {
                                    actions.Build(pawn, pos, UnitType.BARRACKS);
                                    builderNbr = pawn;
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
                var military = _trainType == UnitType.WARRIOR ? myWarriors : myArchers;
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

                // Gather with non-builder pawns
                if (mainBaseNbr >= 0 && mainMineNbr >= 0)
                {
                    foreach (int pawn in myPawns)
                    {
                        if (pawn == builderNbr) continue;
                        var info = state.GetUnit(pawn);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                            actions.Gather(pawn, mainMineNbr, mainBaseNbr);
                    }
                }
            }

            private int? FindClosestEnemy(IGameState state)
            {
                int? best = null;
                foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                                UnitType.BASE, UnitType.BARRACKS })
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
        /// Greedy economy agent: trains many pawns, delays military.
        /// Used to test whether eco-boom is punishable by rushes.
        /// </summary>
        private class GreedyEconomyAgent : PlanningAgentBase
        {
            private readonly int _maxPawns;
            private readonly int _militaryThreshold;
            private int _builderNbr;
            private bool _attacking;

            public GreedyEconomyAgent(int maxPawns, int militaryThreshold)
            {
                _maxPawns = maxPawns;
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
                    foreach (int pawn in myPawns)
                    {
                        var info = state.GetUnit(pawn);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.BASE])
                        {
                            foreach (Position pos in buildPositions)
                            {
                                if (state.IsBoundedAreaBuildable(UnitType.BASE, pos))
                                {
                                    actions.Build(pawn, pos, UnitType.BASE);
                                    return;
                                }
                            }
                        }
                    }
                    return;
                }

                // Train pawns up to cap
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.PAWN]
                        && myPawns.Count < _maxPawns)
                    {
                        actions.Train(baseNbr, UnitType.PAWN);
                    }
                }

                // Build barracks
                if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                    BuildStructure(UnitType.BARRACKS, state, actions);

                // Train warriors once pawns are saturated
                if (myPawns.Count >= _maxPawns)
                {
                    foreach (int barracksNbr in myBarracks)
                    {
                        var info = state.GetUnit(barracksNbr);
                        if (info.HasValue && info.Value.IsBuilt
                            && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                        {
                            actions.Train(barracksNbr, UnitType.WARRIOR);
                        }
                    }
                }

                // Attack when military threshold reached
                if (myWarriors.Count + myArchers.Count >= _militaryThreshold)
                    _attacking = true;

                if (_attacking)
                {
                    foreach (var unitList in new[] { myWarriors, myArchers })
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
                    foreach (int pawn in myPawns)
                    {
                        if (pawn == _builderNbr) continue;
                        var info = state.GetUnit(pawn);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                            actions.Gather(pawn, mainMineNbr, mainBaseNbr);
                    }
                }
            }

            private void BuildStructure(UnitType type, IGameState state, IAgentActions actions)
            {
                foreach (int pawn in myPawns)
                {
                    var info = state.GetUnit(pawn);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[type])
                    {
                        foreach (Position pos in buildPositions)
                        {
                            if (state.IsBoundedAreaBuildable(type, pos))
                            {
                                actions.Build(pawn, pos, type);
                                _builderNbr = pawn;
                                return;
                            }
                        }
                    }
                }
            }

            private int? FindAnyEnemy(IGameState state)
            {
                foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                                UnitType.BASE, UnitType.BARRACKS })
                {
                    foreach (int enemyNbr in state.GetEnemyUnits(ut))
                        return enemyNbr;
                }
                return null;
            }
        }

        /// <summary>
        /// Simple agent that just gathers with all pawns. Used for mine depletion tests
        /// where pawns are pre-placed and we want no training/building overhead.
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
                    foreach (int pawn in myPawns)
                    {
                        var info = state.GetUnit(pawn);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                            && state.MyGold >= GameConstants.COST[UnitType.BASE])
                        {
                            foreach (Position pos in buildPositions)
                            {
                                if (state.IsBoundedAreaBuildable(UnitType.BASE, pos))
                                {
                                    actions.Build(pawn, pos, UnitType.BASE);
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

                foreach (int pawn in myPawns)
                {
                    var info = state.GetUnit(pawn);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                        actions.Gather(pawn, mainMineNbr, mainBaseNbr);
                }
            }
        }

        #endregion

        #region Mixed Army Compositions

        [Fact]
        public void MixedArmy_VsPureWarriors()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MIXED ARMY vs PURE WARRIORS (Focus Fire, Equal Gold) ===");
            sb.AppendLine("  Gold | Mix (Sol+Arc)    | Pure Warriors | Winner | Survivors | HP%    | Ticks");
            sb.AppendLine("  -----+------------------+---------------+--------+-----------+--------+------");

            foreach (int gold in new[] { 400, 600, 800, 1000, 1200, 1600 })
            {
                // Mixed: split gold roughly 50/50 between warriors and archers
                int mixWarriors = (gold / 2) / (int)GameConstants.COST[UnitType.WARRIOR];
                int mixArchers = (gold / 2) / (int)GameConstants.COST[UnitType.ARCHER];
                int pureWarriors = gold / (int)GameConstants.COST[UnitType.WARRIOR];

                var result = RunCombatWithAgents(
                    agent0WarriorCount: mixWarriors, agent0ArcherCount: mixArchers,
                    agent1WarriorCount: pureWarriors, agent1ArcherCount: 0,
                    new FocusFireAgent(), new FocusFireAgent());

                string winner = result.WinnerAgent == 0 ? "Mixed" :
                                result.WinnerAgent == 1 ? "Warrior" : "Draw";
                int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                sb.AppendLine($"  {gold,4} | {mixWarriors}S + {mixArchers}A ({mixWarriors * 100 + mixArchers * 80}g) | {pureWarriors,13} | {winner,-6} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
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
                int mixWarriors = (gold / 2) / (int)GameConstants.COST[UnitType.WARRIOR];
                int mixArchers = (gold / 2) / (int)GameConstants.COST[UnitType.ARCHER];
                int pureArchers = gold / (int)GameConstants.COST[UnitType.ARCHER];

                var result = RunCombatWithAgents(
                    agent0WarriorCount: mixWarriors, agent0ArcherCount: mixArchers,
                    agent1WarriorCount: 0, agent1ArcherCount: pureArchers,
                    new FocusFireAgent(), new FocusFireAgent());

                string winner = result.WinnerAgent == 0 ? "Mixed" :
                                result.WinnerAgent == 1 ? "Archer" : "Draw";
                int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                sb.AppendLine($"  {gold,4} | {mixWarriors}S + {mixArchers}A ({mixWarriors * 100 + mixArchers * 80}g) | {pureArchers,12} | {winner,-6} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
            }

            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public void MixedArmy_OptimalRatio()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MIXED ARMY OPTIMAL RATIO: 1000g budget, vary warrior:archer split ===");
            sb.AppendLine("  vs 10 Pure Warriors (1000g):");
            sb.AppendLine("  Warriors | Archers | Gold Used | Winner | Survivors | HP%    | Ticks");
            sb.AppendLine("  ---------+---------+-----------+--------+-----------+--------+------");

            // Test various ratios against pure warriors
            var ratios = new[] { (10, 0), (8, 2), (6, 5), (5, 6), (4, 7), (2, 10), (0, 12) };
            foreach (var (sol, arc) in ratios)
            {
                int goldUsed = sol * (int)GameConstants.COST[UnitType.WARRIOR] + arc * (int)GameConstants.COST[UnitType.ARCHER];
                var result = RunCombatWithAgents(
                    agent0WarriorCount: sol, agent0ArcherCount: arc,
                    agent1WarriorCount: 10, agent1ArcherCount: 0,
                    new FocusFireAgent(), new FocusFireAgent());

                string winner = result.WinnerAgent == 0 ? "Mix" :
                                result.WinnerAgent == 1 ? "Warrior" : "Draw";
                int survivors = result.WinnerAgent == 0 ? result.Agent0Units : result.Agent1Units;
                float hpPct = result.WinnerAgent == 0 ? result.Agent0HpPercent : result.Agent1HpPercent;

                sb.AppendLine($"  {sol,8} | {arc,7} | {goldUsed,9} | {winner,-6} | {survivors,9} | {hpPct,5:F1}% | {result.TicksElapsed}");
            }

            sb.AppendLine();
            sb.AppendLine("  vs 12 Pure Archers (960g):");
            sb.AppendLine("  Warriors | Archers | Gold Used | Winner | Survivors | HP%    | Ticks");
            sb.AppendLine("  ---------+---------+-----------+--------+-----------+--------+------");

            foreach (var (sol, arc) in ratios)
            {
                int goldUsed = sol * (int)GameConstants.COST[UnitType.WARRIOR] + arc * (int)GameConstants.COST[UnitType.ARCHER];
                var result = RunCombatWithAgents(
                    agent0WarriorCount: sol, agent0ArcherCount: arc,
                    agent1WarriorCount: 0, agent1ArcherCount: 12,
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
        public void RushTiming_WarriorRushVsEconomy()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RUSH TIMING: Warrior Rush (2-pawn) vs Greedy Economy ===");
            sb.AppendLine("  Testing rush with attack thresholds of 2, 3, 4 warriors");
            sb.AppendLine("  vs eco-boom with 5 pawns (delays military)");
            sb.AppendLine();

            foreach (int rushThreshold in new[] { 2, 3, 4 })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(40, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 1000)
                    // Agent 0: rusher
                    .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                    .WithMine(new Position(8, 5), health: 50000)
                    // Agent 1: eco player on the other side
                    .WithUnit(1, UnitType.PAWN, new Position(30, 5))
                    .WithMine(new Position(24, 5), health: 50000)
                    .WithAgent(0, new RushAgent(UnitType.WARRIOR, rushThreshold))
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
                int sol0 = game.GetUnitsByType(0, UnitType.WARRIOR).Count;
                int sol1 = game.GetUnitsByType(1, UnitType.WARRIOR).Count;
                int arc1 = game.GetUnitsByType(1, UnitType.ARCHER).Count;
                int pawns0 = game.GetUnitsByType(0, UnitType.PAWN).Count;
                int pawns1 = game.GetUnitsByType(1, UnitType.PAWN).Count;

                int total0 = CountAllUnits(game, 0);
                int total1 = CountAllUnits(game, 1);
                string winner = total0 == 0 ? "Eco" : total1 == 0 ? "Rush" : "Timeout";

                sb.AppendLine($"  Rush@{rushThreshold} warriors:");
                sb.AppendLine($"    Result: {winner} wins @ tick {game.CurrentTick}");
                sb.AppendLine($"    Rusher:  {sol0} warriors, {pawns0} pawns, base={base0}, gold={game.GetGold(0)}");
                sb.AppendLine($"    Eco:     {sol1} warriors, {arc1} archers, {pawns1} pawns, base={base1}, gold={game.GetGold(1)}");
                sb.AppendLine();
            }

            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public void RushTiming_ArcherRushVsEconomy()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== RUSH TIMING: Archer Rush (2-pawn) vs Greedy Economy ===");
            sb.AppendLine();

            foreach (int rushThreshold in new[] { 2, 3, 4 })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(40, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 1000)
                    .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                    .WithMine(new Position(8, 5), health: 50000)
                    .WithUnit(1, UnitType.PAWN, new Position(30, 5))
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
                int sol1 = game.GetUnitsByType(1, UnitType.WARRIOR).Count;
                int arc1 = game.GetUnitsByType(1, UnitType.ARCHER).Count;
                int pawns0 = game.GetUnitsByType(0, UnitType.PAWN).Count;
                int pawns1 = game.GetUnitsByType(1, UnitType.PAWN).Count;

                int total0 = CountAllUnits(game, 0);
                int total1 = CountAllUnits(game, 1);
                string winner = total0 == 0 ? "Eco" : total1 == 0 ? "Rush" : "Timeout";

                sb.AppendLine($"  Rush@{rushThreshold} archers:");
                sb.AppendLine($"    Result: {winner} wins @ tick {game.CurrentTick}");
                sb.AppendLine($"    Rusher:  {arc0} archers, {pawns0} pawns, base={base0}, gold={game.GetGold(0)}");
                sb.AppendLine($"    Eco:     {sol1} warriors, {arc1} archers, {pawns1} pawns, base={base1}, gold={game.GetGold(1)}");
                sb.AppendLine();
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Economy vs Military Tradeoff

        [Fact]
        public void EcoVsMilitary_PawnCountImpact()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ECONOMY vs MILITARY: Varying eco pawn count vs 2-pawn warrior rush ===");
            sb.AppendLine("  Eco invests in pawns before military; rusher goes straight to barracks");
            sb.AppendLine();

            foreach (int ecoPawns in new[] { 3, 5, 8 })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(40, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 1000)
                    // Rusher (agent 0)
                    .WithUnit(0, UnitType.PAWN, new Position(2, 5))
                    .WithMine(new Position(8, 5), health: 50000)
                    // Eco player (agent 1)
                    .WithUnit(1, UnitType.PAWN, new Position(30, 5))
                    .WithMine(new Position(24, 5), health: 50000)
                    .WithAgent(0, new RushAgent(UnitType.WARRIOR, 3))
                    .WithAgent(1, new GreedyEconomyAgent(ecoPawns, 3))
                    .Build();

                game.InitializeMatch();
                game.InitializeRound();

                game.RunUntil(g =>
                    CountAllUnits(g, 0) == 0 || CountAllUnits(g, 1) == 0,
                    10000);

                int base0 = game.GetUnitsByType(0, UnitType.BASE).Count;
                int base1 = game.GetUnitsByType(1, UnitType.BASE).Count;
                int sol0 = game.GetUnitsByType(0, UnitType.WARRIOR).Count;
                int sol1 = game.GetUnitsByType(1, UnitType.WARRIOR).Count;
                int pawns1 = game.GetUnitsByType(1, UnitType.PAWN).Count;

                int total0 = CountAllUnits(game, 0);
                int total1 = CountAllUnits(game, 1);
                string winner = total0 == 0 ? "Eco" : total1 == 0 ? "Rush" : "Timeout";

                sb.AppendLine($"  Eco with {ecoPawns} pawns vs 3-warrior rush:");
                sb.AppendLine($"    Result: {winner} wins @ tick {game.CurrentTick}");
                sb.AppendLine($"    Rusher:  {sol0} warriors, base={base0}, gold={game.GetGold(0)}");
                sb.AppendLine($"    Eco:     {sol1} warriors, {pawns1} pawns, base={base1}, gold={game.GetGold(1)}");
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
                (solCount: 1, arcCount: 0, label: "1 Warrior"),
                (solCount: 2, arcCount: 0, label: "2 Warriors"),
                (solCount: 3, arcCount: 0, label: "3 Warriors"),
                (solCount: 5, arcCount: 0, label: "5 Warriors"),
                (solCount: 0, arcCount: 1, label: "1 Archer"),
                (solCount: 0, arcCount: 2, label: "2 Archers"),
                (solCount: 0, arcCount: 3, label: "3 Archers"),
                (solCount: 0, arcCount: 5, label: "5 Archers"),
                (solCount: 2, arcCount: 3, label: "2S + 3A Mix"),
            };

            foreach (UnitType buildingType in new[] { UnitType.BASE, UnitType.BARRACKS })
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
                        builder.WithUnit(0, UnitType.WARRIOR, new Position(12, 13 + i));
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
        public void MineDepletion_TimingByPawnCount()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== MINE DEPLETION: Time to exhaust mine by pawn count ===");
            sb.AppendLine("  Pawns | Mine HP | Depletion Tick | Seconds | Gold Collected | Gold/tick");
            sb.AppendLine("  --------+---------+----------------+---------+----------------+----------");

            foreach (int mineHp in new[] { 2000, 5000 })
            {
                foreach (int pawnCount in new[] { 1, 2, 3, 5, 8 })
                {
                    var builder = new SimGameBuilder()
                        .WithMapSize(30, 30)
                        .WithGold(0, 500)
                        .WithGold(1, 0)
                        .WithMine(new Position(10, 5), health: mineHp)
                        .WithAgent(0, new PureGatherAgent())
                        .WithAgent(1, new DoNothingAgent());

                    // Pre-place pawns (skip training delay)
                    for (int i = 0; i < pawnCount; i++)
                        builder.WithUnit(0, UnitType.PAWN, new Position(7, 5 + i));

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

                    sb.AppendLine($"  {pawnCount,7} | {mineHp,7} | {depletionTick,14} | {seconds,7:F1} | {goldCollected,14} | {goldPerTick,8:F2}");
                }
                sb.AppendLine();
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Pawn Harassment

        [Fact]
        public void PawnHarassment_WarriorVsPawns()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PAWN HARASSMENT: Warriors raiding pawns ===");
            sb.AppendLine("  Warriors | Pawns | Pawns Killed | Ticks | Warriors Surviving");
            sb.AppendLine("  ---------+---------+----------------+-------+-------------------");

            foreach (int warriorCount in new[] { 1, 2, 3 })
            {
                foreach (int pawnCount in new[] { 3, 5, 8 })
                {
                    var builder = new SimGameBuilder()
                        .WithMapSize(30, 30)
                        .WithGold(0, 0)
                        .WithGold(1, 0)
                        .WithAgent(0, new AttackClosestAgent())
                        .WithAgent(1, new DoNothingAgent());  // Pawns don't fight back

                    // Warriors at one side
                    for (int i = 0; i < warriorCount; i++)
                        builder.WithUnit(0, UnitType.WARRIOR, new Position(5, 13 + i));

                    // Pawns clustered near a mine
                    for (int i = 0; i < pawnCount; i++)
                        builder.WithUnit(1, UnitType.PAWN, new Position(15, 12 + i));

                    var game = builder.Build();
                    game.InitializeMatch();
                    game.InitializeRound();

                    game.RunUntil(g => g.GetUnitsByType(1, UnitType.PAWN).Count == 0, 2000);

                    int pawnsLeft = game.GetUnitsByType(1, UnitType.PAWN).Count;
                    int pawnsKilled = pawnCount - pawnsLeft;
                    int warriorsLeft = game.GetUnitsByType(0, UnitType.WARRIOR).Count;

                    sb.AppendLine($"  {warriorCount,8} | {pawnCount,7} | {pawnsKilled,14} | {game.CurrentTick,5} | {warriorsLeft,18}");
                }
            }

            sb.AppendLine();
            sb.AppendLine("  === GOLD COST ANALYSIS ===");
            float warriorCost = GameConstants.COST[UnitType.WARRIOR];
            float pawnCost = GameConstants.COST[UnitType.PAWN];
            sb.AppendLine($"  1 Warrior ({warriorCost}g) kills pawns ({pawnCost}g each):");
            sb.AppendLine($"  - Break even at {warriorCost / pawnCost:F0} pawns killed");
            sb.AppendLine($"  - Plus lost mining time while pawns are dead");

            _output.WriteLine(sb.ToString());
        }

        [Fact]
        public void PawnHarassment_ArcherVsPawns()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== PAWN HARASSMENT: Archers raiding pawns ===");
            sb.AppendLine("  Archers | Pawns | Pawns Killed | Ticks | Archers Surviving");
            sb.AppendLine("  --------+---------+----------------+-------+------------------");

            foreach (int archerCount in new[] { 1, 2, 3 })
            {
                foreach (int pawnCount in new[] { 3, 5, 8 })
                {
                    var builder = new SimGameBuilder()
                        .WithMapSize(30, 30)
                        .WithGold(0, 0)
                        .WithGold(1, 0)
                        .WithAgent(0, new AttackClosestAgent())
                        .WithAgent(1, new DoNothingAgent());

                    for (int i = 0; i < archerCount; i++)
                        builder.WithUnit(0, UnitType.ARCHER, new Position(5, 13 + i));

                    for (int i = 0; i < pawnCount; i++)
                        builder.WithUnit(1, UnitType.PAWN, new Position(15, 12 + i));

                    var game = builder.Build();
                    game.InitializeMatch();
                    game.InitializeRound();

                    game.RunUntil(g => g.GetUnitsByType(1, UnitType.PAWN).Count == 0, 2000);

                    int pawnsLeft = game.GetUnitsByType(1, UnitType.PAWN).Count;
                    int pawnsKilled = pawnCount - pawnsLeft;
                    int archersLeft = game.GetUnitsByType(0, UnitType.ARCHER).Count;

                    sb.AppendLine($"  {archerCount,7} | {pawnCount,7} | {pawnsKilled,14} | {game.CurrentTick,5} | {archersLeft,17}");
                }
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion
    }
}
