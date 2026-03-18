using System;
using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Decorator that wraps an IAgentActions and records every command issued,
    /// tagged with the current tick. Delegates all calls to the inner instance.
    ///
    /// Usage: inject between the agent and the real SimAgentActions in the tick loop
    /// by calling SimGame.EnableRecording() before running.
    /// </summary>
    public class CommandRecorder : IAgentActions
    {
        private readonly IAgentActions inner;
        private readonly int agentNbr;
        private readonly Func<int> getCurrentTick;

        public List<CommandRecord> Records { get; } = new List<CommandRecord>();

        public CommandRecorder(IAgentActions inner, int agentNbr, Func<int> getCurrentTick)
        {
            this.inner = inner;
            this.agentNbr = agentNbr;
            this.getCurrentTick = getCurrentTick;
        }

        public CommandResult Move(int unitNbr, Position target)
        {
            Records.Add(new CommandRecord
            {
                Tick = getCurrentTick(),
                AgentNbr = agentNbr,
                Type = CommandType.Move,
                UnitNbr = unitNbr,
                Target = target
            });
            return inner.Move(unitNbr, target);
        }

        public CommandResult Build(int unitNbr, Position target, UnitType unitType)
        {
            Records.Add(new CommandRecord
            {
                Tick = getCurrentTick(),
                AgentNbr = agentNbr,
                Type = CommandType.Build,
                UnitNbr = unitNbr,
                Target = target,
                BuildingType = unitType
            });
            return inner.Build(unitNbr, target, unitType);
        }

        public CommandResult Gather(int pawnNbr, int mineNbr, int baseNbr)
        {
            Records.Add(new CommandRecord
            {
                Tick = getCurrentTick(),
                AgentNbr = agentNbr,
                Type = CommandType.Gather,
                UnitNbr = pawnNbr,
                MineNbr = mineNbr,
                BaseNbr = baseNbr
            });
            return inner.Gather(pawnNbr, mineNbr, baseNbr);
        }

        public CommandResult Train(int buildingNbr, UnitType unitType)
        {
            Records.Add(new CommandRecord
            {
                Tick = getCurrentTick(),
                AgentNbr = agentNbr,
                Type = CommandType.Train,
                BuildingNbr = buildingNbr,
                TrainType = unitType
            });
            return inner.Train(buildingNbr, unitType);
        }

        public CommandResult Attack(int unitNbr, int targetNbr)
        {
            Records.Add(new CommandRecord
            {
                Tick = getCurrentTick(),
                AgentNbr = agentNbr,
                Type = CommandType.Attack,
                UnitNbr = unitNbr,
                TargetUnitNbr = targetNbr
            });
            return inner.Attack(unitNbr, targetNbr);
        }

        public CommandResult Repair(int pawnNbr, int buildingNbr)
        {
            Records.Add(new CommandRecord
            {
                Tick = getCurrentTick(),
                AgentNbr = agentNbr,
                Type = CommandType.Repair,
                UnitNbr = pawnNbr,
                RepairBuildingNbr = buildingNbr
            });
            return inner.Repair(pawnNbr, buildingNbr);
        }

        public void Log(string message)
        {
            Records.Add(new CommandRecord
            {
                Tick = getCurrentTick(),
                AgentNbr = agentNbr,
                Type = CommandType.Log,
                LogMessage = message
            });
            inner.Log(message);
        }
    }
}
