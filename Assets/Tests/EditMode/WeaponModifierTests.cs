using System.Collections.Generic;
using Hangeul.App;
using NUnit.Framework;
using UnityEngine;

namespace Hangeul.Tests
{
    public class WeaponModifierTests
    {
        static ItemStats Damage(DamageMode mode, float amount) => new ItemStats { prefixKind = PrefixEffectKind.Damage, damageMode = mode, damageAmount = amount };
        static ItemStats Element(float percent) => new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = percent };

        // ───── 대미지 더하기 ─────
        [Test]
        public void ApplyDamage_Add_AddsTheValue()
        {
            Assert.AreEqual(5, WeaponModifier.ApplyDamage(3, DamageMode.Add, 2f));
            Assert.AreEqual(3, WeaponModifier.ApplyDamage(3, DamageMode.Add, 0f));
        }

        [Test]
        public void ApplyDamage_Multiply_MultipliesAndRoundsHalfUp()
        {
            Assert.AreEqual(6, WeaponModifier.ApplyDamage(3, DamageMode.Multiply, 2f));
            Assert.AreEqual(5, WeaponModifier.ApplyDamage(3, DamageMode.Multiply, 1.5f));   // 4.5 → 5
            Assert.AreEqual(3, WeaponModifier.ApplyDamage(3, DamageMode.Multiply, 1f));
        }

        [Test]
        public void ApplyDamage_NeverGoesBelowOne()
        {
            Assert.AreEqual(1, WeaponModifier.ApplyDamage(3, DamageMode.Add, -10f));
            Assert.AreEqual(1, WeaponModifier.ApplyDamage(1, DamageMode.Multiply, 0.1f));
        }

        [Test]
        public void From_DamageEffect_AppliesToTheBaseDamage()
        {
            var add = WeaponModifier.From("강화", Damage(DamageMode.Add, 2f));
            Assert.AreEqual(PrefixEffectKind.Damage, add.Kind);
            Assert.AreEqual(4, add.ApplyDamage(2));

            var mul = WeaponModifier.From("강화", Damage(DamageMode.Multiply, 3f));
            Assert.AreEqual(6, mul.ApplyDamage(2));
            Assert.IsNull(mul.Element);
            Assert.IsFalse(mul.RollElement(0.0));   // 속성이 아니면 절대 묻지 않는다
        }

        [Test]
        public void From_NoChangeValues_GiveNoModifier()
        {
            Assert.IsNull(WeaponModifier.From("강화", null));
            Assert.IsNull(WeaponModifier.From("강화", Damage(DamageMode.Add, 0f)));        // 더하기 0
            Assert.IsNull(WeaponModifier.From("강화", Damage(DamageMode.Multiply, 1f)));   // 곱하기 1
            Assert.IsNull(WeaponModifier.From("화염", Element(0f)));                        // 확률 0%
            Assert.IsNull(WeaponModifier.From("", Damage(DamageMode.Add, 5f)));
        }

        // ───── 속성 묻히기 ─────
        [Test]
        public void RollElement_UsesThePercentChance()
        {
            var m = WeaponModifier.From("화염", Element(30f));
            Assert.AreEqual("화염", m.Element);
            Assert.AreEqual(0.3f, m.ElementChance, 1e-6f);
            Assert.IsTrue(m.RollElement(0.0));
            Assert.IsTrue(m.RollElement(0.299));
            Assert.IsFalse(m.RollElement(0.301));  // 확률(30%)을 넘으면 묻지 않는다
            Assert.IsFalse(m.RollElement(0.99));
        }

        [Test]
        public void RollElement_OneHundredPercent_AlwaysApplies()
        {
            var m = WeaponModifier.From("화염", Element(100f));
            Assert.IsTrue(m.RollElement(0.0));
            Assert.IsTrue(m.RollElement(0.9999));
        }

        [Test]
        public void ElementModifier_DoesNotChangeTheDamage()
        {
            var m = WeaponModifier.From("화염", Element(50f));
            Assert.AreEqual(7, m.ApplyDamage(7));
        }

        [Test]
        public void From_OutOfRangeValues_AreClamped()
        {
            Assert.IsTrue(WeaponModifier.From("화염", Element(500f)).RollElement(0.9999));   // 100%로
            Assert.IsNull(WeaponModifier.From("화염", Element(-5f)));                         // 0%로 → 효과 없음
            Assert.AreEqual(1, WeaponModifier.From("강화", Damage(DamageMode.Multiply, -2f)).ApplyDamage(10));   // 0.1배로
        }

        // ───── 범위 보정 (ItemStats) ─────
        [Test]
        public void Clamped_PrefixFields()
        {
            var c = new ItemStats { damageMode = DamageMode.Multiply, damageAmount = 0f, elementChance = 250f }.Clamped();
            Assert.AreEqual(0.1f, c.damageAmount, 1e-6f);   // 곱하기는 0.1배부터
            Assert.AreEqual(100f, c.elementChance);

            var a = new ItemStats { damageMode = DamageMode.Add, damageAmount = -3f }.Clamped();
            Assert.AreEqual(-3f, a.damageAmount);           // 더하기는 깎는 값(-)도 허용

            var bad = new ItemStats { prefixKind = (PrefixEffectKind)9, damageMode = (DamageMode)9 }.Clamped();
            Assert.AreEqual(PrefixEffectKind.Damage, bad.prefixKind);
            Assert.AreEqual(DamageMode.Add, bad.damageMode);
        }

        [Test]
        public void SameAs_And_CopyFrom_CoverThePrefixFields()
        {
            var a = new ItemStats { prefixKind = PrefixEffectKind.Element, elementChance = 40f, damageMode = DamageMode.Multiply, damageAmount = 2f };
            var b = new ItemStats();
            Assert.IsFalse(a.SameAs(b));
            b.CopyFrom(a);
            Assert.IsTrue(a.SameAs(b));
            b.elementChance = 41f;
            Assert.IsFalse(a.SameAs(b));
        }

        // ───── 합성 단어 / 파일 ─────
        static ItemWordsFile File(params WordStatsEntry[] stats)
        {
            var file = new ItemWordsFile
            {
                schemaVersion = 1,
                categories = new List<string> { "무기", "속성", "접두어" },
                items = new List<ItemWordEntry>
                {
                    new ItemWordEntry { word = "검", category = "무기", acceptsPrefix = true, prefixes = new List<string> { "화염", "강화" } },
                    new ItemWordEntry { word = "화염", category = "속성" },
                    new ItemWordEntry { word = "강화", category = "접두어" },
                },
            };
            file.wordStats.AddRange(stats);
            return file;
        }

        [Test]
        public void TryGetCompound_SplitsAPrefixedWord()
        {
            var set = ItemWordSet.FromFile(File(), out _);
            Assert.IsTrue(set.TryGetCompound("화염검", out var prefix, out var baseWord));
            Assert.AreEqual("화염", prefix);
            Assert.AreEqual("검", baseWord);

            Assert.IsFalse(set.TryGetCompound("검", out _, out _));       // 파일에 있는 단어는 합성 단어가 아니다
            Assert.IsFalse(set.TryGetCompound("없는단어", out _, out _));
            Assert.IsFalse(set.TryGetCompound(null, out _, out _));
        }

        [Test]
        public void PrefixWordStats_AreReadFromTheFile_AndCombineWithTheBase()
        {
            var set = ItemWordSet.FromFile(File(
                new WordStatsEntry { word = "검", stats = new ItemStats { damage = 4 } },
                new WordStatsEntry { word = "강화", stats = Damage(DamageMode.Add, 2f) },
                new WordStatsEntry { word = "화염", stats = Element(30f) }), out _);

            set.TryGetCompound("강화검", out var p1, out _);
            var strong = WeaponModifier.From(p1, set.StatsOf(p1));
            Assert.AreEqual(4, set.StatsOf("강화검").damage);               // 본체 수치를 따르고
            Assert.AreEqual(6, strong.ApplyDamage(set.StatsOf("강화검").damage));   // 접두어가 더해진다

            set.TryGetCompound("화염검", out var p2, out _);
            var fire = WeaponModifier.From(p2, set.StatsOf(p2));
            Assert.AreEqual("화염", fire.Element);
            Assert.AreEqual(4, fire.ApplyDamage(4));                         // 속성은 피해량을 바꾸지 않는다
        }

        [Test]
        public void Json_RoundTrip_KeepsPrefixEffects()
        {
            var file = File(new WordStatsEntry { word = "화염", stats = Element(35f) },
                            new WordStatsEntry { word = "강화", stats = Damage(DamageMode.Multiply, 1.5f) });
            var back = JsonUtility.FromJson<ItemWordsFile>(JsonUtility.ToJson(file, true));
            var set = ItemWordSet.FromFile(back, out _);

            Assert.AreEqual(PrefixEffectKind.Element, set.StatsOf("화염").prefixKind);
            Assert.AreEqual(35f, set.StatsOf("화염").elementChance, 1e-4f);
            Assert.AreEqual(PrefixEffectKind.Damage, set.StatsOf("강화").prefixKind);
            Assert.AreEqual(DamageMode.Multiply, set.StatsOf("강화").damageMode);
            Assert.AreEqual(1.5f, set.StatsOf("강화").damageAmount, 1e-4f);
        }
    }
}
