using System.Collections.Generic;
using UnityEngine;

namespace Yowol
{
    /// <summary>
    /// Moving hitbox with a sprite animation. Supports single hit, piercing, and multi-hit ticking
    /// (e.g. the moon slash wave hits each enemy several times as it passes).
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        public Vector2 velocity;
        public float lifetime = 2f;
        public int damage = 10;
        public int targetMask;
        public Vector2 hitSize = new Vector2(0.8f, 0.4f);
        public float knockback = 2f;
        public bool pierce;
        public int hitsPerTarget = 1;
        public float tickInterval = 0.1f;
        public string hitFx;
        public Color hitFxColor = Color.white;
        public float hitFxScale = 0.5f;
        public bool stopOnGround = true;
        public bool heavy;
        public AudioClip hitSound;

        [Header("Homing")]
        public Transform homingTarget;
        public float turnRateDegrees = 0f;

        public GameObject owner;

        float age;
        readonly Dictionary<Health, int> hitCount = new Dictionary<Health, int>();
        readonly Dictionary<Health, float> nextHitTime = new Dictionary<Health, float>();
        readonly HashSet<Health> blocked = new HashSet<Health>();

        public static Projectile Spawn(string fxClip, Vector3 position, Vector2 velocity, int damage, int targetMask,
                                       float scale = 1f, Color? color = null, int order = Fx.DefaultOrder + 2)
        {
            var fx = Fx.Play(fxClip, position, false, scale, 0f, color, order);
            if (fx == null) return null;
            var anim = fx.GetComponent<SpriteAnimator>();
            Object.Destroy(fx); // the projectile manages its own lifetime
            var p = anim.gameObject.AddComponent<Projectile>();
            p.velocity = velocity;
            p.damage = damage;
            p.targetMask = targetMask;
            p.baseScale = scale;
            p.Orient();
            return p;
        }

        float baseScale = 1f;

        void Update()
        {
            age += Time.deltaTime;
            if (age >= lifetime) { Destroy(gameObject); return; }

            if (homingTarget != null && turnRateDegrees > 0f)
            {
                Vector2 to = ((Vector2)homingTarget.position + Vector2.up * 1f - (Vector2)transform.position).normalized;
                float maxRad = turnRateDegrees * Mathf.Deg2Rad * Time.deltaTime;
                velocity = Vector3.RotateTowards(velocity, to * velocity.magnitude, maxRad, 0f);
            }

            transform.position += (Vector3)(velocity * Time.deltaTime);
            Orient();

            if (stopOnGround && Physics2D.OverlapPoint(transform.position, Layers.GroundMask))
            {
                if (!string.IsNullOrEmpty(hitFx)) Fx.Play(hitFx, transform.position, false, hitFxScale, 0, hitFxColor);
                Destroy(gameObject);
                return;
            }

            blocked.Clear();
            foreach (var kv in hitCount)
                if (kv.Value >= hitsPerTarget || Time.time < nextHitTime[kv.Key]) blocked.Add(kv.Key);

            var info = new DamageInfo
            {
                amount = damage, origin = (Vector2)transform.position - velocity.normalized,
                knockback = knockback, heavy = heavy, source = owner
            };
            int hits = Combat.HitBox(transform.position, hitSize * baseScale, targetMask, info, blocked, OnHit);
            if (hits > 0 && !pierce) Destroy(gameObject);
        }

        void OnHit(Health h, Vector2 point)
        {
            hitCount.TryGetValue(h, out int n);
            hitCount[h] = n + 1;
            nextHitTime[h] = Time.time + tickInterval;
            if (!string.IsNullOrEmpty(hitFx)) Fx.Play(hitFx, point, Random.value > 0.5f, hitFxScale, Random.Range(0f, 360f), hitFxColor);
            if (hitSound != null) AudioHub.Sfx(hitSound, 0.7f);
        }

        void Orient()
        {
            if (velocity.sqrMagnitude < 0.0001f) return;
            float ang = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
            bool left = velocity.x < 0f;
            transform.rotation = Quaternion.Euler(0, 0, left ? ang - 180f : ang);
            var s = transform.localScale;
            s.x = left ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            transform.localScale = s;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position, hitSize * baseScale);
        }
    }
}
