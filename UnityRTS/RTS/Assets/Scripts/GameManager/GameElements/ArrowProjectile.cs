using AgentSDK;
using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// A visual-only arrow that arcs from an archer toward its target.
	/// On impact it sticks for 0.5s (following mobile targets), then explodes.
	/// Does not deal damage — damage is handled by the shared StepEngine.AdvanceAllUnits().
	/// </summary>
	public class ArrowProjectile : MonoBehaviour
	{
		private Vector3 start;
		private Vector3 target;
		private float arcHeight;
		private float duration;
		private float elapsed;
		private bool launched;
		private Transform fireTransform;
		private RuntimeAnimatorController fireController;
		private bool mirrorSpawnedThisCycle;
		private Quaternion lastRotation;

		// Impact state
		private enum Phase { Flying, Stuck, Exploding }
		private Phase phase = Phase.Flying;
		private float stuckTimer;
		private Transform targetTransform;
		private RuntimeAnimatorController explosionController;
		private const float STUCK_DURATION = 0.5f;
		private const float EXPLOSION_DURATION = 0.25f; // Destroy at frame 3 of 12 FPS explosion

		internal void Launch(Vector3 from, Vector3 to, float arrowSpeed)
		{
			start = from;
			target = to;
			float distance = Vector3.Distance(from, to);
			duration = distance / arrowSpeed;
			arcHeight = distance * 0.25f;
			transform.position = from;

			// Set initial rotation to match launch direction
			Vector3 dir = (to - from).normalized;
			// Add initial arc tangent (derivative of parabolic arc at t=0 is +arcHeight*4)
			dir.y += arcHeight * 4f / distance;
			dir.Normalize();
			float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
			transform.rotation = Quaternion.Euler(0, 0, angle);

			launched = true;
			elapsed = 0f;
		}

		/// <summary>
		/// Set the target transform so the arrow follows mobile units after impact.
		/// </summary>
		internal void SetTarget(Transform target, RuntimeAnimatorController explosion)
		{
			targetTransform = target;
			explosionController = explosion;
		}

		/// <summary>
		/// Attach a looping fire animation at the arrow tip.
		/// </summary>
		internal void AttachFire(RuntimeAnimatorController controller)
		{
			if (controller == null) return;

			var fireGo = new GameObject("Fire");
			fireGo.transform.SetParent(transform);
			fireGo.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
			fireGo.transform.localRotation = Quaternion.identity;
			fireGo.transform.localPosition = new Vector3(0.25f, 0f, 0f);

			var sr = fireGo.AddComponent<SpriteRenderer>();
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 0;

			var animator = fireGo.AddComponent<Animator>();
			animator.runtimeAnimatorController = controller;

			fireTransform = fireGo.transform;
			fireController = controller;
		}

		private void SpawnMirrorFire(bool arrowFlipped)
		{
			var mirrorGo = new GameObject("FireMirror");
			mirrorGo.transform.SetParent(transform);
			mirrorGo.transform.localRotation = Quaternion.identity;
			mirrorGo.transform.localPosition = new Vector3(0.25f, 0f, 0f);

			float yScale = arrowFlipped ? 0.8f : -0.8f;
			mirrorGo.transform.localScale = new Vector3(0.8f, yScale, 1f);

			var sr = mirrorGo.AddComponent<SpriteRenderer>();
			sr.sortingLayerName = "Agents";
			sr.sortingOrder = 0;

			var animator = mirrorGo.AddComponent<Animator>();
			animator.runtimeAnimatorController = fireController;
		}

		void Update()
		{
			if (!launched) return;

			switch (phase)
			{
				case Phase.Flying:
					UpdateFlying();
					break;
				case Phase.Stuck:
					UpdateStuck();
					break;
				case Phase.Exploding:
					UpdateExploding();
					break;
			}
		}

		private void UpdateFlying()
		{
			elapsed += Time.deltaTime * Constants.GAME_SPEED;
			float t = Mathf.Clamp01(elapsed / duration);

			// Linear interpolation for base position
			Vector3 pos = Vector3.Lerp(start, target, t);

			// Parabolic arc peaking at t=0.5
			pos.y += arcHeight * 4f * t * (1f - t);

			// Compute tangent direction for arrow rotation
			// Use backward difference near the end so the direction doesn't reverse
			float pt = Mathf.Max(t - 0.01f, 0f);
			Vector3 prevPos = Vector3.Lerp(start, target, pt);
			prevPos.y += arcHeight * 4f * pt * (1f - pt);

			Vector3 dir = (pos - prevPos).normalized;
			float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
			lastRotation = Quaternion.Euler(0, 0, angle);
			transform.rotation = lastRotation;
			transform.position = pos;

			// Flip fire upright when arrow points left (angle > 90 or < -90)
			if (fireTransform != null)
			{
				bool flipped = angle > 90f || angle < -90f;
				float yScale = flipped ? -0.8f : 0.8f;
				fireTransform.localScale = new Vector3(0.8f, yScale, 1f);

				// Spawn a mirrored fire each time the animation reaches frame 2
				var animator = fireTransform.GetComponent<Animator>();
				if (animator != null && animator.runtimeAnimatorController != null)
				{
					float loopPos = animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;
					if (loopPos >= 0.5f && !mirrorSpawnedThisCycle)
					{
						mirrorSpawnedThisCycle = true;
						SpawnMirrorFire(flipped);
					}
					else if (loopPos < 0.5f)
					{
						mirrorSpawnedThisCycle = false;
					}
				}
			}

			if (t >= 1f)
			{
				EnterStuckPhase();
			}
		}

		private void EnterStuckPhase()
		{
			phase = Phase.Stuck;
			stuckTimer = 0f;
			transform.rotation = lastRotation;

			// Destroy all fire children
			foreach (Transform child in transform)
			{
				Destroy(child.gameObject);
			}
			fireTransform = null;

			// Spawn a non-looping impact fire on buildings at the arrowhead position
			if (targetTransform != null)
			{
				var targetUnit = targetTransform.GetComponent<Unit>();
				if (targetUnit != null && !targetUnit.CanMove && targetUnit.UnitType != UnitType.MINE)
				{
					SpawnImpactFire();
				}
			}

			// Attach to target so arrow follows mobile units, preserving world position/rotation
			if (targetTransform != null)
			{
				transform.SetParent(targetTransform, worldPositionStays: true);
			}
		}

		/// <summary>
		/// Spawn a one-shot (non-looping) fire at the arrow impact point, parented to the target building.
		/// </summary>
		private void SpawnImpactFire()
		{
			var controllers = GameManager.Instance.BuildingFireControllers;
			if (controllers == null || controllers.Length == 0) return;

			var controller = controllers[UnityEngine.Random.Range(0, controllers.Length)];
			if (controller == null) return;

			// Position at the arrowhead in world space
			Vector3 impactPos = transform.TransformPoint(new Vector3(0.25f, 0f, 0f));

			var fireGo = new GameObject("ImpactFire");
			fireGo.transform.SetParent(targetTransform);
			fireGo.transform.position = impactPos;
			fireGo.transform.localScale = Vector3.one;
			fireGo.transform.localRotation = Quaternion.identity;

			var sr = fireGo.AddComponent<SpriteRenderer>();
			sr.sortingLayerName = "UnitUI";
			sr.sortingOrder = 20;

			var animator = fireGo.AddComponent<Animator>();
			animator.runtimeAnimatorController = controller;
			animator.speed = Constants.GAME_SPEED;

			// Self-destruct after one play-through, scaled by game speed
			float destroyTime = Constants.GAME_SPEED > 0 ? 0.5f / Constants.GAME_SPEED : 0.5f;
			UnityEngine.Object.Destroy(fireGo, destroyTime);
		}

		private void UpdateStuck()
		{
			// If target was destroyed, destroy the arrow too
			if (targetTransform != null && targetTransform.gameObject == null)
			{
				Destroy(gameObject);
				return;
			}

			stuckTimer += Time.deltaTime * Constants.GAME_SPEED;
			if (stuckTimer >= STUCK_DURATION)
			{
				EnterExplodingPhase();
			}
		}

		private void EnterExplodingPhase()
		{
			phase = Phase.Exploding;
			stuckTimer = 0f;

			// Unparent so explosion stays at world position even if target moves/dies
			transform.SetParent(null);

			// Spawn explosion as a standalone GameObject so it survives arrow destruction
			if (explosionController != null)
			{
				var explosionGo = new GameObject("Explosion");
				// Position at the arrow tip in world space
				explosionGo.transform.position = transform.TransformPoint(new Vector3(0.25f, 0f, 0f));
				explosionGo.transform.localScale = new Vector3(0.75f, 0.75f, 1f);
				explosionGo.transform.rotation = Quaternion.identity;

				var sr = explosionGo.AddComponent<SpriteRenderer>();
				sr.sortingLayerName = "Agents";
				sr.sortingOrder = 0;

				var animator = explosionGo.AddComponent<Animator>();
				animator.runtimeAnimatorController = explosionController;
				animator.speed = Constants.GAME_SPEED;

				// Self-destruct after the full animation (8 frames at 12 FPS)
				float explodeTime = Constants.GAME_SPEED > 0 ? 0.667f / Constants.GAME_SPEED : 0.667f;
				Destroy(explosionGo, explodeTime);
			}
		}

		private void UpdateExploding()
		{
			stuckTimer += Time.deltaTime * Constants.GAME_SPEED;
			if (stuckTimer >= EXPLOSION_DURATION)
			{
				// Destroy only the arrow; the explosion lives on independently
				Destroy(gameObject);
			}
		}
	}
}
