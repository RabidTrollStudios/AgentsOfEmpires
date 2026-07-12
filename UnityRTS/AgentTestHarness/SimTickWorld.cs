using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Adapts SimGame to the <see cref="ITickWorld"/> interface
    /// so the shared TickEngine can operate on SimGame's state.
    /// </summary>
    internal class SimTickWorld : ITickWorld
    {
        private readonly SimGame game;

        public SimTickWorld(SimGame game)
        {
            this.game = game;
        }

        public ITickUnit GetUnit(int unitNbr)
        {
            return game.Units.TryGetValue(unitNbr, out var u) ? u : null;
        }

        public IEnumerable<ITickUnit> AllUnits => game.Units.Values;

        public GameGrid Grid => game.Map.Grid;

        public List<Position> FindPath(Position start, Position end)
            => game.Map.Grid.FindPath(start, end);

        public List<Position> FindPathToUnit(Position start, UnitType unitType, Position anchor)
            => game.Map.Grid.FindPathToUnit(start, unitType, anchor);

        public List<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position anchor)
            => game.Map.Grid.GetBuildablePositionsNearUnit(unitType, anchor);

        public bool IsNeighborOfUnit(Position pos, UnitType unitType, Position anchor)
            => game.Map.Grid.IsNeighborOfUnit(pos, unitType, anchor);

        public void AddGold(int agentNbr, int amount)
            => game.Gold[agentNbr] += amount;

        public int GetGold(int agentNbr)
            => game.Gold[agentNbr];

        public ITickUnit SpawnUnit(int ownerAgentNbr, UnitType unitType, Position pos, float health, bool isBuilt)
            => game.PlaceUnit(ownerAgentNbr, unitType, pos, health, isBuilt);

        public void RemoveUnit(ITickUnit unit)
        {
            if (unit is SimUnit su)
                game.RemoveUnitPublic(su);
        }

        public DerivedGameConstants Constants => game.derived;

        public float TickDuration => game.Config.TickDuration;

        public int CurrentTick => game.CurrentTick;
    }
}
