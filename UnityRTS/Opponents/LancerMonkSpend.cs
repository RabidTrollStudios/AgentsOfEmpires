using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [IMPOSSIBLE] Adaptive lancer-focused agent with monk sustain.
    /// Makes decisions based on game state: scales economy when gold-starved,
    /// builds more production when gold-rich, adds monks when taking losses.
    /// Lancers use hit-and-run joust micro. Guaranteed at least 1 monastery.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int ATTACK_THRESHOLD = 4;
        private const float GOLD_STARVED = 100f;
        private const float GOLD_RICH = 150f;
        private const int RETREAT_DISTANCE = 2;
        private const float ANIM_DURATION_BASE = 0.5f; // 6 frames at 12 FPS
        private const float FRAME_DURATION = 0.02f;  // 50 Hz fixed update

        private enum JoustState { Charging, Striking, Retreating }
        private Dictionary<int, JoustState> _joustState = new Dictionary<int, JoustState>();
        private Dictionary<int, int> _strikeFrames = new Dictionary<int, int>();

        private int _lastArmySize;
        private int _framesSinceArmyShrunk;

        private bool _buildQueued;

        public override void InitializeMatch()
        {
            _lastArmySize = 0;
            _framesSinceArmyShrunk = 999;
            _joustState = new Dictionary<int, JoustState>();
            _strikeFrames = new Dictionary<int, int>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            _buildQueued = false;
            mainMineNbr = FindClosestMine(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            int armySize = myWarriors.Count + myArchers.Count + myLancers.Count;
            if (armySize < _lastArmySize) _framesSinceArmyShrunk = 0;
            else _framesSinceArmyShrunk++;
            _lastArmySize = armySize;

            int enemyArmy = state.GetEnemyUnits(UnitType.WARRIOR).Count
                + state.GetEnemyUnits(UnitType.ARCHER).Count
                + state.GetEnemyUnits(UnitType.LANCER).Count;
            bool goldStarved = state.MyGold < GOLD_STARVED;
            bool goldRich = state.MyGold > GOLD_RICH;
            bool takingLosses = _framesSinceArmyShrunk < 20;
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
            if (myTowers.Count == 0 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state) && !_buildQueued)
                BuildStructure(UnitType.TOWER, state, actions);
            else if (myMonasteries.Count == 0 && HasBuiltUnit(myTowers, state) && !IsPawnBuilding(state) && !_buildQueued)
                BuildStructure(UnitType.MONASTERY, state, actions);
            else if (goldRich && myTowers.Count < 3 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state) && !_buildQueued)
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

            foreach (int lancerNbr in myLancers)
            {
                var info = state.GetUnit(lancerNbr);
                if (!info.HasValue) continue;

                if (!_joustState.ContainsKey(lancerNbr))
                {
                    _joustState[lancerNbr] = JoustState.Charging;
                    _strikeFrames[lancerNbr] = 0;
                }

                switch (_joustState[lancerNbr])
                {
                    case JoustState.Charging:
                        // Waiting to reach the target. Engine handles pathing via ATTACK.
                        if (info.Value.CurrentAction == UnitAction.IDLE)
                        {
                            // Pick a target and charge
                            int? target = FindPriorityTarget(state, info.Value.CenterPosition);
                            if (target.HasValue)
                                actions.Attack(lancerNbr, target.Value);
                        }
                        else if (info.Value.CurrentAction == UnitAction.ATTACK)
                        {
                            // Check if we've reached attack range
                            var atkTarget = info.Value.AttackTargetNbr >= 0
                                ? state.GetUnit(info.Value.AttackTargetNbr) : null;
                            if (atkTarget.HasValue)
                            {
                                float dist = Position.Distance(info.Value.CenterPosition,
                                    atkTarget.Value.CenterPosition);
                                float range = GameConstants.EffectiveAttackRange(
                                    UnitType.LANCER, atkTarget.Value.UnitType) + 0.5f;
                                if (dist < range)
                                {
                                    // In range — transition to striking
                                    _joustState[lancerNbr] = JoustState.Striking;
                                    _strikeFrames[lancerNbr] = 0;
                                }
                            }
                        }
                        break;

                    case JoustState.Striking:
                        // In range, dealing damage. Wait one animation cycle then retreat.
                        _strikeFrames[lancerNbr]++;
                        int framesPerAnim = System.Math.Max(1,
                            (int)System.Math.Ceiling(ANIM_DURATION_BASE / (state.GameSpeed * FRAME_DURATION)));
                        if (_strikeFrames[lancerNbr] >= framesPerAnim)
                        {
                            var atkTarget = info.Value.AttackTargetNbr >= 0
                                ? state.GetUnit(info.Value.AttackTargetNbr) : null;
                            Position enemyPos = atkTarget.HasValue
                                ? atkTarget.Value.GridPosition : info.Value.GridPosition;
                            Position? retreat = FindRetreatPosition(
                                info.Value.GridPosition, enemyPos, RETREAT_DISTANCE, state);
                            if (retreat.HasValue)
                            {
                                actions.Move(lancerNbr, retreat.Value);
                                _joustState[lancerNbr] = JoustState.Retreating;
                            }
                            else
                            {
                                // Can't retreat — stay and fight
                                _strikeFrames[lancerNbr] = 0;
                            }
                        }
                        break;

                    case JoustState.Retreating:
                        // Moving away. When move completes (IDLE), pick new target and charge.
                        if (info.Value.CurrentAction == UnitAction.IDLE)
                        {
                            int? target = FindPriorityTarget(state, info.Value.CenterPosition);
                            if (target.HasValue)
                                actions.Attack(lancerNbr, target.Value);
                            _joustState[lancerNbr] = JoustState.Charging;
                        }
                        break;
                }
            }
        }

        private int? FindPriorityTarget(IGameState state, Position fromPos)
        {
            int? best = null;
            float bestDist = float.MaxValue;
            Position myPos = fromPos;

            // First pass: combat units (warriors > archers > lancers > monks > pawns)
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
                                            UnitType.MONK, UnitType.PAWN })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    var info = state.GetUnit(enemyNbr);
                    if (!info.HasValue) continue;
                    float dist = Position.Distance(info.Value.CenterPosition, myPos);
                    if (dist < bestDist) { bestDist = dist; best = enemyNbr; }
                }
            }
            if (best.HasValue) return best;

            // Fallback: buildings
            foreach (UnitType ut in new[] { UnitType.BASE, UnitType.BARRACKS,
                                            UnitType.ARCHERY, UnitType.TOWER, UnitType.MONASTERY })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    var info = state.GetUnit(enemyNbr);
                    if (!info.HasValue) continue;
                    float dist = Position.Distance(info.Value.CenterPosition, myPos);
                    if (dist < bestDist) { bestDist = dist; best = enemyNbr; }
                }
            }
            return best;
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
                        _buildQueued = true;
                        return;
                    }
                }
            }
        }

        private bool IsPawnBuilding(IGameState state)
        {
            foreach (int pawn in myPawns)
            {
                var info = state.GetUnit(pawn);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.BUILD)
                    return true;
            }
            return false;
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
