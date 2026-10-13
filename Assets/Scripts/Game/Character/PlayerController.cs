using Hangeul.Game.FSM;
using Hangeul.Game.Interaction;
using UnityEngine;

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

        [Header("Debug")]
        [SerializeField] string currentState;

        public Rigidbody2D Body { get; private set; }
        public PlayerSpriteAnimator Anim => spriteAnimator;
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

            Idle = new PlayerIdleState(this);
            Move = new PlayerMoveState(this);
            Locked = new PlayerLockedState(this);
            Interact = new PlayerInteractState(this);

            Fsm.StateChanged += (_, next) => currentState = next.GetType().Name;
        }

        void Start() => Fsm.Initialize(Idle);

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

        /// <summary>평상시(제자리/이동 중)인지. 인벤토리 열기처럼 다른 연출·잠금 중에는 시작하면 안 되는 동작이 확인한다.</summary>
        public bool IsFree => Fsm.Current == Idle || Fsm.Current == Move;

        /// <summary>인벤토리/대화 등으로 조작을 막거나 푼다. 풀면 Idle로 돌아간다.</summary>
        public void SetControlLocked(bool locked) => Fsm.ChangeState(locked ? (IState)Locked : Idle);
    }
}
