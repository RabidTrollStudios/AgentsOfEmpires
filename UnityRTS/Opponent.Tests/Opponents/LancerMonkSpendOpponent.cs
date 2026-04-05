using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [IMPOSSIBLE] Adaptive lancer-focused agent with monk sustain.
    /// Makes decisions based on game state: scales economy when gold-starved,
    /// builds more production when gold-rich, adds monks when taking losses.
    /// Lancers use hit-and-run joust micro. Guaranteed at least 1 monastery.
    /// </summary>
    public class LancerMonkSpendOpponent : PlanningAgentBase
    {
        private const int ATTACK_THRESHOLD = 4;
        private const float GOLD_STARVED = 100f;
        private const float GOLD_RICH = 150f;
        private const float RALLY_DISTANCE = 5.0f;

        private int _lastArmySize;
        private int _ticksSinceArmyShrunk;
        private Dictionary<int, float> _lancerLastHp = new Dictionary<int, float>();

        public override void InitializeMatch()
        {
            _lastArmySize = 0;
            _ticksSinceArmyShrunk = 999;
            _lancerLastHp = new Dictionary<int, float>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = FindClosestMine(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            int armySize = myWarriors.Count + myArchers.Count + myLancers.Count;
            if (armySize < _lastArmySize) _ticksSinceArmyShrunk = 0;
            else _ticksSinceArmyShrunk++;
            _lastArmySize = armySize;

            int enemyArmy = state.GetEnemyUnits(UnitType.WARRIOR).Count
                + state.GetEnemyUnits(UnitType.ARCHER).Count
                + state.GetEnemyUnits(UnitType.LANCER).Count;
            bool goldStarved = state.MyGold < GOLD_STARVED;
            bool goldRich = state.MyGold > GOLD_RICH;
            bool takingLosses = _ticksSinceArmyShrunk < 20;
            bool outnumbered = enemyArmy > armySize;
            bool needMorePawns = myPawns.Count < 5 || (goldStarved && myPawns.Count < 8);

            if (needMorePawns)
            {
                foreach (int baseNbr in myBases)
                {
                    var info = state.GetUnit(baseNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                    {
                        actions.Train(baseNbr, UnitType.PAWN);
                    }
                }
            }

            // Build: tower first, then monastery, then scale towers
            if (myTowers.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.TOWER, state, actions);
            else if (myMonasteries.Count == 0 && HasBuiltUnit(myTowers, state))
                BuildStructure(UnitType.MONASTERY, state, actions);
            else if (goldRich && myTowers.Count < 3 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.TOWER, state, actions);

            GatherWithIdlePawns(state, actions);

            // Train lancers
            foreach (int towerNbr in myTowers)
            {
                var info = state.GetUnit(towerNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.LANCER])
                {
                    actions.Train(towerNbr, UnitType.LANCER);
                }
            }

            if ((takingLosses && myMonks.Count < 3) || myMonks.Count < 1)
            {
                foreach (int monasteryNbr in myMonasteries)
                {
                    var info = state.GetUnit(monasteryNbr);
                    if (info.HasValue && info.Value.IsBuilt
                        && info.Value.CurrentAction == UnitAction.IDLE
                        && state.MyGold >= GameConstants.COST[UnitType.MONK])
                    {
                        actions.Train(monasteryNbr, UnitType.MONK);
                    }
                }
            }

            HealWithMonks(state, actions);

            if (myLancers.Count < ATTACK_THRESHOLD) return;

            int? enemyTarget = FindAnyEnemy(state);
            if (!enemyTarget.HasValue) return;
            var targetInfo = state.GetUnit(enemyTarget.Value);
            if (!targetInfo.HasValue) return;

            foreach (int lancerNbr in myLancers)
            {
                var info = state.GetUnit(lancerNbr);
                if (!info.HasValue) continue;

                float currentHp = info.Value.Health;
                float lastHp = _lancerLastHp.ContainsKey(lancerNbr) ? _lancerLastHp[lancerNbr] : currentHp;
                bool tookDamage = currentHp < lastHp;
                _lancerLastHp[lancerNbr] = currentHp;

                if (info.Value.CurrentAction == UnitAction.ATTACK)
                {
                    // Check what this lancer is fighting — only joust against melee targets
                    var myTarget = info.Value.AttackTargetNbr >= 0 ? state.GetUnit(info.Value.AttackTargetNbr) : null;
                    bool fightingRanged = myTarget.HasValue && myTarget.Value.UnitType == UnitType.ARCHER;

                    if (tookDamage && !fightingRanged)
                    {
                        Position enemyPos = myTarget.HasValue ? myTarget.Value.GridPosition : targetInfo.Value.GridPosition;
                        Position? retreat = FindRetreatPosition(info.Value.GridPosition, enemyPos, 3, state);
                        if (retreat.HasValue)
                            actions.Move(lancerNbr, retreat.Value);
                    }
                }
                else if (info.Value.CurrentAction == UnitAction.MOVE)
                {
                    float dist = Position.Distance(info.Value.CenterPosition, targetInfo.Value.CenterPosition);
                    if (dist >= RALLY_DISTANCE)
                        actions.Attack(lancerNbr, enemyTarget.Value);
                }
                else if (info.Value.CurrentAction == UnitAction.IDLE)
                {
                    actions.Attack(lancerNbr, enemyTarget.Value);
                }
            }
        }

        private Position? FindRetreatPosition(Position from, Position enemy, int distance, IGameState state)
        {
            // Calculate direction away from enemy
            int dx = from.X - enemy.X;
            int dy = from.Y - enemy.Y;
            // Normalize to -1/0/1
            int sx = dx > 0 ? 1 : dx < 0 ? -1 : 0;
            int sy = dy > 0 ? 1 : dy < 0 ? -1 : 0;
            if (sx == 0 && sy == 0) sx = 1; // default right if on top of enemy

            // Try primary direction, then fallback rotations
            int[][] dirs = new int[][] {
                new[] { sx, sy },
                new[] { sx, 0 },
                new[] { 0, sy },
                new[] { -sy, sx },  // 90 degrees
                new[] { sy, -sx },  // -90 degrees
            };

            foreach (var dir in dirs)
            {
                Position target = new Position(from.X + dir[0] * distance, from.Y + dir[1] * distance);
                // Check all cells along the path are clear
                bool clear = true;
                for (int i = 1; i <= distance; i++)
                {
                    Position step = new Position(from.X + dir[0] * i, from.Y + dir[1] * i);
                    if (!state.IsPositionBuildable(step)) { clear = false; break; }
                }
                if (clear) return target;
            }
            return null; // no clear retreat path found
        }

        private void HealWithMonks(IGameState state, IAgentActions actions)
        {
            foreach (int monkNbr in myMonks)
            {
                var monkInfo = state.GetUnit(monkNbr);
                if (!monkInfo.HasValue || monkInfo.Value.CurrentAction != UnitAction.IDLE) continue;
                if (monkInfo.Value.Mana < GameConstants.MANA_COST) continue;

                int? bestTarget = null;
                float lowestHpRatio = 0.8f;
                foreach (int unitNbr in myLancers)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.LANCER];
                    if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; }
                }
                foreach (int unitNbr in myWarriors)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.WARRIOR];
                    if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; }
                }
                foreach (int unitNbr in myArchers)
                {
                    var info = state.GetUnit(unitNbr);
                    if (!info.HasValue) continue;
                    float ratio = info.Value.Health / GameConstants.HEALTH[UnitType.ARCHER];
                    if (ratio < lowestHpRatio) { lowestHpRatio = ratio; bestTarget = unitNbr; }
                }
                if (bestTarget.HasValue)
                    actions.Heal(monkNbr, bestTarget.Value);
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
                    // Pick the closest buildable position to this pawn
                    Position pawnPos = info.Value.GridPosition;
                    Position? bestPos = null;
                    float bestDist = float.MaxValue;
                    foreach (Position pos in buildPositions)
                    {
                        if (state.IsBoundedAreaBuildable(type, pos))
                        {
                            float dist = Position.Distance(pos, pawnPos);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestPos = pos;
                            }
                        }
                    }
                    if (bestPos.HasValue)
                    {
                        actions.Build(pawn, bestPos.Value, type);
                        return;
                    }
                }
            }
        }

        private int FindClosestMine(IGameState state)
        {
            Position refPos = mainBaseNbr >= 0 && state.GetUnit(mainBaseNbr).HasValue
                ? state.GetUnit(mainBaseNbr).Value.CenterPosition
                : (myPawns.Count > 0 && state.GetUnit(myPawns[0]).HasValue
                    ? state.GetUnit(myPawns[0]).Value.GridPosition
                    : new Position(0, 0));
            int bestMine = -1;
            float bestDist = float.MaxValue;
            foreach (int mineNbr in mines)
            {
                var info = state.GetUnit(mineNbr);
                if (!info.HasValue || info.Value.Health <= 0) continue;
                float dist = Position.Distance(info.Value.CenterPosition, refPos);
                if (dist < bestDist) { bestDist = dist; bestMine = mineNbr; }
            }
            return bestMine;
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
