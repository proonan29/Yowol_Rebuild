using System;
using System.Collections;
using UnityEngine;

namespace Yowol
{
    /// <summary>Shared enemy plumbing: health, facing, knockback, stun, airborne launch, death.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public abstract class EnemyBase : MonoBehaviour
    {
        public static event Action<EnemyBase> AnyDied;
        public static int AliveCount { get; private set; }

        [Header("Base")]
        public Transform visual;
        public SpriteAnimator anim;
        public int maxHp = 120;
        public int contactDamage = 8;
        public float knockbackResist = 0f;      // 0 = full knockback, 1 = immune
        public bool countsForStageClear = true;

        protected Rigidbody2D rb;
        protected Health health;
        protected HitFlash flash;
        protected float stunUntil;
        protected float airborneUntil;
        protected bool dead;
        bool counted;

        public bool IsDead => dead;
        public bool FacingRight { get; private set; } = true;
        protected Transform Player => PlayerController.Instance != null ? PlayerController.Instance.transform : null;
        protected bool PlayerAlive => PlayerController.Instance != null && !PlayerController.Instance.IsDead;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            flash = GetComponent<HitFlash>();
            health.ResetTo(maxHp);
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }

        protected virtual void OnEnable()
        {
            if (countsForStageClear && !counted) { counted = true; AliveCount++; }
        }

        protected virtual void OnDisable()
        {
            if (counted) { counted = false; AliveCount--; }
        }

        public void Face(bool right)
        {
            FacingRight = right;
            if (visual != null)
            {
                var s = visual.localScale;
                s.x = Mathf.Abs(s.x) * (right ? ArtFacesRight : -ArtFacesRight);
                visual.localScale = s;
            }
        }

        /// <summary>+1 if the source art faces right, -1 if it faces left.</summary>
        protected virtual float ArtFacesRight => 1f;

        public void FaceToward(Vector3 p) => Face(p.x > transform.position.x);

        protected virtual void OnDamaged(DamageInfo info)
        {
            if (dead) return;
            float dir = Mathf.Sign(transform.position.x - info.origin.x);
            if (dir == 0f) dir = 1f;
            float kb = info.knockback * (1f - knockbackResist);
            if (info.launch > 0f && knockbackResist < 0.9f)
            {
                airborneUntil = Time.time + info.launch;
                stunUntil = Mathf.Max(stunUntil, airborneUntil);
                rb.linearVelocity = new Vector2(dir * kb * 0.5f, 11f);
            }
            else if (kb > 0.01f)
            {
                rb.linearVelocity = new Vector2(dir * kb, Mathf.Max(rb.linearVelocity.y, kb * 0.25f));
                stunUntil = Mathf.Max(stunUntil, Time.time + 0.18f + kb * 0.03f);
            }
            AudioHub.Sfx(GameAssets.I.sfxHit, 0.35f, 0.15f);
        }

        protected virtual void OnDied()
        {
            if (dead) return;
            dead = true;
            AudioHub.Sfx(GameAssets.I.sfxEnemyDeath, 0.8f);
            if (counted) { counted = false; AliveCount--; }
            AnyDied?.Invoke(this);
            StartCoroutine(DeathRoutine());
        }

        protected virtual IEnumerator DeathRoutine()
        {
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = new Vector2(rb.linearVelocity.x * 0.3f, 1.2f);
            var srs = GetComponentsInChildren<SpriteRenderer>();
            Fx.Play("CrossWave", transform.position + Vector3.up, false, 0.8f, 0f, new Color(0.7f, 0.5f, 1f, 0.8f));
            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                foreach (var sr in srs) { var c = sr.color; c.a = 1f - t / 0.8f; sr.color = c; }
                if (flash != null) flash.enabled = false;
                yield return null;
            }
            Destroy(gameObject);
        }

        protected bool Stunned => Time.time < stunUntil;
        protected bool Airborne => Time.time < airborneUntil;

        /// <summary>Damage the player on body contact.</summary>
        protected void ContactDamage(Vector2 size, Vector2 offset)
        {
            if (dead || contactDamage <= 0 || !PlayerAlive) return;
            var info = new DamageInfo { amount = contactDamage, origin = transform.position, knockback = 4f, source = gameObject };
            Combat.HitBox((Vector2)transform.position + offset, size, Layers.PlayerMask, info);
        }
    }
}
