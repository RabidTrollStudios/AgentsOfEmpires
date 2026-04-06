using System.Collections.Generic;
using AgentSDK;

namespace Opponent.Tests
{
    /// <summary>
    /// [HARD] Triple archery archer flood: 6 pawns, 3 Archery.
    /// Pure archer production from 3 buildings with volley micro
    /// (target cycling for frames,
    /// retreat 1 frame to maintain distance from melee).
    /// Attacks with archers when army reaches 6+.
    /// </summary>
    public class ArcherDualOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 6;
        private const int ATTACK_THRESHOLD = 6;
        private const float ANIM_DURATION_BASE = 0.5f;
        private const float FRAME_DURATION = 0.02f;

        private enum ArcherState { Targeting, Firing, Kiting }
        private Dictionary<int, ArcherState> _archerState = new Dictionary<int, ArcherState>();
        private Dictionary<int, int> _archerFireFrames = new Dictionary<int, int>();
        private Dictionary<int, int> _archerLastTarget = new Dictionary<int, int>();

        private bool _buildQueued;

        public override void InitializeMatch()
        {
            _archerState = new Dictionary<int, ArcherState>();
            _archerFireFrames = new Dictionary<int, int>();
            _archerLastTarget = new Dictionary<int, int>();
        }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            _buildQueued = false;
            mainMineNbr = FindClosestMine(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            // Build a base first if we dont have one
            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            TrainPawns(state, actions, MAX_PAWNS);

            // Build 3 archeries for triple archer production
            if (myArchery.Count < 3 && HasBuiltUnit(myBases, state) && !IsPawnBuilding(state) && !_buildQueued)
                BuildStructure(UnitType.ARCHERY, state, actions);

            GatherWithIdlePawns(state, actions);

            // Train archers from all archeries
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

            // Attack with archers using volley + kite micro
            if (myArchers.Count < ATTACK_THRESHOLD) return;
            VolleyKiteMicro(state, actions);
        }

        private void VolleyKiteMicro(IGameState state, IAgentActions actions)
        {
            var enemies = new List<int>();
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER,
                                            UnitType.MONK, UnitType.PAWN, UnitType.BASE,
                                            UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER,
                                            UnitType.MONASTERY })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                    enemies.Add(enemyNbr);
            }
            if (enemies.Count == 0) return;

            int framesPerAnim = System.Math.Max(1,
                (int)System.Math.Ceiling(ANIM_DURATION_BASE / (state.GameSpeed * FRAME_DURATION)));

            foreach (int archerNbr in myArchers)
            {
                var info = state.GetUnit(archerNbr);
                if (!info.HasValue) continue;

                if (!_archerState.ContainsKey(archerNbr))
                {
                    _archerState[archerNbr] = ArcherState.Targeting;
                    _archerFireFrames[archerNbr] = 0;
                }

                switch (_archerState[archerNbr])
                {
                    case ArcherState.Targeting:
                        // Pick a different target from last time (volley bonus)
                        int lastTarget = _archerLastTarget.ContainsKey(archerNbr)
                            ? _archerLastTarget[archerNbr] : -1;
                        int chosenTarget = -1;
                        foreach (int enemyNbr in enemies)
                        {
                            if (enemyNbr != lastTarget)
                            { chosenTarget = enemyNbr; break; }
                        }
                        if (chosenTarget < 0) chosenTarget = enemies[0];

                        actions.Attack(archerNbr, chosenTarget);
                        _archerLastTarget[archerNbr] = chosenTarget;
                        _archerState[archerNbr] = ArcherState.Firing;
                        _archerFireFrames[archerNbr] = 0;
                        break;

                    case ArcherState.Firing:
                        // Wait for one attack animation to complete.
                        // Don't issue any commands — let the engine handle movement + attack.
                        _archerFireFrames[archerNbr]++;

                        if (info.Value.CurrentAction == UnitAction.ATTACK)
                        {
                            // Check if in range (actually firing, not walking)
                            var atkTarget = info.Value.AttackTargetNbr >= 0
                                ? state.GetUnit(info.Value.AttackTargetNbr) : null;
                            bool inRange = false;
                            if (atkTarget.HasValue)
                            {
                                float dist = Position.Distance(info.Value.CenterPosition,
                                    atkTarget.Value.CenterPosition);
                                float range = GameConstants.EffectiveAttackRange(
                                    UnitType.ARCHER, atkTarget.Value.UnitType) + 0.5f;
                                inRange = dist < range;
                            }

                            // Once we've been in range for one animation, kite
                            if (inRange && _archerFireFrames[archerNbr] >= framesPerAnim)
                                _archerState[archerNbr] = ArcherState.Kiting;

                            // If target died (atkTarget null), retarget
                            if (atkTarget == null || !atkTarget.HasValue)
                                _archerState[archerNbr] = ArcherState.Targeting;
                        }
                        else if (info.Value.CurrentAction == UnitAction.IDLE
                            && _archerFireFrames[archerNbr] > framesPerAnim * 2)
                        {
                            // Stuck idle for too long — retarget
                            _archerState[archerNbr] = ArcherState.Targeting;
                        }
                        // Otherwise stay in Firing (command is being processed or unit is walking)
                        break;

                    case ArcherState.Kiting:
                        // Brief retreat toward base then retarget
                        if (info.Value.CurrentAction != UnitAction.MOVE && mainBaseNbr >= 0)
                        {
                            var baseInfo = state.GetUnit(mainBaseNbr);
                            if (baseInfo.HasValue)
                                actions.Move(archerNbr, baseInfo.Value.CenterPosition);
                        }

                        if (info.Value.CurrentAction == UnitAction.IDLE
                            || info.Value.CurrentAction == UnitAction.MOVE)
                        {
                            _archerState[archerNbr] = ArcherState.Targeting;
                        }
                        break;
                }
            }
        }

        private int? FindNearestEnemy(Position from, IGameState state)
        {
            float closestDist = float.MaxValue;
            int? closest = null;
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.LANCER, UnitType.ARCHER,
                                            UnitType.MONK, UnitType.PAWN })
            {
                foreach (int enemyNbr in state.GetEnemyUnits(ut))
                {
                    var info = state.GetUnit(enemyNbr);
                    if (!info.HasValue) continue;
                    float dist = Position.Distance(from, info.Value.CenterPosition);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = enemyNbr;
                    }
                }
            }
            return closest;
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
