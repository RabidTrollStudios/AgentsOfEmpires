using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [HARD] Dual archery with kiting: 6 pawns, 2 Archeries, masses archers.
    /// Archers kite — retreat from melee threats, then resume attacking.
    /// Combines the dual-production throughput of ArcherDual with micro.
    /// Tests whether kiting makes massed archers truly uncounterable,
    /// or if warriors/lancers can still close the gap with numbers.
    /// Strategy to beat: overwhelming numbers, fast lancers, or split attacks.
    /// </summary>
    public class ArcherKiteDualOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int MAX_ARCHERY = 2;
        private const int ATTACK_THRESHOLD = 6;
        private const float KITE_THRESHOLD = 5.0f;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            if (myArchery.Count < MAX_ARCHERY && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);

            foreach (int archeryNbr in myArchery)
            {
                var info = state.GetUnit(archeryNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.ARCHER])
                {
                    actions.Train(archeryNbr, UnitType.ARCHER);
                }
            }

            if (myArchers.Count >= ATTACK_THRESHOLD)
                KiteWithArchers(state, actions);
        }

        private void KiteWithArchers(IGameState state, IAgentActions actions)
        {
            int? attackTarget = FindAnyEnemy(state);
            if (!attackTarget.HasValue) return;

            foreach (int archerNbr in myArchers)
            {
                var archerInfo = state.GetUnit(archerNbr);
                if (!archerInfo.HasValue) continue;
                if (archerInfo.Value.CurrentAction != UnitAction.IDLE
                    && archerInfo.Value.CurrentAction != UnitAction.ATTACK) continue;

                Position archerPos = archerInfo.Value.CenterPosition;
                Position? threatPos = FindClosestMeleeThreat(state, archerPos);

                if (threatPos.HasValue)
                {
                    Position retreatTarget = ComputeRetreatPosition(archerPos, threatPos.Value);
                    actions.Move(archerNbr, retreatTarget);
                }
                else
                {
                    actions.Attack(archerNbr, attackTarget.Value);
                }
            }
        }

        private Position? FindClosestMeleeThreat(IGameState state, Position archerPos)
        {
            float closestDist = KITE_THRESHOLD;
            Position? closest = null;

            foreach (UnitType meleeType in new[] { UnitType.WARRIOR, UnitType.LANCER })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(meleeType))
                {
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(archerPos, enemyInfo.Value.CenterPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = enemyInfo.Value.CenterPosition;
                    }
                }
            }
            return closest;
        }

        private Position ComputeRetreatPosition(Position archerPos, Position threatPos)
        {
            int dx = archerPos.X - threatPos.X;
            int dy = archerPos.Y - threatPos.Y;
            if (dx == 0 && dy == 0) dx = 1;
            float length = System.MathF.Sqrt(dx * dx + dy * dy);
            int retreatX = archerPos.X + (int)(3 * dx / length);
            int retreatY = archerPos.Y + (int)(3 * dy / length);
            retreatX = System.Math.Clamp(retreatX, 1, 28);
            retreatY = System.Math.Clamp(retreatY, 1, 28);
            return new Position(retreatX, retreatY);
        }

        private void TrainPawns(IGameState state, IAgentActions actions, int max)
        {
            foreach (int baseNbr in myBases)
            {
                var info = state.GetUnit(baseNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.PAWN]
                    && myPawns.Count < max)
                {
                    actions.Train(baseNbr, UnitType.PAWN);
                }
            }
        }

        private void GatherWithIdlePawns(IGameState state, IAgentActions actions)
        {
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
                            return;
                        }
                    }
                }
            }
        }

        private int? FindAnyEnemy(IGameState state)
        {
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
                                            UnitType.MONK, UnitType.PAWN, UnitType.BASE,
                                            UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER,
                                            UnitType.MONASTERY })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) return enemies[0];
            }
            return null;
        }
    }
}
