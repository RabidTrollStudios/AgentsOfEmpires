using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Command processing: validates queued agent commands and initiates unit actions.
    /// </summary>
    public partial class SimGame
    {
        private void ProcessCommands(SimAgentActions agentActions)
        {
            foreach (var cmd in agentActions.PendingCommands)
            {
                if (!Units.TryGetValue(cmd.UnitNbr, out var unit))
                    continue;

                switch (cmd.Type)
                {
                    case CommandType.Move:
                        ProcessMove(unit, cmd.Target);
                        break;
                    case CommandType.Build:
                        ProcessBuild(unit, cmd.Target, cmd.UnitType);
                        break;
                    case CommandType.Gather:
                        ProcessGather(unit, cmd.MineNbr, cmd.BaseNbr);
                        break;
                    case CommandType.Train:
                        ProcessTrain(unit, cmd.UnitType);
                        break;
                    case CommandType.Attack:
                        ProcessAttack(unit, cmd.TargetUnitNbr);
                        break;
                }
            }
        }

        private void ProcessMove(SimUnit unit, Position target)
        {
            var path = Map.FindPath(unit.GridPosition, target);
            if (path.Count == 0) return;

            unit.CurrentAction = UnitAction.MOVE;
            unit.Path = path;
            unit.PathIndex = 0;
        }

        private void ProcessBuild(SimUnit worker, Position target, UnitType buildingType)
        {
            // Re-validate gold (may have been spent by earlier command this tick)
            float cost = GameConstants.COST[buildingType];
            if (Gold[worker.OwnerAgentNbr] < cost) return;
            if (!Map.IsAreaBuildable(buildingType, target)) return;

            // Deduct gold at build start
            Gold[worker.OwnerAgentNbr] -= (int)cost;

            // Place the building immediately (unbuilt)
            var building = PlaceUnit(worker.OwnerAgentNbr, buildingType, target,
                GameConstants.HEALTH[buildingType], false);

            // Path worker to a cell adjacent to the building
            var path = Map.FindPathToUnit(worker.GridPosition, buildingType, target);

            worker.CurrentAction = UnitAction.BUILD;
            worker.BuildTarget = buildingType;
            worker.BuildSite = target;
            worker.BuildPlaced = true;
            worker.BuildTimer = creationTime[buildingType];
            worker.Path = path;
            worker.PathIndex = 0;
        }

        private void ProcessGather(SimUnit worker, int mineNbr, int baseNbr)
        {
            if (!Units.TryGetValue(mineNbr, out var mine)) return;
            if (!Units.TryGetValue(baseNbr, out var baseUnit)) return;

            // Path to mine
            var path = Map.FindPathToUnit(worker.GridPosition, UnitType.MINE, mine.GridPosition);
            if (path.Count == 0 && !IsAdjacentToUnit(worker.GridPosition, UnitType.MINE, mine.GridPosition))
                return;

            worker.CurrentAction = UnitAction.GATHER;
            worker.GatherMineNbr = mineNbr;
            worker.GatherBaseNbr = baseNbr;
            worker.GatherPhase = GatherPhase.TO_MINE;
            worker.Path = path;
            worker.PathIndex = 0;
            worker.MiningTimer = 0f;
        }

        private void ProcessTrain(SimUnit building, UnitType unitType)
        {
            // Re-validate (building may have received another command this tick)
            if (building.CurrentAction != UnitAction.IDLE) return;
            float cost = GameConstants.COST[unitType];
            if (Gold[building.OwnerAgentNbr] < cost) return;

            Gold[building.OwnerAgentNbr] -= (int)cost;

            building.CurrentAction = UnitAction.TRAIN;
            building.TrainTarget = unitType;
            building.TrainTimer = creationTime[unitType];
        }

        private void ProcessAttack(SimUnit attacker, int targetNbr)
        {
            if (!Units.ContainsKey(targetNbr)) return;

            // Path toward the target (use FindPathToUnit so we path to an adjacent cell,
            // which handles multi-cell buildings that are unwalkable)
            var target = Units[targetNbr];
            var path = Map.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);

            attacker.CurrentAction = UnitAction.ATTACK;
            attacker.AttackTargetNbr = targetNbr;
            attacker.Path = path;
            attacker.PathIndex = 0;
        }
    }
}
