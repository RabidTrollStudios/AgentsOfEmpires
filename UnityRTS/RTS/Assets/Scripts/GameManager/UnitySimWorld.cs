using System.Collections.Generic;
using System.Linq;
using AgentSDK;
using GameManager.GameElements;
using GameConstants = GameManager.Constants;

namespace GameManager
{
    /// <summary>
    /// Adapts GameManager to ISimWorld for the shared StepEngine.
    /// </summary>
    internal class UnitySimWorld : ISimWorld
    {
        public ISimUnit GetUnit(int unitNbr)
        {
            var u = GameManager.Instance.Units.GetUnit(unitNbr);
            return u; // Unit implements ISimUnit
        }

        public IEnumerable<ISimUnit> AllUnits
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
            => Grid.GetBuildablePositionsNearUnit(unitType, anchor);

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

        public ISimUnit SpawnUnit(int ownerAgentNbr, UnitType unitType, Position pos, float health, bool isBuilt)
        {
            var gm = GameManager.Instance;
            var agentGo = gm.Agents[ownerAgentNbr];
            var gridPos = new UnityEngine.Vector3Int(pos.X, pos.Y, 0);
            var go = gm.Units.PlaceUnit(agentGo, gridPos, unitType, UnityEngine.Color.white);
            var unit = go.GetComponent<Unit>();
            if (isBuilt) unit.IsBuilt = true;
            return unit;
        }

        public void RemoveUnit(ISimUnit unit)
        {
            if (unit is Unit u)
            {
                try
                {
                    GameManager.Instance.Units.DestroyUnit(u.gameObject);
                }
                catch (System.Exception ex)
                {
                    UnityEngine.Debug.LogError($"[RemoveUnit] Failed to remove unit {u.UnitNbr}: {ex}");
                }
            }
            else
            {
                UnityEngine.Debug.LogError($"[RemoveUnit] unit is not a Unit: {unit?.GetType().Name ?? "null"}");
            }
        }

        DerivedGameConstants ISimWorld.Constants => GameConstants.Derived;

        public float StepDuration => UnityEngine.Time.fixedDeltaTime;
    }
}
