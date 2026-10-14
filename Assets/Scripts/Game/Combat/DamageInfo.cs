using UnityEngine;

namespace Hangeul.Game.Combat
{
    /// <summary>
    /// 공격 한 번의 정보. 공격하는 쪽이 만들어 <see cref="IDamageable.TakeDamage"/>에 넘긴다.
    /// 맞은 쪽은 이 값으로 체력을 깎고, 밀려나고(<see cref="Knockback"/>), 잠깐 움직이지 못한다(<see cref="StunSeconds"/>).
    /// </summary>
    public readonly struct DamageInfo
    {
        /// <summary>깎을 체력(정수, 하트 방식: 1, 2 …).</summary>
        public readonly int Amount;
        /// <summary>공격하는 편. 맞는 쪽과 같은 편이면 피해가 없다.</summary>
        public readonly Team Team;
        /// <summary>맞은 쪽이 밀려나는 속도(유니티 단위/초, 방향 포함). 0이면 밀리지 않는다.</summary>
        public readonly Vector2 Knockback;
        /// <summary>맞은 뒤 움직일 수 없는 시간(초).</summary>
        public readonly float StunSeconds;
        /// <summary>속성(단어: 불, 얼음 …). 없으면 null. 속성 효과는 아이템 단계에서 쓴다.</summary>
        public readonly string Element;
        /// <summary>때린 오브젝트(없을 수 있음).</summary>
        public readonly GameObject Source;

        public DamageInfo(int amount, Team team, Vector2 knockback = default, float stunSeconds = 0.25f, string element = null, GameObject source = null)
        {
            Amount = amount;
            Team = team;
            Knockback = knockback;
            StunSeconds = stunSeconds;
            Element = element;
            Source = source;
        }
    }
}
