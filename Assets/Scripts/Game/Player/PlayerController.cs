using System.Collections;
using UnityEngine;

namespace Yowol
{
    /// <summary>
    /// Movement, jump, facing, hurt/death and locomotion animation.
    /// Combat (attacks, skills, swaps) lives in PlayerCombat and takes over the animation while acting.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("References")]
        public Transform visual;
        public SpriteAnimator anim;

        [Header("Run")]
        public float moveSpeed = 6.5f;
        public float groundAccel = 70f;
        public float groundDecel = 80f;
        public float airAccel = 45f;

        [Header("Jump")]
        public float jumpVelocity = 14f;
        public float coyoteTime = 0.1f;
        public float jumpBuffer = 0.12f;
        public float fallGravityMultiplier = 1.7f;
        public float shortHopGravityMultiplier = 2.4f;
        public float maxFallSpeed = 24f;

        [Header("Ground check")]
        public Vector2 groundCheckSize = new Vector2(0.55f, 0.12f);
        public float groundCheckOffsetY = 0f;

        [Header("Hurt")]
        public float hurtStun = 0.28f;
        public float hurtKnockback = 6f;
        public float invulnerableAfterHit = 0.9f;

        [Header("Audio")]
        public float footstepInterval = 0.32f;

        public bool FacingRight { get; private set; } = true;
        public bool IsGrounded { get; private set; }
        public bool IsDead { get; private set; }
        public Rigidbody2D Body => rb;
        public Health Health => health;
        public float LastGroundY { get; private set; }

        Rigidbody2D rb;
        Health health;
        PlayerCombat combat;
        float baseGravity;
        float lastGroundedTime = -9f, lastJumpPressTime = -9f;
        float hurtUntil;
        bool jumpFromRun;
        bool wasGrounded = true;
        float landingUntil;
        float footstepTimer;
        Vector3 lastSafePosition;
        float frozenUntil;

        void Awake()
        {
            Instance = this;
            rb = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            combat = GetComponent<PlayerCombat>();
            baseGravity = rb.gravityScale;
            health.invulnerableAfterHit = invulnerableAfterHit;
            health.Damaged += OnDamaged;
            health.Died += OnDied;
            lastSafePosition = transform.position;
            LastGroundY = transform.position.y;
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public void SetFacing(bool right)
        {
            FacingRight = right;
            if (visual != null)
            {
                var s = visual.localScale;
                s.x = Mathf.Abs(s.x) * (right ? 1f : -1f);
                visual.localScale = s;
            }
        }

        public float FacingSign => FacingRight ? 1f : -1f;

        /// <summary>Stop all control for a moment (cutscenes, stage transitions).</summary>
        public void Freeze(float seconds)
        {
            frozenUntil = Time.time + seconds;
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
        }

        void Update()
        {
            if (IsDead) return;

            bool groundedNow = Physics2D.OverlapBox((Vector2)transform.position + Vector2.up * groundCheckOffsetY,
                                                    groundCheckSize, 0f, Layers.GroundMask) != null && rb.linearVelocity.y <= 0.1f;
            IsGrounded = groundedNow;
            if (IsGrounded)
            {
                lastGroundedTime = Time.time;
                LastGroundY = transform.position.y;
                if (!wasGrounded) OnLanded();
                if (Physics2D.OverlapBox((Vector2)transform.position + new Vector2(-0.4f, -0.1f), new Vector2(0.1f, 0.2f), 0, Layers.GroundMask) &&
                    Physics2D.OverlapBox((Vector2)transform.position + new Vector2(0.4f, -0.1f), new Vector2(0.1f, 0.2f), 0, Layers.GroundMask))
                    lastSafePosition = transform.position;
            }
            wasGrounded = IsGrounded;

            if (GameInput.JumpPressed) lastJumpPressTime = Time.time;

            bool controllable = Time.time >= hurtUntil && Time.time >= frozenUntil && (combat == null || !combat.BlocksMovement);
            float x = GameInput.MoveX;

            if (controllable && Mathf.Abs(x) > 0.01f && (combat == null || !combat.BlocksTurning))
                SetFacing(x > 0f);

            bool canJump = controllable && (combat == null || !combat.BlocksJump);
            if (canJump && Time.time - lastJumpPressTime <= jumpBuffer && Time.time - lastGroundedTime <= coyoteTime)
            {
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpVelocity);
                lastJumpPressTime = -9f;
                lastGroundedTime = -9f;
                IsGrounded = false;
                wasGrounded = false;
                jumpFromRun = Mathf.Abs(x) > 0.1f;
                AudioHub.Sfx(GameAssets.I.sfxJump, 0.6f);
            }

            // Fell off the world: return to the last safe ledge.
            if (transform.position.y < LastGroundY - 25f) Respawn(15);

            UpdateAnimation(x);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            Vector2 v = rb.linearVelocity;

            if (IsDead || Time.time < frozenUntil)
            {
                v.x = Mathf.MoveTowards(v.x, 0f, groundDecel * dt);
            }
            else if (Time.time >= hurtUntil)
            {
                float mult = combat != null ? combat.MoveMultiplier : 1f;
                float target = GameInput.MoveX * moveSpeed * mult;
                if (combat != null && combat.OverrideVelocityX.HasValue) target = combat.OverrideVelocityX.Value;
                float a = IsGrounded ? (Mathf.Abs(target) > 0.01f ? groundAccel : groundDecel) : airAccel;
                if (combat != null && combat.OverrideVelocityX.HasValue) a = 200f;
                v.x = Mathf.MoveTowards(v.x, target, a * dt);
            }

            if (combat != null && combat.SuspendGravity)
            {
                rb.gravityScale = 0f;
                v.y = Mathf.MoveTowards(v.y, 0f, 60f * dt);
            }
            else if (v.y < 0f) rb.gravityScale = baseGravity * fallGravityMultiplier;
            else if (v.y > 0f && !GameInput.JumpHeld) rb.gravityScale = baseGravity * shortHopGravityMultiplier;
            else rb.gravityScale = baseGravity;

            v.y = Mathf.Max(v.y, -maxFallSpeed);
            rb.linearVelocity = v;
        }

        void OnLanded()
        {
            landingUntil = Time.time + 0.09f;
            AudioHub.Sfx(GameAssets.I.sfxFootstep, 0.5f);
        }

        void UpdateAnimation(float x)
        {
            if (anim == null || combat == null) return;
            if (combat.OwnsAnimation) return;
            var w = combat.Weapon;

            if (!IsGrounded)
            {
                string clip = jumpFromRun ? w.jumpClip : w.jumpInPlaceClip;
                // 12-frame jump: 0 takeoff, 1-5 rising, 6-8 apex, 9 falling, 10-11 landing.
                float t = Mathf.Clamp01(0.5f - rb.linearVelocity.y / (2f * jumpVelocity));
                int frame = 1 + Mathf.RoundToInt(t * 8f);
                anim.SetFrame(clip, frame);
                return;
            }

            if (Time.time < landingUntil)
            {
                anim.SetFrame(jumpFromRun ? w.jumpClip : w.jumpInPlaceClip, 10);
                return;
            }

            bool running = Mathf.Abs(x) > 0.1f && Time.time >= frozenUntil && Mathf.Abs(rb.linearVelocity.x) > 0.5f;
            if (running)
            {
                anim.Play("Run");
                footstepTimer -= Time.deltaTime;
                if (footstepTimer <= 0f)
                {
                    footstepTimer = footstepInterval;
                    AudioHub.Sfx(GameAssets.I.sfxFootstep, 0.25f, 0.15f);
                }
            }
            else
            {
                anim.Play(w.idleClip);
                footstepTimer = 0f;
            }
        }

        void OnDamaged(DamageInfo info)
        {
            if (IsDead) return;
            if (combat != null && combat.SuperArmor)
            {
                CameraRig.Shake(0.15f, 0.15f);
                AudioHub.Sfx(GameAssets.I.sfxPlayerHurt, 0.6f);
                return;
            }
            float dir = Mathf.Sign(transform.position.x - info.origin.x);
            if (dir == 0) dir = -FacingSign;
            hurtUntil = Time.time + hurtStun;
            rb.linearVelocity = new Vector2(dir * hurtKnockback * (info.heavy ? 1.4f : 1f), 5f);
            combat?.Interrupt();
            CameraRig.Shake(info.heavy ? 0.35f : 0.2f, 0.2f);
            HitStop.Do(info.heavy ? 0.09f : 0.05f);
            AudioHub.Sfx(GameAssets.I.sfxPlayerHurt, 0.8f);
            var voices = GameAssets.I.voiceHurt;
            if (voices != null && voices.Length > 0 && Random.value < 0.6f) AudioHub.Sfx(voices[Random.Range(0, voices.Length)], 0.7f);
        }

        void OnDied()
        {
            IsDead = true;
            combat?.Interrupt();
            GameInput.Locked = true;
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            anim.Play("Death", true);
            AudioHub.Sfx(GameAssets.I.sfxDeath, 0.9f);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            HitStop.SlowMotion(0.35f, 0.6f);
            yield return new WaitForSecondsRealtime(1.8f);
            GameInput.Locked = false;
            GameUI.Instance?.ShowGameOver();
        }

        /// <summary>Return to the last safe ground position, losing some HP.</summary>
        public void Respawn(int damage)
        {
            rb.linearVelocity = Vector2.zero;
            transform.position = lastSafePosition + Vector3.up * 0.5f;
            if (damage > 0)
            {
                var info = DamageInfo.Make(damage, transform.position + Vector3.right * FacingSign);
                health.TakeDamage(info);
            }
            CameraRig.Instance?.SnapToTarget();
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * groundCheckOffsetY, groundCheckSize);
        }
    }
}
