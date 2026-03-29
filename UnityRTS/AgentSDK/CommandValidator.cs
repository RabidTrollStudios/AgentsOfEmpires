using System.Collections.Generic;
using System.Linq;

namespace AgentSDK
{
    /// <summary>
    /// Shared command validation rules. Both the Unity game engine and SimGame
    /// call these to ensure identical acceptance/rejection of agent commands.
    /// </summary>
    public static class CommandValidator
    {
        /// <summary>
        /// Validate a Move command.
        /// </summary>
        public static CommandResult ValidateMove(
            UnitType unitType, bool isPositionValid)
        {
            if (!GameConstants.CAN_MOVE[unitType])
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (!isPositionValid)
                return CommandResult.INVALID_POSITION;
            return CommandResult.SUCCESS;
        }

        /// <summary>
        /// Validate a Build command.
        /// </summary>
        public static CommandResult ValidateBuild(
            UnitType builderType, UnitType buildingType,
            int agentGold, bool areaBuildable,
            IEnumerable<UnitType> ownedBuiltTypes)
        {
            if (!GameConstants.CAN_BUILD[builderType])
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (!GameConstants.BUILDS[builderType].Contains(buildingType))
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (!areaBuildable)
                return CommandResult.AREA_NOT_BUILDABLE;

            float cost = GameConstants.COST[buildingType];
            if (agentGold < cost)
                return CommandResult.INSUFFICIENT_GOLD;

            // Check dependencies
            foreach (UnitType dep in GameConstants.DEPENDENCY[buildingType])
            {
                if (!ownedBuiltTypes.Contains(dep))
                    return CommandResult.MISSING_DEPENDENCY;
            }

            return CommandResult.SUCCESS;
        }

        /// <summary>
        /// Validate a Gather command.
        /// </summary>
        public static CommandResult ValidateGather(
            UnitType pawnType,
            UnitType mineType, UnitType baseType,
            int baseOwner, int agentNbr)
        {
            if (!GameConstants.CAN_GATHER[pawnType])
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (mineType != UnitType.MINE)
                return CommandResult.INVALID_TARGET;
            if (baseType != UnitType.BASE)
                return CommandResult.INVALID_TARGET;
            if (baseOwner != agentNbr)
                return CommandResult.INVALID_TARGET;
            return CommandResult.SUCCESS;
        }

        /// <summary>
        /// Validate a Train command.
        /// </summary>
        public static CommandResult ValidateTrain(
            UnitType buildingType, UnitType trainType,
            bool isBuilt, UnitAction currentAction,
            int agentGold)
        {
            if (!GameConstants.CAN_TRAIN[buildingType])
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (!isBuilt)
                return CommandResult.BUILDING_NOT_FINISHED;
            if (currentAction != UnitAction.IDLE)
                return CommandResult.UNIT_BUSY;
            if (!GameConstants.TRAINS[buildingType].Contains(trainType))
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;

            float cost = GameConstants.COST[trainType];
            if (agentGold < cost)
                return CommandResult.INSUFFICIENT_GOLD;

            return CommandResult.SUCCESS;
        }

        /// <summary>
        /// Validate an Attack command.
        /// </summary>
        public static CommandResult ValidateAttack(
            UnitType attackerType,
            int targetOwner, int agentNbr,
            UnitType targetType)
        {
            if (!GameConstants.CAN_ATTACK[attackerType])
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (targetOwner == agentNbr)
                return CommandResult.FRIENDLY_FIRE;
            if (targetType == UnitType.MINE)
                return CommandResult.INVALID_TARGET;
            return CommandResult.SUCCESS;
        }

        /// <summary>
        /// Validate a Repair command.
        /// </summary>
        public static CommandResult ValidateRepair(
            UnitType pawnType,
            UnitType buildingType, bool buildingIsBuilt,
            int buildingOwner, int agentNbr)
        {
            if (!GameConstants.CAN_BUILD[pawnType])
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (GameConstants.CAN_MOVE[buildingType])
                return CommandResult.INVALID_TARGET;
            if (buildingType == UnitType.MINE)
                return CommandResult.INVALID_TARGET;
            if (!buildingIsBuilt)
                return CommandResult.BUILDING_NOT_FINISHED;
            if (buildingOwner != agentNbr)
                return CommandResult.FRIENDLY_FIRE;
            return CommandResult.SUCCESS;
        }

        /// <summary>
        /// Validate a Heal command.
        /// </summary>
        public static CommandResult ValidateHeal(
            UnitType healerType, float healerMana,
            UnitType targetType, float targetHealth,
            int targetOwner, int agentNbr)
        {
            if (!GameConstants.CAN_HEAL[healerType])
                return CommandResult.UNIT_CANNOT_PERFORM_ACTION;
            if (healerMana < GameConstants.MANA_COST)
                return CommandResult.INSUFFICIENT_MANA;
            if (targetOwner != agentNbr)
                return CommandResult.INVALID_TARGET;
            if (!GameConstants.CAN_MOVE[targetType])
                return CommandResult.INVALID_TARGET;

            float targetMaxHealth = GameConstants.HEALTH[targetType];
            if (targetMaxHealth > 0 && targetHealth > targetMaxHealth - GameConstants.HEAL_AMOUNT)
                return CommandResult.INVALID_TARGET;

            return CommandResult.SUCCESS;
        }
    }
}
