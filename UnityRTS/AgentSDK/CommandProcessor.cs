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
        public static CommandResult ProcessMove(ITickUnit unit, Position target, ITickWorld world)
        {
            if (!unit.CanMove) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            // Allow MOVE to interrupt BUILD/REPAIR
            if (unit.CurrentAction == UnitAction.BUILD || unit.CurrentAction == UnitAction.REPAIR)
            {
                TickEngine.SetIdle(unit);
            }

            // Try avoidUnits first, fall back to normal
            var path = world.Grid.FindPath(unit.GridPosition, target, avoidUnits: true);
            if (path.Count == 0)
                path = world.FindPath(unit.GridPosition, target);
            if (path.Count == 0) return CommandResult.NO_PATH_FOUND;

            unit.CurrentAction = UnitAction.MOVE;
            unit.TickPath = path;
            unit.PathIndex = 0;
            unit.PathProgress = 0f;
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessBuild(ITickUnit pawn, Position target, UnitType buildingType, ITickWorld world)
        {
            if (!pawn.CanBuild) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            // ── RESUME PATH ──────────────────────────────────────────────────
            // If a same-owner unbuilt building of this type already sits at the
            // target, attach the pawn to finish it instead of placing a new one.
            // Progress lives on the building (BuildProgress), so it survived the
            // previous builder's death/abandonment and any pawn can pick it up.
            // No gold is charged and no unit is spawned — those happened when the
            // building was first placed.
            var existing = FindUnbuiltBuildingAt(world, target, buildingType, pawn.OwnerAgentNbr);
            if (existing != null && pawn.CurrentAction != UnitAction.BUILD)
            {
                var resumePath = world.FindPathToUnit(pawn.GridPosition, buildingType, target);
                if (resumePath.Count == 0 && !world.IsNeighborOfUnit(pawn.GridPosition, buildingType, target))
                    return CommandResult.NO_PATH_FOUND;

                pawn.CurrentAction = UnitAction.BUILD;
                pawn.BuildTarget = buildingType;
                pawn.BuildSite = target;
                pawn.BuildTargetNbr = existing.UnitNbr;
                pawn.BuildTimer = 0f;
                pawn.TickPath = resumePath;
                pawn.PathIndex = 0;
                return CommandResult.SUCCESS;
            }

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
            pawn.TickPath = path;
            pawn.PathIndex = 0;
            return CommandResult.SUCCESS;
        }

        /// <summary>
        /// Find an unbuilt building of the given type anchored at <paramref name="position"/>
        /// owned by <paramref name="ownerAgentNbr"/>. Returns null if none — used to resume
        /// construction of a building whose original builder died or moved away.
        /// </summary>
        private static ITickUnit FindUnbuiltBuildingAt(
            ITickWorld world, Position position, UnitType buildingType, int ownerAgentNbr)
        {
            foreach (var u in world.AllUnits)
            {
                if (!u.IsBuilt
                    && u.UnitType == buildingType
                    && u.OwnerAgentNbr == ownerAgentNbr
                    && u.GridPosition == position)
                    return u;
            }
            return null;
        }

        public static CommandResult ProcessGather(ITickUnit pawn, int mineNbr, int baseNbr, ITickWorld world)
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
            pawn.TickPath = path;
            pawn.PathIndex = 0;
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessTrain(ITickUnit building, UnitType unitType, ITickWorld world)
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

        public static CommandResult ProcessAttack(ITickUnit attacker, int targetNbr, ITickWorld world)
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
                attacker.TickPath = null;
                attacker.PathIndex = 0;
                attacker.PathProgress = 0f;
            }
            else
            {
                var path = world.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);
                attacker.TickPath = path;
                attacker.PathIndex = 0;
            }
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessRepair(ITickUnit pawn, int buildingNbr, ITickWorld world)
        {
            // Allow REPAIR to interrupt BUILD
            if (pawn.CurrentAction == UnitAction.BUILD)
                TickEngine.SetIdle(pawn);

            if (!pawn.CanBuild) return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            var building = world.GetUnit(buildingNbr);
            if (building == null || building.Health <= 0) return CommandResult.INVALID_TARGET;

            var path = world.FindPathToUnit(pawn.GridPosition, building.UnitType, building.GridPosition);
            if (path.Count == 0 && !world.IsNeighborOfUnit(pawn.GridPosition, building.UnitType, building.GridPosition))
                return CommandResult.NO_PATH_FOUND;

            pawn.CurrentAction = UnitAction.REPAIR;
            pawn.RepairBuildingNbr = buildingNbr;
            pawn.TickPath = path;
            pawn.PathIndex = 0;
            return CommandResult.SUCCESS;
        }

        public static CommandResult ProcessHeal(ITickUnit monk, int targetNbr, ITickWorld world)
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
                monk.TickPath = null;
                monk.PathIndex = 0;
                monk.PathProgress = 0f;
            }
            else
            {
                var path = world.FindPathToUnit(monk.GridPosition, target.UnitType, target.GridPosition);
                monk.TickPath = path;
                monk.PathIndex = 0;
            }
            return CommandResult.SUCCESS;
        }
    }
}
