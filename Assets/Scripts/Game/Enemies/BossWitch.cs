using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    /// <summary>
    /// "Soul Witch" boss. Floats over the arena and cycles through patterns:
    /// soul volley, flame pillars, swoop dash and (phase 2) reaper summons.
    /// Phase 2 starts at 50% HP: faster, more projectiles.
    /// </summary>
    public class BossWitch : EnemyBase
    {
        [Header("Arena")]
        public float arenaMinX = -8f;
        public float arenaMaxX = 8f;
        public float groundY = 0f;
        public float hoverHeight = 2.6f;

        [Header("Tuning")]
        public string bossName = "혼령의 마녀";
        public float moveSpeed = 2.4f;
        public float patternGap = 1.6f;
        public int orbDamage = 14;
        public int pillarDamage = 20;
        public int dashDamage = 18;
        public Color soulColor = new Color(0.62f, 0.4f, 1f);

        public bool Phase2 { get; private set; }
        public bool Started { get; private set; }
        public Health HealthComponent => health;

        Light2D aura;
        float hoverPhase;
        float targetX;
        bool busy;
        Coroutine brain;
        readonly List<GameObject> summons = new List<GameObject>();

        public event System.Action Defeated;

        protected override void Awake()
        {
            base.Awake();
            knockbackResist = 1f;
            rb.gravityScale = 0f;
            aura = GetComponentInChildren<Light2D>();
            targetX = transform.position.x;
            health.damageNumberOffset = new Vector2(0f, 3.4f);
        }

        public void Begin()
        {
            if (Started) return;
            Started = true;
            brain = StartCoroutine(Brain());
        }

        void Update()
        {
            if (dead) return;
            hoverPhase += Time.deltaTime;
            if (aura != null) aura.intensity = (Phase2 ? 1.6f : 1.1f) + Mathf.Sin(hoverPhase * 3f) * 0.25f;
            if (!busy && Player != null && Started) FaceToward(Player.position);
        }

        void FixedUpdate()
        {
            if (dead || busy) return;
            float y = groundY + hoverHeight + Mathf.Sin(hoverPhase * 1.6f) * 0.35f;
            float x = Mathf.MoveTowards(transform.position.x, targetX, moveSpeed * (Phase2 ? 1.4f : 1f) * Time.fixedDeltaTime);
            rb.MovePosition(new Vector2(x, Mathf.Lerp(transform.position.y, y, 0.08f)));
            ContactDamage(new Vector2(1.6f, 2.6f), new Vector2(0f, 0f));
        }

        IEnumerator Brain()
        {
            anim.Play("Boss_Float");
            yield return new WaitForSeconds(1.0f);
            int last = -1;
            while (!dead)
            {
                if (!Phase2 && health.Ratio <= 0.5f) yield return EnterPhase2();
                PickPosition();
                yield return new WaitForSeconds(patternGap * (Phase2 ? 0.65f : 1f));
                if (dead || !PlayerAlive) { yield return null; continue; }

                int pick;
                do
                {
                    float r = Random.value;
                    if (Phase2 && r < 0.22f && AliveSummons() < 2) pick = 3;
                    else if (r < 0.45f) pick = 0;
                    else if (r < 0.75f) pick = 1;
                    else pick = 2;
                } while (pick == last && Random.value < 0.6f);
                last = pick;

                busy = true;
                switch (pick)
                {
                    case 0: yield return SoulVolley(); break;
                    case 1: yield return FlamePillars(); break;
                    case 2: yield return SwoopDash(); break;
                    case 3: yield return Summon(); break;
                }
                busy = false;
                anim.Play("Boss_Float");
            }
        }

        void PickPosition()
        {
            if (Player == null) return;
            float px = Player.position.x;
            float side = Random.value > 0.5f ? 1f : -1f;
            float x = px + side * Random.Range(3.5f, 5.5f);
            if (x < arenaMinX + 1.5f || x > arenaMaxX - 1.5f) x = px - side * Random.Range(3.5f, 5.5f);
            targetX = Mathf.Clamp(x, arenaMinX + 1.5f, arenaMaxX - 1.5f);
        }

        IEnumerator EnterPhase2()
        {
            Phase2 = true;
            busy = true;
            GameUI.Instance?.ShowWarning("혼령의 분노 - 마녀가 각성합니다");
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 1f);
            Fx.Flash(transform.position, soulColor, 12f, 3f, 0.8f);
            CameraRig.Shake(0.5f, 0.6f);
            anim.Play("Boss_Cast", true);
            yield return new WaitForSeconds(1.2f);
            busy = false;
        }

        // ---------------------------------------------------------------- patterns

        IEnumerator SoulVolley()
        {
            anim.Play("Boss_Cast", true);
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 0.8f);
            int count = Phase2 ? 5 : 3;
            var orbs = new List<Transform>();
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.PI * (0.15f + 0.7f * i / Mathf.Max(1, count - 1));
                Vector2 pos = (Vector2)transform.position + new Vector2(Mathf.Cos(a) * 2.2f, Mathf.Sin(a) * 1.6f + 0.6f);
                orbs.Add(SpawnOrb(pos).transform);
                yield return new WaitForSeconds(0.12f);
            }
            yield return new WaitForSeconds(0.45f);
            foreach (var o in orbs)
            {
                if (o == null) continue;
                var p = o.GetComponent<Projectile>();
                if (Player != null)
                {
                    Vector2 dir = ((Vector2)Player.position + Vector2.up - (Vector2)o.position).normalized;
                    p.velocity = dir * (Phase2 ? 9f : 7f);
                    p.homingTarget = Player;
                    p.turnRateDegrees = Phase2 ? 50f : 35f;
                }
                p.lifetime = 4f;
                AudioHub.Sfx(GameAssets.I.sfxBossBeam, 0.4f, 0.2f);
                yield return new WaitForSeconds(0.15f);
            }
            yield return new WaitForSeconds(0.4f);
        }

        GameObject SpawnOrb(Vector2 pos)
        {
            var go = new GameObject("SoulOrb");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = GameAssets.I.softCircle;
            sr.sharedMaterial = GameAssets.I.unlitSprite;
            sr.color = new Color(soulColor.r, soulColor.g, soulColor.b, 0.95f);
            sr.sortingOrder = Fx.DefaultOrder + 5;
            go.transform.localScale = Vector3.one * 0.9f;
            var core = new GameObject("Core");
            core.transform.SetParent(go.transform, false);
            core.transform.localScale = Vector3.one * 0.45f;
            var csr = core.AddComponent<SpriteRenderer>();
            csr.sprite = GameAssets.I.softCircle;
            csr.sharedMaterial = GameAssets.I.unlitSprite;
            csr.sortingOrder = Fx.DefaultOrder + 6;
            var light = go.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = soulColor;
            light.intensity = 1.2f;
            light.pointLightOuterRadius = 2.2f;
            var p = go.AddComponent<Projectile>();
            p.velocity = Vector2.zero;
            p.lifetime = 99f;
            p.damage = orbDamage;
            p.targetMask = Layers.PlayerMask;
            p.hitSize = new Vector2(0.6f, 0.6f);
            p.knockback = 4f;
            p.hitFx = "CrossWave";
            p.hitFxColor = soulColor;
            p.hitFxScale = 0.5f;
            p.owner = gameObject;
            go.AddComponent<Bob>().amplitude = 0.05f;
            Fx.Play("CrossWave", pos, false, 0.4f, 0f, soulColor);
            return go;
        }

        IEnumerator FlamePillars()
        {
            anim.Play("Boss_Cast", true);
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 0.8f);
            int count = Phase2 ? 5 : 3;
            float px = Player != null ? Player.position.x : transform.position.x;
            var xs = new List<float>();
            for (int i = 0; i < count; i++)
            {
                float x = px + (i - (count - 1) / 2f) * (Phase2 ? 2.4f : 3.0f) + Random.Range(-0.4f, 0.4f);
                xs.Add(Mathf.Clamp(x, arenaMinX + 0.5f, arenaMaxX - 0.5f));
            }
            var marks = new List<Fx>();
            foreach (var x in xs)
            {
                var m = Fx.Play("MintRing", new Vector3(x, groundY + 0.05f), false, 0.32f, 0f, new Color(1f, 0.35f, 0.6f), 5, null, 1.0f);
                if (m != null) marks.Add(m);
            }
            yield return new WaitForSeconds(Phase2 ? 0.75f : 0.95f);
            AudioHub.Sfx(GameAssets.I.sfxExplosion, 0.8f);
            CameraRig.Shake(0.3f, 0.25f);
            var already = new HashSet<Health>();
            foreach (var x in xs)
            {
                Vector2 c = new Vector2(x, groundY + 2f);
                Fx.Play("SwordFlurry", c, false, 0.9f, 90f, soulColor, Fx.DefaultOrder, null, 0.55f);
                Fx.Play("CrossWave", new Vector2(x, groundY + 0.6f), false, 0.9f, 0f, soulColor);
                Fx.Flash(c, soulColor, 4f, 2f, 0.4f);
                var info = new DamageInfo { amount = pillarDamage, origin = new Vector2(x, groundY), knockback = 3f, heavy = true, source = gameObject };
                Combat.HitBox(c, new Vector2(1.3f, 4f), Layers.PlayerMask, info, already);
            }
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator SwoopDash()
        {
            if (Player == null) yield break;
            // Telegraph: rise, glow, then sweep across at player height.
            float dir = Player.position.x > transform.position.x ? 1f : -1f;
            Face(dir > 0);
            anim.Play("Boss_Cast", true);
            Vector2 start = transform.position;
            Vector2 up = new Vector2(dir > 0 ? arenaMinX + 1f : arenaMaxX - 1f, groundY + hoverHeight + 1.2f);
            for (float t = 0; t < 0.5f; t += Time.deltaTime)
            {
                rb.MovePosition(Vector2.Lerp(start, up, t / 0.5f));
                yield return null;
            }
            for (int i = 0; i < 3; i++)
            {
                flash?.SetBaseColor(new Color(1f, 0.6f, 0.9f));
                yield return new WaitForSeconds(0.1f);
                flash?.SetBaseColor(Color.white);
                yield return new WaitForSeconds(0.1f);
            }
            AudioHub.Sfx(GameAssets.I.sfxHeavySwing, 1f);
            float y = groundY + 1.3f;
            float endX = dir > 0 ? arenaMaxX - 1f : arenaMinX + 1f;
            Vector2 from = new Vector2(transform.position.x, y);
            var already = new HashSet<Health>();
            float speed = Phase2 ? 17f : 13f;
            float dur = Mathf.Abs(endX - from.x) / speed;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                var p = Vector2.Lerp(from, new Vector2(endX, y), t / dur);
                rb.MovePosition(p);
                var info = new DamageInfo { amount = dashDamage, origin = p - new Vector2(dir, 0), knockback = 7f, heavy = true, source = gameObject };
                Combat.HitBox(p, new Vector2(2.2f, 2.4f), Layers.PlayerMask, info, already);
                if (Random.value < 0.3f) Fx.Play("SwordSpark", p + Random.insideUnitCircle, false, 0.5f, Random.Range(0, 360f), soulColor);
                yield return null;
            }
            targetX = endX;
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Summon()
        {
            anim.Play("Boss_Cast", true);
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 1f);
            GameUI.Instance?.ShowWarning("마녀가 사신을 불러냅니다");
            yield return new WaitForSeconds(0.6f);
            var prefab = GameAssets.I.reaperPrefab;
            if (prefab != null)
            {
                foreach (float x in new[] { arenaMinX + 1.5f, arenaMaxX - 1.5f })
                {
                    var pos = new Vector3(x, groundY + 0.1f);
                    Fx.Play("CrossWave", pos + Vector3.up, false, 1.2f, 0f, soulColor);
                    Fx.Flash(pos + Vector3.up, soulColor, 4f, 2f, 0.4f);
                    var r = Instantiate(prefab, pos, Quaternion.identity);
                    var e = r.GetComponent<EnemyBase>();
                    if (e != null) e.countsForStageClear = false;
                    var re = r.GetComponent<ReaperEnemy>();
                    if (re != null) { re.detectRange = 30f; re.maxHp = 90; }
                    summons.Add(r);
                }
            }
            yield return new WaitForSeconds(0.8f);
        }

        int AliveSummons()
        {
            summons.RemoveAll(s => s == null);
            return summons.Count;
        }

        // ---------------------------------------------------------------- damage & death

        protected override void OnDamaged(DamageInfo info)
        {
            AudioHub.Sfx(GameAssets.I.sfxBossHurt, 0.35f, 0.2f);
        }

        protected override void OnDied()
        {
            if (dead) return;
            base.OnDied();
            foreach (var s in summons) if (s != null) s.GetComponent<Health>()?.Kill();
            Defeated?.Invoke();
        }

        protected override IEnumerator DeathRoutine()
        {
            if (brain != null) StopCoroutine(brain);
            busy = true;
            rb.linearVelocity = Vector2.zero;
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            HitStop.SlowMotion(0.3f, 1.2f);
            CameraRig.Shake(0.6f, 1.2f);
            AudioHub.Sfx(GameAssets.I.sfxExplosion, 1f);
            anim.Play("Boss_Death", true);
            Fx.Flash(transform.position, soulColor, 14f, 3.5f, 1.5f);
            float len = anim.LengthOf("Boss_Death");
            yield return new WaitForSeconds(len + 0.3f);
            if (aura != null) aura.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
