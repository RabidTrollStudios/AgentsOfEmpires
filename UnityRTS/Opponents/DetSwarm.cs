using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    // ============================================================================
    // [DET-SNAPSHOT of Swarm, frozen 2026-07-12] TEST-ONLY deterministic copy.
    //
    // A faithful, verbatim copy of the competitive Swarm agent, included ONLY in the
    // parity test set so cross-engine parity runs on a real, long, engine-exercising
    // game. Swarm was already fully deterministic (no RNG, no Dictionary/HashSet
    // iteration feeding a decision, no raw List.Sort — it relies on PlanningAgentBase's
    // pre-ordered unit lists and DeterministicSort), so this copy needs no changes.
    //
    // FROZEN: this is a point-in-time snapshot, NOT maintained in lockstep with Swarm.
    // The competitive Swarm may later change or gain intentional non-determinism; this
    // copy intentionally stays as it was on 2026-07-12. Do not "sync" it. Built to
    // EnemyAgents/PlanningAgent_DetSwarm.dll.
    // ============================================================================
    /// <summary>
    /// [HARD] Relentless aggression: 4 pawns for economy, gets a barracks,
    /// then constantly produces warriors and attacks immediately.
    /// Never waits to mass up — sends troops the moment they're idle.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 4;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;
            if (mainMineNbr < 0 || !state.GetUnit(mainMineNbr).HasValue || state.GetUnit(mainMineNbr).Value.Health <= 0)
                mainMineNbr = FindClosestMine(state);

            // Build a base first — game starts with only a pawn and a mine
            if (myBases.Count == 0)
            {
                BuildStructure(UnitType.BASE, state, actions);
                return;
            }

            TrainPawns(state, actions);

            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildStructure(UnitType.BARRACKS, state, actions);

            GatherWithIdlePawns(state, actions);

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

            // Attack immediately — don't wait
            DefendTroops(myWarriors, state, actions);
            AttackWithUnits(myWarriors, state, actions);
        }

        private void TrainPawns(IGameState state, IAgentActions actions)
        {
            foreach (int baseNbr in myBases)
            {
                var info = state.GetUnit(baseNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.PAWN]
                    && myPawns.Count < MAX_PAWNS)
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

        private int FindClosestMine(IGameState state)
        {
            if (mines.Count == 0) return -1;
            if (myPawns.Count == 0) return mines[0];
            var pawnInfo = state.GetUnit(myPawns[0]);
            if (!pawnInfo.HasValue) return mines[0];

            Position pawnPos = pawnInfo.Value.GridPosition;
            int bestMine = -1;
            int bestPathLen = int.MaxValue;
            foreach (int mineNbr in mines)
            {
                var mineInfo = state.GetUnit(mineNbr);
                if (mineInfo.HasValue && mineInfo.Value.Health > 0)
                {
                    int pathLen = state.GetPathToUnit(pawnPos, UnitType.MINE, mineInfo.Value.GridPosition).Count;
                    if (pathLen > 0 && pathLen < bestPathLen)
                    {
                        bestPathLen = pathLen;
                        bestMine = mineNbr;
                    }
                }
            }

            if (bestMine == -1)
            {
                float bestDist = float.MaxValue;
                foreach (int mineNbr in mines)
                {
                    var mineInfo = state.GetUnit(mineNbr);
                    if (mineInfo.HasValue && mineInfo.Value.Health > 0)
                    {
                        float dist = Position.Distance(pawnPos, mineInfo.Value.CenterPosition);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestMine = mineNbr;
                        }
                    }
                }
            }

            return bestMine >= 0 ? bestMine : mines[0];
        }

        private void BuildStructure(UnitType type, IGameState state, IAgentActions actions)
        {
            foreach (int pawn in myPawns)
            {
                var info = state.GetUnit(pawn);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[type])
                {
                    Position buildPos = FindBestBuildPosition(type, state);
                    if (buildPos.X >= 0)
                    {
                        actions.Build(pawn, buildPos, type);
                        return;
                    }
                }
            }
        }

        private Position FindBestBuildPosition(UnitType type, IGameState state)
        {
            var freshPositions = state.FindProspectiveBuildPositions(type);

            if (type == UnitType.BASE && mainMineNbr >= 0)
            {
                var mineInfo = state.GetUnit(mainMineNbr);
                if (mineInfo.HasValue)
                {
                    Position minePos = mineInfo.Value.GridPosition;
                    float bestDist = float.MaxValue;
                    Position bestPos = new Position(-1, -1);
                    foreach (Position pos in freshPositions)
                    {
                        float dist = Position.Distance(pos, mineInfo.Value.CenterPosition);
                        if (dist >= 2f && dist < bestDist)
                        {
                            bestDist = dist;
                            bestPos = pos;
                        }
                    }
                    if (bestPos.X >= 0) return bestPos;
                }
            }
            else if (type == UnitType.BARRACKS && mainBaseNbr >= 0)
            {
                var baseInfo = state.GetUnit(mainBaseNbr);
                if (baseInfo.HasValue)
                {
                    Position basePos = baseInfo.Value.GridPosition;
                    float bestDist = float.MaxValue;
                    Position bestPos = new Position(-1, -1);
                    foreach (Position pos in freshPositions)
                    {
                        float dist = Position.Distance(pos, baseInfo.Value.CenterPosition);
                        if (dist >= 2f && dist < bestDist)
                        {
                            bestDist = dist;
                            bestPos = pos;
                        }
                    }
                    if (bestPos.X >= 0) return bestPos;
                }
            }

            return freshPositions.Count > 0 ? freshPositions[0] : new Position(-1, -1);
        }

        private void AttackWithUnits(List<int> units, IGameState state, IAgentActions actions)
        {
            foreach (int unitNbr in units)
            {
                var info = state.GetUnit(unitNbr);
                if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;
                int? target = FindClosestEnemy(unitNbr, state);
                if (target.HasValue)
                    actions.Attack(unitNbr, target.Value);
            }
        }

        private void DefendTroops(List<int> units, IGameState state, IAgentActions actions)
        {
            foreach (int unitNbr in units)
            {
                var info = state.GetUnit(unitNbr);
                if (!info.HasValue || info.Value.CurrentAction != UnitAction.IDLE) continue;
                Position myPos = info.Value.GridPosition;
                float myRange = GameConstants.ATTACK_RANGE[info.Value.UnitType];

                int? bestTarget = null;
                float bestDist = float.MaxValue;
                foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.PAWN,
                                                UnitType.BASE, UnitType.BARRACKS })
                {
                    var enemies = state.GetEnemyUnits(ut);
                    foreach (int enemyNbr in enemies)
                    {
                        var enemyInfo = state.GetUnit(enemyNbr);
                        if (!enemyInfo.HasValue) continue;
                        float dist = Position.Distance(myPos, enemyInfo.Value.CenterPosition);
                        if (dist <= myRange && dist < bestDist)
                        {
                            bestDist = dist;
                            bestTarget = enemyNbr;
                        }
                    }
                }
                if (bestTarget.HasValue)
                    actions.Attack(unitNbr, bestTarget.Value);
            }
        }

        private int? FindClosestEnemy(int attackerNbr, IGameState state)
        {
            var attackerInfo = state.GetUnit(attackerNbr);
            if (!attackerInfo.HasValue) return null;
            Position attackerPos = attackerInfo.Value.GridPosition;

            int? bestTarget = null;
            float bestDist = float.MaxValue;
            foreach (UnitType ut in new[] { UnitType.WARRIOR, UnitType.ARCHER, UnitType.LANCER, UnitType.PAWN,
                                            UnitType.BASE, UnitType.BARRACKS, UnitType.ARCHERY, UnitType.TOWER })
            {
                var enemies = state.GetEnemyUnits(ut);
                foreach (int enemyNbr in enemies)
                {
                    var enemyInfo = state.GetUnit(enemyNbr);
                    if (!enemyInfo.HasValue) continue;
                    float dist = Position.Distance(attackerPos, enemyInfo.Value.CenterPosition);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestTarget = enemyNbr;
                    }
                }
            }
            return bestTarget;
        }
    }
}
