using UnityEngine;

namespace Hangeul.Game.Combat
{
    /// <summary>
    /// 닿으면 피해를 주는 구역(함정, 가시, 테스트용). Trigger 콜라이더가 필요하다. 닿아 있는 동안 계속 시도하지만
    /// 맞는 쪽의 무적 시간(<see cref="Health"/>) 때문에 그 간격으로만 맞는다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class DamageZone : MonoBehaviour
    {
        [SerializeField, Min(1)] int damage = 1;
        [Tooltip("이 구역이 누구 편의 공격인가. 같은 편은 맞지 않는다. 플레이어를 때리려면 Enemy.")]
        [SerializeField] Team team = Team.Enemy;
        [Tooltip("맞은 쪽이 구역 바깥으로 밀려나는 속도(유니티 단위/초). 0이면 밀리지 않는다.")]
        [SerializeField, Min(0f)] float knockbackSpeed = 2.5f;
        [SerializeField, Min(0f)] float stunSeconds = 0.25f;
        [SerializeField] string element = "";

        Collider2D _collider;

        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void Awake() => _collider = GetComponent<Collider2D>();

        void OnTriggerStay2D(Collider2D other)
        {
            var target = other.GetComponentInParent<IDamageable>();
            if (target == null || !Hurtbox.Accepts(target, other)) return;   // 피격 판정이 따로 있는 대상은 그 영역에만 맞는다

            Vector2 away = (Vector2)other.bounds.center - (Vector2)_collider.bounds.center;
            if (away.sqrMagnitude < 1e-6f) away = Vector2.right;
            var info = new DamageInfo(damage, team, away.normalized * knockbackSpeed, stunSeconds,
                string.IsNullOrEmpty(element) ? null : element, gameObject);
            target.TakeDamage(info);
        }
    }
}
