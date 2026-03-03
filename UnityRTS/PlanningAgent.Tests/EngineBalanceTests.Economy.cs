using System.Linq;
using System.Text;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace PlanningAgent.Tests
{
    public partial class EngineBalanceTests
    {
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
            foreach (UnitType ut in new[] { UnitType.BASE, UnitType.BARRACKS })
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
            sb.AppendLine($"  Worker cost: {GameConstants.COST[UnitType.WORKER]}g  |  Mining capacity: {GameConstants.MINING_CAPACITY[UnitType.WORKER]}g/trip");

            _output.WriteLine(sb.ToString());
        }

        #endregion
    }
}
