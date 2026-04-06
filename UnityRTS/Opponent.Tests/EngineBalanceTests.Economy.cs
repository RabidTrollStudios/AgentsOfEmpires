using System.Linq;
using System.Text;
using AgentSDK;
using AgentTestHarness;
using Xunit;

namespace Opponent.Tests
{
    public partial class EngineBalanceTests
    {
        #region Economy: Pawn Scaling

        [Fact]
        public void PawnScaling()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ECONOMY: Pawn Scaling ===");
            sb.AppendLine("  Pawns | Gold@500f | Gold@1000f | Gold@2000f | Gold/frame");
            sb.AppendLine("  --------+-----------+------------+------------+---------");

            foreach (int pawnCap in new[] { 1, 2, 3, 5, 8, 10 })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 0)
                    .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                    .WithMine(new Position(10, 5), health: 50000)
                    .WithAgent(0, new PureEconomyAgent(pawnCap))
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

                float goldPerFrame = gold2000 / 2000f;

                sb.AppendLine($"  {pawnCap,7} | {gold500,9} | {gold1000,10} | {gold2000,10} | {goldPerFrame,8:F2}");
            }

            _output.WriteLine(sb.ToString());
        }

        #endregion

        #region Economy: Build Timeline

        [Fact]
        public void BuildTimeline()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BUILD TIMELINE: Time to key milestones (3 pawns, starting with 1 pawn + 1000g) ===");

            foreach (UnitType trainType in new[] { UnitType.WARRIOR, UnitType.ARCHER })
            {
                var game = new SimGameBuilder()
                    .WithMapSize(30, 30)
                    .WithGold(0, 1000)
                    .WithGold(1, 0)
                    .WithUnit(0, UnitType.PAWN, new Position(5, 5))
                    .WithMine(new Position(10, 5), health: 50000)
                    .WithAgent(0, new BuildTimelineAgent(3, trainType))
                    .WithAgent(1, new DoNothingAgent())
                    .Build();

                game.InitializeMatch();
                game.InitializeRound();

                int frameBase = -1;
                int frameBarracks = -1;
                int frameFirstUnit = -1;

                for (int t = 0; t < 5000; t++)
                {
                    game.Run(1);

                    if (frameBase < 0 && game.GetUnitsByType(0, UnitType.BASE).Any(u => u.IsBuilt))
                        frameBase = game.CurrentFrame;

                    if (frameBarracks < 0 && game.GetUnitsByType(0, UnitType.BARRACKS).Any(u => u.IsBuilt))
                        frameBarracks = game.CurrentFrame;

                    if (frameFirstUnit < 0)
                    {
                        var units = game.GetUnitsByType(0, trainType);
                        if (units.Count > 0)
                            frameFirstUnit = game.CurrentFrame;
                    }

                    if (frameBarracks >= 0 && frameFirstUnit >= 0)
                        break;
                }

                sb.AppendLine($"  {trainType}: Base built @ frame {frameBase}, Barracks built @ frame {frameBarracks}, First {trainType} @ frame {frameFirstUnit}");
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

            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER })
            {
                float cost = GameConstants.COST[ut];
                float hp = GameConstants.HEALTH[ut];
                float dmg = GameConstants.BASE_DAMAGE[ut];
                float range = GameConstants.ATTACK_RANGE[ut];
                float speed = DerivedGameConstants.SPEED_MULTIPLIER[ut];

                float dpsPerGold = dmg / cost;
                float hpPerGold = hp / cost;

                sb.AppendLine($"  {ut,-7} | {cost,4:F0} | {hp,4:F0} | {dmg,4:F0} | {dpsPerGold,8:F3}  | {hpPerGold,7:F1}  | {range,5:F1} | {speed:F2}");
            }

            sb.AppendLine();
            sb.AppendLine("  === DAMAGE MULTIPLIERS (attacker vs defender) ===");
            sb.AppendLine("  Attacker | vs Warrior | vs Archer | vs Building");
            sb.AppendLine("  ---------+------------+-----------+------------");
            foreach (UnitType at in new[] { UnitType.WARRIOR, UnitType.ARCHER })
            {
                float vsSol = GameConstants.DamageMultiplier(at, UnitType.WARRIOR);
                float vsArc = GameConstants.DamageMultiplier(at, UnitType.ARCHER);
                float vsBld = GameConstants.DamageMultiplier(at, UnitType.BASE);
                sb.AppendLine($"  {at,-8} | {vsSol,10:F2}x | {vsArc,9:F2}x | {vsBld,10:F2}x");
            }

            sb.AppendLine();
            sb.AppendLine("  === EFFECTIVE TTK (seconds, accounting for armor) ===");
            sb.AppendLine("  Attacker | Kill Warrior      | Kill Archer");
            sb.AppendLine("  ---------+-------------------+-------------------");
            foreach (UnitType at in new[] { UnitType.WARRIOR, UnitType.ARCHER })
            {
                float baseDmg = GameConstants.BASE_DAMAGE[at];
                float effVsSol = baseDmg * GameConstants.DamageMultiplier(at, UnitType.WARRIOR);
                float effVsArc = baseDmg * GameConstants.DamageMultiplier(at, UnitType.ARCHER);
                float ttkSol = GameConstants.HEALTH[UnitType.WARRIOR] / effVsSol;
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
                    ? $"Sol={GameConstants.CREATION_TIME_MULTIPLIER[UnitType.WARRIOR]:F0} / Arc={GameConstants.CREATION_TIME_MULTIPLIER[UnitType.ARCHER]:F0}"
                    : ut == UnitType.BASE
                        ? $"Pawn={GameConstants.CREATION_TIME_MULTIPLIER[UnitType.PAWN]:F0}"
                        : "N/A";
                sb.AppendLine($"  {ut,-9} | {cost,4:F0} | {hp,4:F0} | {buildTime,15:F1} | {trainInfo}");
            }

            sb.AppendLine();
            sb.AppendLine($"  Pawn cost: {GameConstants.COST[UnitType.PAWN]}g  |  Mining capacity: {GameConstants.MINING_CAPACITY[UnitType.PAWN]}g/trip");

            _output.WriteLine(sb.ToString());
        }

        #endregion
    }
}
