using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [EASY] Pure economy: trains pawns and gathers gold, but never builds
    /// military. Has no way to fight back — any army wins.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 10;

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

            // Train pawns
            foreach (int baseNbr in myBases)
            {
                UnitInfo? baseInfo = state.GetUnit(baseNbr);
                if (baseInfo.HasValue && baseInfo.Value.IsBuilt
                    && baseInfo.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.PAWN]
                    && myPawns.Count < MAX_PAWNS)
                {
                    actions.Train(baseNbr, UnitType.PAWN);
                }
            }

            // Gather with idle pawns
            foreach (int pawn in myPawns)
            {
                UnitInfo? unitInfo = state.GetUnit(pawn);
                if (unitInfo.HasValue && unitInfo.Value.CurrentAction == UnitAction.IDLE
                    && mainBaseNbr >= 0 && mainMineNbr >= 0)
                {
                    UnitInfo? mineInfo = state.GetUnit(mainMineNbr);
                    UnitInfo? baseInfo = state.GetUnit(mainBaseNbr);
                    if (mineInfo.HasValue && baseInfo.HasValue && mineInfo.Value.Health > 0)
                    {
                        actions.Gather(pawn, mainMineNbr, mainBaseNbr);
                    }
                }
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

            return freshPositions.Count > 0 ? freshPositions[0] : new Position(-1, -1);
        }
    }
}
