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

                if (tickWorld == null) tickWorld = new SimTickWorld(this);
                CommandResult result;
                switch (cmd.Type)
                {
                    case CommandType.MOVE:
                        result = CommandProcessor.ProcessMove(unit, cmd.Target, tickWorld);
                        break;
                    case CommandType.BUILD:
                        result = CommandProcessor.ProcessBuild(unit, cmd.Target, cmd.UnitType, tickWorld);
                        break;
                    case CommandType.GATHER:
                        result = CommandProcessor.ProcessGather(unit, cmd.MineNbr, cmd.BaseNbr, tickWorld);
                        break;
                    case CommandType.TRAIN:
                        result = CommandProcessor.ProcessTrain(unit, cmd.UnitType, tickWorld);
                        break;
                    case CommandType.ATTACK:
                        result = CommandProcessor.ProcessAttack(unit, cmd.TargetUnitNbr, tickWorld);
                        break;
                    case CommandType.REPAIR:
                        result = CommandProcessor.ProcessRepair(unit, cmd.TargetUnitNbr, tickWorld);
                        break;
                    case CommandType.HEAL:
                        result = CommandProcessor.ProcessHeal(unit, cmd.TargetUnitNbr, tickWorld);
                        break;
                    default: result = CommandResult.SUCCESS; break;
                }
                if (result != CommandResult.SUCCESS)
                    FailedCommands[agentNbr].Add(new FailedCommand(cmd.UnitNbr, cmd.Type, result));
            }
        }
    }
}
