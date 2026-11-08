using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Yowol
{
    /// <summary>
    /// Weapons, normal attacks, skills (E/Q) and weapon swapping.
    /// While an action runs it owns the animation and tells PlayerController what movement is allowed.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerCombat : MonoBehaviour
    {
        public WeaponData[] weapons = new WeaponData[3];
        public int weaponIndex;
        public float attackBufferTime = 0.18f;

        public WeaponData Weapon => weapons[Mathf.Clamp(weaponIndex, 0, weapons.Length - 1)];

        // Movement contract read by PlayerController.
        public bool OwnsAnimation { get; private set; }
        public bool BlocksMovement { get; private set; }
        public bool BlocksJump { get; private set; }
        public bool BlocksTurning { get; private set; }
        public bool SuspendGravity { get; private set; }
        public float MoveMultiplier { get; private set; } = 1f;
        public float? OverrideVelocityX { get; private set; }
        public bool IsActing => action != null;
        /// <summary>Skills keep going when hit (damage still applies, no stagger).</summary>
        public bool SuperArmor { get; private set; }

        public event System.Action WeaponChanged;
        public event System.Action<int> SkillUsed;

        PlayerController pc;
        Health health;
        SpriteAnimator anim;
        Rigidbody2D rb;
        Coroutine action;
        readonly float[,] readyAt = new float[3, 2];
        int combo;
        float lastAttackEnd = -9f;
        float nextAttackTime;
        float attackPressedAt = -9f;
        bool parryActive;
        bool parrySucceeded;
        Health parryAttacker;
        int parryIncoming;
        readonly List<Fx> loopingFx = new List<Fx>();

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            health = GetComponent<Health>();
            rb = GetComponent<Rigidbody2D>();
            anim = pc.anim;
            weaponIndex = Mathf.Clamp(RunState.WeaponIndex, 0, weapons.Length - 1);
            health.Intercept = ParryIntercept;
        }

        void Update()
        {
            if (pc.IsDead || GameInput.Locked || Time.timeScale == 0f) return;

            if (GameInput.AttackPressed) attackPressedAt = Time.time;
            bool wantAttack = Time.time - attackPressedAt <= attackBufferTime;

            if (action != null) return;

            int sel = GameInput.WeaponSelectPressed;
            if (sel >= 0 && sel != weaponIndex && weapons[sel] != null)
            {
                Begin(SwapRoutine(sel));
                return;
            }
            if (wantAttack && Time.time >= nextAttackTime)
            {
                attackPressedAt = -9f;
                Begin(AttackRoutine());
                return;
            }
            if (GameInput.Skill1Pressed) TrySkill(0);
            else if (GameInput.Skill2Pressed) TrySkill(1);
        }

        // ---------------------------------------------------------------- cooldowns

        public float CooldownRemaining(int skill) => Mathf.Max(0f, readyAt[weaponIndex, skill] - Time.time);

        public float CooldownRatio(int skill)
        {
            float total = skill == 0 ? Weapon.skill1Cooldown : Weapon.skill2Cooldown;
            return total <= 0f ? 0f : Mathf.Clamp01(CooldownRemaining(skill) / total);
        }

        void TrySkill(int skill)
        {
            if (CooldownRemaining(skill) > 0f) return;
            IEnumerator routine = null;
            switch (Weapon.id)
            {
                case WeaponId.Sword: routine = skill == 0 ? SwordFlurry() : SwordMoonSlash(); break;
                case WeaponId.Bow: routine = skill == 0 ? BowArrowRain() : BowMoonBeam(); break;
                case WeaponId.GreatSword: routine = skill == 0 ? GreatSwordParry() : GreatSwordSlam(); break;
            }
            if (routine == null) return;
            readyAt[weaponIndex, skill] = Time.time + (skill == 0 ? Weapon.skill1Cooldown : Weapon.skill2Cooldown);
            SkillUsed?.Invoke(skill);
            SuperArmor = true;
            AudioHub.Sfx(GameAssets.I.sfxSkill, 0.7f);
            Begin(routine);
        }

        // ---------------------------------------------------------------- action plumbing

        void Begin(IEnumerator routine) => action = StartCoroutine(Run(routine));

        IEnumerator Run(IEnumerator routine)
        {
            yield return routine;
            End();
        }

        void End()
        {
            action = null;
            OwnsAnimation = false;
            BlocksMovement = BlocksJump = BlocksTurning = SuspendGravity = false;
            SuperArmor = false;
            MoveMultiplier = 1f;
            OverrideVelocityX = null;
            parryActive = false;
            health.Invulnerable = false;
        }

        /// <summary>Called when the player is hit or dies.</summary>
        public void Interrupt()
        {
            if (action != null) StopCoroutine(action);
            foreach (var fx in loopingFx) if (fx != null) fx.Stop(0.15f);
            loopingFx.Clear();
            End();
        }

        Vector2 Front(float x, float y) => (Vector2)transform.position + new Vector2(pc.FacingSign * x, y);

        static WaitForSeconds Wait(float s) => new WaitForSeconds(s);

        float TimeToFrame(string clip, int frame)
        {
            var c = anim.library.Get(clip);
            return c != null ? c.TimeUntilFrame(frame) : 0.1f;
        }

        float ClipLength(string clip) => anim.LengthOf(clip);

        void OnEnemyHit(Health target, Vector2 point, bool heavy)
        {
            Fx.Play("SwordHit", point, Random.value > 0.5f, heavy ? 0.9f : 0.6f, Random.Range(0f, 360f));
            AudioHub.Sfx(GameAssets.I.sfxHit, heavy ? 0.9f : 0.6f);
            HitStop.Do(heavy ? 0.07f : 0.035f);
            CameraRig.Shake(heavy ? 0.3f : 0.1f, 0.15f);
        }

        // ---------------------------------------------------------------- normal attack

        IEnumerator AttackRoutine()
        {
            var w = Weapon;
            if (Time.time - lastAttackEnd > 0.7f) combo = 0;
            OwnsAnimation = true;
            BlocksJump = true;
            BlocksTurning = true;
            MoveMultiplier = w.moveWhileAttacking;
            anim.Play(w.attackClip, true);

            AudioHub.Sfx(w.id == WeaponId.GreatSword ? GameAssets.I.sfxHeavySwing : GameAssets.I.sfxSwing, 0.7f);
            var voices = GameAssets.I.voiceAttack;
            if (voices != null && voices.Length > 0 && Random.value < 0.25f) AudioHub.Sfx(voices[Random.Range(0, voices.Length)], 0.6f);

            if (pc.IsGrounded && w.lungeSpeed > 0f) OverrideVelocityX = pc.FacingSign * w.lungeSpeed;
            yield return Wait(TimeToFrame(w.attackClip, w.hitFrame));
            OverrideVelocityX = null;

            if (w.id == WeaponId.Bow) FireArrow(w);
            else MeleeStrike(w);

            nextAttackTime = Time.time + Mathf.Max(0.05f, w.cooldown - TimeToFrame(w.attackClip, w.hitFrame));
            float rest = ClipLength(w.attackClip) - TimeToFrame(w.attackClip, w.hitFrame);
            float t = 0f;
            while (t < rest)
            {
                // A buffered attack cancels the recovery frames into the next swing.
                if (t > rest * 0.4f && Time.time - attackPressedAt <= attackBufferTime && Time.time >= nextAttackTime) break;
                t += Time.deltaTime;
                yield return null;
            }
            combo = (combo + 1) % 3;
            lastAttackEnd = Time.time;
        }

        void MeleeStrike(WeaponData w)
        {
            bool heavy = w.id == WeaponId.GreatSword || combo == 2;
            int dmg = w.damage + (combo == 2 ? Mathf.RoundToInt(w.damage * 0.4f) : 0);
            Vector2 center = Front(w.hitOffset.x, w.hitOffset.y);
            var info = new DamageInfo { amount = dmg, origin = transform.position, knockback = w.knockback * (heavy ? 1.5f : 1f), heavy = heavy, source = gameObject };
            Combat.HitBox(center, w.hitSize, Layers.EnemyMask, info, null, (h, p) => OnEnemyHit(h, p, heavy));
            Combat.DebugBox(center, w.hitSize, Color.red);
            if (w.comboFx != null && w.comboFx.Length > 0)
            {
                string fx = w.comboFx[combo % w.comboFx.Length];
                Fx.Play(fx, Front(w.fxOffset.x, w.fxOffset.y), !pc.FacingRight, w.fxScale);
            }
            if (w.id == WeaponId.GreatSword) CameraRig.Shake(0.15f, 0.12f);
        }

        void FireArrow(WeaponData w)
        {
            AudioHub.Sfx(GameAssets.I.sfxBow, 0.8f);
            var p = Projectile.Spawn("Arrow", Front(0.9f, 1.05f), new Vector2(pc.FacingSign * 22f, 0f), w.damage, Layers.EnemyMask, 0.55f);
            if (p == null) return;
            p.lifetime = 0.9f;
            p.hitSize = new Vector2(2.0f, 0.5f);
            p.knockback = w.knockback;
            p.hitFx = "SwordHit";
            p.hitFxScale = 0.45f;
            p.hitSound = GameAssets.I.sfxHit;
            p.owner = gameObject;
        }

        // ---------------------------------------------------------------- swap

        IEnumerator SwapRoutine(int to)
        {
            string clip = $"Swap_{Weapon.id}_To_{weapons[to].id}";
            OwnsAnimation = true;
            BlocksJump = true;
            MoveMultiplier = 0.35f;
            anim.Play(clip, true);
            yield return Wait(TimeToFrame(clip, 2));
            weaponIndex = to;
            RunState.WeaponIndex = to;
            WeaponChanged?.Invoke();
            Fx.Flash(transform.position + Vector3.up, new Color(0.5f, 0.8f, 1f), 3f, 1f, 0.2f);
            yield return Wait(Mathf.Max(0f, ClipLength(clip) - TimeToFrame(clip, 2)));
        }

        // ---------------------------------------------------------------- Sword

        // E: rapid flurry in front (10 x 10 by default)
        IEnumerator SwordFlurry()
        {
            var w = Weapon;
            OwnsAnimation = BlocksMovement = BlocksJump = BlocksTurning = true;
            SuspendGravity = !pc.IsGrounded;
            MoveMultiplier = 0f;
            anim.SetFrame(w.attackClip, 0);
            yield return Wait(0.18f);
            Vector2 center = Front(1.8f, 1.1f);
            var fx = Fx.Play("SwordFlurry", center, !pc.FacingRight, 1.0f, 0f, null, Fx.DefaultOrder, null, 0.8f);
            if (fx != null) loopingFx.Add(fx);
            int hits = Mathf.Max(1, w.skill1Hits);
            float step = 0.63f / hits;
            for (int i = 0; i < hits; i++)
            {
                anim.SetFrame(w.attackClip, i % 3);
                var info = new DamageInfo { amount = w.skill1Damage, origin = transform.position, knockback = 0.6f, source = gameObject };
                Combat.HitBox(center, new Vector2(3.2f, 2.6f), Layers.EnemyMask, info, null, (h, p) =>
                {
                    Fx.Play("SwordSpark", p, Random.value > 0.5f, 0.5f, Random.Range(0f, 360f));
                    AudioHub.Sfx(GameAssets.I.sfxHit, 0.45f, 0.2f);
                });
                if (i % 2 == 0) AudioHub.Sfx(GameAssets.I.sfxSwing, 0.4f, 0.2f);
                CameraRig.Shake(0.07f, 0.08f);
                yield return Wait(step);
            }
            anim.Play(w.attackClip, true);
            yield return Wait(0.18f);
        }

        // Q: moon slash wave travelling forward, hits each enemy several times, brief invulnerability
        IEnumerator SwordMoonSlash()
        {
            var w = Weapon;
            OwnsAnimation = BlocksMovement = BlocksJump = BlocksTurning = true;
            MoveMultiplier = 0f;
            health.GrantInvulnerability(0.5f);
            anim.Play(w.attackClip, true);
            yield return Wait(TimeToFrame(w.attackClip, 1));
            var p = Projectile.Spawn("MoonSlash", Front(1.2f, 1.1f), new Vector2(pc.FacingSign * 12f, 0f), w.skill2Damage, Layers.EnemyMask, 1.6f);
            if (p != null)
            {
                p.lifetime = 0.7f;
                p.pierce = true;
                p.hitsPerTarget = Mathf.Max(1, w.skill2Hits);
                p.tickInterval = 0.1f;
                p.hitSize = new Vector2(2.6f, 2.4f);
                p.knockback = 1f;
                p.heavy = true;
                p.stopOnGround = false;
                p.hitFx = "SwordHit";
                p.hitFxScale = 0.6f;
                p.hitSound = GameAssets.I.sfxHit;
                p.owner = gameObject;
            }
            Fx.Flash(Front(1.5f, 1.1f), new Color(0.5f, 0.85f, 1f), 6f, 2f, 0.35f);
            CameraRig.Shake(0.25f, 0.2f);
            AudioHub.Sfx(GameAssets.I.sfxHeavySwing, 0.9f);
            yield return Wait(ClipLength(w.attackClip) - TimeToFrame(w.attackClip, 1) + 0.1f);
        }

        // ---------------------------------------------------------------- Bow

        // E: arrow rain on an area ahead for several seconds
        IEnumerator BowArrowRain()
        {
            var w = Weapon;
            OwnsAnimation = BlocksJump = BlocksTurning = true;
            MoveMultiplier = 0f;
            anim.Play(w.attackClip, true);
            yield return Wait(0.35f);
            AudioHub.Sfx(GameAssets.I.sfxBow, 0.9f);
            float groundY = pc.LastGroundY;
            Vector2 center = new Vector2(transform.position.x + pc.FacingSign * 3.2f, groundY);
            StartCoroutine(ArrowRainDamage(center, w));
            yield return Wait(0.15f);
        }

        IEnumerator ArrowRainDamage(Vector2 groundCenter, WeaponData w)
        {
            const float duration = 6f;
            var ring = Fx.Play("MintRing", groundCenter + Vector2.up * 0.05f, false, 0.55f, 0f, null, 5, null, duration);
            var rain = Fx.Play("ArrowRain", groundCenter + Vector2.up * 2.0f, false, 0.6f, 0f, new Color(1, 1, 1, 0.9f), Fx.DefaultOrder, null, duration);
            int hits = Mathf.Max(1, w.skill1Hits);
            float step = duration / hits;
            yield return Wait(0.15f);
            for (int i = 0; i < hits; i++)
            {
                var info = new DamageInfo { amount = w.skill1Damage, origin = groundCenter + Vector2.up * 4f, knockback = 0.2f, source = gameObject };
                Combat.HitBox(groundCenter + Vector2.up * 1.6f, new Vector2(4.2f, 3.6f), Layers.EnemyMask, info, null,
                    (h, p) => Fx.Play("SwordSpark", p, false, 0.35f, Random.Range(0f, 360f), new Color(0.6f, 1f, 0.85f)));
                yield return Wait(step);
            }
        }

        // Q: summon the moon bow and fire a piercing beam barrage
        IEnumerator BowMoonBeam()
        {
            var w = Weapon;
            OwnsAnimation = BlocksMovement = BlocksJump = BlocksTurning = true;
            SuspendGravity = !pc.IsGrounded;
            MoveMultiplier = 0f;
            anim.SetFrame(w.attackClip, 1);
            var bow = Fx.Play("BowAppear", Front(1.1f, 1.3f), !pc.FacingRight, 0.6f);
            if (bow != null) loopingFx.Add(bow);
            yield return Wait(0.45f);
            const float castTime = 2.2f;
            int hits = Mathf.Max(1, w.skill2Hits);
            float step = castTime / hits;
            for (int i = 0; i < hits; i++)
            {
                anim.SetFrame(w.attackClip, 2);
                var visual = Projectile.Spawn("BowBeam", Front(1.6f, 1.15f + Random.Range(-0.25f, 0.25f)),
                                              new Vector2(pc.FacingSign * 30f, 0f), 0, 0, 0.9f);
                if (visual != null) { visual.lifetime = 0.3f; visual.stopOnGround = false; }
                var info = new DamageInfo { amount = w.skill2Damage, origin = transform.position, knockback = 0.8f, source = gameObject };
                Combat.HitBox(Front(4.8f, 1.1f), new Vector2(8.5f, 2.8f), Layers.EnemyMask, info, null,
                    (h, p) => Fx.Play("SwordHit", p, false, 0.5f, Random.Range(0f, 360f), new Color(0.7f, 1f, 0.9f)));
                AudioHub.Sfx(GameAssets.I.sfxBow, 0.5f, 0.15f);
                CameraRig.Shake(0.08f, 0.08f);
                Fx.Flash(Front(1.5f, 1.2f), new Color(0.6f, 1f, 0.9f), 4f, 1.2f, 0.12f);
                yield return Wait(step);
            }
            anim.SetFrame(w.attackClip, 3);
            yield return Wait(0.2f);
        }

        // ---------------------------------------------------------------- Great sword

        // E: parry window. A blocked hit triggers a doubled counter strike.
        IEnumerator GreatSwordParry()
        {
            var w = Weapon;
            OwnsAnimation = BlocksMovement = BlocksJump = BlocksTurning = true;
            MoveMultiplier = 0f;
            anim.SetFrame(w.attackClip, 0);
            var glint = Fx.Play("CrossWave", Front(0.5f, 1.2f), false, 0.5f, 0f, new Color(0.8f, 0.9f, 1f, 0.8f));
            parryActive = true;
            parrySucceeded = false;
            float until = Time.time + 0.5f;
            while (Time.time < until && !parrySucceeded) yield return null;
            parryActive = false;
            if (!parrySucceeded)
            {
                yield return Wait(0.25f);
                yield break;
            }

            // Counter attack
            health.GrantInvulnerability(0.6f);
            HitStop.Do(0.12f);
            Fx.Flash(transform.position + Vector3.up, Color.white, 6f, 2.5f, 0.3f);
            anim.Play(w.attackClip, true);
            yield return Wait(TimeToFrame(w.attackClip, 2));
            Vector2 at = parryAttacker != null ? (Vector2)parryAttacker.transform.position + Vector2.up * 1f : Front(1.6f, 1.1f);
            Fx.Play("CrossWave", at, false, 1.3f);
            int dmg = Mathf.Max(60, parryIncoming * 2 + w.damage);
            var info = new DamageInfo { amount = dmg, origin = transform.position, knockback = 6f, heavy = true, source = gameObject };
            Combat.HitCircle(at, 2.2f, Layers.EnemyMask, info, null, (h, p) => OnEnemyHit(h, p, true));
            CameraRig.Shake(0.4f, 0.25f);
            AudioHub.Sfx(GameAssets.I.sfxHeavySwing, 1f);
            yield return Wait(0.25f);
        }

        bool ParryIntercept(ref DamageInfo info)
        {
            if (!parryActive) return false;
            parrySucceeded = true;
            parryIncoming = info.amount;
            parryAttacker = info.source != null ? info.source.GetComponentInParent<Health>() : null;
            Fx.Play("SwordHit", (Vector2)transform.position + new Vector2(pc.FacingSign * 0.6f, 1.2f), false, 0.9f);
            AudioHub.Sfx(GameAssets.I.sfxHit, 1f);
            return true;
        }

        // Q: leap and slam the ground, launching enemies (3 hits)
        IEnumerator GreatSwordSlam()
        {
            var w = Weapon;
            OwnsAnimation = BlocksJump = BlocksTurning = true;
            MoveMultiplier = 0.2f;
            health.Invulnerable = true;
            anim.SetFrame(w.attackClip, 1);
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, 9f);
            yield return Wait(0.28f);
            BlocksMovement = true;
            MoveMultiplier = 0f;
            float t = 0f;
            while (!pc.IsGrounded && t < 0.8f)
            {
                rb.linearVelocity = new Vector2(0f, -26f);
                t += Time.deltaTime;
                yield return null;
            }
            anim.Play(w.attackClip, true);
            anim.SetFrame(w.attackClip, 3);
            Vector2 ground = new Vector2(transform.position.x, transform.position.y);
            Fx.Play("GreatSlam", ground + new Vector2(pc.FacingSign * 1.2f, 1.0f), !pc.FacingRight, 1.4f);
            Fx.Flash(ground + Vector2.up, new Color(0.5f, 0.75f, 1f), 7f, 2.5f, 0.4f);
            AudioHub.Sfx(GameAssets.I.sfxExplosion, 0.9f);
            CameraRig.Shake(0.6f, 0.35f);
            int hits = Mathf.Max(1, w.skill2Hits);
            var already = new HashSet<Health>();
            for (int i = 0; i < hits; i++)
            {
                already.Clear();
                var info = new DamageInfo { amount = w.skill2Damage, origin = ground, knockback = 2f, launch = 1.5f, heavy = true, source = gameObject };
                Combat.HitCircle(ground + Vector2.up * 0.8f, 4f, Layers.EnemyMask, info, already, (h, p) => OnEnemyHit(h, p, true));
                yield return Wait(0.12f);
            }
            health.Invulnerable = false;
            yield return Wait(0.2f);
        }

        void OnDrawGizmosSelected()
        {
            if (weapons == null || weapons.Length == 0 || weapons[0] == null) return;
            var w = Weapon;
            Gizmos.color = Color.red;
            float sign = Application.isPlaying && pc != null ? pc.FacingSign : 1f;
            Gizmos.DrawWireCube(transform.position + new Vector3(sign * w.hitOffset.x, w.hitOffset.y), w.hitSize);
        }
    }
}
