using System.Collections.Generic;
using UnityEngine;

namespace Yowol
{
    /// <summary>
    /// Hooded reaper: patrols, chases, telegraphs with a short wind-up, then lunges with its blade.
    /// </summary>
    public class ReaperEnemy : EnemyBase
    {
        enum State { Patrol, Chase, Windup, Lunge, Recover }

        [Header("Reaper")]
        public float patrolSpeed = 1.4f;
        public float patrolRange = 3f;
        public float chaseSpeed = 3.2f;
        public float detectRange = 8f;
        public float attackRange = 2.4f;
        public float windupTime = 0.5f;
        public float lungeSpeed = 9f;
        public float lungeTime = 0.22f;
        public float recoverTime = 0.6f;
        public int attackDamage = 12;
        public Vector2 attackBox = new Vector2(2.2f, 1.6f);

        State state = State.Patrol;
        float stateTime;
        float homeX;
        int patrolDir = 1;
        readonly HashSet<Health> lungeHits = new HashSet<Health>();
        SpriteRenderer body;

        protected override float ArtFacesRight => -1f;   // the reaper art faces left

        protected override void Awake()
        {
            base.Awake();
            homeX = transform.position.x;
            body = anim != null ? anim.Renderer : GetComponentInChildren<SpriteRenderer>();
            patrolDir = Random.value > 0.5f ? 1 : -1;
            Face(patrolDir > 0);
        }

        void Enter(State s)
        {
            state = s;
            stateTime = 0f;
            if (s == State.Windup)
            {
                anim.speed = 0f;
                Fx.Play("SwordSpark", transform.position + new Vector3(FacingRight ? 0.9f : -0.9f, 1.2f), false, 0.4f, 0f, new Color(1f, 0.3f, 0.3f));
            }
            else anim.speed = 1f;
            if (s == State.Lunge)
            {
                lungeHits.Clear();
                Fx.Play("SwordSlash1", transform.position + new Vector3(FacingRight ? 1.2f : -1.2f, 0.9f), !FacingRight, 0.55f, 0f, new Color(1f, 0.45f, 0.5f, 0.9f));
                AudioHub.Sfx(GameAssets.I.sfxSwing, 0.5f, 0.2f);
            }
        }

        void Update()
        {
            if (dead) return;
            stateTime += Time.deltaTime;

            if (state == State.Windup && body != null)
            {
                float pulse = Mathf.PingPong(stateTime * 8f, 1f);
                flash?.SetBaseColor(Color.Lerp(Color.white, new Color(1f, 0.55f, 0.55f), pulse));
            }
            else flash?.SetBaseColor(Color.white);

            if (Stunned || Airborne) return;

            var player = Player;
            float dx = player != null ? player.position.x - transform.position.x : 0f;
            float dy = player != null ? Mathf.Abs(player.position.y - transform.position.y) : 99f;
            bool sees = PlayerAlive && Mathf.Abs(dx) < detectRange && dy < 3f;

            switch (state)
            {
                case State.Patrol:
                    if (sees) { Enter(State.Chase); break; }
                    if (Mathf.Abs(transform.position.x - homeX) > patrolRange && Mathf.Sign(transform.position.x - homeX) == patrolDir)
                        patrolDir = -patrolDir;
                    if (!GroundAhead(patrolDir) || WallAhead(patrolDir)) patrolDir = -patrolDir;
                    Face(patrolDir > 0);
                    break;
                case State.Chase:
                    if (!sees && stateTime > 1f) { homeX = transform.position.x; Enter(State.Patrol); break; }
                    FaceToward(player.position);
                    if (Mathf.Abs(dx) <= attackRange) Enter(State.Windup);
                    break;
                case State.Windup:
                    if (player != null && stateTime < windupTime * 0.6f) FaceToward(player.position);
                    if (stateTime >= windupTime) Enter(State.Lunge);
                    break;
                case State.Lunge:
                    var info = new DamageInfo { amount = attackDamage, origin = transform.position, knockback = 5f, source = gameObject };
                    Combat.HitBox((Vector2)transform.position + new Vector2(FacingRight ? 1.1f : -1.1f, 0.9f), attackBox,
                                  Layers.PlayerMask, info, lungeHits);
                    if (stateTime >= lungeTime) Enter(State.Recover);
                    break;
                case State.Recover:
                    if (stateTime >= recoverTime) Enter(sees ? State.Chase : State.Patrol);
                    break;
            }
        }

        void FixedUpdate()
        {
            if (dead) return;
            if (Stunned || Airborne)
            {
                if (!Airborne) rb.linearVelocity = new Vector2(Mathf.MoveTowards(rb.linearVelocity.x, 0, 30f * Time.fixedDeltaTime), rb.linearVelocity.y);
                return;
            }
            float vx = 0f;
            switch (state)
            {
                case State.Patrol: vx = patrolDir * patrolSpeed; break;
                case State.Chase:
                    vx = (FacingRight ? 1 : -1) * chaseSpeed;
                    if (!GroundAhead(FacingRight ? 1 : -1)) vx = 0f;
                    break;
                case State.Lunge:
                    vx = (FacingRight ? 1 : -1) * lungeSpeed;
                    if (!GroundAhead(FacingRight ? 1 : -1)) vx = 0f;
                    break;
            }
            rb.linearVelocity = new Vector2(vx, rb.linearVelocity.y);
            anim.speed = state == State.Windup ? 0f : Mathf.Clamp(Mathf.Abs(vx) / patrolSpeed, 0.6f, 2.2f);
            if (state != State.Lunge) ContactDamage(new Vector2(1.0f, 1.4f), new Vector2(0f, 0.8f));
        }

        protected override void OnDamaged(DamageInfo info)
        {
            base.OnDamaged(info);
            if (!dead && (state == State.Patrol)) Enter(State.Chase);
            if (state == State.Windup && info.heavy) Enter(State.Recover);   // heavy hits interrupt the wind-up
        }

        bool GroundAhead(int dir)
        {
            Vector2 from = (Vector2)transform.position + new Vector2(dir * 0.8f, 0.3f);
            return Physics2D.Raycast(from, Vector2.down, 1.2f, Layers.GroundMask);
        }

        bool WallAhead(int dir)
        {
            Vector2 from = (Vector2)transform.position + new Vector2(0f, 0.8f);
            return Physics2D.Raycast(from, new Vector2(dir, 0), 0.9f, Layers.GroundMask);
        }
    }
}
