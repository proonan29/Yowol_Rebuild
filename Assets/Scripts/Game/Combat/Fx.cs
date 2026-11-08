using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    /// <summary>Spawns sprite-sheet effects from the shared FX library.</summary>
    public class Fx : MonoBehaviour
    {
        public const int DefaultOrder = 60;

        SpriteAnimator anim;
        float killAt = -1f;
        float fadeOut;
        SpriteRenderer sr;
        Color baseColor;

        public static Fx Play(string clip, Vector3 position, bool flipX = false, float scale = 1f,
                              float rotation = 0f, Color? color = null, int order = DefaultOrder,
                              Transform parent = null, float lifetime = -1f, float speed = 1f)
        {
            var assets = GameAssets.I;
            if (assets == null || assets.fx == null || assets.fx.Get(clip) == null)
            {
                Debug.LogWarning($"[Fx] Unknown effect '{clip}'");
                return null;
            }
            var go = new GameObject("FX_" + clip);
            go.transform.SetParent(parent, true);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, 0, rotation);
            go.transform.localScale = new Vector3(flipX ? -scale : scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            if (assets.unlitSprite != null) sr.sharedMaterial = assets.unlitSprite;
            sr.sortingOrder = order;
            sr.color = color ?? Color.white;
            var anim = go.AddComponent<SpriteAnimator>();
            anim.library = assets.fx;
            anim.speed = speed;
            var fx = go.AddComponent<Fx>();
            fx.anim = anim;
            fx.sr = sr;
            fx.baseColor = sr.color;
            anim.Play(clip, true);
            if (lifetime > 0f) { fx.killAt = Time.time + lifetime; fx.fadeOut = Mathf.Min(0.3f, lifetime * 0.3f); }
            else if (anim.CurrentClip.loop) { fx.killAt = Time.time + 2f; fx.fadeOut = 0.3f; }
            return fx;
        }

        /// <summary>Adds a short-lived 2D light flash at a position.</summary>
        public static void Flash(Vector3 position, Color color, float radius = 4f, float intensity = 1.5f, float duration = 0.25f)
        {
            var go = new GameObject("FX_Flash");
            go.transform.position = position;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = color;
            light.pointLightOuterRadius = radius;
            light.pointLightInnerRadius = radius * 0.2f;
            light.intensity = intensity;
            var f = go.AddComponent<LightFade>();
            f.duration = duration;
        }

        public void Stop(float fade = 0.2f)
        {
            killAt = Time.time + fade;
            fadeOut = fade;
        }

        void Update()
        {
            if (anim == null) return;
            if (killAt < 0f)
            {
                if (anim.IsFinished) Destroy(gameObject);
                return;
            }
            float left = killAt - Time.time;
            if (left <= 0f) { Destroy(gameObject); return; }
            if (left < fadeOut && fadeOut > 0f)
            {
                var c = baseColor; c.a *= left / fadeOut; sr.color = c;
            }
        }
    }
}
