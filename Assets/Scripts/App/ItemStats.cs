using System;

namespace Hangeul.App
{
    /// <summary>접두어·속성 단어가 합성 단어에 주는 효과의 종류.</summary>
    public enum PrefixEffectKind
    {
        Damage = 0,    // 대미지 더하기: 무기의 피해량에 값을 더하거나 곱한다
        Element = 1,   // 속성 묻히기: 때릴 때 일정 확률로 그 단어(예: 화염)가 적에게 묻는다
    }

    /// <summary>대미지 더하기의 계산 방식.</summary>
    public enum DamageMode
    {
        Add = 0,        // 피해량 + 값
        Multiply = 1,   // 피해량 × 값
    }

    /// <summary>
    /// 단어 하나의 상세 수치(DB 관리 씬에서 단어마다 열고 닫으며 고친다). 필드마다 쓰는 단어 종류가 다르다:
    /// 무기(<see cref="ItemKind.Weapon"/>)는 근접 공격 수치, 음식(<see cref="ItemKind.Food"/>)은 <see cref="heal"/>,
    /// 접두어·속성 카테고리의 단어(<see cref="ItemWordSet.IsPrefixCategory"/>)는 <see cref="prefixKind"/> 이하의 효과.
    /// 필드 초기값이 곧 <b>기본값</b>이다 — 수치를 따로 정하지 않은 단어는 이 값으로 보이지만, 게임은 그런 단어에는 이 값을 쓰지 않고
    /// 원래 설정(플레이어의 근접 공격 컴포넌트, 음식 기본 회복량, 효과 없음)을 그대로 쓴다(<see cref="ItemWordSet.StatsOf"/>가 null).
    /// 무기 기본값은 지금 씬의 플레이어 근접 공격 설정과 같게 맞춰 두었다.
    /// </summary>
    [Serializable]
    public sealed class ItemStats
    {
        // ───── 무기 (근접 공격) ─────
        /// <summary>한 번 맞출 때의 피해(체력). 하트 한 칸 = 체력 2.</summary>
        public int damage = 1;
        /// <summary>맞은 쪽이 공격 방향으로 밀려나는 속도(유니티 단위/초).</summary>
        public float knockback = 4f;
        /// <summary>맞은 쪽이 움직일 수 없는 시간(초).</summary>
        public float stun = 0.3f;
        /// <summary>공격 시작점에서 앞쪽으로 뻗는 길이(유니티 단위).</summary>
        public float reach = 1.27f;
        /// <summary>앞쪽 상자의 폭(공격 방향의 수직 방향 길이, 유니티 단위).</summary>
        public float width = 1.2f;
        /// <summary>준비 동작 시간(초). 이 동안은 아직 맞지 않는다.</summary>
        public float windup = 0.08f;
        /// <summary>판정이 켜져 있는 시간(초).</summary>
        public float active = 0.12f;
        /// <summary>마무리 동작 시간(초). 판정은 끝났지만 아직 다시 공격할 수 없다.</summary>
        public float recovery = 0.18f;

        // ───── 음식 ─────
        /// <summary>먹었을 때 회복하는 체력. 하트 한 칸 = 체력 2.</summary>
        public int heal = 2;

        // ───── 접두어·속성 (이 단어가 앞에 붙은 합성 단어의 무기에 주는 효과) ─────
        /// <summary>효과의 종류: 대미지 더하기 / 속성 묻히기.</summary>
        public PrefixEffectKind prefixKind = PrefixEffectKind.Damage;
        /// <summary>대미지 더하기의 계산 방식(더하기 / 곱하기).</summary>
        public DamageMode damageMode = DamageMode.Add;
        /// <summary>대미지 더하기의 값. 더하기면 피해량에 더할 값(0이면 변화 없음), 곱하기면 곱할 값(1이면 변화 없음).</summary>
        public float damageAmount = 0f;
        /// <summary>속성 묻히기의 확률(퍼센트, 0~100). 속성 이름은 이 단어 자신이다(예: 화염 → "화염!").</summary>
        public float elementChance = 0f;
        /// <summary>속성이 묻었을 때 적 머리 위에 뜨는 글자의 색("#RRGGBB").</summary>
        public string elementColor = ColorHex.DefaultElement;

        public ItemStats Clone() => (ItemStats)MemberwiseClone();

        public void CopyFrom(ItemStats o)
        {
            damage = o.damage; knockback = o.knockback; stun = o.stun;
            reach = o.reach; width = o.width;
            windup = o.windup; active = o.active; recovery = o.recovery;
            heal = o.heal;
            prefixKind = o.prefixKind; damageMode = o.damageMode; damageAmount = o.damageAmount; elementChance = o.elementChance;
            elementColor = o.elementColor;
        }

        public bool SameAs(ItemStats o)
            => o != null && damage == o.damage && heal == o.heal
               && prefixKind == o.prefixKind && damageMode == o.damageMode && elementColor == o.elementColor
               && Near(knockback, o.knockback) && Near(stun, o.stun) && Near(reach, o.reach) && Near(width, o.width)
               && Near(windup, o.windup) && Near(active, o.active) && Near(recovery, o.recovery)
               && Near(damageAmount, o.damageAmount) && Near(elementChance, o.elementChance);

        static bool Near(float a, float b) => Math.Abs(a - b) < 1e-5f;

        /// <summary>손으로 고친 파일이나 잘못 친 값을 쓸 수 있는 범위로 맞춘 복사본. 게임은 항상 이 값을 쓴다.</summary>
        public ItemStats Clamped()
        {
            var kind = Enum.IsDefined(typeof(PrefixEffectKind), prefixKind) ? prefixKind : PrefixEffectKind.Damage;
            var mode = Enum.IsDefined(typeof(DamageMode), damageMode) ? damageMode : DamageMode.Add;
            return new ItemStats
            {
                damage = Clamp(damage, 1, 9999),
                knockback = Clamp(knockback, 0f, 100f),
                stun = Clamp(stun, 0f, 10f),
                reach = Clamp(reach, 0.01f, 30f),
                width = Clamp(width, 0.01f, 30f),
                windup = Clamp(windup, 0f, 10f),
                active = Clamp(active, 0.01f, 10f),    // 판정 시간이 0이면 공격이 아무도 맞히지 못한다
                recovery = Clamp(recovery, 0f, 10f),
                heal = Clamp(heal, 1, 9999),
                prefixKind = kind,
                damageMode = mode,
                // 곱하기는 0 이하로 가면 피해가 사라지므로 0.1배부터, 더하기는 깎는 값(-)도 허용한다
                damageAmount = mode == DamageMode.Multiply ? Clamp(damageAmount, 0.1f, 99f) : Clamp(damageAmount, -999f, 999f),
                elementChance = Clamp(elementChance, 0f, 100f),
                elementColor = ColorHex.NormalizeOr(elementColor, ColorHex.DefaultElement),   // 색이 아닌 글자(빈 칸, 오타)는 기본 색으로
            };
        }

        static int Clamp(int v, int lo, int hi) => Math.Max(lo, Math.Min(hi, v));

        // NaN은 비교가 모두 거짓이라 그대로 새어 나가므로 따로 막는다
        static float Clamp(float v, float lo, float hi) => float.IsNaN(v) ? lo : Math.Max(lo, Math.Min(hi, v));
    }

    /// <summary>파일에 적는 단어별 수치 한 줄. 수치를 따로 정한 단어만 목록에 있다(나머지는 기본값).</summary>
    [Serializable]
    public sealed class WordStatsEntry
    {
        public string word;
        public ItemStats stats = new ItemStats();
    }
}
