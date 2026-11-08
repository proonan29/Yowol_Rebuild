using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    /// <summary>Pit / out-of-bounds: returns the player to safe ground with a small HP penalty.</summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public class KillZone : MonoBehaviour
    {
        public int damage = 15;

        void OnTriggerEnter2D(Collider2D other)
        {
            var pc = other.GetComponentInParent<PlayerController>();
            if (pc != null && !pc.IsDead) { ScreenFader.Instance.Blink(() => pc.Respawn(damage)); return; }
            var e = other.GetComponentInParent<EnemyBase>();
            if (e != null) e.GetComponent<Health>()?.Kill();
        }
    }
}
