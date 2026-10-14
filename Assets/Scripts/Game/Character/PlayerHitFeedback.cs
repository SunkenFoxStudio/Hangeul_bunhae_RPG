using Hangeul.Game.Combat;
using Hangeul.Game.Feedback;
using UnityEngine;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// 플레이어의 전투 연출 모음(타격감): 맞았을 때 번쩍임·짧은 멈춤·화면 흔들림, 무적 시간 동안 깜빡임,
    /// 공격이 적중했을 때의 짧은 멈춤·화면 흔들림. 값은 모두 인스펙터에서 조절한다(0이면 그 연출을 끈다).
    /// 게임 규칙에는 관여하지 않고 이벤트(Health.Damaged, PlayerMeleeAttack.Struck)를 받아 보여주기만 한다.
    /// </summary>
    [AddComponentMenu("Hangeul/Player Hit Feedback")]
    public sealed class PlayerHitFeedback : MonoBehaviour
    {
        [Header("맞았을 때")]
        [Tooltip("맞은 순간 게임이 멈추는 시간(초).")]
        [SerializeField, Min(0f)] float hurtHitStop = 0.08f;
        [Tooltip("화면 흔들림 세기(월드 단위, 0.03 ≈ 3픽셀)와 시간(초).")]
        [SerializeField, Min(0f)] float hurtShakeStrength = 0.04f;
        [SerializeField, Min(0f)] float hurtShakeSeconds = 0.2f;
        [SerializeField, Min(0f)] float flashSeconds = 0.12f;
        [SerializeField] Color flashColor = new Color(1f, 0.35f, 0.35f, 1f);

        [Header("무적 시간 동안 깜빡임")]
        [SerializeField, Min(1f)] float blinkPerSecond = 10f;
        [SerializeField, Range(0f, 1f)] float blinkAlpha = 0.35f;

        [Header("공격이 적중했을 때")]
        [SerializeField, Min(0f)] float strikeHitStop = 0.05f;
        [SerializeField, Min(0f)] float strikeShakeStrength = 0.015f;
        [SerializeField, Min(0f)] float strikeShakeSeconds = 0.08f;

        Health _health;
        PlayerMeleeAttack _melee;
        SpriteRenderer _sprite;
        Color _baseColor = Color.white;
        float _flashUntil;

        void Awake()
        {
            _health = GetComponent<Health>();
            // 플레이어 그림의 SpriteRenderer(공격 이펙트 Slash의 것이 아니라)
            var animator = GetComponentInChildren<PlayerSpriteAnimator>();
            _sprite = animator != null ? animator.GetComponent<SpriteRenderer>() : null;
            if (_sprite != null) _baseColor = _sprite.color;
        }

        // 컴포넌트가 만들어지는 순서와 상관없이 다른 컴포넌트를 찾도록 Start에서 구독한다
        void Start()
        {
            _melee = GetComponent<PlayerMeleeAttack>();
            if (_health != null) _health.Damaged += OnDamaged;
            if (_melee != null) _melee.Struck += OnStruck;
        }

        void OnDestroy()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            if (_melee != null) _melee.Struck -= OnStruck;
        }

        void OnDamaged(Health health, DamageInfo info, int taken)
        {
            _flashUntil = Time.time + flashSeconds;
            CameraShake.Shake(hurtShakeStrength, hurtShakeSeconds);
            HitStop.Request(hurtHitStop);
        }

        void OnStruck(int hits)
        {
            CameraShake.Shake(strikeShakeStrength, strikeShakeSeconds);
            HitStop.Request(strikeHitStop);
        }

        void Update()
        {
            if (_sprite == null || _health == null) return;

            Color c = Time.time < _flashUntil ? flashColor : _baseColor;
            // 무적 시간 동안 깜빡인다(죽었을 때는 제외)
            if (_health.IsInvincible && !_health.IsDead)
                c.a *= Mathf.FloorToInt(Time.time * blinkPerSecond * 2f) % 2 == 0 ? 1f : blinkAlpha;
            _sprite.color = c;
        }
    }
}
