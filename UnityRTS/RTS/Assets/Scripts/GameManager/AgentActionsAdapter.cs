using System.Collections.Generic;
using AgentSDK;
using UnityEngine;

namespace GameManager
{
    /// <summary>
    /// Implements IAgentActions by resolving unit IDs to Unit objects
    /// and delegating to the existing Agent command methods (which handle validation).
    /// Enforces per-unit cooldowns on failed commands to prevent agents from spamming
    /// expensive operations (like A* pathfinding) every frame.
    /// </summary>
    public class AgentActionsAdapter : IAgentActions
    {
        private Agent agent;
        private UnitManager unitManager;

        /// <summary>
        /// Per-unit cooldown: maps unitNbr to the frame number when the cooldown expires.
        /// While on cooldown, commands for that unit return OnCooldown immediately.
        /// </summary>
        private readonly Dictionary<int, int> cooldownExpiry = new Dictionary<int, int>();

        /// <summary>
        /// Per-unit consecutive failure count for exponential backoff.
        /// Reset to 0 on any successful command for that unit.
        /// </summary>
        private readonly Dictionary<int, int> failureCount = new Dictionary<int, int>();

        /// <summary>
        /// Base cooldown in frames after a failed command. Doubles with each
        /// consecutive failure up to a maximum of MAX_COOLDOWN_FRAMES.
        /// At ~60 fps: 15 frames ≈ 0.25s, 120 frames ≈ 2s.
        /// </summary>
        private const int BASE_COOLDOWN_FRAMES = 15;
        private const int MAX_COOLDOWN_FRAMES = 120;

        public AgentActionsAdapter(Agent agent, UnitManager unitManager)
        {
            this.agent = agent;
            this.unitManager = unitManager;
        }

        /// <summary>
        /// Check if a unit is on cooldown. Returns true if the command should be blocked.
        /// </summary>
        private bool IsOnCooldown(int unitNbr)
        {
            if (cooldownExpiry.TryGetValue(unitNbr, out int expiry))
            {
                if (Time.frameCount < expiry)
                    return true;
                cooldownExpiry.Remove(unitNbr);
            }
            return false;
        }

        /// <summary>
        /// Called after a command fails. Sets an exponential backoff cooldown for the unit.
        /// </summary>
        private void ApplyCooldown(int unitNbr)
        {
            failureCount.TryGetValue(unitNbr, out int failures);
            failures++;
            failureCount[unitNbr] = failures;

            // Exponential backoff: 15, 30, 60, 120, 120, 120, ...
            int cooldown = BASE_COOLDOWN_FRAMES * (1 << System.Math.Min(failures - 1, 3));
            if (cooldown > MAX_COOLDOWN_FRAMES) cooldown = MAX_COOLDOWN_FRAMES;

            cooldownExpiry[unitNbr] = Time.frameCount + cooldown;
        }

        /// <summary>
        /// Called after a command succeeds. Resets the failure count for the unit.
        /// </summary>
        private void ResetCooldown(int unitNbr)
        {
            failureCount.Remove(unitNbr);
            cooldownExpiry.Remove(unitNbr);
        }

        /// <summary>
        /// Process a command result: apply cooldown on failure, reset on success.
        /// </summary>
        private CommandResult ProcessResult(int unitNbr, CommandResult result)
        {
            if (result == CommandResult.SUCCESS)
                ResetCooldown(unitNbr);
            else
                ApplyCooldown(unitNbr);
            return result;
        }

        public CommandResult Move(int unitNbr, Position target)
        {
            if (IsOnCooldown(unitNbr))
            {
                agent.CmdLog?.LogCommand("MOVE", $"unit#{unitNbr} -> ({target.X}, {target.Y})", "THROTTLED: on cooldown");
                return CommandResult.ON_COOLDOWN;
            }
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) return ProcessResult(unitNbr, CommandResult.UNIT_NOT_FOUND);
            return ProcessResult(unitNbr, agent.Move(unit, new Vector3Int(target.X, target.Y, 0)));
        }

        public CommandResult Build(int unitNbr, Position target, AgentSDK.UnitType unitType)
        {
            if (IsOnCooldown(unitNbr))
            {
                agent.CmdLog?.LogCommand("BUILD", $"unit#{unitNbr} -> {unitType} at ({target.X}, {target.Y})", "THROTTLED: on cooldown");
                return CommandResult.ON_COOLDOWN;
            }
            var unit = unitManager.GetUnit(unitNbr);
            if (unit == null) return ProcessResult(unitNbr, CommandResult.UNIT_NOT_FOUND);
            return ProcessResult(unitNbr, agent.Build(unit, new Vector3Int(target.X, target.Y, 0), unitType));
        }

        public CommandResult Gather(int pawnNbr, int mineNbr, int baseNbr)
        {
            if (IsOnCooldown(pawnNbr))
            {
                agent.CmdLog?.LogCommand("GATHER", $"pawn#{pawnNbr} -> mine#{mineNbr}, base#{baseNbr}", "THROTTLED: on cooldown");
                return CommandResult.ON_COOLDOWN;
            }
            var pawn = unitManager.GetUnit(pawnNbr);
            var mine = unitManager.GetUnit(mineNbr);
            var baseUnit = unitManager.GetUnit(baseNbr);
            if (pawn == null || mine == null || baseUnit == null)
                return ProcessResult(pawnNbr, pawn == null ? CommandResult.UNIT_NOT_FOUND : CommandResult.TARGET_NOT_FOUND);
            return ProcessResult(pawnNbr, agent.Gather(pawn, mine, baseUnit));
        }

        public CommandResult Train(int buildingNbr, AgentSDK.UnitType unitType)
        {
            // Training cooldowns use the building's unit number
            if (IsOnCooldown(buildingNbr))
            {
                agent.CmdLog?.LogCommand("TRAIN", $"building#{buildingNbr} -> {unitType}", "THROTTLED: on cooldown");
                return CommandResult.ON_COOLDOWN;
            }
            var building = unitManager.GetUnit(buildingNbr);
            if (building == null) return ProcessResult(buildingNbr, CommandResult.UNIT_NOT_FOUND);
            return ProcessResult(buildingNbr, agent.Train(building, unitType));
        }

        public CommandResult Attack(int unitNbr, int targetNbr)
        {
            if (IsOnCooldown(unitNbr))
            {
                agent.CmdLog?.LogCommand("ATTACK", $"unit#{unitNbr} -> target#{targetNbr}", "THROTTLED: on cooldown");
                return CommandResult.ON_COOLDOWN;
            }
            var unit = unitManager.GetUnit(unitNbr);
            var target = unitManager.GetUnit(targetNbr);
            if (unit == null || target == null)
                return ProcessResult(unitNbr, unit == null ? CommandResult.UNIT_NOT_FOUND : CommandResult.TARGET_NOT_FOUND);
            return ProcessResult(unitNbr, agent.Attack(unit, target));
        }

        public CommandResult Repair(int pawnNbr, int buildingNbr)
        {
            if (IsOnCooldown(pawnNbr))
            {
                agent.CmdLog?.LogCommand("REPAIR", $"pawn#{pawnNbr} -> building#{buildingNbr}", "THROTTLED: on cooldown");
                return CommandResult.ON_COOLDOWN;
            }
            var pawn = unitManager.GetUnit(pawnNbr);
            var building = unitManager.GetUnit(buildingNbr);
            if (pawn == null || building == null)
                return ProcessResult(pawnNbr, pawn == null ? CommandResult.UNIT_NOT_FOUND : CommandResult.TARGET_NOT_FOUND);
            return ProcessResult(pawnNbr, agent.Repair(pawn, building));
        }

        public void Log(string message)
        {
            agent.Log(message);
        }
    }
}
