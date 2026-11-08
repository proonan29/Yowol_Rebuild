using System;
using UnityEngine;

namespace Yowol
{
    public struct DamageInfo
    {
        public int amount;
        public Vector2 origin;       // world point the hit came from (knockback direction)
        public float knockback;      // horizontal impulse
        public float launch;         // > 0 throws the target airborne for this many seconds
        public bool heavy;           // stronger feedback (shake, hitstop)
        public GameObject source;

        public static DamageInfo Make(int amount, Vector2 origin, float knockback = 3f, GameObject source = null)
            => new DamageInfo { amount = amount, origin = origin, knockback = knockback, source = source };
    }

    /// <summary>Hit points + invulnerability. Anything that can be hurt has one.</summary>
    public class Health : MonoBehaviour
    {
        public int max = 100;
        [Tooltip("Seconds of invulnerability after taking a hit.")]
        public float invulnerableAfterHit = 0f;
        public bool showDamageNumbers = true;
        public Vector2 damageNumberOffset = new Vector2(0f, 2f);

        public int Current { get; private set; }
        public bool IsDead => Current <= 0;
        public float Ratio => max > 0 ? (float)Current / max : 0f;
        public bool Invulnerable { get; set; }
        public bool InHitInvulnerability => Time.time < invulnerableUntil;

        /// <summary>Return true to cancel the hit (parry, shields...).</summary>
        public delegate bool HitInterceptor(ref DamageInfo info);
        public HitInterceptor Intercept;

        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public event Action Changed;

        float invulnerableUntil;

        void Awake() { Current = max; }

        public void ResetTo(int newMax)
        {
            max = newMax;
            Current = newMax;
            Changed?.Invoke();
        }

        public void GrantInvulnerability(float seconds) =>
            invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);

        public bool TakeDamage(DamageInfo info)
        {
            if (IsDead || Invulnerable || Time.time < invulnerableUntil || info.amount <= 0) return false;
            if (Intercept != null && Intercept(ref info)) return false;

            Current = Mathf.Max(0, Current - info.amount);
            if (invulnerableAfterHit > 0f) invulnerableUntil = Time.time + invulnerableAfterHit;
            if (showDamageNumbers) DamageNumber.Spawn((Vector2)transform.position + damageNumberOffset, info.amount, info.heavy);
            Damaged?.Invoke(info);
            Changed?.Invoke();
            if (Current == 0) Died?.Invoke();
            return true;
        }

        public void Heal(int amount)
        {
            if (IsDead) return;
            Current = Mathf.Min(max, Current + amount);
            Changed?.Invoke();
        }

        public void Kill()
        {
            if (IsDead) return;
            Current = 0;
            Changed?.Invoke();
            Died?.Invoke();
        }
    }
}
