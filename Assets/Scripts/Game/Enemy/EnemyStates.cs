using Hangeul.Game.FSM;
using UnityEngine;

namespace Hangeul.Game.Enemy
{
    /// <summary>적 상태의 공통 기반. 기본 구현은 모두 비어 있고 필요한 것만 override 한다(플레이어 상태와 같은 방식).</summary>
    public abstract class EnemyState : IState
    {
        protected readonly EnemyController Owner;

        protected EnemyState(EnemyController owner) => Owner = owner;

        public virtual void Enter() { }
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDeltaTime) { }
        public virtual void Exit() { }
    }

    /// <summary>
    /// 대기 / 배회. 잠깐 서 있다가 원래 자리 근처의 한 점으로 천천히 걸어가기를 반복한다.
    /// 플레이어가 감지 거리 안으로 들어오면 추격으로 전이한다.
    /// </summary>
    public sealed class EnemyIdleState : EnemyState
    {
        float _timer;
        bool _walking;
        Vector2 _destination;

        public EnemyIdleState(EnemyController owner) : base(owner) { }

        public override void Enter()
        {
            Owner.Anim?.Play(EnemyAnim.Idle);
            StartResting();
        }

        public override void Tick(float deltaTime)
        {
            if (Owner.Aggro.Update(Owner.DistanceToTarget, Owner.TargetAlive))
            {
                Owner.Fsm.ChangeState(Owner.Chase);
                return;
            }

            _timer -= deltaTime;
            if (_walking && (_timer <= 0f || Vector2.Distance(Owner.transform.position, _destination) < 0.05f)) StartResting();
            else if (!_walking && _timer <= 0f) StartWandering();
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            if (!_walking) { Owner.Stop(fixedDeltaTime); return; }

            Vector2 dir = (_destination - (Vector2)Owner.transform.position).normalized;
            Owner.FaceDirection(dir);
            Owner.MoveToward(dir, Owner.MoveSpeed * Owner.WanderSpeedFactor, fixedDeltaTime);
        }

        void StartResting()
        {
            _walking = false;
            _timer = Random.Range(Owner.IdleSecondsMin, Owner.IdleSecondsMax);
            Owner.Anim?.Play(EnemyAnim.Idle);
        }

        void StartWandering()
        {
            if (Owner.WanderRadius <= 0.01f) { StartResting(); return; }
            _destination = Owner.Home + Random.insideUnitCircle * Owner.WanderRadius;
            _walking = true;
            _timer = 2f;   // 도착하지 못해도(벽에 막혀도) 이 시간이 지나면 쉰다
            Owner.Anim?.Play(EnemyAnim.Move);
        }

        public override void Exit() { }
    }

    /// <summary>추격. 플레이어 쪽으로 곧장 걷는다(벽에 닿으면 벽을 따라 미끄러진다). 놓치거나 플레이어가 죽으면 대기로 돌아간다.</summary>
    public sealed class EnemyChaseState : EnemyState
    {
        public EnemyChaseState(EnemyController owner) : base(owner) { }

        public override void Enter() => Owner.Anim?.Play(EnemyAnim.Move);

        public override void Tick(float deltaTime)
        {
            if (!Owner.Aggro.Update(Owner.DistanceToTarget, Owner.TargetAlive))
                Owner.Fsm.ChangeState(Owner.Idle);
        }

        public override void FixedTick(float fixedDeltaTime)
        {
            Vector2 dir = Owner.DirectionToTarget;
            Owner.FaceDirection(dir);
            Owner.MoveToward(dir, Owner.MoveSpeed, fixedDeltaTime);
        }
    }

    /// <summary>
    /// 맞은 직후. 공격이 정한 시간(StunSeconds) 동안 움직이지 못하고 공격이 정한 속도(Knockback)로 밀려난 뒤 서서히 멈춘다.
    /// 이 동안 몸통 접촉 피해는 꺼진다. 끝나면 플레이어를 쫓는다(맞으면 바로 알아챈다).
    /// </summary>
    public sealed class EnemyHurtState : EnemyState
    {
        float _timer;

        public EnemyHurtState(EnemyController owner) : base(owner) { }

        public override void Enter()
        {
            var hit = Owner.Health.LastHit;
            _timer = Mathf.Max(0.05f, hit.StunSeconds);
            Owner.SetContactActive(false);
            Owner.Anim?.Play(EnemyAnim.Hurt, restart: true);
            Owner.Body.linearVelocity = hit.Knockback;
        }

        public override void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            if (_timer > 0f) return;

            if (Owner.TargetAlive) Owner.Aggro.Update(0f);   // 맞았으니 거리와 상관없이 알아챈다
            Owner.Fsm.ChangeState(Owner.Aggro.Active ? (IState)Owner.Chase : Owner.Idle);
        }

        public override void FixedTick(float fixedDeltaTime)
            => Owner.ApproachVelocity(Vector2.zero, Owner.HurtDeceleration, fixedDeltaTime);

        public override void Exit() => Owner.SetContactActive(true);
    }

    /// <summary>죽음. 접촉 피해와 충돌을 끄고, 드롭을 떨어뜨리고, 서서히 사라진 뒤 오브젝트를 없앤다.</summary>
    public sealed class EnemyDeadState : EnemyState
    {
        float _timer;

        public EnemyDeadState(EnemyController owner) : base(owner) { }

        public override void Enter()
        {
            _timer = Owner.DeathSeconds;
            Owner.SetContactActive(false);
            foreach (var col in Owner.GetComponents<Collider2D>()) col.enabled = false;   // 시체는 길을 막지 않는다
            Owner.Anim?.Play(EnemyAnim.Dead, restart: true);
            Owner.SpawnDrops();
        }

        public override void Tick(float deltaTime)
        {
            _timer -= deltaTime;
            Owner.Anim?.SetAlpha(Owner.DeathSeconds > 0f ? Mathf.Clamp01(_timer / Owner.DeathSeconds) : 0f);
            if (_timer <= 0f) Object.Destroy(Owner.gameObject);
        }

        public override void FixedTick(float fixedDeltaTime)
            => Owner.ApproachVelocity(Vector2.zero, Owner.HurtDeceleration, fixedDeltaTime);
    }
}
