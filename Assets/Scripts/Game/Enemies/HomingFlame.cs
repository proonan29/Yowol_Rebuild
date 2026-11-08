using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    /// <summary>
    /// Boss skill 3: a flame that chases the player through the air and explodes after its lifetime.
    /// It has its own HP, so the player can destroy it before it goes off (design doc: "무력화 가능").
    /// </summary>
    public class HomingFlame : MonoBehaviour
    {
        BossWitch boss;
        int damage;
        float speed, life, age;
        Color color;
        SpriteRenderer sr;
        Light2D glow;
        bool done;
        Vector3 baseScale;

        public static HomingFlame Spawn(Vector2 pos, BossWitch boss, int damage, float speed, float life, Color color)
        {
            var a = GameAssets.I;
            var go = new GameObject("HomingFlame") { layer = Layers.Enemy };
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.85f;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = a.unlitSprite;
            sr.sortingOrder = Fx.DefaultOrder + 4;
            var anim = go.AddComponent<SpriteAnimator>();
            anim.library = a.fx;
            anim.Play("FireFlame");
            sr.color = new Color(1f, 0.82f, 0.95f);
            var core = new GameObject("Core");
            core.transform.SetParent(go.transform, false);
            core.transform.localScale = Vector3.one * 1.6f;
            var csr = core.AddComponent<SpriteRenderer>();
            csr.sprite = a.softCircle; csr.sharedMaterial = a.unlitSprite; csr.sortingOrder = Fx.DefaultOrder + 3;
            csr.color = new Color(1f, 0.45f, 0.75f, 0.55f);
            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.7f;
            var h = go.AddComponent<Health>();
            h.ResetTo(30);
            h.damageNumberOffset = new Vector2(0f, 0.8f);
            go.AddComponent<HitFlash>();
            var glow = go.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point;
            glow.color = color;
            glow.intensity = 1.3f;
            glow.pointLightOuterRadius = 2.4f;

            var hf = go.AddComponent<HomingFlame>();
            hf.boss = boss; hf.damage = damage; hf.speed = speed; hf.life = life; hf.color = color;
            hf.sr = sr; hf.glow = glow; hf.baseScale = go.transform.localScale;
            h.Died += () => hf.Explode(false);
            Fx.Play("CrossWave", pos, false, 0.5f, 0f, color);
            return hf;
        }

        void Update()
        {
            if (done) return;
            age += Time.deltaTime;
            var pc = PlayerController.Instance;
            if (pc != null && !pc.IsDead)
            {
                Vector3 target = pc.transform.position + Vector3.up * 1.3f;
                float s = speed * (age < 0.5f ? 0.3f : 1f);
                transform.position = Vector3.MoveTowards(transform.position, target, s * Time.deltaTime);
            }
            float left = life - age;
            if (left < 1.2f)
            {
                float k = Mathf.PingPong(age * (left < 0.5f ? 16f : 8f), 1f);
                sr.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.45f), k);
                transform.localScale = baseScale * (1f + 0.25f * k);
                glow.intensity = 1.3f + 1.5f * k;
            }
            if (age >= life) Explode(true);
        }

        public void Fizzle() => Explode(false);

        void Explode(bool harmful)
        {
            if (done) return;
            done = true;
            Vector3 p = transform.position;
            Fx.Play("FireBoom", p + Vector3.down * 0.7f, false, harmful ? 0.7f : 0.4f);
            Fx.Flash(p, color, harmful ? 5f : 3f, harmful ? 2.5f : 1.2f, 0.3f);
            AudioHub.Sfx(GameAssets.I.sfxExplosion, harmful ? 0.7f : 0.4f, 0.2f);
            if (harmful)
            {
                CameraRig.Shake(0.25f, 0.2f);
                var pc = PlayerController.Instance;
                if (pc != null && !pc.IsDead && Vector2.Distance(pc.transform.position + Vector3.up * 0.9f, p) < 1.8f)
                    pc.Health.TakeDamage(new DamageInfo { amount = damage, origin = p, knockback = 5f, heavy = true,
                                                          source = boss != null ? boss.gameObject : gameObject });
            }
            if (boss != null) boss.ForgetFlame(this);
            Destroy(gameObject);
        }
    }
}
