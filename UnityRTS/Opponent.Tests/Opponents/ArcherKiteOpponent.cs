using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [HARD] Kiting archers: 5 pawns, archery, trains archers.
    /// Archers automatically kite — when an enemy melee unit is within
    /// the danger zone (closer than kite threshold), the archer retreats
    /// away from the threat. Once safe distance is restored, resumes attacking.
    /// This exploits archer range (9.0) vs melee range (1.0-2.5) by
    /// maintaining distance and dealing free damage while retreating.
    /// Attacks with 4+ archers.
    /// Strategy to beat: surround with fast units, or overwhelm with numbers
    /// so kiting can't outrun all threats simultaneously.
    /// </summary>
    public class ArcherKiteOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 5;
        private const int ATTACK_THRESHOLD = 4;

        /// <summary>
        /// If any enemy melee unit is closer than this, the archer retreats.
        /// Set between melee attack range (~2.5) and archer attack range (9.0)
        /// so archers run before enemies can hit them.
        /// </summary>
        private const float KITE_THRESHOLD = 5.0f;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            TrainPawns(state, actions, MAX_PAWNS);
            GatherWithIdlePawns(state, actions);

            if (myArchery.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.ARCHERY, state, actions);

            // Train archers
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

            // Kite-and-attack with archers
            if (myArchers.Count >= ATTACK_THRESHOLD)
                KiteWithArchers(state, actions);
        }

        /// <summary>
        /// For each archer: if a melee enemy is dangerously close, move away.
        /// Otherwise, attack the nearest enemy.
        /// </summary>
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

                // Check if any enemy melee unit is within the danger zone
                Position? threatPos = FindClosestMeleeThreat(state, archerPos);

                if (threatPos.HasValue)
                {
                    // Retreat away from the threat
                    Position retreatTarget = ComputeRetreatPosition(archerPos, threatPos.Value, state);
                    actions.Move(archerNbr, retreatTarget);
                }
                else
                {
                    // Safe — attack
                    actions.Attack(archerNbr, attackTarget.Value);
                }
            }
        }

        /// <summary>
        /// Find the closest enemy melee unit (warrior or lancer) within KITE_THRESHOLD.
        /// Returns the threat's position, or null if no threat is close.
        /// </summary>
        private Position? FindClosestMeleeThreat(IGameState state, Position archerPos)
        {
            float closestDist = KITE_THRESHOLD;
            Position? closest = null;

            // Check warriors (range 1.0) and lancers (range 2.5) — both melee threats
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

        /// <summary>
        /// Compute a retreat position: move directly away from the threat.
        /// Clamps to map bounds.
        /// </summary>
        private Position ComputeRetreatPosition(Position archerPos, Position threatPos, IGameState state)
        {
            // Direction away from threat
            int dx = archerPos.X - threatPos.X;
            int dy = archerPos.Y - threatPos.Y;

            // Normalize to a retreat step of ~3 tiles
            if (dx == 0 && dy == 0) dx = 1; // Default: move right if on top of threat

            // Scale to ~3 tiles retreat distance
            float length = System.MathF.Sqrt(dx * dx + dy * dy);
            int retreatX = archerPos.X + (int)(3 * dx / length);
            int retreatY = archerPos.Y + (int)(3 * dy / length);

            // Clamp to map bounds (assume 30x30 standard map, leave 1-tile border)
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
