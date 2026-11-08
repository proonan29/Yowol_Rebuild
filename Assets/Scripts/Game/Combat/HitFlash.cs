using System.Collections;
using TMPro;
using UnityEngine;

namespace Yowol
{
    /// <summary>Tints sprites when the owner is hit, and blinks during invulnerability.</summary>
    public class HitFlash : MonoBehaviour
    {
        public SpriteRenderer[] renderers;
        public Color flashColor = new Color(1f, 0.35f, 0.35f, 1f);
        public float flashTime = 0.1f;
        public bool blinkWhileInvulnerable;

        Health health;
        Color[] baseColors;
        float flashUntil;

        void Awake()
        {
            health = GetComponent<Health>();
            if (renderers == null || renderers.Length == 0) renderers = GetComponentsInChildren<SpriteRenderer>();
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;
            if (health != null) health.Damaged += _ => flashUntil = Time.time + flashTime;
        }

        public void SetBaseColor(Color c)
        {
            for (int i = 0; i < baseColors.Length; i++) baseColors[i] = c;
        }

        void LateUpdate()
        {
            bool flash = Time.time < flashUntil;
            bool hidden = blinkWhileInvulnerable && health != null && !health.IsDead && health.InHitInvulnerability &&
                          !flash && Mathf.Repeat(Time.time, 0.12f) < 0.06f;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                var c = flash ? flashColor : baseColors[i];
                if (hidden) c.a *= 0.25f;
                renderers[i].color = c;
            }
        }
    }
}
