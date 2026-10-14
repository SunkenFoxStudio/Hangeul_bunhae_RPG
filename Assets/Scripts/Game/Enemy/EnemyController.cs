using System;
using Hangeul.App;
using Hangeul.Game.Character;
using Hangeul.Game.Combat;
using Hangeul.Game.FSM;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Hangeul.Game.Enemy
{
    /// <summary>죽을 때 떨어뜨리는 것. 보통 단어 아이템(WorldItem)을 프리팹으로 만들어 넣는다.</summary>
    [Serializable]
    public sealed class EnemyDrop
    {
        [Tooltip("떨어뜨릴 프리팹(예: 단어가 들어 있는 WorldItem 프리팹).")]
        public GameObject prefab;
        [Range(0f, 1f)] public float chance = 1f;
    }

    /// <summary>
    /// 적의 루트(근접 추격형 샘플). 플레이어와 같은 방식의 단순 FSM(대기·배회 → 추격 → 피격 → 죽음)으로 움직이고,
    /// 이동·감지·피격 설정을 한곳에 모은다. 동작 자체는 <see cref="EnemyState"/>를 상속한 상태들이 담당한다.
    /// 몸통에 닿으면 피해를 주는 것은 자식 <c>ContactZone</c>의 <see cref="DamageZone"/>이 하고, 피격·죽음 중에는 꺼진다.
    /// 시간은 <c>Time.deltaTime</c>/<c>fixedDeltaTime</c> 기준이라 인벤토리를 열어 게임이 멈추면 같이 멈춘다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class EnemyController : MonoBehaviour
    {
        [Header("이동 (유니티 단위/초)")]
        [SerializeField, Min(0f)] float moveSpeed = 0.7f;
        [SerializeField, Min(0f)] float acceleration = 8f;
        [SerializeField, Min(0f)] float deceleration = 12f;

        [Header("감지")]
        [Tooltip("이 거리 안으로 들어오면 쫓기 시작한다.")]
        [SerializeField, Min(0f)] float detectRange = 1.6f;
        [Tooltip("이 거리보다 멀어지면 놓친다(감지 거리보다 커야 경계에서 깜빡이지 않는다).")]
        [SerializeField, Min(0f)] float loseRange = 2.6f;

        [Header("대기 / 배회")]
        [Tooltip("원래 자리에서 이 거리 안쪽을 어슬렁거린다. 0이면 제자리에 서 있는다.")]
        [SerializeField, Min(0f)] float wanderRadius = 0.5f;
        [SerializeField, Range(0.1f, 1f)] float wanderSpeedFactor = 0.4f;
        [SerializeField, Min(0.1f)] float idleSecondsMin = 1f;
        [SerializeField, Min(0.1f)] float idleSecondsMax = 2.5f;

        [Header("피격 / 죽음")]
        [Tooltip("맞아서 밀려난 뒤 멈추는 빠르기(단위/초²).")]
        [SerializeField, Min(0f)] float hurtDeceleration = 8f;
        [Tooltip("맞았을 때 색이 변하는 시간(초).")]
        [SerializeField, Min(0f)] float hitFlashSeconds = 0.12f;
        [Tooltip("죽은 뒤 서서히 사라지는 시간(초).")]
        [SerializeField, Min(0f)] float deathSeconds = 0.5f;

        [Header("접촉 피해")]
        [Tooltip("자식 ContactZone의 DamageZone. 피격·죽음 중에는 꺼진다. 비워 두면 접촉 피해가 없다.")]
        [SerializeField] DamageZone contactZone;

        [Header("드롭")]
        [SerializeField] EnemyDrop[] drops;

        [Header("그래픽")]
        [SerializeField] EnemySpriteAnimator spriteAnimator;

        [Header("Debug (실행 중에만 갱신)")]
        [SerializeField] string currentState;
        [Tooltip("플레이어까지의 거리. 감지 거리(Detect Range)와 비교해 보세요.")]
        [SerializeField] float distanceToPlayer;
        [Tooltip("지금 플레이어를 쫓고 있는지.")]
        [SerializeField] bool chasing;

        public Rigidbody2D Body { get; private set; }
        public Health Health { get; private set; }
        public EnemySpriteAnimator Anim => spriteAnimator;
        public StateMachine Fsm { get; } = new StateMachine();
        public Facing Facing { get; private set; } = Facing.Down;

        public EnemyIdleState Idle { get; private set; }
        public EnemyChaseState Chase { get; private set; }
        public EnemyHurtState Hurt { get; private set; }
        public EnemyDeadState Dead { get; private set; }

        public PlayerController Target { get; private set; }
        public AggroTracker Aggro { get; private set; }
        public Vector2 Home { get; private set; }

        public float MoveSpeed => moveSpeed;
        public float WanderRadius => wanderRadius;
        public float IdleSecondsMin => idleSecondsMin;
        public float IdleSecondsMax => Mathf.Max(idleSecondsMin, idleSecondsMax);
        public float WanderSpeedFactor => wanderSpeedFactor;
        public float HurtDeceleration => hurtDeceleration;
        public float DeathSeconds => deathSeconds;

        void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            Health = GetComponent<Health>();
            if (spriteAnimator == null) spriteAnimator = GetComponentInChildren<EnemySpriteAnimator>();
            Aggro = new AggroTracker(detectRange, loseRange);
            Home = transform.position;

            Idle = new EnemyIdleState(this);
            Chase = new EnemyChaseState(this);
            Hurt = new EnemyHurtState(this);
            Dead = new EnemyDeadState(this);

            Fsm.StateChanged += (_, next) => currentState = next.GetType().Name;
        }

        void OnEnable()
        {
            Health.Damaged += OnDamaged;
            Health.Died += OnDied;
        }

        void OnDisable()
        {
            Health.Damaged -= OnDamaged;
            Health.Died -= OnDied;
        }

        void Start()
        {
            Target = FindFirstObjectByType<PlayerController>();
            Fsm.Initialize(Idle);
        }

        void Update()
        {
            // 인스펙터 값을 플레이 중에 바꿔도 바로 적용되도록 매 프레임 읽는다(Awake에서 한 번만 읽으면 바뀌지 않는다)
            Aggro.SetRanges(detectRange, loseRange);
            distanceToPlayer = DistanceToTarget;
            chasing = Aggro.Active;
            Fsm.Tick(Time.deltaTime);
            if (spriteAnimator != null) spriteAnimator.SetFacing(Facing);
        }

        void FixedUpdate() => Fsm.FixedTick(Time.fixedDeltaTime);

        void OnDamaged(Health health, DamageInfo info, int taken)
        {
            if (Fsm.Current == Dead) return;
            if (spriteAnimator != null) spriteAnimator.Flash(hitFlashSeconds);
            Fsm.ChangeState(Hurt);
        }

        void OnDied(Health health) => Fsm.ChangeState(Dead);

        // ── 상태들이 쓰는 도구 ──
        public bool TargetAlive => Target != null && !Target.Health.IsDead;

        /// <summary>플레이어까지의 거리(플레이어가 없으면 무한대).</summary>
        public float DistanceToTarget => Target != null ? Vector2.Distance(transform.position, Target.transform.position) : float.PositiveInfinity;

        public Vector2 DirectionToTarget
        {
            get
            {
                if (Target == null) return Vector2.zero;
                Vector2 d = (Vector2)Target.transform.position - (Vector2)transform.position;
                return d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.zero;
            }
        }

        public void ApproachVelocity(Vector2 target, float rate, float fixedDelta)
            => Body.linearVelocity = Vector2.MoveTowards(Body.linearVelocity, target, rate * fixedDelta);

        public void MoveToward(Vector2 direction, float speed, float fixedDelta)
            => ApproachVelocity(direction * speed, acceleration, fixedDelta);

        public void Stop(float fixedDelta) => ApproachVelocity(Vector2.zero, deceleration, fixedDelta);

        public void FaceDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude > 1e-4f) Facing = FacingUtil.FromVector(direction);
        }

        /// <summary>몸통 접촉 피해를 켜고 끈다(피격·죽음 중에는 끈다).</summary>
        public void SetContactActive(bool active)
        {
            if (contactZone == null) return;
            contactZone.enabled = active;
            var col = contactZone.GetComponent<Collider2D>();
            if (col != null) col.enabled = active;
        }

        /// <summary>죽을 때 드롭 표에 따라 아이템을 떨어뜨린다.</summary>
        public void SpawnDrops()
        {
            if (drops == null) return;
            foreach (var drop in drops)
            {
                if (drop == null || drop.prefab == null || Random.value > drop.chance) continue;
                Vector2 scatter = Random.insideUnitCircle * 0.15f;
                Instantiate(drop.prefab, (Vector2)transform.position + scatter, Quaternion.identity);
            }
        }

        // Scene 뷰에서 선택하면 감지/포기 거리와 배회 범위를 보여준다
        void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying ? (Vector3)Home : transform.position;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = new Color(1f, 0.9f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(detectRange, loseRange));
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Gizmos.DrawWireSphere(center, wanderRadius);
        }
    }
}
