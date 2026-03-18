using UnityEngine;

namespace GameManager.GameElements
{
	/// <summary>
	/// A visual-only gold nugget that spawns at the pickaxe head on downswing,
	/// arcs upward, then bounces down onto the pawn. Self-destructs on arrival.
	/// </summary>
	public class GoldNuggetProjectile : MonoBehaviour
	{
		private Vector3 start;
		private Vector3 end;
		private float arcHeight;
		private float duration;
		private float elapsed;
		private bool launched;

		private const float FLIGHT_DURATION = 0.4f;
		private const float ARC_HEIGHT = 1.2f;

		internal void Launch(Vector3 from, Vector3 to)
		{
			start = from;
			end = to;
			duration = FLIGHT_DURATION;
			arcHeight = ARC_HEIGHT;
			transform.position = from;
			launched = true;
			elapsed = 0f;
		}

		void Update()
		{
			if (!launched) return;

			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);

			// Linear interpolation for base position
			Vector3 pos = Vector3.Lerp(start, end, t);

			// Parabolic arc peaking at t=0.5
			pos.y += arcHeight * 4f * t * (1f - t);

			transform.position = pos;

			if (t >= 1f)
			{
				Destroy(gameObject);
			}
		}
	}
}
