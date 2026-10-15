using Hangeul.Game.Combat;
using Hangeul.Game.Feedback;
using Hangeul.Game.FSM;
using Hangeul.Game.World;
using Unity.Cinemachine;
using Hangeul.Game.Interaction;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// 플레이어 캐릭터의 루트. 입력·이동 설정·애니메이션을 한곳에 모으고, 동작 자체는 FSM 상태들이 담당한다.
    /// 상태 추가 방법: PlayerState를 상속한 클래스를 만들고, 여기서 인스턴스를 만들어 상태에서 ChangeState로 전이한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(PlayerInputReader))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] PlayerSpriteAnimator spriteAnimator;

        [Header("Movement (units: Unity unit, PPU=100 기준 1unit = 100px)")]
        [SerializeField, Min(0f)] float moveSpeed = 1.5f;
        [SerializeField, Min(0f)] float acceleration = 20f;
        [SerializeField, Min(0f)] float deceleration = 30f;
        [SerializeField, Range(0f, 0.5f)] float inputDeadZone = 0.15f;

        [Header("Interaction")]
        [SerializeField, Min(0f)] float interactDuration = 0.25f;

        [Header("Combat")]
        [Tooltip("Health 컴포넌트가 없으면 이 값으로 자동으로 붙인다(하트 방식 정수 체력).")]
        [SerializeField, Min(1)] int maxHealth = 6;
        [Tooltip("한 번 맞은 뒤 더 맞지 않는 시간(초).")]
        [SerializeField, Min(0f)] float invincibleSeconds = 1f;
        [Tooltip("맞아서 밀려난 뒤 멈추는 빠르기(단위/초²).")]
        [SerializeField, Min(0f)] float hurtDeceleration = 8f;
        [Tooltip("쓰러진 뒤 되살아나기까지 걸리는 시간(초).")]
        [SerializeField, Min(0f)] float respawnDelay = 1.5f;
        [Tooltip("되살아난 직후 무적 시간(초).")]
        [SerializeField, Min(0f)] float respawnInvincibleSeconds = 1.5f;
        [Tooltip("죽은 뒤 체크포인트로 돌아갈 때 화면이 까맣게 덮이고 걷히는 시간(초, 각각).")]
        [SerializeField, Min(0f)] float respawnFadeSeconds = 0.4f;

        [Header("Debug")]
        [SerializeField] string currentState;

        public Rigidbody2D Body { get; private set; }
        /// <summary>상태가 쓰는 애니메이션 창구. 나중에 유니티 Animator로 바꿀 때는 이 인터페이스의 다른 구현으로 교체한다.</summary>
        public IPlayerAnimator Anim => spriteAnimator;
        public StateMachine Fsm { get; } = new StateMachine();

        /// <summary>이번 프레임의 이동 입력(최대 길이 1, 데드존 적용).</summary>
        public Vector2 MoveInput { get; private set; }
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0f;
        public Facing Facing { get; private set; } = Facing.Down;
        public float MoveSpeed => moveSpeed;

        public PlayerIdleState Idle { get; private set; }
        public PlayerMoveState Move { get; private set; }
        public PlayerLockedState Locked { get; private set; }
        public PlayerInteractState Interact { get; private set; }
        public PlayerAttackState Attack { get; private set; }
        public PlayerMeleeAttack Melee { get; private set; }
        /// <summary>인벤토리 아이템 사용(무기 장착, 음식 먹기)과 공격 가능 여부. 무기를 장착해야 공격할 수 있다.</summary>
        public PlayerItemUser Items { get; private set; }
        public PlayerHurtState Hurt { get; private set; }
        public PlayerDeadState Dead { get; private set; }

        public Health Health { get; private set; }
        public float HurtDeceleration => hurtDeceleration;
        public float RespawnDelay => respawnDelay;

        public PlayerInputReader Input => _input;
        public InteractionSensor Sensor { get; private set; }
        public float InteractDuration => interactDuration;

        PlayerInputReader _input;

        void Awake()
        {
            Body = GetComponent<Rigidbody2D>();
            _input = GetComponent<PlayerInputReader>();
            if (spriteAnimator == null) spriteAnimator = GetComponentInChildren<PlayerSpriteAnimator>();

            // 센서가 없으면 기본값으로 자동 추가한다(직접 붙이면 범위를 조절할 수 있다)
            Sensor = GetComponent<InteractionSensor>();
            if (Sensor == null) Sensor = gameObject.AddComponent<InteractionSensor>();

            // 체력이 없으면 설정값으로 자동으로 붙인다(직접 붙이면 인스펙터에서 조절할 수 있다)
            Health = GetComponent<Health>();
            if (Health == null)
            {
                Health = gameObject.AddComponent<Health>();
                Health.Configure(Team.Player, maxHealth, invincibleSeconds);
            }

            // 공격 설정: 없으면 기본값으로 자동으로 붙인다(Hangeul ▸ Setup ▸ Player Attack 메뉴로 미리 붙이면 인스펙터에서 조절할 수 있다)
            Melee = GetComponent<PlayerMeleeAttack>();
            if (Melee == null) Melee = gameObject.AddComponent<PlayerMeleeAttack>();

            // 아이템 사용: 없으면 기본값으로 자동으로 붙인다(직접 붙이면 "무기 필수"나 음식 회복량을 인벤토리에서 조절할 수 있다)
            Items = GetComponent<PlayerItemUser>();
            if (Items == null) Items = gameObject.AddComponent<PlayerItemUser>();

            // 타격감 연출(번쩍임, 깜빡임, 히트스톱, 화면 흔들림): 없으면 기본값으로 붙인다(Hangeul ▸ Setup ▸ Combat HUD & Feedback 메뉴로 미리 붙이면 인스펙터에서 보인다)
            if (GetComponent<PlayerHitFeedback>() == null) gameObject.AddComponent<PlayerHitFeedback>();

            Idle = new PlayerIdleState(this);
            Move = new PlayerMoveState(this);
            Locked = new PlayerLockedState(this);
            Interact = new PlayerInteractState(this);
            Attack = new PlayerAttackState(this);
            Hurt = new PlayerHurtState(this);
            Dead = new PlayerDeadState(this);

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

        void OnDamaged(Health health, DamageInfo info, int taken)
        {
            if (Fsm.Current != Dead) Fsm.ChangeState(Hurt);
        }

        void OnDied(Health health) => Fsm.ChangeState(Dead);

        /// <summary>
        /// 쓰러진 뒤 체크포인트로 돌아가는 연출을 시작한다: 화면이 까맣게 덮이고(<c>respawnFadeSeconds</c>), 그 사이에 체크포인트로 옮겨
        /// 체력을 채운 뒤 화면이 걷힌다. 화면 페이드가 없으면(0초) 바로 돌아간다.
        /// </summary>
        public void BeginRespawn()
        {
            if (respawnFadeSeconds <= 0f) { RespawnAtCheckpoint(); return; }

            var fader = ScreenFader.Instance;
            fader.FadeOut(respawnFadeSeconds, () =>
            {
                RespawnAtCheckpoint();
                fader.FadeIn(respawnFadeSeconds);
            });
        }

        /// <summary>
        /// 가장 최근에 켠 체크포인트(없으면 시작 위치)로 옮겨 체력을 가득 채우고 잠깐 무적으로 Idle에 돌아온다.
        /// 카메라는 부드럽게 따라가지 않고 곧바로 옮겨진다.
        /// </summary>
        public void RespawnAtCheckpoint()
        {
            Vector2 destination = Checkpoints.Position ?? (Vector2)transform.position;
            Vector2 delta = destination - (Vector2)transform.position;

            Body.position = destination;
            transform.position = destination;
            Body.linearVelocity = Vector2.zero;
            foreach (var cam in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
                cam.OnTargetObjectWarped(transform, delta);

            Health.Revive();
            Health.GrantInvincibility(respawnInvincibleSeconds);
            Fsm.ChangeState(Idle);
        }

        void Start()
        {
            Checkpoints.SetDefault(transform.position);   // 체크포인트를 켜기 전에 죽으면 시작한 자리로 돌아온다
            Fsm.Initialize(Idle);
        }

        void Update()
        {
            MoveInput = ReadMoveInput();
            Fsm.Tick(Time.deltaTime);
            if (spriteAnimator != null) spriteAnimator.SetFacing(Facing);
        }

        void FixedUpdate() => Fsm.FixedTick(Time.fixedDeltaTime);

        Vector2 ReadMoveInput()
        {
            Vector2 raw = _input.Move;
            return raw.magnitude < inputDeadZone ? Vector2.zero : Vector2.ClampMagnitude(raw, 1f);
        }

        /// <summary>현재 이동 입력으로 바라보는 방향을 갱신한다(입력이 없으면 유지).</summary>
        public void UpdateFacingFromInput()
        {
            if (HasMoveInput) Facing = FacingUtil.FromVector(MoveInput);
        }

        /// <summary>현재 속도를 목표 속도로 rate(units/s²)만큼 접근시킨다. FixedUpdate에서 호출.</summary>
        public void ApproachVelocity(Vector2 target, float rate, float fixedDelta)
        {
            Body.linearVelocity = Vector2.MoveTowards(Body.linearVelocity, target, rate * fixedDelta);
        }

        public void Accelerate(Vector2 target, float fixedDelta) => ApproachVelocity(target, acceleration, fixedDelta);
        public void Decelerate(float fixedDelta) => ApproachVelocity(Vector2.zero, deceleration, fixedDelta);

        /// <summary>
        /// 상호작용 키를 눌렀고 앞에 대상이 있으면 Interact 상태로 전이한다. 전이했으면 true.
        /// Idle/Move 상태의 Tick에서 호출한다.
        /// </summary>
        public bool TryStartInteract()
        {
            if (!_input.InteractPressed || Sensor.Current == null) return false;
            Fsm.ChangeState(Interact);
            return true;
        }

        /// <summary>바라보는 방향을 직접 정한다(공격 방향 등).</summary>
        public void SetFacing(Facing facing) => Facing = facing;

        /// <summary>
        /// 공격 입력을 눌렀으면 Attack 상태로 전이한다. 전이했으면 true. Idle/Move 상태의 Tick에서 호출한다.
        /// UI(버튼 등) 위에서 누른 클릭은 공격으로 치지 않는다. 무기를 장착하지 않았으면 공격할 수 없고,
        /// 공격하려 했다는 것을 알림으로 알려 준다(<see cref="PlayerItemUser.CanAttack"/>, <see cref="PlayerItemUser.NotifyNoWeapon"/>).
        /// </summary>
        public bool TryStartAttack()
        {
            if (!_input.AttackPressed || Health.IsDead) return false;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return false;
            if (!Items.CanAttack) { Items.NotifyNoWeapon(); return false; }
            Fsm.ChangeState(Attack);
            return true;
        }

        /// <summary>평상시(제자리/이동 중)인지. 인벤토리 열기처럼 다른 연출·잠금 중에는 시작하면 안 되는 동작이 확인한다.</summary>
        public bool IsFree => Fsm.Current == Idle || Fsm.Current == Move;

        /// <summary>인벤토리/대화 등으로 조작을 막거나 푼다. 풀면 Idle로 돌아간다.</summary>
        public void SetControlLocked(bool locked) => Fsm.ChangeState(locked ? (IState)Locked : Idle);
    }
}
