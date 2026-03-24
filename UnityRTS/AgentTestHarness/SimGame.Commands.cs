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
                    case CommandType.Repair:
                        ProcessRepair(unit, cmd.TargetUnitNbr);
                        break;
                    case CommandType.Heal:
                        ProcessHeal(unit, cmd.TargetUnitNbr);
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

        private void ProcessBuild(SimUnit pawn, Position target, UnitType buildingType)
        {
            // Re-validate gold (may have been spent by earlier command this tick)
            float cost = GameConstants.COST[buildingType];
            if (Gold[pawn.OwnerAgentNbr] < cost) return;
            if (!Map.IsAreaBuildable(buildingType, target)) return;

            // Deduct gold at build start
            Gold[pawn.OwnerAgentNbr] -= (int)cost;

            // Place the building immediately (unbuilt)
            var building = PlaceUnit(pawn.OwnerAgentNbr, buildingType, target,
                GameConstants.HEALTH[buildingType], false);

            // Path pawn to a cell adjacent to the building
            var path = Map.FindPathToUnit(pawn.GridPosition, buildingType, target);

            pawn.CurrentAction = UnitAction.BUILD;
            pawn.BuildTarget = buildingType;
            pawn.BuildSite = target;
            pawn.BuildPlaced = true;
            pawn.BuildTimer = creationTime[buildingType];
            pawn.Path = path;
            pawn.PathIndex = 0;
        }

        private void ProcessGather(SimUnit pawn, int mineNbr, int baseNbr)
        {
            if (!Units.TryGetValue(mineNbr, out var mine)) return;
            if (!Units.TryGetValue(baseNbr, out var baseUnit)) return;

            // Path to mine
            var path = Map.FindPathToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition);
            if (path.Count == 0 && !IsAdjacentToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition))
                return;

            pawn.CurrentAction = UnitAction.GATHER;
            pawn.GatherMineNbr = mineNbr;
            pawn.GatherBaseNbr = baseNbr;
            pawn.GatherPhase = GatherPhase.TO_MINE;
            pawn.Path = path;
            pawn.PathIndex = 0;
            pawn.MiningTimer = 0f;
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

        private void ProcessRepair(SimUnit pawn, int buildingNbr)
        {
            if (!Units.TryGetValue(buildingNbr, out var building)) return;

            var path = Map.FindPathToUnit(pawn.GridPosition, building.UnitType, building.GridPosition);

            pawn.CurrentAction = UnitAction.REPAIR;
            pawn.RepairBuildingNbr = buildingNbr;
            pawn.Path = path;
            pawn.PathIndex = 0;
        }

        private void ProcessHeal(SimUnit monk, int targetNbr)
        {
            if (!Units.TryGetValue(targetNbr, out var target)) return;

            var path = Map.FindPathToUnit(monk.GridPosition, target.UnitType, target.GridPosition);

            monk.CurrentAction = UnitAction.HEAL;
            monk.HealTargetNbr = targetNbr;
            monk.Path = path;
            monk.PathIndex = 0;
        }
    }
}
