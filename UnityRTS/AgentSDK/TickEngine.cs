using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentSDK
{
    /// <summary>
    /// Shared game tick engine. Processes all unit tasks and movement
    /// in deterministic order. Both Unity and SimGame call this to
    /// guarantee identical game logic.
    /// </summary>
    public static class TickEngine
    {
        /// <summary>
        /// Advance all units one tick: task logic + movement per unit,
        /// in deterministic UnitNbr order.
        /// </summary>
        public static void AdvanceAllUnits(ITickWorld world, ITickCallbacks callbacks)
        {
            // Phase 2: Task logic + movement per unit in deterministic order
            var units = world.AllUnits.ToList();
            units.Sort((a, b) => a.UnitNbr.CompareTo(b.UnitNbr));

            foreach (var unit in units)
            {
                // Task logic
                switch (unit.CurrentAction)
                {
                    case UnitAction.MOVE:
                        AdvanceMove(unit);
                        break;
                    case UnitAction.TRAIN:
                        AdvanceTrain(unit, world, callbacks);
                        break;
                    case UnitAction.BUILD:
                        AdvanceBuild(unit, world, callbacks);
                        break;
                    case UnitAction.GATHER:
                        AdvanceGather(unit, world, callbacks);
                        break;
                    case UnitAction.ATTACK:
                        AdvanceAttack(unit, world, callbacks);
                        break;
                    case UnitAction.REPAIR:
                        AdvanceRepair(unit, world, callbacks);
                        break;
                    case UnitAction.HEAL:
                        AdvanceHeal(unit, world, callbacks);
                        break;
                }

                // Movement
                if (unit.TickPath != null && unit.PathIndex < unit.TickPath.Count)
                    MoveUnitOneStep(unit, world, callbacks);
            }

            // Phase 3: Mana regen for all units
            foreach (var unit in units)
            {
                float maxMana = GameConstants.MAX_MANA[unit.UnitType];
                float manaRegen = world.Constants.ManaRegen;
                unit.Mana = TaskEngine.RegenMana(unit.Mana, maxMana, manaRegen, world.TickDuration);
            }

            // Phase 4: Remove dead units
            var deadUnits = new List<ITickUnit>();
            foreach (var unit in units)
            {
                if (unit.Health <= 0)
                    deadUnits.Add(unit);
            }
            foreach (var dead in deadUnits)
            {
                callbacks.OnUnitKilled(dead);
                world.RemoveUnit(dead);
            }
        }

        /// <summary>Transition a unit to IDLE, clearing movement state.</summary>
        private static void GoIdle(ITickUnit unit)
        {
            unit.CurrentAction = UnitAction.IDLE;
            unit.TickPath = null;
            unit.PathIndex = 0;
            unit.MoveAccumulator = 0f;
            unit.AttackTargetNbr = -1;
            unit.HealTargetNbr = -1;
            unit.BuildTargetNbr = -1;
            unit.RepairBuildingNbr = -1;
        }

        #region Movement

        private static void AdvanceMove(ITickUnit unit)
        {
            if (unit.TickPath == null || unit.PathIndex >= unit.TickPath.Count)
            {
                GoIdle(unit);
            }
        }

        private static void MoveUnitOneStep(ITickUnit unit, ITickWorld world, ITickCallbacks callbacks)
        {
            if (unit.TickPath == null || unit.PathIndex >= unit.TickPath.Count) return;

            float speed = world.Constants.MovingSpeed[unit.UnitType];
            unit.MoveAccumulator += speed;

            while (unit.TickPath != null && unit.PathIndex < unit.TickPath.Count)
            {
                Position nextPos = unit.TickPath[unit.PathIndex];

                var result = TaskEngine.TryMoveToCell(
                    unit.GridPosition, nextPos, unit.MoveAccumulator,
                    world.Grid, out float distCost);

                switch (result)
                {
                    case TaskEngine.MoveResult.Moved:
                        unit.MoveAccumulator -= distCost;

                        Position oldPos = unit.GridPosition;
                        if (unit.CanMove)
                        {
                            world.Grid.SetCellOccupied(nextPos, true);
                            world.Grid.SetCellOccupied(oldPos, false);
                        }

                        unit.GridPosition = nextPos;
                        unit.PathIndex++;

                        callbacks.OnUnitMoved(unit, oldPos, nextPos);

                        if (unit.PathIndex >= unit.TickPath.Count)
                        {
                            if (unit.CurrentAction == UnitAction.MOVE)
                            {
                                GoIdle(unit);
                            }
                        }
                        continue;

                    case TaskEngine.MoveResult.BlockedByUnit:
                        if (unit.PathIndex == unit.TickPath.Count - 1)
                        {
                            if (unit.CurrentAction == UnitAction.MOVE)
                                GoIdle(unit);
                            return;
                        }
                        goto case TaskEngine.MoveResult.Moved;

                    case TaskEngine.MoveResult.BlockedByTerrain:
                        unit.MoveAccumulator -= distCost;
                        Position dest = unit.TickPath[unit.TickPath.Count - 1];
                        var repath = world.FindPath(unit.GridPosition, dest);
                        if (repath.Count > 0)
                        {
                            unit.TickPath = repath;
                            unit.PathIndex = 0;
                            callbacks.OnUnitRepath(unit, repath);
                        }
                        else if (unit.CurrentAction == UnitAction.MOVE)
                        {
                            GoIdle(unit);
                        }
                        else
                        {
                            unit.TickPath = null;
                            unit.PathIndex = 0;
                        }
                        return;

                    default: // InsufficientMovement
                        return;
                }
            }
        }

        #endregion

        #region Training

        private static void AdvanceTrain(ITickUnit building, ITickWorld world, ITickCallbacks callbacks)
        {
            building.TrainTimer += world.TickDuration;
            if (building.TrainTimer < world.Constants.CreationTime[building.TrainTarget])
                return;

            var spawnPositions = world.GetBuildablePositionsNearUnit(building.UnitType, building.GridPosition);
            if (spawnPositions.Count == 0)
            {
                building.TrainTimer = world.Constants.CreationTime[building.TrainTarget] - world.TickDuration;
                return;
            }

            Position spawnPos = spawnPositions[0];
            float health = GameConstants.HEALTH[building.TrainTarget];
            var spawned = world.SpawnUnit(building.OwnerAgentNbr, building.TrainTarget, spawnPos, health, true);
            GoIdle(building);

            callbacks.OnTrainingComplete(building, spawned);
        }

        #endregion

        #region Building

        private static void AdvanceBuild(ITickUnit pawn, ITickWorld world, ITickCallbacks callbacks)
        {
            if (pawn.TickPath != null && pawn.PathIndex < pawn.TickPath.Count)
                return;

            pawn.BuildTimer += world.TickDuration;
            float creationTime = world.Constants.CreationTime[pawn.BuildTarget];

            callbacks.OnBuildProgress(pawn, world.GetUnit(pawn.BuildTargetNbr), pawn.BuildTimer, creationTime);

            if (pawn.BuildTimer < creationTime)
                return;

            // Build complete
            var building = world.GetUnit(pawn.BuildTargetNbr);
            if (building != null)
            {
                building.IsBuilt = true;
                callbacks.OnBuildComplete(pawn, building);
            }
            GoIdle(pawn);
        }

        #endregion

        #region Gathering

        private static void AdvanceGather(ITickUnit pawn, ITickWorld world, ITickCallbacks callbacks)
        {
            switch (pawn.GatherPhase)
            {
                case GatherPhase.TO_MINE:
                    AdvanceGatherToMine(pawn, world, callbacks);
                    break;
                case GatherPhase.MINING:
                    AdvanceGatherMining(pawn, world, callbacks);
                    break;
                case GatherPhase.TO_BASE:
                    AdvanceGatherToBase(pawn, world, callbacks);
                    break;
            }
        }

        private static void AdvanceGatherToMine(ITickUnit pawn, ITickWorld world, ITickCallbacks callbacks)
        {
            var mine = world.GetUnit(pawn.GatherMineNbr);
            if (mine == null || mine.Health <= 0)
            {
                GoIdle(pawn);
                return;
            }

            if (pawn.TickPath != null && pawn.PathIndex < pawn.TickPath.Count)
                return;

            if (world.IsNeighborOfUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition))
            {
                var oldPhase = pawn.GatherPhase;
                pawn.GatherPhase = GatherPhase.MINING;
                pawn.MiningTimer = 0f;
                pawn.GoldCarried = 0;
                callbacks.OnGatherPhaseChanged(pawn, oldPhase, GatherPhase.MINING);
            }
            else
            {
                var repath = world.FindPathToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition);
                if (repath.Count > 0)
                {
                    pawn.TickPath = repath;
                    pawn.PathIndex = 0;
                }
                else
                {
                    GoIdle(pawn);
                }
            }
        }

        private static void AdvanceGatherMining(ITickUnit pawn, ITickWorld world, ITickCallbacks callbacks)
        {
            var mine = world.GetUnit(pawn.GatherMineNbr);
            if (mine == null)
            {
                GoIdle(pawn);
                return;
            }

            if (mine.Health <= 0)
            {
                var baseForEmpty = world.GetUnit(pawn.GatherBaseNbr);
                if (baseForEmpty == null)
                {
                    GoIdle(pawn);
                    return;
                }
                var emptyPath = world.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseForEmpty.GridPosition);
                var oldPhase = pawn.GatherPhase;
                pawn.GatherPhase = GatherPhase.TO_BASE;
                pawn.TickPath = emptyPath;
                pawn.PathIndex = 0;
                callbacks.OnGatherPhaseChanged(pawn, oldPhase, GatherPhase.TO_BASE);
                return;
            }

            float miningSpeed = world.Constants.MiningSpeed;
            pawn.MiningTimer += world.TickDuration * miningSpeed;

            if (pawn.MiningTimer >= 1f)
            {
                int goldChunk = (int)pawn.MiningTimer;
                mine.Health -= goldChunk;
                pawn.GoldCarried += goldChunk;
                pawn.MiningTimer -= goldChunk;
                callbacks.OnMiningTick(pawn, mine, goldChunk);
            }

            float miningCapacity = world.Constants.MiningCapacity;
            if (pawn.GoldCarried >= (int)miningCapacity)
            {
                var baseUnit = world.GetUnit(pawn.GatherBaseNbr);
                if (baseUnit == null)
                {
                    GoIdle(pawn);
                    return;
                }

                var path = world.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition);
                var oldPhase = pawn.GatherPhase;
                pawn.GatherPhase = GatherPhase.TO_BASE;
                pawn.TickPath = path;
                pawn.PathIndex = 0;
                callbacks.OnGatherPhaseChanged(pawn, oldPhase, GatherPhase.TO_BASE);
            }
        }

        private static void AdvanceGatherToBase(ITickUnit pawn, ITickWorld world, ITickCallbacks callbacks)
        {
            var baseUnit = world.GetUnit(pawn.GatherBaseNbr);
            if (baseUnit == null)
            {
                GoIdle(pawn);
                return;
            }

            if (pawn.TickPath != null && pawn.PathIndex < pawn.TickPath.Count)
                return;

            if (world.IsNeighborOfUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition))
            {
                world.AddGold(pawn.OwnerAgentNbr, pawn.GoldCarried);
                callbacks.OnGoldDeposited(pawn, pawn.GoldCarried);
                pawn.GoldCarried = 0;

                var mine = world.GetUnit(pawn.GatherMineNbr);
                if (mine == null || mine.Health <= 0)
                {
                    GoIdle(pawn);
                    return;
                }

                var path = world.FindPathToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition);
                var oldPhase = pawn.GatherPhase;
                pawn.GatherPhase = GatherPhase.TO_MINE;
                pawn.TickPath = path;
                pawn.PathIndex = 0;
                callbacks.OnGatherPhaseChanged(pawn, oldPhase, GatherPhase.TO_MINE);
            }
            else
            {
                var path = world.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition);
                if (path.Count > 0)
                {
                    pawn.TickPath = path;
                    pawn.PathIndex = 0;
                }
                else
                {
                    GoIdle(pawn);
                }
            }
        }

        #endregion

        #region Combat

        private static void AdvanceAttack(ITickUnit attacker, ITickWorld world, ITickCallbacks callbacks)
        {
            var target = world.GetUnit(attacker.AttackTargetNbr);
            if (target == null || target.Health <= 0)
            {
                GoIdle(attacker);
                return;
            }

            if (TaskEngine.IsInAttackRange(attacker.UnitType, attacker.CenterPosition,
                    target.UnitType, target.CenterPosition))
            {
                float dmg = TaskEngine.ComputeDamagePerTick(
                    attacker.UnitType, target.UnitType,
                    world.Constants.Damage[attacker.UnitType], world.TickDuration);
                target.Health -= dmg;
                callbacks.OnDamageDealt(attacker, target, dmg);

                if (target.Health <= 0)
                {
                    GoIdle(attacker);
                    callbacks.OnUnitKilled(target);
                    return;
                }
            }
            else
            {
                // Retarget: check for closer enemy in range
                int? closerNbr = FindClosestEnemyInRange(attacker, world);
                if (closerNbr.HasValue && closerNbr.Value != attacker.AttackTargetNbr)
                {
                    attacker.AttackTargetNbr = closerNbr.Value;
                    attacker.TickPath = null;
                    attacker.PathIndex = 0;
                    return;
                }

                // Out of range — repath if path exhausted
                if (attacker.TickPath == null || attacker.PathIndex >= attacker.TickPath.Count)
                {
                    var path = world.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);
                    if (path.Count > 0)
                    {
                        attacker.TickPath = path;
                        attacker.PathIndex = 0;
                        callbacks.OnUnitRepath(attacker, path);
                    }
                }
            }
        }

        private static int? FindClosestEnemyInRange(ITickUnit attacker, ITickWorld world)
        {
            float closestDist = float.MaxValue;
            int? closest = null;

            foreach (var enemy in world.AllUnits)
            {
                if (enemy.OwnerAgentNbr == attacker.OwnerAgentNbr) continue;
                if (enemy.UnitType == UnitType.MINE) continue;
                if (enemy.Health <= 0) continue;

                float effectiveRange = GameConstants.EffectiveAttackRange(attacker.UnitType, enemy.UnitType);
                float dist = Position.Distance(attacker.CenterPosition, enemy.CenterPosition);
                if (dist <= effectiveRange + 0.1f && dist < closestDist)
                {
                    closestDist = dist;
                    closest = enemy.UnitNbr;
                }
            }
            return closest;
        }

        #endregion

        #region Repair

        private static void AdvanceRepair(ITickUnit pawn, ITickWorld world, ITickCallbacks callbacks)
        {
            var building = world.GetUnit(pawn.RepairBuildingNbr);
            if (building == null || building.Health <= 0)
            {
                GoIdle(pawn);
                return;
            }

            if (pawn.TickPath != null && pawn.PathIndex < pawn.TickPath.Count)
                return;

            float maxHp = GameConstants.HEALTH[building.UnitType];
            if (building.Health >= maxHp)
            {
                GoIdle(pawn);
                return;
            }

            float repairAmount = TaskEngine.ComputeRepairPerTick(
                building.UnitType, world.Constants.CreationTime[building.UnitType], world.TickDuration);
            building.Health = Math.Min(building.Health + repairAmount, maxHp);
            callbacks.OnRepairTick(pawn, building, repairAmount);

            if (building.Health >= maxHp)
            {
                GoIdle(pawn);
            }
        }

        #endregion

        #region Healing

        private static void AdvanceHeal(ITickUnit monk, ITickWorld world, ITickCallbacks callbacks)
        {
            var target = world.GetUnit(monk.HealTargetNbr);
            if (target == null || target.Health <= 0)
            {
                GoIdle(monk);
                monk.HealTargetNbr = -1;
                return;
            }

            if (TaskEngine.IsInHealRange(monk.UnitType, monk.CenterPosition, target.CenterPosition))
            {
                if (!TaskEngine.CanHeal(monk.Mana, target.Health, target.UnitType))
                {
                    GoIdle(monk);
                monk.HealTargetNbr = -1;
                    return;
                }

                float healAmount = TaskEngine.ComputeHealAmount(target.UnitType);
                float targetMaxHealth = GameConstants.HEALTH[target.UnitType];
                target.Health = Math.Min(target.Health + healAmount, targetMaxHealth);
                monk.Mana -= GameConstants.MANA_COST;
                callbacks.OnHealApplied(monk, target, healAmount);

                GoIdle(monk);
                monk.HealTargetNbr = -1;
            }
            else
            {
                if (monk.TickPath == null || monk.PathIndex >= monk.TickPath.Count)
                {
                    var path = world.FindPathToUnit(monk.GridPosition, target.UnitType, target.GridPosition);
                    if (path.Count > 0)
                    {
                        monk.TickPath = path;
                        monk.PathIndex = 0;
                    }
                }
            }
        }

        #endregion
    }
}
