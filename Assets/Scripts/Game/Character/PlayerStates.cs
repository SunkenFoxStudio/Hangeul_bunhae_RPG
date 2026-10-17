using Hangeul.Game.FSM;
using UnityEngine;

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
            if (Owner.TryStartAttack()) return;
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
            if (Owner.TryStartAttack()) return;
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

    /// <summary>
    /// 맞은 직후. 공격이 정한 시간(<see cref="Combat.DamageInfo.StunSeconds"/>) 동안 조작할 수 없고,
    /// 공격이 정한 속도(<see cref="Combat.DamageInfo.Knockback"/>)로 밀려난 뒤 서서히 멈춘다. 끝나면 Idle(체력이 0이면 Dead가 이 상태를 덮어쓴다).
    /// </summary>
    public sealed class PlayerHurtState : PlayerState
    {
        float _timer;

        public PlayerHurtState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            var hit = Owner.Health.LastHit;
            _timer = Mathf.Max(0.05f, hit.StunSeconds);
            Owner.Anim?.PlayFitted(PlayerAnim.Hurt, _timer);   // 비어 있으면 Idle이 보인다
            Owner.Body.linearVelocity = hit.Knockback;
        }

        public override void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer <= 0f) Owner.Fsm.ChangeState(Owner.Idle);
        }

        public override void FixedTick(float fixedDeltaTime) =>
            Owner.ApproachVelocity(Vector2.zero, Owner.HurtDeceleration, fixedDeltaTime);
    }

    /// <summary>
    /// 체력이 0이 됐을 때. 잠깐 쓰러져 있다가(<see cref="PlayerController.RespawnDelay"/>) 화면이 까맣게 덮이는 사이에
    /// 마지막 체크포인트로 돌아가 되살아난다(<see cref="PlayerController.BeginRespawn"/>).
    /// </summary>
    public sealed class PlayerDeadState : PlayerState
    {
        float _timer;
        bool _respawning;

        public PlayerDeadState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            _timer = Owner.RespawnDelay;
            _respawning = false;
            Owner.Anim?.Play(PlayerAnim.Dead, restart: true);   // 한 번 재생하고 마지막 장면에 머문다(비어 있으면 Idle)
        }

        public override void Tick(float deltaTime)
        {
            if (_respawning) return;
            _timer -= deltaTime;
            if (_timer > 0f) return;

            _respawning = true;
            Owner.BeginRespawn();
        }

        public override void FixedTick(float fixedDeltaTime) =>
            Owner.ApproachVelocity(Vector2.zero, Owner.HurtDeceleration, fixedDeltaTime);
    }

    /// <summary>
    /// 근접 공격. 준비 → 판정 → 마무리 시간표(<see cref="PlayerMeleeAttack.Timeline"/>)대로 흐른다. 시간은 애니메이션 프레임 수와 무관하다.
    /// 방향은 시작할 때 정해지면 끝까지 유지된다. 판정 구간에는 앞쪽 상자 안의 피해 가능한 대상을 한 번씩 때린다.
    /// 준비·판정 동안 살짝 앞으로 나아가고(lunge), 마무리에는 멈춘다. 맞으면 Hurt가 이 상태를 끊는다.
    /// 장착한 무기가 원거리(활)면 제자리에서 시위를 당기고(준비 시간) 그 시간이 끝나는 순간 화살 하나를 쏜 뒤 쉰다(마무리 시간).
    /// 쏠 화살은 그 순간 가방에서 다시 고른다(그새 가방에서 없어졌으면 쏘지 못한다).
    /// </summary>
    public sealed class PlayerAttackState : PlayerState
    {
        readonly System.Collections.Generic.HashSet<Combat.IDamageable> _hit = new System.Collections.Generic.HashSet<Combat.IDamageable>();
        float _t;
        bool _effectShown;
        bool _ranged;     // 이번 공격이 활인지(시작할 때 정해진다)
        bool _fired;
        Vector2 _dir;

        public PlayerAttackState(PlayerController owner) : base(owner) { }

        public override void Enter()
        {
            var melee = Owner.Melee;
            Owner.SetFacing(melee.ChooseFacing(Owner));
            _dir = FacingUtil.ToVector(Owner.Facing);
            _t = 0f;
            _effectShown = false;
            _fired = false;
            _ranged = Owner.Items.WeaponIsRanged;
            _hit.Clear();
            // 프레임이 몇 장이든 공격 한 번(준비+판정+마무리)의 시간 안에 모션 전체가 재생된다
            Owner.Anim?.PlayFitted(PlayerAnim.Attack, Timeline.Total);
        }

        // 이번 공격의 시간표: 활이면 활의 것(준비 → 쏨 → 마무리), 아니면 근접의 것
        Hangeul.App.AttackTimeline Timeline => _ranged ? Owner.Ranged.Timeline : Owner.Melee.Timeline;

        public override void Tick(float deltaTime)
        {
            var melee = Owner.Melee;
            _t += deltaTime;

            var phase = Timeline.PhaseAt(_t);
            if (_ranged)
            {
                // 준비가 끝나는 순간 한 번 쏜다(프레임이 길어서 그 순간을 건너뛰어도 한 번은 쏜다)
                if (!_fired && phase >= Hangeul.App.AttackPhase.Active)
                {
                    _fired = true;
                    if (Owner.Items.TryPickArrow(out _, out var arrowModifier)) Owner.Ranged.Fire(Owner.Facing, arrowModifier);
                }
            }
            // 프레임이 길어서 판정 구간을 건너뛰어도 한 번은 반드시 친다
            else if (phase == Hangeul.App.AttackPhase.Active || (phase >= Hangeul.App.AttackPhase.Recovery && !_effectShown))
            {
                if (!_effectShown) { melee.ShowEffect(Owner.Facing); _effectShown = true; }
                melee.Strike(Owner.Facing, _hit);
            }
            if (phase == Hangeul.App.AttackPhase.Done)
                Owner.Fsm.ChangeState(Owner.HasMoveInput ? (IState)Owner.Move : Owner.Idle);
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            var phase = Timeline.PhaseAt(_t);
            if (_ranged || phase == Hangeul.App.AttackPhase.Recovery || phase == Hangeul.App.AttackPhase.Done)
                Owner.Decelerate(fixedDeltaTime);   // 활은 앞으로 나아가지 않고 제자리에서 쏜다
            else
                Owner.ApproachVelocity(_dir * Owner.Melee.LungeSpeed, 40f, fixedDeltaTime);   // 공격하는 쪽으로 살짝 나아간다
        }

        public override void Exit() => Owner.Melee.HideEffect();
    }
}
