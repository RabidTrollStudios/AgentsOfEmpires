using System.Collections.Generic;
using System.Linq;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Command processing: validates queued agent commands and initiates unit actions.
    /// </summary>
    public partial class SimGame
    {
        /// <summary>
        /// Merge both agents' pending commands and process in deterministic order:
        /// (AgentNbr, CommandType, UnitNbr). Matches Unity's DeferredCommandQueue sort.
        /// </summary>
        private void ProcessCommandsSorted()
        {
            FailedCommands[0].Clear();
            FailedCommands[1].Clear();

            var allCommands = new List<(int agentNbr, SimCommand cmd)>();
            for (int a = 0; a < 2; a++)
            {
                foreach (var cmd in actions[a].PendingCommands)
                    allCommands.Add((a, cmd));
            }

            allCommands.Sort((a, b) =>
            {
                int cmp = a.agentNbr.CompareTo(b.agentNbr);
                if (cmp != 0) return cmp;
                cmp = ((int)a.cmd.Type).CompareTo((int)b.cmd.Type);
                if (cmp != 0) return cmp;
                return a.cmd.UnitNbr.CompareTo(b.cmd.UnitNbr);
            });

            // Track which units have already received a command this tick.
            // Only the first command per unit is processed — later ones are dropped.
            // This prevents a GATHER from overriding a BUILD issued in the same tick.
            var processedUnits = new HashSet<int>();

            foreach (var (agentNbr, cmd) in allCommands)
            {
                if (!Units.TryGetValue(cmd.UnitNbr, out var unit))
                    continue;
                if (!processedUnits.Add(cmd.UnitNbr))
                    continue; // skip — this unit already has a command this tick

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
            // Dispatch-time validation via shared CommandValidator (matches Unity's EventDispatcher)
            bool areaBuildable = Map.IsAreaBuildable(buildingType, target, pawn.GridPosition);
            var ownedBuiltTypes = Units.Values
                .Where(u => u.OwnerAgentNbr == pawn.OwnerAgentNbr && u.IsBuilt)
                .Select(u => u.UnitType);
            var result = CommandValidator.ValidateBuild(
                pawn.UnitType, buildingType,
                Gold[pawn.OwnerAgentNbr], areaBuildable, ownedBuiltTypes);
            if (result != CommandResult.SUCCESS)
            {
                FailedCommands[pawn.OwnerAgentNbr].Add(
                    new FailedCommand(pawn.UnitNbr, CommandType.Build, result));
                return;
            }

            // Deduct gold at build start
            int cost = (int)GameConstants.COST[buildingType];
            Gold[pawn.OwnerAgentNbr] -= cost;

            // Path pawn BEFORE placing building (matches Unity's StartBuilding order).
            // This ensures the pathfinder sees the same grid state in both engines.
            var path = Map.FindPathToUnit(pawn.GridPosition, buildingType, target);

            if (path.Count == 0 && !IsAdjacentToUnit(pawn.GridPosition, buildingType, target))
            {
                Gold[pawn.OwnerAgentNbr] += cost;
                FailedCommands[pawn.OwnerAgentNbr].Add(
                    new FailedCommand(pawn.UnitNbr, CommandType.Build, CommandResult.NO_PATH_FOUND));
                return;
            }

            // Place the building immediately (unbuilt) — after pathfinding
            var building = PlaceUnit(pawn.OwnerAgentNbr, buildingType, target,
                GameConstants.HEALTH[buildingType], false);

            pawn.CurrentAction = UnitAction.BUILD;
            pawn.BuildTarget = buildingType;
            pawn.BuildSite = target;
            pawn.BuildPlaced = true;
            pawn.BuildTargetNbr = building.UnitNbr;
            pawn.BuildTimer = 0f; // count up (matches Unity's BuildProgress)
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
            // Dispatch-time validation via shared CommandValidator (matches Unity's EventDispatcher)
            var result = CommandValidator.ValidateTrain(
                building.UnitType, unitType,
                building.IsBuilt, building.CurrentAction,
                Gold[building.OwnerAgentNbr]);
            if (result != CommandResult.SUCCESS)
            {
                FailedCommands[building.OwnerAgentNbr].Add(
                    new FailedCommand(building.UnitNbr, CommandType.Train, result));
                return;
            }

            Gold[building.OwnerAgentNbr] -= (int)GameConstants.COST[unitType];

            building.CurrentAction = UnitAction.TRAIN;
            building.TrainTarget = unitType;
            building.TrainTimer = 0f; // count up (matches Unity's taskTime)
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
