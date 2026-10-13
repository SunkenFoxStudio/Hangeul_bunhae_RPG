using Hangeul.Game.FSM;

namespace Hangeul.Game.Character
{
    /// <summary>플레이어 상태의 공통 기반. 기본 구현은 모두 비어 있고 필요한 것만 override 한다.</summary>
    public abstract class PlayerState : IState
    {
        protected readonly PlayerController Owner;

        protected PlayerState(PlayerController owner) => Owner = owner;

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDeltaTime) { }
        public virtual void Exit() { }
    }

    /// <summary>제자리. 입력이 생기면 Move로 전이하고, 남은 속도는 감속한다.</summary>
    public sealed class PlayerIdleState : PlayerState
    {
        public PlayerIdleState(PlayerController owner) : base(owner) { }

        public override void Enter() => Owner.Anim?.Play(PlayerAnim.Idle);

        public override void Tick(float deltaTime)
        {
            if (Owner.TryStartInteract()) return;
            if (Owner.HasMoveInput) Owner.Fsm.ChangeState(Owner.Move);
        }

        public override void FixedTick(float fixedDeltaTime) => Owner.Decelerate(fixedDeltaTime);
    }

    /// <summary>이동 중. 입력이 사라지면 Idle로 전이한다.</summary>
    public sealed class PlayerMoveState : PlayerState
    {
        public PlayerMoveState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            Owner.UpdateFacingFromInput();
            Owner.Anim?.Play(PlayerAnim.Walk);
        }

        public override void Tick(float deltaTime)
        {
            if (Owner.TryStartInteract()) return;
            if (!Owner.HasMoveInput) { Owner.Fsm.ChangeState(Owner.Idle); return; }
            Owner.UpdateFacingFromInput();
        }

        public override void FixedTick(float fixedDeltaTime) =>
            Owner.Accelerate(Owner.MoveInput * Owner.MoveSpeed, fixedDeltaTime);
    }

    /// <summary>
    /// 상호작용 실행. 진입 시 대상의 Interact를 호출하고 멈춘 채 짧게 대기한 뒤 Idle로 돌아간다.
    /// 대상이 Interact 안에서 <see cref="PlayerController.SetControlLocked"/>를 부르면 Locked로 넘어가 이 상태는 끝난다.
    /// </summary>
    public sealed class PlayerInteractState : PlayerState
    {
        float _timer;

        public PlayerInteractState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            _timer = Owner.InteractDuration;
            Owner.Anim?.Play(PlayerAnim.Idle);
            Owner.Sensor.Current?.Interact(Owner);
        }

        public override void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer <= 0f) Owner.Fsm.ChangeState(Owner.Idle);
        }

        public override void FixedTick(float fixedDeltaTime) => Owner.Decelerate(fixedDeltaTime);
    }

    /// <summary>조작 불가(인벤토리 열림, 대화, 컷신 등). 입력을 무시하고 멈춘다.</summary>
    public sealed class PlayerLockedState : PlayerState
    {
        public PlayerLockedState(PlayerController owner) : base(owner) { }

        public override void Enter() => Owner.Anim?.Play(PlayerAnim.Idle);

        public override void FixedTick(float fixedDeltaTime) => Owner.Decelerate(fixedDeltaTime);
    }
}
