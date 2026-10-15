using System;
using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.Domain;
using UnityEngine;

namespace Hangeul.UI
{
    /// <summary>단어가 어느 목록에 들어 있는지(= 단어의 종류).</summary>
    public enum WordKind
    {
        Plain,           // 수치가 없는 일반 단어
        Weapon,
        Armor,
        Food,
        PrefixDamage,    // 접두어 단어 — 대미지 더하기
        PrefixElement,   // 접두어 단어 — 속성 묻히기
    }

    /// <summary>카테고리 이름과 그 종류. 종류가 그 카테고리의 단어가 어느 목록에 들어가는지, 어떤 수치 칸이 있는지를 정한다.</summary>
    [Serializable]
    public sealed class CategoryDef
    {
        public string name;
        public CategoryType type;
    }

    // ───── 단어 기록 (종류마다 하나씩) ─────
    // 모든 단어에 공통: 단어, 카테고리, 아이템 스프라이트. 종류마다 고유한 칸이 따로 있다.
    // "JSON이 주인인 칸"(단어, 카테고리, 접두어 규칙, 수치)과 "에셋이 주인인 칸"(스프라이트)이 나뉘어 있어,
    // JSON에서 가져올 때 스프라이트는 절대 건드리지 않는다.

    [Serializable]
    public abstract class WordRecord
    {
        public string word;
        public string category;
        [Tooltip("아이템 스프라이트(인벤토리·줍기 아이템 그림). JSON에서 가져와도 바뀌지 않는다.")]
        public Sprite sprite;
        [Tooltip("목록에서 보이는 순서(클수록 위). 새로 추가한 단어가 가장 크다. JSON으로 내보낼 때도 이 순서(최근 추가가 맨 위)로 적는다.")]
        public int order;

        public abstract WordKind Kind { get; }

        /// <summary>같은 종류의 기록을 깊게 복사한다(스프라이트는 같은 에셋을 가리킨다).</summary>
        public abstract WordRecord Clone();

        /// <summary>JSON이 주인인 칸(스프라이트와 단어 자신을 뺀 모두)이 같은지.</summary>
        public abstract bool JsonEquals(WordRecord other);

        /// <summary>JSON이 주인인 칸을 source에서 복사한다(같은 종류일 때). 스프라이트와 단어는 그대로 둔다.</summary>
        public abstract void CopyJsonFieldsFrom(WordRecord source);

        /// <summary>파일(JSON)과 게임 규칙이 쓰는 수치(ItemStats)로. includeDefaults가 false면 기본값을 따르는 단어는 null(수치를 따로 정하지 않음).</summary>
        public abstract ItemStats StatsForFile(ItemDatabaseData db, bool includeDefaults);

        /// <summary>단어 줄 하나(ItemWordEntry). 접두어 단어는 접두어 규칙이 없다.</summary>
        public virtual ItemWordEntry ToEntry() => new ItemWordEntry { word = word, category = category };

        protected bool SameCategory(WordRecord o) => o != null && o.Kind == Kind && category == o.category;
    }

    /// <summary>접두어가 아닌 단어: 접두어 규칙(접두어를 받을 수 있는지, 어떤 접두어가 붙는지)을 가진다.</summary>
    [Serializable]
    public abstract class NormalWordRecord : WordRecord
    {
        public bool acceptsPrefix;
        public List<string> prefixes = new List<string>();

        protected bool SameNormal(WordRecord o)
        {
            if (!SameCategory(o)) return false;
            var n = (NormalWordRecord)o;
            return acceptsPrefix == n.acceptsPrefix && SameList(prefixes, n.prefixes);
        }

        protected void CopyNormal(WordRecord source)
        {
            var n = (NormalWordRecord)source;
            category = n.category;
            acceptsPrefix = n.acceptsPrefix;
            prefixes = n.prefixes != null ? new List<string>(n.prefixes) : new List<string>();
        }

        protected void CloneNormalInto(NormalWordRecord copy)
        {
            copy.prefixes = prefixes != null ? new List<string>(prefixes) : new List<string>();
        }

        public override ItemWordEntry ToEntry() => new ItemWordEntry
        {
            word = word, category = category, acceptsPrefix = acceptsPrefix,
            prefixes = prefixes != null ? new List<string>(prefixes) : new List<string>(),
        };

        internal static bool SameList(List<string> a, List<string> b)
        {
            int na = a?.Count ?? 0, nb = b?.Count ?? 0;
            if (na != nb) return false;
            for (int i = 0; i < na; i++) if (a[i] != b[i]) return false;
            return true;
        }
    }

    [Serializable]
    public sealed class PlainWordRecord : NormalWordRecord
    {
        public override WordKind Kind => WordKind.Plain;
        public override WordRecord Clone() { var c = (PlainWordRecord)MemberwiseClone(); CloneNormalInto(c); return c; }
        public override bool JsonEquals(WordRecord o) => SameNormal(o);
        public override void CopyJsonFieldsFrom(WordRecord s) => CopyNormal(s);
        public override ItemStats StatsForFile(ItemDatabaseData db, bool includeDefaults) => null;
    }

    /// <summary>방어구. 수치는 아직 없다(장착·방어력은 후속) — 단어, 카테고리, 스프라이트만 가진다.</summary>
    [Serializable]
    public sealed class ArmorWordRecord : NormalWordRecord
    {
        public override WordKind Kind => WordKind.Armor;
        public override WordRecord Clone() { var c = (ArmorWordRecord)MemberwiseClone(); CloneNormalInto(c); return c; }
        public override bool JsonEquals(WordRecord o) => SameNormal(o);
        public override void CopyJsonFieldsFrom(WordRecord s) => CopyNormal(s);
        public override ItemStats StatsForFile(ItemDatabaseData db, bool includeDefaults) => null;
    }

    [Serializable]
    public sealed class WeaponWordRecord : NormalWordRecord
    {
        [Tooltip("켜면 이 단어는 에셋의 '무기 기본값'을 따른다. 끄면 아래 수치를 쓴다.")]
        public bool useDefaults = true;
        public WeaponStats stats = new WeaponStats();

        public override WordKind Kind => WordKind.Weapon;

        public override WordRecord Clone()
        {
            var c = (WeaponWordRecord)MemberwiseClone();
            CloneNormalInto(c);
            c.stats = (stats ?? new WeaponStats()).Clone();
            return c;
        }

        public override bool JsonEquals(WordRecord o)
            => SameNormal(o) && useDefaults == ((WeaponWordRecord)o).useDefaults && (useDefaults || (stats ?? new WeaponStats()).SameAs(((WeaponWordRecord)o).stats));

        public override void CopyJsonFieldsFrom(WordRecord s)
        {
            CopyNormal(s);
            var w = (WeaponWordRecord)s;
            useDefaults = w.useDefaults;
            stats = (w.stats ?? new WeaponStats()).Clone();
        }

        public override ItemStats StatsForFile(ItemDatabaseData db, bool includeDefaults)
            => useDefaults ? (includeDefaults ? db.defaultWeapon.ToItemStats() : null) : (stats ?? new WeaponStats()).ToItemStats();
    }

    [Serializable]
    public sealed class FoodWordRecord : NormalWordRecord
    {
        [Tooltip("켜면 이 단어는 에셋의 '음식 기본값'을 따른다. 끄면 아래 수치를 쓴다.")]
        public bool useDefaults = true;
        public FoodStats stats = new FoodStats();

        public override WordKind Kind => WordKind.Food;

        public override WordRecord Clone()
        {
            var c = (FoodWordRecord)MemberwiseClone();
            CloneNormalInto(c);
            c.stats = (stats ?? new FoodStats()).Clone();
            return c;
        }

        public override bool JsonEquals(WordRecord o)
            => SameNormal(o) && useDefaults == ((FoodWordRecord)o).useDefaults && (useDefaults || (stats ?? new FoodStats()).SameAs(((FoodWordRecord)o).stats));

        public override void CopyJsonFieldsFrom(WordRecord s)
        {
            CopyNormal(s);
            var f = (FoodWordRecord)s;
            useDefaults = f.useDefaults;
            stats = (f.stats ?? new FoodStats()).Clone();
        }

        public override ItemStats StatsForFile(ItemDatabaseData db, bool includeDefaults)
            => useDefaults ? (includeDefaults ? db.defaultFood.ToItemStats() : null) : (stats ?? new FoodStats()).ToItemStats();
    }

    /// <summary>접두어 단어 — 대미지 더하기. 접두어 단어 자신은 접두어를 받지 않는다.</summary>
    [Serializable]
    public sealed class PrefixDamageWordRecord : WordRecord
    {
        [Tooltip("켜면 이 단어는 에셋의 '접두어(대미지) 기본값'을 따른다. 끄면 아래 효과를 쓴다.")]
        public bool useDefaults = true;
        public PrefixDamageEffect effect = new PrefixDamageEffect();

        public override WordKind Kind => WordKind.PrefixDamage;

        public override WordRecord Clone()
        {
            var c = (PrefixDamageWordRecord)MemberwiseClone();
            c.effect = (effect ?? new PrefixDamageEffect()).Clone();
            return c;
        }

        public override bool JsonEquals(WordRecord o)
            => SameCategory(o) && useDefaults == ((PrefixDamageWordRecord)o).useDefaults
               && (useDefaults || (effect ?? new PrefixDamageEffect()).SameAs(((PrefixDamageWordRecord)o).effect));

        public override void CopyJsonFieldsFrom(WordRecord s)
        {
            var p = (PrefixDamageWordRecord)s;
            category = p.category;
            useDefaults = p.useDefaults;
            effect = (p.effect ?? new PrefixDamageEffect()).Clone();
        }

        public override ItemStats StatsForFile(ItemDatabaseData db, bool includeDefaults)
            => useDefaults ? (includeDefaults ? db.defaultPrefixDamage.ToItemStats() : null) : (effect ?? new PrefixDamageEffect()).ToItemStats();
    }

    /// <summary>접두어 단어 — 속성 묻히기(확률과 글자 색). 접두어 단어 자신은 접두어를 받지 않는다.</summary>
    [Serializable]
    public sealed class PrefixElementWordRecord : WordRecord
    {
        [Tooltip("켜면 이 단어는 에셋의 '접두어(속성) 기본값'을 따른다. 끄면 아래 효과를 쓴다.")]
        public bool useDefaults = true;
        public PrefixElementEffect effect = new PrefixElementEffect();

        public override WordKind Kind => WordKind.PrefixElement;

        public override WordRecord Clone()
        {
            var c = (PrefixElementWordRecord)MemberwiseClone();
            c.effect = (effect ?? new PrefixElementEffect()).Clone();
            return c;
        }

        public override bool JsonEquals(WordRecord o)
            => SameCategory(o) && useDefaults == ((PrefixElementWordRecord)o).useDefaults
               && (useDefaults || (effect ?? new PrefixElementEffect()).SameAs(((PrefixElementWordRecord)o).effect));

        public override void CopyJsonFieldsFrom(WordRecord s)
        {
            var p = (PrefixElementWordRecord)s;
            category = p.category;
            useDefaults = p.useDefaults;
            effect = (p.effect ?? new PrefixElementEffect()).Clone();
        }

        public override ItemStats StatsForFile(ItemDatabaseData db, bool includeDefaults)
            => useDefaults ? (includeDefaults ? db.defaultPrefixElement.ToItemStats() : null) : (effect ?? new PrefixElementEffect()).ToItemStats();
    }

    /// <summary>
    /// 게임 데이터 에셋(<see cref="ItemDatabase"/>) 안의 모든 데이터. 단어는 종류별 목록에 따로 들어 있고,
    /// 종류마다 "기본값"을 한 곳에 둔다(기본값을 따르는 단어는 이 값으로 동작한다).
    /// 게임 규칙은 파일(JSON)과 같은 모양인 <see cref="ItemWordsFile"/>을 쓰므로 <see cref="ToFile"/>이 바꿔 준다.
    /// </summary>
    [Serializable]
    public sealed class ItemDatabaseData
    {
        [Tooltip("카테고리와 그 종류. 종류를 바꾸면 '카테고리 종류에 맞게 단어 정리' 메뉴로 단어를 알맞은 목록에 옮긴다.")]
        public List<CategoryDef> categories = new List<CategoryDef>();

        [Header("기본값 (단어가 '기본값 사용'일 때 따르는 값 — 처음 값은 지금 씬의 근접 공격 설정과 같다)")]
        public WeaponStats defaultWeapon = new WeaponStats();
        public FoodStats defaultFood = new FoodStats();
        public PrefixDamageEffect defaultPrefixDamage = new PrefixDamageEffect();
        public PrefixElementEffect defaultPrefixElement = new PrefixElementEffect();

        [Header("단어 (종류별 목록)")]
        public List<WeaponWordRecord> weapons = new List<WeaponWordRecord>();
        public List<FoodWordRecord> foods = new List<FoodWordRecord>();
        public List<ArmorWordRecord> armors = new List<ArmorWordRecord>();
        public List<PlainWordRecord> plains = new List<PlainWordRecord>();
        public List<PrefixDamageWordRecord> prefixDamages = new List<PrefixDamageWordRecord>();
        public List<PrefixElementWordRecord> prefixElements = new List<PrefixElementWordRecord>();

        // ───── 조회 ─────
        public IEnumerable<WordRecord> AllWords()
        {
            foreach (var r in weapons) if (r != null) yield return r;
            foreach (var r in foods) if (r != null) yield return r;
            foreach (var r in armors) if (r != null) yield return r;
            foreach (var r in plains) if (r != null) yield return r;
            foreach (var r in prefixDamages) if (r != null) yield return r;
            foreach (var r in prefixElements) if (r != null) yield return r;
        }

        public int WordCount => AllWords().Count();

        /// <summary>새 단어에 줄 순서 값: 지금 있는 어떤 단어보다 크다(목록의 맨 위에 온다).</summary>
        public int NextOrder() => AllWords().Select(r => r.order).DefaultIfEmpty(0).Max() + 1;

        /// <summary>단어로 찾는다(어느 목록에 있든). 없으면 null.</summary>
        public WordRecord Find(string word)
        {
            if (string.IsNullOrEmpty(word)) return null;
            foreach (var r in AllWords()) if (r.word == word) return r;
            return null;
        }

        public CategoryDef FindCategory(string name)
        {
            if (name == null) return null;
            foreach (var c in categories) if (c != null && c.name == name) return c;
            return null;
        }

        /// <summary>카테고리의 종류. 모르는 카테고리는 일반.</summary>
        public CategoryType TypeOfCategory(string name) => FindCategory(name)?.type ?? CategoryType.Plain;

        // ───── 추가 / 삭제 ─────
        public void Add(WordRecord r)
        {
            switch (r.Kind)
            {
                case WordKind.Weapon: weapons.Add((WeaponWordRecord)r); break;
                case WordKind.Food: foods.Add((FoodWordRecord)r); break;
                case WordKind.Armor: armors.Add((ArmorWordRecord)r); break;
                case WordKind.PrefixDamage: prefixDamages.Add((PrefixDamageWordRecord)r); break;
                case WordKind.PrefixElement: prefixElements.Add((PrefixElementWordRecord)r); break;
                default: plains.Add((PlainWordRecord)r); break;
            }
        }

        public bool Remove(WordRecord r)
        {
            switch (r.Kind)
            {
                case WordKind.Weapon: return weapons.Remove(r as WeaponWordRecord);
                case WordKind.Food: return foods.Remove(r as FoodWordRecord);
                case WordKind.Armor: return armors.Remove(r as ArmorWordRecord);
                case WordKind.PrefixDamage: return prefixDamages.Remove(r as PrefixDamageWordRecord);
                case WordKind.PrefixElement: return prefixElements.Remove(r as PrefixElementWordRecord);
                default: return plains.Remove(r as PlainWordRecord);
            }
        }

        public static WordRecord Create(WordKind kind)
        {
            switch (kind)
            {
                case WordKind.Weapon: return new WeaponWordRecord();
                case WordKind.Food: return new FoodWordRecord();
                case WordKind.Armor: return new ArmorWordRecord();
                case WordKind.PrefixDamage: return new PrefixDamageWordRecord();
                case WordKind.PrefixElement: return new PrefixElementWordRecord();
                default: return new PlainWordRecord();
            }
        }

        // ───── 카테고리 종류에 맞게 정리 ─────
        /// <summary>카테고리 종류 → 그 카테고리의 단어가 들어갈 목록. 접두어 카테고리는 이미 접두어 단어면 그 종류를 유지하고, 아니면 대미지 더하기로 시작한다.</summary>
        public static WordKind KindFor(CategoryType type, WordKind current)
        {
            switch (type)
            {
                case CategoryType.Weapon: return WordKind.Weapon;
                case CategoryType.Armor: return WordKind.Armor;
                case CategoryType.Food: return WordKind.Food;
                case CategoryType.Prefix: return current == WordKind.PrefixDamage || current == WordKind.PrefixElement ? current : WordKind.PrefixDamage;
                default: return WordKind.Plain;
            }
        }

        /// <summary>
        /// 단어를 다른 종류의 기록으로 바꿔 알맞은 목록에 넣는다(단어, 카테고리, 스프라이트와 접두어 규칙은 유지, 수치는 기본값을 따르는 상태로 시작).
        /// 이미 그 종류면 그대로 돌려준다. 바뀐 새 기록을 돌려준다(목록의 맨 끝에 들어간다).
        /// </summary>
        public WordRecord ConvertTo(WordRecord r, WordKind to)
        {
            if (r.Kind == to) return r;

            var converted = Create(to);
            converted.word = r.word;
            converted.category = r.category;
            converted.sprite = r.sprite;
            converted.order = r.order;   // 종류가 바뀌어도 목록에서의 자리는 그대로
            if (r is NormalWordRecord from && converted is NormalWordRecord target)
            {
                target.acceptsPrefix = from.acceptsPrefix;
                target.prefixes = from.prefixes != null ? new List<string>(from.prefixes) : new List<string>();
            }
            Remove(r);
            Add(converted);
            return converted;
        }

        /// <summary>
        /// 카테고리의 종류가 바뀌었을 때, 단어를 그 종류에 맞는 목록으로 옮긴다(예: '물약' 카테고리를 음식으로 바꾸면 그 단어들이 음식 목록으로 가서
        /// 회복량 칸이 생긴다). 옮긴 단어 수를 돌려준다.
        /// </summary>
        public int Reclassify()
        {
            int moved = 0;
            foreach (var r in AllWords().ToList())
            {
                var expected = KindFor(TypeOfCategory(r.category), r.Kind);
                if (expected == r.Kind) continue;
                ConvertTo(r, expected);
                moved++;
            }
            return moved;
        }

        // ───── 편집 (인스펙터가 쓴다) ─────
        /// <summary>
        /// 새 단어를 카테고리의 종류에 맞는 목록에 추가한다. 한글 음절로만 이루어져야 하고 금지 음절이 없어야 하며 이미 있는 단어가 아니어야 한다.
        /// 실패하면 false와 이유(error)를 돌려준다.
        /// </summary>
        public bool TryAddWord(string word, string category, out WordRecord record, out string error)
        {
            record = null;
            word = word?.Trim();
            if (string.IsNullOrEmpty(word)) { error = "단어를 입력하세요."; return false; }
            if (!ItemWordSet.IsValidWord(word)) { error = $"한글 음절로만 쓸 수 있습니다: {word}"; return false; }
            foreach (char ch in word)
                if (ForbiddenSyllables.IsBanned(ch)) { error = $"금지 음절('{ch}')이 들어 있습니다: {word}"; return false; }
            if (Find(word) != null) { error = $"이미 있는 단어입니다: {word}"; return false; }
            if (FindCategory(category) == null) { error = "먼저 단어를 넣을 카테고리를 고르세요."; return false; }

            record = Create(KindFor(TypeOfCategory(category), WordKind.Plain));
            record.word = word;
            record.category = category;
            record.order = NextOrder();   // 새 단어는 목록의 맨 위에
            Add(record);
            error = null;
            return true;
        }

        /// <summary>카테고리를 추가한다. 이름이 비었거나 이미 있으면 false와 이유.</summary>
        public bool TryAddCategory(string name, CategoryType type, out string error)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) { error = "카테고리 이름을 입력하세요."; return false; }
            if (FindCategory(name) != null) { error = $"이미 있는 카테고리입니다: {name}"; return false; }
            categories.Add(new CategoryDef { name = name, type = type });
            error = null;
            return true;
        }

        /// <summary>카테고리 이름을 바꾸고, 그 카테고리의 단어들이 가리키는 이름도 함께 바꾼다. 이름이 비었거나 다른 카테고리와 겹치면 false와 이유.</summary>
        public bool TryRenameCategory(string oldName, string newName, out string error)
        {
            newName = newName?.Trim();
            var def = FindCategory(oldName);
            if (def == null) { error = $"없는 카테고리입니다: {oldName}"; return false; }
            if (string.IsNullOrEmpty(newName)) { error = "카테고리 이름을 입력하세요."; return false; }
            if (newName == oldName) { error = null; return true; }
            if (FindCategory(newName) != null) { error = $"이미 있는 카테고리입니다: {newName}"; return false; }

            def.name = newName;
            foreach (var r in AllWords()) if (r.category == oldName) r.category = newName;
            error = null;
            return true;
        }

        /// <summary>카테고리에 속한 단어 수.</summary>
        public int WordCountIn(string category) => AllWords().Count(r => r.category == category);

        /// <summary>단어가 없는 카테고리만 지운다. 단어가 있으면 false.</summary>
        public bool TryRemoveCategory(string name)
        {
            var def = FindCategory(name);
            if (def == null || WordCountIn(name) > 0) return false;
            return categories.Remove(def);
        }

        // ───── 복사 ─────
        public ItemDatabaseData DeepClone()
        {
            var c = new ItemDatabaseData
            {
                categories = categories.Where(x => x != null).Select(x => new CategoryDef { name = x.name, type = x.type }).ToList(),
                defaultWeapon = (defaultWeapon ?? new WeaponStats()).Clone(),
                defaultFood = (defaultFood ?? new FoodStats()).Clone(),
                defaultPrefixDamage = (defaultPrefixDamage ?? new PrefixDamageEffect()).Clone(),
                defaultPrefixElement = (defaultPrefixElement ?? new PrefixElementEffect()).Clone(),
            };
            foreach (var r in AllWords()) c.Add(r.Clone());
            return c;
        }

        // ───── 파일(JSON)과 게임 규칙이 쓰는 모양으로 ─────
        /// <summary>
        /// <see cref="ItemWordsFile"/>로 바꾼다. forGame이 true면 <b>게임이 쓰는 모양</b> — 기본값을 따르는 단어도 기본값을 채운 수치를 가져서
        /// 게임이 에셋의 기본값으로 동작한다. false면 <b>JSON으로 내보내는 모양</b> — 수치를 따로 정한 단어만 wordStats에 적는다
        /// (기본값을 따르는 단어는 "따로 정하지 않음"으로 남는다). 카테고리 종류(categoryTypes)는 둘 다 적는다.
        /// </summary>
        public ItemWordsFile ToFile(bool forGame)
        {
            var file = new ItemWordsFile { schemaVersion = 1 };

            foreach (var c in categories)
            {
                if (c == null || string.IsNullOrEmpty(c.name)) continue;
                file.categories.Add(c.name);
                file.categoryTypes.Add(new CategoryTypeEntry { name = c.name, type = c.type });
            }

            // 최근에 추가한 단어가 먼저 오게 적는다(DB 화면도 새 단어를 맨 위에 넣는다). 순서 값이 같으면 목록에 들어 있는 순서 그대로.
            foreach (var r in AllWords().OrderByDescending(x => x.order))
            {
                if (string.IsNullOrEmpty(r.word)) continue;
                file.items.Add(r.ToEntry());
                var stats = r.StatsForFile(this, includeDefaults: forGame);
                if (stats != null) file.wordStats.Add(new WordStatsEntry { word = r.word, stats = stats });
            }
            return file;
        }
    }
}
