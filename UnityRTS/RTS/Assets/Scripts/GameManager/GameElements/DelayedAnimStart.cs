using UnityEngine;

namespace GameManager.GameElements
{
    /// <summary>
    /// Simple helper that starts an Animator after a delay.
    /// Used to stagger building death dust clouds.
    /// </summary>
    public class DelayedAnimStart : MonoBehaviour
    {
        internal float delay;
        internal float targetSpeed = 1f;
        private float elapsed;

        void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= delay)
            {
                var anim = GetComponent<Animator>();
                if (anim != null)
                    anim.speed = targetSpeed;
                Destroy(this); // remove the helper component
            }
        }
    }
}
