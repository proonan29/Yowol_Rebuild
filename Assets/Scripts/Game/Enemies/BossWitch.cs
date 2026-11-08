using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Yowol
{
    /// <summary>
    /// Chapter 1 boss "혼돈" (사흉). Patterns follow the design document plus the original team's chain idea:
    ///  1. Laser rain   - telegraph lines over the map (always one on the player), then vertical lasers.
    ///  2. Fire throw   - three flames thrown forward; each landing spot burns for 3 s.
    ///  3. Homing flame - flames chase the player in the air and explode; they can be destroyed by attacks.
    ///  4. Chain strike - warning line, then a chain shoots through the player's position. The last chain of a
    ///                    barrage is a purple skull chain that stays embedded: hitting it fills the groggy gauge
    ///                    (3 hits, then 5). Groggy = the boss falls to the ground and takes extra damage.
    ///  5. Altar gimmick (after the 2nd groggy or at 30% HP): destroy both altars before the timer ends,
    ///                    otherwise "탐식" kills the player instantly.
    /// Phase 2 (50% HP): faster, more lasers / flames / chains.
    /// </summary>
    public class BossWitch : EnemyBase
    {
        [Header("Arena")]
        public float arenaMinX = -8f;
        public float arenaMaxX = 8f;
        public float groundY = 0f;
        public float hoverHeight = 2.6f;
        [Tooltip("Pivot height above the ground when the boss stands on the floor (groggy).")]
        public float standingHeight = 3.1f;

        [Header("Tuning")]
        public string bossName = "혼돈";
        public float moveSpeed = 2.4f;
        public float patternGap = 1.3f;
        public int laserDamage = 24;
        public int fireHitDamage = 16;
        public int floorTickDamage = 6;
        public int homingDamage = 20;
        public int chainDamage = 14;
        public Color soulColor = new Color(0.62f, 0.4f, 1f);

        [Header("Groggy / gimmick")]
        public int firstGroggyHits = 3;
        public int secondGroggyHits = 5;
        public float groggyDuration = 5f;
        public float groggyDamageMultiplier = 1.5f;
        public float gimmickTime = 15f;
        public int altarHp = 260;

        public bool Phase2 { get; private set; }
        public bool Started { get; private set; }
        public bool Groggy { get; private set; }
        public Health HealthComponent => health;

        public event System.Action Defeated;

        Light2D aura;
        float hoverPhase;
        float targetX;
        bool busy;
        bool gimmickActive, gimmickDone;
        int groggyCount, chainHits;
        Coroutine brain;
        GameObject activeSpecial;
        readonly List<GameObject> transient = new List<GameObject>();
        readonly List<GameObject> summons = new List<GameObject>();
        readonly List<GameObject> altars = new List<GameObject>();
        readonly List<HomingFlame> flames = new List<HomingFlame>();

        int RequiredHits => groggyCount == 0 ? firstGroggyHits : secondGroggyHits;

        protected override void Awake()
        {
            base.Awake();
            knockbackResist = 1f;
            rb.gravityScale = 0f;
            aura = GetComponentInChildren<Light2D>();
            targetX = transform.position.x;
            health.damageNumberOffset = new Vector2(0f, 0.6f);
            health.Intercept = (ref DamageInfo info) =>
            {
                if (Groggy) { info.amount = Mathf.RoundToInt(info.amount * groggyDamageMultiplier); info.heavy = true; }
                return false;
            };
        }

        public void Begin()
        {
            if (Started) return;
            Started = true;
            brain = StartCoroutine(Brain(true));
        }

        void Update()
        {
            if (dead) return;
            hoverPhase += Time.deltaTime;
            if (aura != null)
                aura.intensity = gimmickActive ? 2.4f + Mathf.Sin(hoverPhase * 9f) * 0.6f
                               : (Phase2 ? 1.6f : 1.1f) + Mathf.Sin(hoverPhase * 3f) * 0.25f;
            if (!busy && !Groggy && Player != null && Started) FaceToward(Player.position);
        }

        void FixedUpdate()
        {
            if (dead || busy || Groggy || gimmickActive) return;
            float y = groundY + hoverHeight + Mathf.Sin(hoverPhase * 1.6f) * 0.35f;
            float x = Mathf.MoveTowards(transform.position.x, targetX, moveSpeed * (Phase2 ? 1.4f : 1f) * Time.fixedDeltaTime);
            rb.MovePosition(new Vector2(x, Mathf.Lerp(transform.position.y, y, 0.08f)));
            ContactDamage(new Vector2(1.6f, 2.6f), new Vector2(0f, -1.3f));
        }

        // ================================================================ brain

        IEnumerator Brain(bool intro)
        {
            anim.Play("Boss_Float");
            if (intro) yield return new WaitForSeconds(1.0f);
            int last = -1;
            while (!dead)
            {
                if (!Phase2 && health.Ratio <= 0.5f) yield return EnterPhase2();
                if (!gimmickDone && (groggyCount >= 2 || health.Ratio <= 0.3f))
                {
                    busy = true;
                    yield return AltarGimmick();
                    busy = false;
                    continue;
                }
                PickPosition();
                yield return new WaitForSeconds(patternGap * (Phase2 ? 0.7f : 1f));
                if (dead || !PlayerAlive) { yield return null; continue; }

                int pick;
                do
                {
                    float r = Random.value;
                    if (r < 0.27f) pick = 0;
                    else if (r < 0.5f) pick = 1;
                    else if (r < 0.68f && flames.Count < 3) pick = 2;
                    else pick = 3;
                } while (pick == last && Random.value < 0.7f);
                last = pick;

                busy = true;
                switch (pick)
                {
                    case 0: yield return LaserRain(); break;
                    case 1: yield return FireThrow(); break;
                    case 2: yield return HomingFlames(); break;
                    case 3: yield return ChainBarrage(); break;
                }
                busy = false;
                anim.Play("Boss_Float");
            }
        }

        void Interrupt()
        {
            if (brain != null) StopCoroutine(brain);
            brain = null;
            foreach (var t in transient) if (t != null) Destroy(t);
            transient.Clear();
            busy = false;
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
            GameUI.Instance?.ShowWarning("탐식의 힘이 깨어납니다 - 혼돈이 각성합니다");
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 1f);
            Fx.Flash(transform.position, soulColor, 12f, 3f, 0.8f);
            CameraRig.Shake(0.5f, 0.6f);
            anim.Play("Boss_Cast", true);
            yield return new WaitForSeconds(1.2f);
            busy = false;
        }

        // ================================================================ helpers

        Vector2 PlayerCenter => Player != null ? (Vector2)Player.position + Vector2.up * 0.9f : (Vector2)transform.position;

        bool HitPlayer(int amount, Vector2 origin, float knockback, bool heavy)
        {
            var pc = PlayerController.Instance;
            if (pc == null || pc.IsDead) return false;
            return pc.Health.TakeDamage(new DamageInfo { amount = amount, origin = origin, knockback = knockback, heavy = heavy, source = gameObject });
        }

        static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t);
        }

        Vector2 Hand => (Vector2)transform.position + new Vector2(FacingRight ? 0.9f : -0.9f, -1.1f);

        // ================================================================ 1. laser rain

        IEnumerator LaserRain()
        {
            anim.Play("Boss_Cast", true);
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 0.8f);
            int waves = Phase2 ? 2 : 1;
            float spacing = Phase2 ? 3.0f : 3.6f;
            float offset = 0f;
            for (int w = 0; w < waves; w++)
            {
                var xs = new List<float>();
                float px = Mathf.Clamp(PlayerCenter.x, arenaMinX + 0.5f, arenaMaxX - 0.5f) + offset;
                for (float x = px; x <= arenaMaxX - 0.3f; x += spacing) xs.Add(x);
                for (float x = px - spacing; x >= arenaMinX + 0.3f; x -= spacing) xs.Add(x);
                float tele = Phase2 ? 0.75f : 0.95f;
                foreach (var x in xs)
                {
                    var t = Fx.Play("LaserTelegraph", new Vector3(x, groundY), false, 1.12f, 0f, new Color(1f, 0.75f, 1f, 0.9f), Fx.DefaultOrder - 5, null, tele);
                    if (t != null) transient.Add(t.gameObject);
                }
                yield return new WaitForSeconds(tele);
                AudioHub.Sfx(GameAssets.I.sfxBossBeam, 1f);
                CameraRig.Shake(0.3f, 0.35f);
                foreach (var x in xs) StartCoroutine(LaserColumn(x));
                yield return new WaitForSeconds(0.55f);
                offset = spacing * 0.5f;
            }
            yield return new WaitForSeconds(0.6f);
        }

        IEnumerator LaserColumn(float x)
        {
            var fx = Fx.Play("LaserStart", new Vector3(x, groundY), false, 1f, 0f, null, Fx.DefaultOrder, null, 1.5f);
            if (fx == null) yield break;
            fx.transform.localScale = new Vector3(0.6f, 1.08f, 1f);
            var a = fx.GetComponent<SpriteAnimator>();
            var glow = fx.gameObject.AddComponent<Light2D>();
            glow.lightType = Light2D.LightType.Point; glow.color = soulColor; glow.intensity = 1.5f; glow.pointLightOuterRadius = 3.5f;
            yield return new WaitForSeconds(a.LengthOf("LaserStart") * 0.6f);
            a.Play("LaserLoop", true);
            bool hit = false;
            for (float t = 0; t < 0.55f; t += Time.deltaTime)
            {
                var p = PlayerCenter;
                if (!hit && Mathf.Abs(p.x - x) < 0.75f && p.y < groundY + 10f)
                    hit = HitPlayer(laserDamage, new Vector2(x, p.y), 4f, true);
                yield return null;
            }
            if (fx != null) { a.Play("LaserEnd", true); glow.intensity = 0.6f; }
        }

        // ================================================================ 2. fire throw

        IEnumerator FireThrow()
        {
            if (Player != null) FaceToward(Player.position);
            anim.Play("Boss_Cast", true);
            float dir = FacingRight ? 1f : -1f;
            float px = PlayerCenter.x;
            for (int i = 0; i < 3; i++)
            {
                float tx = Mathf.Clamp(px + dir * (i - 1) * 2.4f + Random.Range(-0.4f, 0.4f), arenaMinX + 0.8f, arenaMaxX - 0.8f);
                StartCoroutine(ThrownFlame(Hand, tx));
                AudioHub.Sfx(GameAssets.I.sfxSwing, 0.6f, 0.2f);
                yield return new WaitForSeconds(Phase2 ? 0.25f : 0.35f);
            }
            yield return new WaitForSeconds(0.8f);
        }

        IEnumerator ThrownFlame(Vector2 from, float tx)
        {
            var flame = Fx.Play("FireFlame", from, false, 0.75f, 0f, null, Fx.DefaultOrder + 2, null, 5f);
            if (flame == null) yield break;
            Vector2 to = new Vector2(tx, groundY + 0.3f);
            float dur = 0.65f, h = 3f;
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (flame == null) yield break;
                float k = t / dur;
                flame.transform.position = Vector2.Lerp(from, to, k) + Vector2.up * (4f * h * k * (1f - k));
                yield return null;
            }
            if (flame != null) Destroy(flame.gameObject);
            Fx.Play("FireBoom", new Vector3(tx, groundY), false, 0.75f);
            Fx.Play("FireDust", new Vector3(tx, groundY), false, 0.8f);
            Fx.Flash(new Vector3(tx, groundY + 0.8f), soulColor, 4f, 2f, 0.35f);
            AudioHub.Sfx(GameAssets.I.sfxExplosion, 0.6f, 0.2f);
            CameraRig.Shake(0.2f, 0.15f);
            var p = PlayerCenter;
            if (Mathf.Abs(p.x - tx) < 1.3f && p.y < groundY + 2.4f) HitPlayer(fireHitDamage, new Vector2(tx, groundY), 5f, true);
            StartCoroutine(FireFloor(tx));
        }

        IEnumerator FireFloor(float x)
        {
            const float burn = 3f;
            var fx = Fx.Play("FireFloorStart", new Vector3(x, groundY + 0.02f), false, 0.8f, 0f, null, 15, null, burn + 1.1f);
            if (fx == null) yield break;
            var a = fx.GetComponent<SpriteAnimator>();
            var light = fx.gameObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point; light.color = soulColor; light.intensity = 1f; light.pointLightOuterRadius = 2.5f;
            yield return new WaitForSeconds(a.LengthOf("FireFloorStart"));
            if (fx == null) yield break;
            a.Play("FireFloorLoop", true);
            float tick = 0f;
            for (float t = 0; t < burn; t += Time.deltaTime)
            {
                tick -= Time.deltaTime;
                var p = PlayerCenter;
                if (tick <= 0f && Mathf.Abs(p.x - x) < 1.15f && p.y < groundY + 1.4f)
                {
                    var pc = PlayerController.Instance;
                    if (pc != null && !pc.IsDead)
                        pc.Health.TakeDamage(new DamageInfo { amount = floorTickDamage, origin = new Vector2(x, groundY), knockback = 1.5f, source = gameObject });
                    tick = 0.5f;
                }
                yield return null;
            }
            if (fx != null) { a.Play("FireFloorEnd", true); light.intensity = 0.4f; }
        }

        // ================================================================ 3. homing flames

        IEnumerator HomingFlames()
        {
            anim.Play("Boss_Cast", true);
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 0.8f);
            int count = Phase2 ? 3 : 2;
            for (int i = 0; i < count; i++)
            {
                float a = Mathf.PI * (0.2f + 0.6f * i / Mathf.Max(1, count - 1));
                Vector2 pos = (Vector2)transform.position + new Vector2(Mathf.Cos(a) * 2f, Mathf.Sin(a) * 1.2f - 0.4f);
                var hf = HomingFlame.Spawn(pos, this, homingDamage, Phase2 ? 3.3f : 2.6f, Phase2 ? 4f : 4.5f, soulColor);
                flames.Add(hf);
                yield return new WaitForSeconds(0.25f);
            }
            flames.RemoveAll(f => f == null);
            yield return new WaitForSeconds(0.7f);
        }

        internal void ForgetFlame(HomingFlame f) => flames.Remove(f);

        // ================================================================ 4. chain strike

        IEnumerator ChainBarrage()
        {
            anim.Play("Boss_Cast", true);
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 0.7f);
            int n = Phase2 ? 4 : 3;
            for (int i = 0; i < n; i++)
            {
                if (!PlayerAlive) break;
                Vector2[] origins =
                {
                    new Vector2(arenaMinX + 0.6f, groundY + 9.5f),
                    new Vector2(arenaMaxX - 0.6f, groundY + 9.5f),
                    new Vector2(Mathf.Lerp(arenaMinX, arenaMaxX, Random.Range(0.25f, 0.75f)), groundY + 10.5f),
                    Hand,
                };
                Vector2 origin = origins[Random.Range(0, origins.Length)];
                Vector2 target = PlayerCenter;
                target.y = Mathf.Max(target.y, groundY + 0.5f);
                bool special = i == n - 1 && activeSpecial == null && !Groggy;
                yield return ChainStrike(origin, target, special);
                yield return new WaitForSeconds(Phase2 ? 0.15f : 0.3f);
            }
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator ChainStrike(Vector2 origin, Vector2 target, bool special)
        {
            Vector2 dir = (target - origin).normalized;
            float len = dir.y < -0.05f ? (origin.y - groundY) / -dir.y : 30f;
            len = Mathf.Min(len, 30f);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 end = origin + dir * len;

            // warning line
            var warn = new GameObject("ChainWarning");
            transient.Add(warn);
            warn.transform.position = origin + dir * (len * 0.5f);
            warn.transform.rotation = Quaternion.Euler(0, 0, angle);
            var wsr = warn.AddComponent<SpriteRenderer>();
            wsr.sprite = GameAssets.I.white;
            wsr.sharedMaterial = GameAssets.I.unlitSprite;
            wsr.sortingOrder = Fx.DefaultOrder - 3;
            Color wc = special ? new Color(0.75f, 0.35f, 1f) : new Color(1f, 0.15f, 0.25f);
            float tele = Phase2 ? 0.42f : 0.55f;
            for (float t = 0; t < tele; t += Time.deltaTime)
            {
                float pulse = 0.35f + 0.45f * Mathf.PingPong(t * 8f, 1f);
                wsr.color = new Color(wc.r, wc.g, wc.b, pulse);
                warn.transform.localScale = new Vector3(len, Mathf.Lerp(0.03f, 0.09f, t / tele), 1f);
                yield return null;
            }
            Destroy(warn);
            transient.Remove(warn);

            // chain
            var chain = BuildChain(origin, angle, special, out var links, out var tip, out var col);
            transient.Add(chain);
            AudioHub.Sfx(GameAssets.I.sfxHeavySwing, 0.8f, 0.15f);
            bool hit = false;
            const float extend = 0.13f;
            for (float t = 0; t <= extend; t += Time.deltaTime)
            {
                SetChainLength(links, tip, col, len * Mathf.Clamp01(t / extend));
                Vector2 cur = origin + dir * (len * Mathf.Clamp01(t / extend));
                if (!hit && SegmentDistance(PlayerCenter, origin, cur) < 0.5f)
                    hit = HitPlayer(chainDamage, cur - dir, 5f, true);
                yield return null;
            }
            SetChainLength(links, tip, col, len);
            Fx.Play("FireDust", new Vector3(end.x, groundY), false, 0.7f);
            Fx.Play("SwordSpark", end, false, 0.6f, Random.Range(0f, 360f), special ? soulColor : Color.white);
            CameraRig.Shake(special ? 0.35f : 0.22f, 0.18f);
            AudioHub.Sfx(GameAssets.I.sfxExplosion, 0.45f, 0.2f);

            if (special)
            {
                transient.Remove(chain);
                activeSpecial = chain;
                StartCoroutine(SpecialChainLife(chain, links, tip, col, len));
                yield break;
            }
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                if (!hit && SegmentDistance(PlayerCenter, origin, end) < 0.45f) hit = HitPlayer(chainDamage, end - dir, 4f, false);
                yield return null;
            }
            yield return RetractChain(chain, links, tip, col, len, 0.15f);
            transient.Remove(chain);
        }

        GameObject BuildChain(Vector2 origin, float angle, bool special, out SpriteRenderer links, out Transform tip, out BoxCollider2D col)
        {
            var a = GameAssets.I;
            var root = new GameObject(special ? "SkullChain" : "Chain");
            root.transform.position = origin;
            root.transform.rotation = Quaternion.Euler(0, 0, angle);
            var lgo = new GameObject("Links");
            lgo.transform.SetParent(root.transform, false);
            links = lgo.AddComponent<SpriteRenderer>();
            links.sprite = special ? a.specialChain : a.chainLink;
            links.sharedMaterial = special ? a.unlitSprite : a.litSprite;
            links.drawMode = SpriteDrawMode.Tiled;
            links.tileMode = SpriteTileMode.Continuous;
            links.sortingOrder = Fx.DefaultOrder - 2;
            float thick = links.sprite != null ? links.sprite.bounds.size.y : 0.8f;
            links.size = new Vector2(0.01f, thick);
            tip = null;
            if (!special && a.chainTip != null)
            {
                var tg = new GameObject("Tip");
                tg.transform.SetParent(root.transform, false);
                var tsr = tg.AddComponent<SpriteRenderer>();
                tsr.sprite = a.chainTip; tsr.flipX = true; tsr.sharedMaterial = a.litSprite; tsr.sortingOrder = Fx.DefaultOrder - 1;
                tip = tg.transform;
            }
            col = null;
            if (special)
            {
                root.layer = Layers.Enemy;
                lgo.layer = Layers.Enemy;
                col = lgo.AddComponent<BoxCollider2D>();
                col.isTrigger = true;
                var h = root.AddComponent<Health>();
                h.showDamageNumbers = false;
                h.ResetTo(99999);
                h.invulnerableAfterHit = 0.15f;
                h.Damaged += info => OnSpecialChainHit();
                var light = lgo.AddComponent<Light2D>();
                light.lightType = Light2D.LightType.Point; light.color = soulColor; light.intensity = 1.2f; light.pointLightOuterRadius = 3f;
            }
            return root;
        }

        static void SetChainLength(SpriteRenderer links, Transform tip, BoxCollider2D col, float len)
        {
            if (links == null) return;
            links.size = new Vector2(Mathf.Max(0.01f, len), links.size.y);
            if (tip != null) tip.localPosition = new Vector3(len, 0f, 0f);
            if (col != null) { col.size = new Vector2(Mathf.Max(0.1f, len * 0.9f), 0.9f); col.offset = new Vector2(len * 0.5f, 0f); }
            var light = links.GetComponent<Light2D>();
            if (light != null) light.transform.localPosition = Vector3.zero;
        }

        IEnumerator RetractChain(GameObject chain, SpriteRenderer links, Transform tip, BoxCollider2D col, float len, float dur)
        {
            for (float t = 0; t < dur; t += Time.deltaTime)
            {
                if (chain == null) yield break;
                SetChainLength(links, tip, col, len * (1f - t / dur));
                yield return null;
            }
            if (chain != null) Destroy(chain);
        }

        IEnumerator SpecialChainLife(GameObject chain, SpriteRenderer links, Transform tip, BoxCollider2D col, float len)
        {
            GameUI.Instance?.SetCounter($"해골 사슬을 공격하세요  {chainHits} / {RequiredHits}");
            float life = Phase2 ? 3f : 3.5f;
            for (float t = 0; t < life; t += Time.deltaTime)
            {
                if (chain == null || chain != activeSpecial) yield break;
                if (links != null)
                {
                    float k = 0.75f + 0.25f * Mathf.Sin(Time.time * 10f);
                    links.color = Color.Lerp(Color.white, soulColor, (t > life - 1f ? Mathf.PingPong(t * 6f, 1f) : 0.2f)) * k + new Color(0, 0, 0, 1f - k);
                }
                yield return null;
            }
            if (chain == activeSpecial) activeSpecial = null;
            GameUI.Instance?.SetCounter("");
            yield return RetractChain(chain, links, tip, col, len, 0.2f);
        }

        void OnSpecialChainHit()
        {
            if (dead || Groggy || gimmickActive) return;
            chainHits++;
            Fx.Play("SwordHit", PlayerCenter + new Vector2(PlayerController.Instance != null ? PlayerController.Instance.FacingSign * 1.2f : 0f, 0f),
                    false, 0.6f, Random.Range(0f, 360f), soulColor);
            AudioHub.Sfx(GameAssets.I.sfxHit, 0.9f);
            HitStop.Do(0.04f);
            GameUI.Instance?.SetCounter($"해골 사슬을 공격하세요  {chainHits} / {RequiredHits}");
            if (chainHits < RequiredHits) return;

            chainHits = 0;
            if (activeSpecial != null) { Fx.Play("CrossWave", activeSpecial.transform.position, false, 1.2f, 0f, soulColor); Destroy(activeSpecial); }
            activeSpecial = null;
            GameUI.Instance?.SetCounter("");
            Interrupt();
            brain = StartCoroutine(GroggyThenResume(groggyDuration, true));
        }

        IEnumerator GroggyThenResume(float duration, bool fromChain)
        {
            yield return GroggyRoutine(duration, fromChain);
            if (!dead) brain = StartCoroutine(Brain(false));
        }

        IEnumerator GroggyRoutine(float duration, bool fromChain)
        {
            Groggy = true;
            busy = true;
            if (fromChain) groggyCount++;
            GameUI.Instance?.ShowWarning(fromChain ? "사슬이 끊어졌습니다 - 혼돈이 무너집니다!" : "제단 파괴! 혼돈이 힘을 잃었습니다!", 1.8f);
            AudioHub.Sfx(GameAssets.I.sfxBossHurt, 1f);
            Fx.Flash(transform.position, Color.white, 10f, 2.5f, 0.5f);
            CameraRig.Shake(0.45f, 0.4f);
            anim.Play("Boss_Death", true);
            anim.speed = 0f;
            anim.SetFrame("Boss_Death", 3);
            Vector2 from = transform.position;
            Vector2 down = new Vector2(from.x, groundY + standingHeight);
            for (float t = 0; t < 0.35f; t += Time.deltaTime)
            {
                rb.MovePosition(Vector2.Lerp(from, down, t / 0.35f * (t / 0.35f)));
                yield return null;
            }
            rb.MovePosition(down);
            Fx.Play("FireDust", new Vector3(down.x, groundY), false, 1.4f);
            flash?.SetBaseColor(new Color(0.75f, 0.75f, 0.85f));
            if (fromChain && Phase2) SummonReapers();
            yield return new WaitForSeconds(duration);
            flash?.SetBaseColor(Color.white);
            anim.speed = 1f;
            anim.Play("Boss_Cast", true);
            Vector2 up = new Vector2(down.x, groundY + hoverHeight);
            for (float t = 0; t < 0.6f; t += Time.deltaTime)
            {
                rb.MovePosition(Vector2.Lerp(down, up, t / 0.6f));
                yield return null;
            }
            Groggy = false;
            busy = false;
        }

        void SummonReapers()
        {
            var prefab = GameAssets.I.reaperPrefab;
            if (prefab == null) return;
            summons.RemoveAll(s => s == null);
            if (summons.Count >= 2) return;
            foreach (float x in new[] { arenaMinX + 1.5f, arenaMaxX - 1.5f })
            {
                var pos = new Vector3(x, groundY + 0.1f);
                Fx.Play("CrossWave", pos + Vector3.up, false, 1.2f, 0f, soulColor);
                var r = Instantiate(prefab, pos, Quaternion.identity);
                var e = r.GetComponent<EnemyBase>();
                if (e != null) e.countsForStageClear = false;
                var re = r.GetComponent<ReaperEnemy>();
                if (re != null) re.detectRange = 30f;
                summons.Add(r);
            }
        }

        // ================================================================ 5. altar gimmick

        IEnumerator AltarGimmick()
        {
            gimmickDone = true;
            gimmickActive = true;
            health.Invulnerable = true;
            if (activeSpecial != null) { Destroy(activeSpecial); activeSpecial = null; }
            GameUI.Instance?.SetCounter("");
            anim.Play("Boss_Cast", true);
            AudioHub.Sfx(GameAssets.I.sfxBossCast, 1f);
            GameUI.Instance?.ShowWarning("혼돈이 막대한 힘을 모읍니다! 제한 시간 안에 제단을 파괴하세요!", 3f);

            Vector2 from = transform.position, center = new Vector2((arenaMinX + arenaMaxX) * 0.5f, groundY + hoverHeight + 1.6f);
            for (float t = 0; t < 1f; t += Time.deltaTime) { rb.MovePosition(Vector2.Lerp(from, center, t)); yield return null; }

            altars.Clear();
            foreach (float x in new[] { arenaMinX + 2.4f, arenaMaxX - 2.4f }) altars.Add(SpawnAltar(new Vector2(x, groundY)));

            float left = gimmickTime, nextLaser = 3f, nextPulse = 0f;
            while (left > 0f && altars.Exists(a => a != null && a.activeSelf))
            {
                left -= Time.deltaTime;
                nextLaser -= Time.deltaTime;
                nextPulse -= Time.deltaTime;
                GameUI.Instance?.SetTimer(left);
                if (nextPulse <= 0f) { Fx.Play("CrossWave", transform.position, false, 1.6f, 0f, soulColor); nextPulse = 0.8f; }
                if (nextLaser <= 0f && PlayerAlive)
                {
                    // light pressure while the player works on the altars
                    float x = PlayerCenter.x;
                    var t = Fx.Play("LaserTelegraph", new Vector3(x, groundY), false, 1.12f, 0f, new Color(1f, 0.75f, 1f, 0.9f), Fx.DefaultOrder - 5, null, 0.8f);
                    StartCoroutine(DelayedLaser(x, 0.8f));
                    nextLaser = Phase2 ? 3.2f : 4f;
                }
                yield return null;
            }
            GameUI.Instance?.SetTimer(-1f);
            gimmickActive = false;

            if (altars.Exists(a => a != null && a.activeSelf))
            {
                // failure: 탐식 - the player is devoured
                GameUI.Instance?.ShowWarning("탐식 - 혼돈이 모든 것을 삼킵니다", 2.5f);
                Fx.Flash(transform.position, soulColor, 30f, 6f, 1.2f);
                CameraRig.Shake(0.9f, 1.2f);
                AudioHub.Sfx(GameAssets.I.sfxExplosion, 1f, 0f);
                for (float x = arenaMinX + 1f; x <= arenaMaxX; x += 2.2f) Fx.Play("FireBoom", new Vector3(x, groundY), false, 1.1f);
                foreach (var a in altars) if (a != null) Destroy(a);
                health.Invulnerable = false;
                yield return new WaitForSeconds(0.3f);
                PlayerController.Instance?.Health.Kill();
                yield break;
            }
            health.Invulnerable = false;
            yield return GroggyRoutine(groggyDuration + 1f, false);
        }

        IEnumerator DelayedLaser(float x, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!dead) StartCoroutine(LaserColumn(x));
        }

        GameObject SpawnAltar(Vector2 groundPoint)
        {
            var a = GameAssets.I;
            var go = new GameObject("Altar") { layer = Layers.Enemy };
            go.transform.position = groundPoint;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = a.altar;
            sr.sharedMaterial = a.litSprite;
            sr.sortingOrder = 8;
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.6f, 1.8f);
            col.offset = new Vector2(0f, 0.9f);
            var h = go.AddComponent<Health>();
            h.ResetTo(altarHp);
            h.damageNumberOffset = new Vector2(0f, 2.2f);
            go.AddComponent<HitFlash>();
            var light = new GameObject("Light");
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0, 1f, 0);
            var l = light.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point; l.color = soulColor; l.intensity = 1.6f; l.pointLightOuterRadius = 3.2f;
            h.Died += () =>
            {
                Fx.Play("FireBoom", go.transform.position, false, 1.1f);
                Fx.Play("FireDust", go.transform.position, false, 1.2f);
                Fx.Flash(go.transform.position + Vector3.up, soulColor, 6f, 3f, 0.5f);
                AudioHub.Sfx(GameAssets.I.sfxExplosion, 1f);
                CameraRig.Shake(0.35f, 0.3f);
                go.SetActive(false);
            };
            Fx.Play("FireDust", groundPoint, false, 1.3f);
            Fx.Flash(groundPoint + Vector2.up, soulColor, 5f, 2f, 0.6f);
            return go;
        }

        // ================================================================ damage & death

        protected override void OnDamaged(DamageInfo info)
        {
            AudioHub.Sfx(GameAssets.I.sfxBossHurt, Groggy ? 0.5f : 0.3f, 0.2f);
        }

        protected override void OnDied()
        {
            if (dead) return;
            base.OnDied();
            Interrupt();
            StopAllCoroutines();
            StartCoroutine(DeathRoutine());
            GameUI.Instance?.SetCounter("");
            GameUI.Instance?.SetTimer(-1f);
            foreach (var s in summons) if (s != null) s.GetComponent<Health>()?.Kill();
            foreach (var f in flames) if (f != null) f.Fizzle();
            foreach (var a in altars) if (a != null) Destroy(a);
            if (activeSpecial != null) Destroy(activeSpecial);
            Defeated?.Invoke();
        }

        protected override IEnumerator DeathRoutine()
        {
            busy = true;
            rb.linearVelocity = Vector2.zero;
            foreach (var c in GetComponentsInChildren<Collider2D>()) c.enabled = false;
            HitStop.SlowMotion(0.3f, 1.2f);
            CameraRig.Shake(0.6f, 1.2f);
            AudioHub.Sfx(GameAssets.I.sfxExplosion, 1f);
            anim.speed = 1f;
            anim.Play("Boss_Death", true);
            Fx.Flash(transform.position, soulColor, 14f, 3.5f, 1.5f);
            float len = anim.LengthOf("Boss_Death");
            yield return new WaitForSeconds(len + 0.3f);
            if (aura != null) aura.enabled = false;
            gameObject.SetActive(false);
        }
    }
}
