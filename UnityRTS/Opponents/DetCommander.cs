using System.Collections.Generic;
using AgentSDK;

namespace PlanningAgent
{
    /// <summary>
    /// [TEST-ONLY] Fully deterministic macro agent for cross-engine PARITY testing.
    ///
    /// Unlike the competitive agents (which may use RNG or rely on runtime-specific
    /// enumeration order), every decision here is a pure function of the
    /// deterministically-ordered game state — no System.Random / UnityEngine.Random,
    /// targets chosen by list order (lowest unitNbr, by type priority) rather than by
    /// float distance (which can tie ambiguously), builds placed at the first buildable
    /// cell. Running this agent as BOTH sides produces byte-identical state in the Unity
    /// engine and the headless SimGame, so it is the reference matchup for the
    /// SameAgents parity test. Keep the non-deterministic agents for competitive play;
    /// use this one to prove engine parity. See memory: warcrap-pathfinding-budget,
    /// warcrap-parity-state.
    /// </summary>
    public class PlanningAgent : PlanningAgentBase
    {
        private const int MAX_PAWNS = 5;

        public override void InitializeMatch() { }

        public override void Update(IGameState state, IAgentActions actions)
        {
            UpdateGameState(state);
            mainMineNbr = mines.Count > 0 ? mines[0] : -1;
            mainBaseNbr = myBases.Count > 0 ? myBases[0] : -1;

            // Build a base first (first buildable cell — deterministic).
            if (myBases.Count == 0)
            {
                BuildFirst(UnitType.BASE, state, actions);
                return;
            }

            // Train pawns from an idle built base.
            foreach (int baseNbr in myBases)
            {
                var info = state.GetUnit(baseNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && myPawns.Count < MAX_PAWNS
                    && state.MyGold >= GameConstants.COST[UnitType.PAWN])
                    actions.Train(baseNbr, UnitType.PAWN);
            }

            // Gather with idle pawns.
            if (mainBaseNbr >= 0 && mainMineNbr >= 0)
            {
                var mineInfo = state.GetUnit(mainMineNbr);
                if (mineInfo.HasValue && mineInfo.Value.Health > 0)
                    foreach (int pawn in myPawns)
                    {
                        var info = state.GetUnit(pawn);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                            actions.Gather(pawn, mainMineNbr, mainBaseNbr);
                    }
            }

            // Barracks once a base is built.
            if (myBarracks.Count == 0 && HasBuiltUnit(myBases, state))
                BuildFirst(UnitType.BARRACKS, state, actions);

            // Train warriors from an idle built barracks.
            foreach (int barracksNbr in myBarracks)
            {
                var info = state.GetUnit(barracksNbr);
                if (info.HasValue && info.Value.IsBuilt
                    && info.Value.CurrentAction == UnitAction.IDLE
                    && state.MyGold >= GameConstants.COST[UnitType.WARRIOR])
                    actions.Train(barracksNbr, UnitType.WARRIOR);
            }

            // Attack the priority target (lowest-unitNbr enemy, by type order)
            // once we have a few warriors.
            if (myWarriors.Count >= 3)
            {
                int? target = PriorityTarget(state);
                if (target.HasValue)
                    foreach (int w in myWarriors)
                    {
                        var info = state.GetUnit(w);
                        if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE)
                            actions.Attack(w, target.Value);
                    }
            }
        }

        private int? PriorityTarget(IGameState state)
        {
            foreach (UnitType ut in new[] { UnitType.PAWN, UnitType.BASE, UnitType.BARRACKS,
                                            UnitType.WARRIOR, UnitType.ARCHER })
            {
                var enemies = state.GetEnemyUnits(ut);
                if (enemies.Count > 0) return enemies[0];
            }
            return null;
        }

        private void BuildFirst(UnitType type, IGameState state, IAgentActions actions)
        {
            if (state.MyGold < GameConstants.COST[type]) return;
            int pawn = -1;
            foreach (int p in myPawns)
            {
                var info = state.GetUnit(p);
                if (info.HasValue && info.Value.CurrentAction == UnitAction.IDLE) { pawn = p; break; }
            }
            if (pawn < 0)
                foreach (int p in myPawns)
                {
                    var info = state.GetUnit(p);
                    if (info.HasValue && info.Value.CurrentAction == UnitAction.GATHER) { pawn = p; break; }
                }
            if (pawn < 0) return;

            // Build at the buildable cell NEAREST the chosen pawn, not the global
            // first cell. FindProspectiveBuildPositions returns cells in absolute
            // grid order (from the map origin), so a naive first-cell pick sends the
            // top-right pawn trekking across the whole map to build near the origin —
            // absurd, and lopsided on a mirrored map. Sorting by distance to the pawn
            // (with DeterministicSort's coordinate tiebreak) keeps it deterministic AND
            // makes both mirrored spawns behave symmetrically.
            var pawnInfo = state.GetUnit(pawn);
            Position anchor = pawnInfo.HasValue ? pawnInfo.Value.GridPosition : new Position(0, 0);

            var candidates = new List<Position>(state.FindProspectiveBuildPositions(type));
            DeterministicSort.SortByDistance(candidates, anchor);

            foreach (Position pos in candidates)
                if (state.IsAreaBuildable(type, pos))
                {
                    actions.Build(pawn, pos, type);
                    return;
                }
        }
    }
}
