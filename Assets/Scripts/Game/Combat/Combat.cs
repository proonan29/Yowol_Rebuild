using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yowol
{
    /// <summary>Hit detection helpers and game-feel effects (hitstop).</summary>
    public static class Combat
    {
        static readonly List<Collider2D> results = new List<Collider2D>(32);
        static readonly HashSet<Health> frameSet = new HashSet<Health>();

        static ContactFilter2D Filter(int mask)
        {
            var f = new ContactFilter2D();
            f.SetLayerMask(mask);
            f.useTriggers = true;
            return f;
        }

        /// <summary>Damage every Health in the box. 'already' prevents hitting the same target twice per swing.</summary>
        public static int HitBox(Vector2 center, Vector2 size, int mask, DamageInfo info,
                                 HashSet<Health> already = null, Action<Health, Vector2> onHit = null)
        {
            results.Clear();
            Physics2D.OverlapBox(center, size, 0f, Filter(mask), results);
            return Apply(center, info, already, onHit);
        }

        public static int HitCircle(Vector2 center, float radius, int mask, DamageInfo info,
                                    HashSet<Health> already = null, Action<Health, Vector2> onHit = null)
        {
            results.Clear();
            Physics2D.OverlapCircle(center, radius, Filter(mask), results);
            return Apply(center, info, already, onHit);
        }

        static int Apply(Vector2 center, DamageInfo info, HashSet<Health> already, Action<Health, Vector2> onHit)
        {
            frameSet.Clear();
            int hits = 0;
            foreach (var col in results)
            {
                if (col == null) continue;
                var h = col.GetComponentInParent<Health>();
                if (h == null || h.IsDead || !frameSet.Add(h)) continue;
                if (already != null && already.Contains(h)) continue;
                if (h.TakeDamage(info))
                {
                    already?.Add(h);
                    hits++;
                    onHit?.Invoke(h, col.ClosestPoint(center));
                }
            }
            return hits;
        }

        public static void DebugBox(Vector2 c, Vector2 s, Color col, float dur = 0.2f)
        {
#if UNITY_EDITOR
            Vector2 h = s * 0.5f;
            Debug.DrawLine(c + new Vector2(-h.x, -h.y), c + new Vector2(h.x, -h.y), col, dur);
            Debug.DrawLine(c + new Vector2(h.x, -h.y), c + new Vector2(h.x, h.y), col, dur);
            Debug.DrawLine(c + new Vector2(h.x, h.y), c + new Vector2(-h.x, h.y), col, dur);
            Debug.DrawLine(c + new Vector2(-h.x, h.y), c + new Vector2(-h.x, -h.y), col, dur);
#endif
        }
    }
}
