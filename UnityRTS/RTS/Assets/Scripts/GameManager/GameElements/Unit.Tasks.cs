using System.Collections.Generic;
using AgentSDK;
using GameManager.EnumTypes;
using UnityEngine;

namespace GameManager.GameElements
{
	public partial class Unit
	{
		// =====================================================================
		// Legacy task methods (UpdateAttack, UpdateBuild, UpdateTrain,
		// UpdateGather, UpdateRepair, UpdateHeal) have been removed.
		// All task logic is now driven by the shared TickEngine.AdvanceAllUnits()
		// called from GameManager.SimulateTick(). The methods below are
		// visual-only helpers used by callbacks and the animation system.
		// =====================================================================

		/// <summary>
		/// Compute the distance from this unit to the closest occupied cell of the target.
		/// For 1x1 units this is equivalent to CenterGridPosition distance.
		/// For buildings, this measures to the nearest edge cell rather than the center.
		/// </summary>
		internal float DistanceToClosestCell(Unit target)
		{
			var size = Constants.UNIT_SIZE[target.UnitType];
			if (size.x <= 1 && size.y <= 1)
				return Vector3.Distance(target.CenterGridPosition, CenterGridPosition);

			float bestDist = float.MaxValue;
			for (int dx = 0; dx < size.x; dx++)
			{
				for (int dy = 0; dy < size.y; dy++)
				{
					var cell = new Vector3Int(target.GridPosition.x + dx,
					                          target.GridPosition.y - dy, 0);
					float dist = Vector3.Distance((Vector3)cell, (Vector3)GridPosition);
					if (dist < bestDist)
						bestDist = dist;
				}
			}
			return bestDist;
		}

		/// <summary>
		/// Spawn a visual arrow projectile from this archer toward the target.
		/// Arrow spawns at the bow tip position on frame 5 of the shoot animation.
		/// For buildings, aims at the closest occupied cell. Adds random perturbation
		/// so arrows don't all land on the exact same pixel.
		/// </summary>
		internal void SpawnArrow(Unit target)
		{
			Sprite arrowSprite = GameManager.Instance.ArrowSprite;
			if (arrowSprite == null) return;

			var arrowGo = new GameObject("Arrow");
			var sr = arrowGo.AddComponent<SpriteRenderer>();
			sr.sprite = arrowSprite;
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 0;

			// Bow tip offset on frame 5: upper-forward relative to sprite center
			float offsetX = facingRight ? 0.2f : -0.2f;
			Vector3 spawnPos = WorldPosition + new Vector3(offsetX, 0.3f, 0);

			// For buildings, aim at the closest cell; for units, aim at their center cell
			Vector3Int aimCell;
			var size = Constants.UNIT_SIZE[target.UnitType];
			if (size.x > 1 || size.y > 1)
			{
				// Find closest occupied cell of the building to this archer
				float bestDist = float.MaxValue;
				aimCell = target.CenterGridPosition;
				for (int dx = 0; dx < size.x; dx++)
				{
					for (int dy = 0; dy < size.y; dy++)
					{
						var cell = new Vector3Int(target.GridPosition.x + dx,
						                          target.GridPosition.y - dy, 0);
						float dist = Vector3.Distance((Vector3)cell, (Vector3)GridPosition);
						if (dist < bestDist)
						{
							bestDist = dist;
							aimCell = cell;
						}
					}
				}
			}
			else
			{
				aimCell = target.CenterGridPosition;
			}

			// Cell center + random perturbation within the cell
			Vector3 targetPos = (Vector3)aimCell + new Vector3(
				0.5f + UnityEngine.Random.Range(-0.3f, 0.3f),
				0.5f + UnityEngine.Random.Range(-0.3f, 0.3f), 0);

			// Inset by half a cell from building edges so arrows don't land in empty corners
			if (size.x > 1 || size.y > 1)
			{
				float inset = 0.5f;
				float left   = target.GridPosition.x + inset;
				float right  = target.GridPosition.x + size.x - inset;
				float bottom = target.GridPosition.y - size.y + 1 + inset;
				float top    = target.GridPosition.y + 1 - inset;
				targetPos.x = Mathf.Clamp(targetPos.x, left, right);
				targetPos.y = Mathf.Clamp(targetPos.y, bottom, top);
			}

			var arrow = arrowGo.AddComponent<ArrowProjectile>();
			arrow.Launch(spawnPos, targetPos, ARROW_SPEED);
			arrow.SetTarget(target.transform, GameManager.Instance.ExplosionAnimatorController);
			arrow.AttachFire(GameManager.Instance.FireAnimatorController);
		}

		/// <summary>
		/// Spawn a gold nugget at the pickaxe head that arcs up and bounces down to the pawn.
		/// Also triggers the mine's highlight animation.
		/// </summary>
		internal void SpawnGoldNugget()
		{
			Sprite goldSprite = GameManager.Instance.GoldResourceSprite;
			if (goldSprite == null) return;

			// Trigger the mine's highlight animation from the beginning
			var mineUnit = MineUnit;
			if (mineUnit != null)
			{
				var mineAnimator = mineUnit.GetComponent<Animator>();
				if (mineAnimator != null && mineAnimator.runtimeAnimatorController != null)
					mineAnimator.Play(0, 0, 0f);
			}

			var nuggetGo = new GameObject("GoldNugget");
			var sr = nuggetGo.AddComponent<SpriteRenderer>();
			sr.sprite = goldSprite;
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 0;

			// Pickaxe tip at frame 3 downswing: pixel (150,115) in 192x192 frame, PPU=80, center pivot
			// Y offset raised so the coin's bottom edge aligns with the tip of the pickaxe
			float tipX = facingRight ? 0.675f : -0.675f;
			Vector3 spawnPos = WorldPosition + new Vector3(tipX, -0.14f, 0);

			// Target: pawn's center (slightly above feet)
			Vector3 targetPos = WorldPosition + new Vector3(0f, 0.2f, 0);

			var nugget = nuggetGo.AddComponent<GoldNuggetProjectile>();
			nugget.Launch(spawnPos, targetPos);
		}

		/// <summary>
		/// Spawn a heal VFX on the target unit.
		/// </summary>
		internal void SpawnHealEffect(Unit target)
		{
			var controller = GameManager.Instance.HealEffectAnimatorController;
			if (controller == null) return;

			var healGo = new GameObject("HealEffect");
			healGo.transform.SetParent(target.transform);
			healGo.transform.localPosition = new Vector3(0f, 0.5f, 0f);
			healGo.transform.localScale = Vector3.one;

			var sr = healGo.AddComponent<SpriteRenderer>();
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 2; // above units (sortingOrder 0-1) but Y-sorted with them
			sr.spriteSortPoint = SpriteSortPoint.Pivot;

			var anim = healGo.AddComponent<Animator>();
			anim.runtimeAnimatorController = controller;

			// Auto-destroy after 1 second (animation duration)
			Object.Destroy(healGo, 1.1f);
		}
	}
}
