using System;

namespace Hangeul.App
{
    // 단어 종류별 수치. 게임 데이터 에셋(ItemDatabase)의 단어 목록이 종류마다 이 중 하나만 가진다 —
    // 음식에 피해량 칸이 없고 무기에 회복량 칸이 없다. 파일(JSON)과 게임 규칙은 모든 칸을 합친 ItemStats를 쓰므로
    // 서로 바꾸는 함수(ToItemStats / From)가 있다. 필드 초기값이 그 종류의 기본값이다(ItemStats의 기본값과 같다).

    /// <summary>무기(근접 공격)의 수치.</summary>
    [Serializable]
    public sealed class WeaponStats
    {
        public int damage = 1;
        public float knockback = 4f;
        public float stun = 0.3f;
        public float reach = 1.27f;
        public float width = 1.2f;
        public float windup = 0.08f;
        public float active = 0.12f;
        public float recovery = 0.18f;

        public WeaponStats Clone() => (WeaponStats)MemberwiseClone();

        public bool SameAs(WeaponStats o) => o != null && ToItemStats().SameAs(o.ToItemStats());

        public ItemStats ToItemStats() => new ItemStats
        {
            damage = damage, knockback = knockback, stun = stun, reach = reach, width = width,
            windup = windup, active = active, recovery = recovery,
        };

        public static WeaponStats From(ItemStats s) => new WeaponStats
        {
            damage = s.damage, knockback = s.knockback, stun = s.stun, reach = s.reach, width = s.width,
            windup = s.windup, active = s.active, recovery = s.recovery,
        };
    }

    /// <summary>음식의 수치.</summary>
    [Serializable]
    public sealed class FoodStats
    {
        /// <summary>먹었을 때 회복하는 체력. 하트 한 칸 = 체력 2.</summary>
        public int heal = 2;

        public FoodStats Clone() => (FoodStats)MemberwiseClone();

        public bool SameAs(FoodStats o) => o != null && heal == o.heal;

        public ItemStats ToItemStats() => new ItemStats { heal = heal };

        public static FoodStats From(ItemStats s) => new FoodStats { heal = s.heal };
    }

    /// <summary>접두어 효과 — 대미지 더하기: 이 접두어가 붙은 무기의 피해량에 값을 더하거나 곱한다.</summary>
    [Serializable]
    public sealed class PrefixDamageEffect
    {
        public DamageMode mode = DamageMode.Add;
        /// <summary>더하기면 더할 값(기본 0 = 변화 없음), 곱하기면 곱할 값(1 = 변화 없음).</summary>
        public float amount = 0f;

        public PrefixDamageEffect Clone() => (PrefixDamageEffect)MemberwiseClone();

        public bool SameAs(PrefixDamageEffect o) => o != null && ToItemStats().SameAs(o.ToItemStats());

        public ItemStats ToItemStats() => new ItemStats { prefixKind = PrefixEffectKind.Damage, damageMode = mode, damageAmount = amount };

        public static PrefixDamageEffect From(ItemStats s) => new PrefixDamageEffect { mode = s.damageMode, amount = s.damageAmount };
    }

    /// <summary>접두어 효과 — 속성 묻히기: 때릴 때 일정 확률로 이 단어(예: 화염)가 적에게 묻고 머리 위에 "화염!"이 뜬다.</summary>
    [Serializable]
    public sealed class PrefixElementEffect
    {
        /// <summary>묻을 확률(퍼센트, 0~100). 기본 0% = 묻지 않음.</summary>
        public float chancePercent = 0f;
        /// <summary>머리 위 글자의 색("#RRGGBB").</summary>
        public string color = ColorHex.DefaultElement;

        public PrefixElementEffect Clone() => (PrefixElementEffect)MemberwiseClone();

        public bool SameAs(PrefixElementEffect o) => o != null && ToItemStats().SameAs(o.ToItemStats());

        public ItemStats ToItemStats() => new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = chancePercent, elementColor = color };

        public static PrefixElementEffect From(ItemStats s)
            => new PrefixElementEffect { chancePercent = s.elementChance, color = ColorHex.NormalizeOr(s.elementColor, ColorHex.DefaultElement) };
    }
}
