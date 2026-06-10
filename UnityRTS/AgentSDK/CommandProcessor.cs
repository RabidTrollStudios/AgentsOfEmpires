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
        /// <summary>
        /// Collect the current path destination of every friendly unit that already has
        /// a path, EXCLUDING the caller. Used to prevent two attackers from picking the
        /// same destination cell when both commands are issued in the same step (before
        /// either unit has physically arrived and marked the cell WALKABLE).
        /// </summary>
        internal static HashSet<Position> CollectClaimedDestinations(ISimUnit self, ISimWorld world)
        {
            var claimed = new HashSet<Position>();
            foreach (var other in world.AllUnits)
            {
                if (other.UnitNbr == self.UnitNbr) continue;
                if (other.OwnerAgentNbr != self.OwnerAgentNbr) continue;
                if (other.SimPath == null || other.SimPath.Count == 0) continue;
                // Only the final destination is "claimed" — intermediate cells can be
                // passed through by multiple units via the mid-path pass-through rule.
                claimed.Add(other.SimPath[other.SimPath.Count - 1]);
            }
            return claimed;
        }


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

            // Path BEFORE placing building (matches both engines).
            // Claim set excludes cells already chosen by other pawns' active paths.
            var claimedDests = CollectClaimedDestinations(pawn, world);
            var path = world.Grid.FindPathToUnit(
                pawn.GridPosition, buildingType, target, claimedDests);

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

            // Claim set prevents multiple pawns from picking the same adjacent-to-mine
            // cell when their commands are issued in the same step.
            var claimedDests = CollectClaimedDestinations(pawn, world);
            var path = world.Grid.FindPathToUnit(
                pawn.GridPosition, UnitType.MINE, mine.GridPosition, claimedDests);
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

            // Always pathfind to an OPEN cell within attack range of the target.
            // FindPathToAttackPosition searches the full attack-range disc (not just
            // the building's adjacency ring), so multiple attackers naturally spread
            // to different stand-cells instead of stacking on the same neighbor.
            // For mobile targets, the disc is small (1-2 cells) and behaves the same
            // as the old FindPathToUnit. For large buildings the disc is much wider,
            // which is what fixes melee stacking on bases/barracks/etc.
            //
            // claimedDests excludes cells that are ALREADY the final destination of
            // another friendly unit's active path. Without this, when 3 warriors
            // issue attacks on the same step, they all see the closest disc cell as
            // OPEN and all path to it — even though only the first to arrive can
            // actually stand there. With this filter, warriors 2 and 3 skip warrior
            // 1's reserved destination.
            var claimedDests = CollectClaimedDestinations(attacker, world);
            var path = world.Grid.FindPathToAttackPosition(
                attacker.GridPosition, attacker.UnitType,
                target.UnitType, target.GridPosition, attacker.UnitNbr,
                claimedDests);
            // Fallback if no in-range OPEN cell exists (e.g., target completely
            // surrounded by trees and other units): use the legacy ring-based path
            // so the attacker still tries to approach. Pass the same claim set.
            if (path.Count == 0)
                path = world.Grid.FindPathToUnit(
                    attacker.GridPosition, target.UnitType, target.GridPosition, claimedDests);
            attacker.SimPath = path;
            attacker.PathIndex = 0;
            attacker.PathProgress = 0f;
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

            // Claim set prevents multiple repairers from picking the same adjacent cell
            // when their commands land in the same step.
            var claimedDests = CollectClaimedDestinations(pawn, world);
            var path = world.Grid.FindPathToUnit(
                pawn.GridPosition, building.UnitType, building.GridPosition, claimedDests);
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

            // Always pathfind to the target — AdvanceHeal will stop movement when in range.
            // Claim set prevents multiple monks from picking the same adjacent cell to the
            // heal target when commands are issued in the same step.
            var claimedDests = CollectClaimedDestinations(monk, world);
            var path = world.Grid.FindPathToUnit(
                monk.GridPosition, target.UnitType, target.GridPosition, claimedDests);
            monk.SimPath = path;
            monk.PathIndex = 0;
            monk.PathProgress = 0f;
            return CommandResult.SUCCESS;
        }
    }
}
