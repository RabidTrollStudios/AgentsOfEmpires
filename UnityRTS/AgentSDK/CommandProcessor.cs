using System.Collections.Generic;

namespace AgentSDK
{
    /// <summary>
    /// Shared command processing for both Unity and SimGame.
    /// Validates, computes paths, deducts gold, and initializes unit state.
    /// Both engines call these methods to guarantee identical command handling.
    /// </summary>
    public static class CommandProcessor
    {
        public static CommandResult ProcessMove(ISimUnit unit, Position target, ISimWorld world)
        {
            if (!unit.CanMove) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            // Allow MOVE to interrupt BUILD/REPAIR
            if (unit.CurrentAction == UnitAction.BUILD || unit.CurrentAction == UnitAction.REPAIR)
            {
                StepEngine.SetIdle(unit);
            }

            // Try avoidUnits first, fall back to normal
            var path = world.Grid.FindPath(unit.GridPosition, target, avoidUnits: true);
            if (path.Count == 0)
                path = world.FindPath(unit.GridPosition, target);
            if (path.Count == 0) return CommandResult.NO_PATH_FOUND;

            unit.CurrentAction = UnitAction.MOVE;
            unit.SimPath = path;
            unit.PathIndex = 0;
            unit.PathProgress = 0f;
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessBuild(ISimUnit pawn, Position target, UnitType buildingType, ISimWorld world)
        {
            // Validation
            bool areaBuildable = world.Grid.IsAreaBuildable(buildingType, target, pawn.GridPosition);
            var ownedBuiltTypes = new List<UnitType>();
            foreach (var u in world.AllUnits)
            {
                if (u.OwnerAgentNbr == pawn.OwnerAgentNbr && u.IsBuilt)
                    ownedBuiltTypes.Add(u.UnitType);
            }
            var result = CommandValidator.ValidateBuild(
                pawn.UnitType, buildingType,
                world.GetGold(pawn.OwnerAgentNbr), areaBuildable, ownedBuiltTypes);
            if (result != CommandResult.SUCCESS) return result;

            // Deduct gold
            int cost = (int)GameConstants.COST[buildingType];
            world.AddGold(pawn.OwnerAgentNbr, -cost);

            // Path BEFORE placing building (matches both engines)
            var path = world.FindPathToUnit(pawn.GridPosition, buildingType, target);

            if (path.Count == 0 && !world.IsNeighborOfUnit(pawn.GridPosition, buildingType, target))
            {
                // Refund gold
                world.AddGold(pawn.OwnerAgentNbr, cost);
                return CommandResult.NO_PATH_FOUND;
            }

            // Place building
            var building = world.SpawnUnit(pawn.OwnerAgentNbr, buildingType, target,
                GameConstants.HEALTH[buildingType], false);

            // Initialize pawn state
            pawn.CurrentAction = UnitAction.BUILD;
            pawn.BuildTarget = buildingType;
            pawn.BuildSite = target;
            pawn.BuildTargetNbr = building.UnitNbr;
            pawn.BuildTimer = 0f;
            pawn.SimPath = path;
            pawn.PathIndex = 0;
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessGather(ISimUnit pawn, int mineNbr, int baseNbr, ISimWorld world)
        {
            if (pawn.CurrentAction == UnitAction.BUILD || pawn.CurrentAction == UnitAction.REPAIR)
                return CommandResult.UNIT_BUSY;
            if (!pawn.CanGather) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            var mine = world.GetUnit(mineNbr);
            var baseUnit = world.GetUnit(baseNbr);
            if (mine == null || baseUnit == null) return CommandResult.INVALID_TARGET;

            var path = world.FindPathToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition);
            // Accept if path found or already adjacent
            if (path.Count == 0 && !world.IsNeighborOfUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition))
                return CommandResult.NO_PATH_FOUND;

            pawn.CurrentAction = UnitAction.GATHER;
            pawn.GatherMineNbr = mineNbr;
            pawn.GatherBaseNbr = baseNbr;
            pawn.GatherPhase = GatherPhase.TO_MINE;
            pawn.MiningTimer = 0f;
            pawn.GoldCarried = 0;
            pawn.SimPath = path;
            pawn.PathIndex = 0;
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessTrain(ISimUnit building, UnitType unitType, ISimWorld world)
        {
            var result = CommandValidator.ValidateTrain(
                building.UnitType, unitType,
                building.IsBuilt, building.CurrentAction,
                world.GetGold(building.OwnerAgentNbr));
            if (result != CommandResult.SUCCESS) return result;

            world.AddGold(building.OwnerAgentNbr, -(int)GameConstants.COST[unitType]);

            building.CurrentAction = UnitAction.TRAIN;
            building.TrainTarget = unitType;
            building.TrainTimer = 0f;
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessAttack(ISimUnit attacker, int targetNbr, ISimWorld world)
        {
            if (attacker.CurrentAction == UnitAction.BUILD || attacker.CurrentAction == UnitAction.REPAIR)
                return CommandResult.UNIT_BUSY;
            if (!attacker.CanAttack) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            var target = world.GetUnit(targetNbr);
            if (target == null || target.Health <= 0) return CommandResult.INVALID_TARGET;

            attacker.CurrentAction = UnitAction.ATTACK;
            attacker.AttackTargetNbr = targetNbr;

            // Check if already in range — skip pathfinding
            if (TaskEngine.IsInAttackRange(attacker.UnitType, attacker.CenterPosition,
                    target.UnitType, target.CenterPosition))
            {
                attacker.SimPath = null;
                attacker.PathIndex = 0;
                attacker.PathProgress = 0f;
            }
            else
            {
                var path = world.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);
                attacker.SimPath = path;
                attacker.PathIndex = 0;
            }
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessRepair(ISimUnit pawn, int buildingNbr, ISimWorld world)
        {
            // Allow REPAIR to interrupt BUILD
            if (pawn.CurrentAction == UnitAction.BUILD)
                StepEngine.SetIdle(pawn);

            if (!pawn.CanBuild) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            var building = world.GetUnit(buildingNbr);
            if (building == null || building.Health <= 0) return CommandResult.INVALID_TARGET;

            var path = world.FindPathToUnit(pawn.GridPosition, building.UnitType, building.GridPosition);
            if (path.Count == 0 && !world.IsNeighborOfUnit(pawn.GridPosition, building.UnitType, building.GridPosition))
                return CommandResult.NO_PATH_FOUND;

            pawn.CurrentAction = UnitAction.REPAIR;
            pawn.RepairBuildingNbr = buildingNbr;
            pawn.SimPath = path;
            pawn.PathIndex = 0;
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessHeal(ISimUnit monk, int targetNbr, ISimWorld world)
        {
            if (monk.CurrentAction == UnitAction.BUILD || monk.CurrentAction == UnitAction.REPAIR)
                return CommandResult.UNIT_BUSY;
            if (!monk.CanHeal) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (monk.Mana < GameConstants.MANA_COST) return CommandResult.INSUFFICIENT_MANA;

            var target = world.GetUnit(targetNbr);
            if (target == null || target.Health <= 0) return CommandResult.INVALID_TARGET;

            monk.CurrentAction = UnitAction.HEAL;
            monk.HealTargetNbr = targetNbr;

            // Check if already in range — skip pathfinding
            if (TaskEngine.IsInHealRange(monk.UnitType, monk.CenterPosition, target.CenterPosition))
            {
                monk.SimPath = null;
                monk.PathIndex = 0;
                monk.PathProgress = 0f;
            }
            else
            {
                var path = world.FindPathToUnit(monk.GridPosition, target.UnitType, target.GridPosition);
                monk.SimPath = path;
                monk.PathIndex = 0;
            }
            return CommandResult.SUCCESS;
        }
    }
}
