using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using GameConstants = GameManager.Constants;

namespace GameManager
{
    /// <summary>
    /// Adapts GameManager to ITickWorld for the shared TickEngine.
    /// </summary>
    internal class UnityTickWorld : ITickWorld
    {
        public ITickUnit GetUnit(int unitNbr)
        {
            var u = GameManager.Instance.Units.GetUnit(unitNbr);
            return u; // Unit implements ITickUnit
        }

        public IEnumerable<ITickUnit> AllUnits
        {
            get
            {
                var all = GameManager.Instance.Units.GetAllUnits();
                foreach (var kvp in all)
                {
                    var u = kvp.Value.GetComponent<Unit>();
                    if (u != null) yield return u;
                }
            }
        }

        public GameGrid Grid => GameManager.Instance.Map.Grid;

        public List<Position> FindPath(Position start, Position end)
            => Grid.FindPath(start, end);

        public List<Position> FindPathToUnit(Position start, UnitType unitType, Position anchor)
            => Grid.FindPathToUnit(start, unitType, anchor);

        public List<Position> GetBuildablePositionsNearUnit(UnitType unitType, Position anchor)
        {
            var r = Grid.GetBuildablePositionsNearUnit(unitType, anchor);
            UnityEngine.Debug.Log($"[TRAINDBG] GetBuildablePositionsNearUnit({unitType}, {anchor.X},{anchor.Y}) -> {r.Count} cells");
            return r;
        }

        public bool IsNeighborOfUnit(Position pos, UnitType unitType, Position anchor)
            => Grid.IsNeighborOfUnit(pos, unitType, anchor);

        public void AddGold(int agentNbr, int amount)
        {
            foreach (var agentGo in GameManager.Instance.Agents.Values)
            {
                var agent = agentGo.GetComponent<AgentController>().Agent;
                if (agent.AgentNbr == agentNbr)
                {
                    agent.Gold += amount;
                    return;
                }
            }
        }

        public int GetGold(int agentNbr)
        {
            foreach (var agentGo in GameManager.Instance.Agents.Values)
            {
                var agent = agentGo.GetComponent<AgentController>().Agent;
                if (agent.AgentNbr == agentNbr)
                    return agent.Gold;
            }
            return 0;
        }

        public ITickUnit SpawnUnit(int ownerAgentNbr, UnitType unitType, Position pos, float health, bool isBuilt)
        {
            UnityEngine.Debug.Log($"[TRAINDBG] SpawnUnit(owner={ownerAgentNbr}, {unitType}, {pos.X},{pos.Y}) — Agents has key? {GameManager.Instance.Agents != null && GameManager.Instance.Agents.ContainsKey(ownerAgentNbr)}");
            var gm = GameManager.Instance;
            var agentGo = gm.Agents[ownerAgentNbr];
            var gridPos = new UnityEngine.Vector3Int(pos.X, pos.Y, 0);
            var go = gm.Units.PlaceUnit(agentGo, gridPos, unitType, UnityEngine.Color.white);
            var unit = go.GetComponent<Unit>();
            if (isBuilt) unit.IsBuilt = true;
            return unit;
        }

        public void RemoveUnit(ITickUnit unit)
        {
            if (unit is Unit u)
                GameManager.Instance.Units.DestroyUnit(u.gameObject);
        }

        DerivedGameConstants ITickWorld.Constants => GameConstants.Derived;

        public float TickDuration
        {
            get
            {
                float d = UnityEngine.Time.fixedDeltaTime;
                if (!_loggedTickDur) { UnityEngine.Debug.Log($"[TRAINDBG] TickDuration = {d}"); _loggedTickDur = true; }
                return d;
            }
        }
        private bool _loggedTickDur;
    }
}
