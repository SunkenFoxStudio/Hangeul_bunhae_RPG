using System;
using Hangeul.App;
using UnityEngine;
using UnityEngine.Events;

namespace Hangeul.Game.Combat
{
    /// <summary>
    /// 체력 컴포넌트. 정수 체력(<see cref="HitPoints"/>)을 감싸고, 편(<see cref="Team"/>)과 맞은 뒤의 무적 시간을 관리한다.
    /// 맞으면 <see cref="Damaged"/>, 체력이 0이 되면 이어서 <see cref="Died"/>가 발생한다. 경직/밀려남 같은 반응은 이 이벤트를 받는 쪽(플레이어, 적의 FSM)이 한다.
    /// 무적 시간은 게임 시간(<c>Time.time</c>) 기준이라 일시정지 중에는 흐르지 않는다.
    /// </summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] Team team = Team.Neutral;
        [SerializeField, Min(1)] int maxHealth = 6;
        [Tooltip("한 번 맞은 뒤 이 시간(초) 동안은 더 맞지 않는다. 0이면 무적 없음.")]
        [SerializeField, Min(0f)] float invincibleSeconds = 0.8f;
        [Tooltip("죽으면 호출. 인스펙터에서 연결하는 용도(코드에서는 Died 이벤트를 쓴다).")]
        [SerializeField] UnityEvent onDied;

        [Header("Debug")]
        [SerializeField] int currentHealth;

        HitPoints _hp;
        float _invincibleUntil;

        public Team Team => team;
        public int Current => Hp.Current;
        public int Max => Hp.Max;
        public bool IsDead => Hp.IsDead;
        public bool IsInvincible => Time.time < _invincibleUntil;
        /// <summary>가장 최근에 실제로 맞은 공격(경직/밀려남에 쓴다).</summary>
        public DamageInfo LastHit { get; private set; }

        /// <summary>맞았을 때(체력이 깎인 뒤). 인자: 이 체력, 맞은 공격, 실제로 깎인 양.</summary>
        public event Action<Health, DamageInfo, int> Damaged;
        /// <summary>체력이 0이 됐을 때(Damaged 직후 한 번).</summary>
        public event Action<Health> Died;
        /// <summary>체력이 바뀔 때마다(맞음, 회복, 부활, 최대 체력 변경). HUD 갱신용.</summary>
        public event Action<Health> Changed;

        HitPoints Hp => _hp ??= new HitPoints(maxHealth);

        void Awake()
        {
            _hp = new HitPoints(maxHealth);
            currentHealth = _hp.Current;
        }

        /// <summary>코드로 붙인 컴포넌트의 편과 체력을 정한다(플레이어가 자동으로 붙일 때). 가득 채운다.</summary>
        public void Configure(Team newTeam, int max, float invincible)
        {
            team = newTeam;
            maxHealth = Mathf.Max(1, max);
            invincibleSeconds = Mathf.Max(0f, invincible);
            _hp = new HitPoints(maxHealth);
            currentHealth = _hp.Current;
            Changed?.Invoke(this);
        }

        public bool TakeDamage(DamageInfo info)
        {
            if (Hp.IsDead || info.Amount <= 0) return false;
            if (team != Team.Neutral && info.Team == team) return false;   // 같은 편
            if (IsInvincible) return false;

            int taken = Hp.Damage(info.Amount);
            if (taken <= 0) return false;

            LastHit = info;
            if (invincibleSeconds > 0f) _invincibleUntil = Time.time + invincibleSeconds;
            currentHealth = Hp.Current;

            Damaged?.Invoke(this, info, taken);
            Changed?.Invoke(this);
            if (Hp.IsDead)
            {
                Died?.Invoke(this);
                onDied?.Invoke();
            }
            return true;
        }

        public void Heal(int amount)
        {
            if (Hp.Heal(amount) <= 0) return;
            currentHealth = Hp.Current;
            Changed?.Invoke(this);
        }

        /// <summary>되살린다(가득 채움). 무적도 풀어 준다.</summary>
        public void Revive()
        {
            Hp.Revive();
            _invincibleUntil = 0f;
            currentHealth = Hp.Current;
            Changed?.Invoke(this);
        }

        /// <summary>일정 시간 무적으로 만든다(부활 직후 등).</summary>
        public void GrantInvincibility(float seconds) => _invincibleUntil = Mathf.Max(_invincibleUntil, Time.time + seconds);

        void OnValidate()
        {
            // 플레이 중에 인스펙터에서 최대 체력을 바꾸면 바로 반영한다
            if (Application.isPlaying && _hp != null && _hp.Max != maxHealth)
            {
                _hp.SetMax(maxHealth, fill: false);
                currentHealth = _hp.Current;
            }
        }
    }
}
