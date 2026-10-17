using System;
using System.Collections.Generic;

namespace Hangeul.App
{
    /// <summary>
    /// 접두어·속성이 붙은 무기('화염검' = 화염 + 검)에 더해지는 효과 하나. 게임 엔진과 무관한 순수 규칙이다.
    /// 효과는 둘 중 하나다 — 피해량을 더하거나 곱하거나(<see cref="ApplyDamage"/>), 때릴 때 일정 확률로 속성이 적에게 묻는다
    /// (<see cref="RollElement"/>, 묻으면 적 머리 위에 "화염!"이 뜬다).
    /// </summary>
    public sealed class WeaponModifier
    {
        public PrefixEffectKind Kind { get; }
        public DamageMode Mode { get; }
        public float Amount { get; }
        /// <summary>묻는 속성의 이름(= 접두어 단어 자신). 속성 묻히기가 아니면 null.</summary>
        public string Element { get; }
        /// <summary>속성이 묻을 확률(0~1).</summary>
        public float ElementChance { get; }
        /// <summary>속성이 묻었을 때 뜨는 글자의 색("#RRGGBB"). 속성 묻히기가 아니면 null.</summary>
        public string ElementColor { get; }

        WeaponModifier(PrefixEffectKind kind, DamageMode mode, float amount, string element, float chance, string color)
        {
            Kind = kind; Mode = mode; Amount = amount; Element = element; ElementChance = chance; ElementColor = color;
        }

        /// <summary>
        /// 접두어 단어의 수치(<see cref="ItemWordSet.StatsOf"/>)로 효과를 만든다. 수치가 없거나 아무 변화가 없는 값
        /// (더하기 0, 곱하기 1, 확률 0%)이면 null — 효과를 걸 필요가 없다.
        /// </summary>
        public static WeaponModifier From(string prefixWord, ItemStats stats)
        {
            if (stats == null || string.IsNullOrEmpty(prefixWord)) return null;
            var s = stats.Clamped();

            if (s.prefixKind == PrefixEffectKind.Element)
                return s.elementChance <= 0f ? null : new WeaponModifier(PrefixEffectKind.Element, DamageMode.Add, 0f, prefixWord, s.elementChance / 100f, s.elementColor);

            bool neutral = s.damageMode == DamageMode.Multiply ? Math.Abs(s.damageAmount - 1f) < 1e-5f : Math.Abs(s.damageAmount) < 1e-5f;
            return neutral ? null : new WeaponModifier(PrefixEffectKind.Damage, s.damageMode, s.damageAmount, null, 0f, null);
        }

        /// <summary>피해량에 이 효과를 적용한 값(반올림, 최소 1). 대미지 더하기가 아니면 그대로.</summary>
        public int ApplyDamage(int baseDamage) => Kind == PrefixEffectKind.Damage ? ApplyDamage(baseDamage, Mode, Amount) : baseDamage;

        /// <summary>피해량 계산: 더하기면 base + 값, 곱하기면 base × 값. 반올림(0.5는 올림)하고 1보다 작아지지 않는다.</summary>
        public static int ApplyDamage(int baseDamage, DamageMode mode, float amount)
        {
            double result = mode == DamageMode.Multiply ? baseDamage * (double)amount : baseDamage + (double)amount;
            return Math.Max(1, (int)Math.Round(result, MidpointRounding.AwayFromZero));
        }

        /// <summary>
        /// 속성이 이번 타격에서 묻는지. roll은 0 이상 1 미만의 난수(0.3이면 확률 30%일 때 묻는다).
        /// 속성 묻히기가 아니면 항상 false.
        /// </summary>
        public bool RollElement(double roll) => Kind == PrefixEffectKind.Element && Element != null && roll < ElementChance;

        // ───── 여럿을 겹쳐 쓸 때 (활: 활에 붙은 접두어 + 쏘는 화살에 붙은 접두어) ─────
        /// <summary>
        /// 피해량에 효과들을 앞에서부터 차례로 적용한다(null은 건너뛴다). 효과마다 반올림하고 1보다 작아지지 않는다.
        /// 효과가 하나도 없으면 피해량 그대로(1보다 작으면 1).
        /// </summary>
        public static int ApplyDamage(int baseDamage, IEnumerable<WeaponModifier> modifiers)
        {
            int damage = Math.Max(1, baseDamage);
            if (modifiers == null) return damage;
            foreach (var m in modifiers)
                if (m != null) damage = m.ApplyDamage(damage);
            return damage;
        }

        /// <summary>
        /// 속성 묻히기 효과들을 앞에서부터 차례로 굴려(효과마다 따로 <paramref name="random"/>을 한 번씩 부른다) 처음으로 묻는 효과를 돌려준다.
        /// 아무것도 묻지 않으면 null. 속성 묻히기가 아닌 효과와 null은 굴리지 않는다.
        /// </summary>
        public static WeaponModifier RollElement(IEnumerable<WeaponModifier> modifiers, Func<double> random)
        {
            if (modifiers == null || random == null) return null;
            foreach (var m in modifiers)
                if (m != null && m.Kind == PrefixEffectKind.Element && m.RollElement(random())) return m;
            return null;
        }
    }
}
