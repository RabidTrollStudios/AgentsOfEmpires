using System;
using System.Collections.Generic;
using System.Linq;

namespace AgentSDK
{
    /// <summary>
    /// Shared game step engine. Processes all unit tasks and movement
    /// in deterministic order. Both Unity and SimGame call this to
    /// guarantee identical game logic.
    /// </summary>
    public static class StepEngine
    {
        /// <summary>
        /// Advance all units one step: task logic + movement per unit,
        /// in deterministic UnitNbr order.
        /// </summary>
        public static void AdvanceAllUnits(ISimWorld world, ISimCallbacks callbacks)
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

                // Movement is now handled by MovementSystem.Advance called separately
            }

            // Phase 3: Mana regen + ability cooldowns
            foreach (var unit in units)
            {
                float maxMana = GameConstants.MAX_MANA[unit.UnitType];
                float manaRegen = world.Constants.ManaRegen;
                unit.Mana = TaskEngine.RegenMana(unit.Mana, maxMana, manaRegen, world.StepDuration);

                // Warrior charge cooldown ticks down
                if (unit.UnitType == UnitType.WARRIOR && unit.ChargeCooldown > 0f)
                    unit.ChargeCooldown = Math.Max(0f, unit.ChargeCooldown - world.StepDuration);
            }

            // Phase 4: Remove dead units
            var deadUnits = new List<ISimUnit>();
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

        /// <summary>Transition a unit to IDLE, clearing all movement and target state.</summary>
        public static void SetIdle(ISimUnit unit) => GoIdle(unit);

        private static void GoIdle(ISimUnit unit)
        {
            unit.CurrentAction = UnitAction.IDLE;
            unit.SimPath = null;
            unit.PathIndex = 0;
            unit.PathProgress = 0f;
            unit.AttackTargetNbr = -1;
            unit.HealTargetNbr = -1;
            unit.BuildTargetNbr = -1;
            unit.RepairBuildingNbr = -1;
            // Reset attack cooldown so next attack starts fresh
            unit.AttackCooldown = 0f;
            // Ability state: keep ChargeCooldown (ticks down over time),
            // keep VolleyTargetNbr/VolleyTimer (persists between attacks),
            // reset JoustDistance (lancer starts accumulating on next move).
            unit.JoustDistance = 0f;
        }

        #region Movement

        private static void AdvanceMove(ISimUnit unit)
        {
            if (unit.SimPath == null || unit.PathIndex >= unit.SimPath.Count)
            {
                GoIdle(unit);
            }
        }

        // MoveUnitOneStep removed — movement is now handled by MovementSystem.Advance
        // called separately by Unity (per-frame) and SimGame (per-step).

        #endregion

        #region Training

        private static void AdvanceTrain(ISimUnit building, ISimWorld world, ISimCallbacks callbacks)
        {
            building.TrainTimer += world.StepDuration;
            if (building.TrainTimer < world.Constants.CreationTime[building.TrainTarget])
                return;

            var spawnPositions = world.GetBuildablePositionsNearUnit(building.UnitType, building.GridPosition);
            if (spawnPositions.Count == 0)
            {
                building.TrainTimer = world.Constants.CreationTime[building.TrainTarget] - world.StepDuration;
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

        private static void AdvanceBuild(ISimUnit pawn, ISimWorld world, ISimCallbacks callbacks)
        {
            if (pawn.SimPath != null && pawn.PathIndex < pawn.SimPath.Count)
                return;

            pawn.BuildTimer += world.StepDuration;
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

        private static void AdvanceGather(ISimUnit pawn, ISimWorld world, ISimCallbacks callbacks)
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

        private static void AdvanceGatherToMine(ISimUnit pawn, ISimWorld world, ISimCallbacks callbacks)
        {
            var mine = world.GetUnit(pawn.GatherMineNbr);
            if (mine == null || mine.Health <= 0)
            {
                GoIdle(pawn);
                return;
            }

            if (pawn.SimPath != null && pawn.PathIndex < pawn.SimPath.Count)
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
                    pawn.SimPath = repath;
                    pawn.PathIndex = 0;
                }
                else
                {
                    GoIdle(pawn);
                }
            }
        }

        private static void AdvanceGatherMining(ISimUnit pawn, ISimWorld world, ISimCallbacks callbacks)
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
                // If already adjacent to base, deposit immediately
                if (world.IsNeighborOfUnit(pawn.GridPosition, UnitType.BASE, baseForEmpty.GridPosition))
                {
                    world.AddGold(pawn.OwnerAgentNbr, pawn.GoldCarried);
                    callbacks.OnGoldDeposited(pawn, pawn.GoldCarried);
                    pawn.GoldCarried = 0;
                    GoIdle(pawn);
                }
                else
                {
                    var emptyPath = world.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseForEmpty.GridPosition);
                    var oldPhase = pawn.GatherPhase;
                    pawn.GatherPhase = GatherPhase.TO_BASE;
                    pawn.SimPath = emptyPath;
                    pawn.PathIndex = 0;
                    callbacks.OnGatherPhaseChanged(pawn, oldPhase, GatherPhase.TO_BASE);
                }
                return;
            }

            float miningSpeed = world.Constants.MiningSpeed;
            pawn.MiningTimer += world.StepDuration * miningSpeed;

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

                // If already adjacent to base, deposit immediately without pathing
                if (world.IsNeighborOfUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition))
                {
                    world.AddGold(pawn.OwnerAgentNbr, pawn.GoldCarried);
                    callbacks.OnGoldDeposited(pawn, pawn.GoldCarried);
                    pawn.GoldCarried = 0;

                    // Re-check mine (same variable from outer scope)
                    if (mine == null || mine.Health <= 0)
                    {
                        GoIdle(pawn);
                        return;
                    }

                    // If also adjacent to mine, resume mining immediately
                    if (world.IsNeighborOfUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition))
                    {
                        var oldPhase2 = pawn.GatherPhase;
                        pawn.GatherPhase = GatherPhase.MINING;
                        pawn.MiningTimer = 0f;
                        pawn.GoldCarried = 0;
                        callbacks.OnGatherPhaseChanged(pawn, oldPhase2, GatherPhase.MINING);
                    }
                    else
                    {
                        var minePath = world.FindPathToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition);
                        var oldPhase2 = pawn.GatherPhase;
                        pawn.GatherPhase = GatherPhase.TO_MINE;
                        pawn.SimPath = minePath;
                        pawn.PathIndex = 0;
                        callbacks.OnGatherPhaseChanged(pawn, oldPhase2, GatherPhase.TO_MINE);
                    }
                }
                else
                {
                    var path = world.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition);
                    var oldPhase = pawn.GatherPhase;
                    pawn.GatherPhase = GatherPhase.TO_BASE;
                    pawn.SimPath = path;
                    pawn.PathIndex = 0;
                    callbacks.OnGatherPhaseChanged(pawn, oldPhase, GatherPhase.TO_BASE);
                }
            }
        }

        private static void AdvanceGatherToBase(ISimUnit pawn, ISimWorld world, ISimCallbacks callbacks)
        {
            var baseUnit = world.GetUnit(pawn.GatherBaseNbr);
            if (baseUnit == null)
            {
                GoIdle(pawn);
                return;
            }

            if (pawn.SimPath != null && pawn.PathIndex < pawn.SimPath.Count)
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

                // If already adjacent to mine, resume mining immediately
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
                    var path = world.FindPathToUnit(pawn.GridPosition, UnitType.MINE, mine.GridPosition);
                    var oldPhase = pawn.GatherPhase;
                    pawn.GatherPhase = GatherPhase.TO_MINE;
                    pawn.SimPath = path;
                    pawn.PathIndex = 0;
                    callbacks.OnGatherPhaseChanged(pawn, oldPhase, GatherPhase.TO_MINE);
                }
            }
            else
            {
                var path = world.FindPathToUnit(pawn.GridPosition, UnitType.BASE, baseUnit.GridPosition);
                if (path.Count > 0)
                {
                    pawn.SimPath = path;
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

        private static void AdvanceAttack(ISimUnit attacker, ISimWorld world, ISimCallbacks callbacks)
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
                // Anti-stack: if in range but sharing a cell and stationary, spread out
                if (attacker.CanMove
                    && world.Grid.GetOccupantCount(attacker.GridPosition) > 1
                    && (attacker.SimPath == null || attacker.PathIndex >= attacker.SimPath.Count))
                {
                    var spreadPath = world.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);
                    if (spreadPath.Count > 0)
                    {
                        attacker.SimPath = spreadPath;
                        attacker.PathIndex = 0;
                        attacker.PathProgress = 0f;
                    }
                }

                // Tick down attack cooldown
                if (attacker.AttackCooldown > 0f)
                {
                    attacker.AttackCooldown -= world.StepDuration;
                    return; // waiting for next attack — no damage this step
                }

                // Ready to strike — deal discrete damage
                float baseDmg = world.Constants.Damage[attacker.UnitType];
                float dmg = baseDmg * GameConstants.DamageMultiplier(attacker.UnitType, target.UnitType);

                // Archer Volley: bonus damage on first hit against a new/cooled-down target
                if (attacker.UnitType == UnitType.ARCHER)
                {
                    bool isNewTarget = attacker.VolleyTargetNbr != target.UnitNbr;
                    bool isCooledDown = attacker.VolleyTimer >= GameConstants.VOLLEY_COOLDOWN;
                    if (isNewTarget || isCooledDown)
                    {
                        dmg *= GameConstants.VOLLEY_BONUS_MULTIPLIER;
                        attacker.VolleyTargetNbr = target.UnitNbr;
                        attacker.VolleyTimer = 0f;
                    }
                    else
                    {
                        attacker.VolleyTimer += world.StepDuration;
                    }
                }

                // Lancer Joust: bonus damage after traveling minimum distance
                if (attacker.UnitType == UnitType.LANCER && attacker.JoustDistance >= GameConstants.JOUST_MIN_DISTANCE)
                {
                    dmg *= GameConstants.JOUST_BONUS_MULTIPLIER;
                    attacker.JoustDistance = 0f;
                }
                else if (attacker.UnitType == UnitType.LANCER)
                {
                    attacker.JoustDistance = 0f;
                }

                target.Health -= dmg;
                callbacks.OnDamageDealt(attacker, target, dmg);

                // Reset attack cooldown (scaled by game speed via StepDuration)
                float baseCooldown = GameConstants.BASE_ATTACK_COOLDOWN[attacker.UnitType];
                attacker.AttackCooldown = baseCooldown > 0 ? baseCooldown : world.StepDuration;

                // Warrior resets charge cooldown on hit (engaged in melee)
                if (attacker.UnitType == UnitType.WARRIOR)
                    attacker.ChargeCooldown = GameConstants.CHARGE_COOLDOWN;

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
                    attacker.SimPath = null;
                    attacker.PathIndex = 0;
                    return;
                }

                // Out of range — repath if path exhausted or target moved significantly
                bool pathExhausted = attacker.SimPath == null || attacker.PathIndex >= attacker.SimPath.Count;
                bool targetMoved = false;
                if (!pathExhausted && attacker.SimPath.Count > 0 && target.CanMove)
                {
                    // Check if target has moved >2 cells from our path destination
                    Position pathDest = attacker.SimPath[attacker.SimPath.Count - 1];
                    float destToTarget = Position.Distance(pathDest, target.CenterPosition);
                    targetMoved = destToTarget > 2.0f;
                }

                if (pathExhausted || targetMoved)
                {
                    var path = world.FindPathToUnit(attacker.GridPosition, target.UnitType, target.GridPosition);
                    if (path.Count > 0)
                    {
                        attacker.SimPath = path;
                        attacker.PathIndex = 0;
                        callbacks.OnUnitRepath(attacker, path);
                    }
                }
            }
        }

        private static int? FindClosestEnemyInRange(ISimUnit attacker, ISimWorld world)
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

        private static void AdvanceRepair(ISimUnit pawn, ISimWorld world, ISimCallbacks callbacks)
        {
            var building = world.GetUnit(pawn.RepairBuildingNbr);
            if (building == null || building.Health <= 0)
            {
                GoIdle(pawn);
                return;
            }

            if (pawn.SimPath != null && pawn.PathIndex < pawn.SimPath.Count)
                return;

            float maxHp = GameConstants.HEALTH[building.UnitType];
            if (building.Health >= maxHp)
            {
                GoIdle(pawn);
                return;
            }

            float repairAmount = TaskEngine.ComputeRepairPerTick(
                building.UnitType, world.Constants.CreationTime[building.UnitType], world.StepDuration);
            building.Health = Math.Min(building.Health + repairAmount, maxHp);
            callbacks.OnRepairTick(pawn, building, repairAmount);

            if (building.Health >= maxHp)
            {
                GoIdle(pawn);
            }
        }

        #endregion

        #region Healing

        private static void AdvanceHeal(ISimUnit monk, ISimWorld world, ISimCallbacks callbacks)
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
                // Out of range — repath if path exhausted or target moved
                bool pathExhausted = monk.SimPath == null || monk.PathIndex >= monk.SimPath.Count;
                bool targetMoved = false;
                if (!pathExhausted && monk.SimPath.Count > 0 && target.CanMove)
                {
                    Position pathDest = monk.SimPath[monk.SimPath.Count - 1];
                    float destToTarget = Position.Distance(pathDest, target.CenterPosition);
                    targetMoved = destToTarget > 2.0f;
                }

                if (pathExhausted || targetMoved)
                {
                    var path = world.FindPathToUnit(monk.GridPosition, target.UnitType, target.GridPosition);
                    if (path.Count > 0)
                    {
                        monk.SimPath = path;
                        monk.PathIndex = 0;
                    }
                }
            }
        }

        #endregion
    }
}
