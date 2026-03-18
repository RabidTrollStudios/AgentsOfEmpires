using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent.Tests
{
    /// <summary>
    /// [EASY] Pure economy: trains pawns and gathers gold, but never builds
    /// military. Has no way to fight back — any army wins.
    /// Strategy to beat: build a barracks, train a single warrior.
    /// </summary>
    public class GathererOpponent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 10;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            // Train pawns
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

            // Gather
            GatherWithIdlePawns(state, actions);
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
    }
}
