using System.Collections.Generic;
using AgentSDK;

namespace AgentTestHarness
{
    /// <summary>
    /// Replays a pre-recorded sequence of commands. Implements IPlanningAgent
    /// so it can be plugged into SimGame as a replacement agent for deterministic replay.
    ///
    /// Commands are matched by tick number. The player tracks ticks internally
    /// by counting Update() calls (since IGameState doesn't expose tick number).
    /// </summary>
    public class CommandPlayer : IPlanningAgent
    {
        private readonly List<CommandRecord> records;
        private int currentTick;
        private int nextIndex;

        public CommandPlayer(List<CommandRecord> records)
        {
            this.records = records;
        }

        public void InitializeMatch()
        {
            currentTick = 0;
            nextIndex = 0;
        }

        public void InitializeRound(IGameState state) { }

        public void Learn(IGameState state) { }

        public void Update(IGameState state, IAgentActions actions)
        {
            // SimGame.Tick() increments CurrentTick before calling Update,
            // so the first Update sees tick 1. Match that by pre-incrementing.
            currentTick++;

            while (nextIndex < records.Count && records[nextIndex].Tick == currentTick)
            {
                Replay(records[nextIndex], actions);
                nextIndex++;
            }
        }

        private static void Replay(CommandRecord r, IAgentActions actions)
        {
            switch (r.Type)
            {
                case CommandType.Move:
                    actions.Move(r.UnitNbr, r.Target);
                    break;
                case CommandType.Build:
                    actions.Build(r.UnitNbr, r.Target, r.BuildingType);
                    break;
                case CommandType.Gather:
                    actions.Gather(r.UnitNbr, r.MineNbr, r.BaseNbr);
                    break;
                case CommandType.Train:
                    actions.Train(r.BuildingNbr, r.TrainType);
                    break;
                case CommandType.Attack:
                    actions.Attack(r.UnitNbr, r.TargetUnitNbr);
                    break;
                case CommandType.Repair:
                    actions.Repair(r.UnitNbr, r.RepairBuildingNbr);
                    break;
                case CommandType.Log:
                    actions.Log(r.LogMessage);
                    break;
            }
        }
    }
}
