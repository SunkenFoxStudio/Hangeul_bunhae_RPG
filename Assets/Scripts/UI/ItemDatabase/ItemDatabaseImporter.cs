using System.Collections.Generic;
using System.Linq;
using Hangeul.App;

namespace Hangeul.UI
{
    /// <summary>JSON에서 가져올 때 이미 있는 단어를 어떻게 할지.</summary>
    public enum ImportMode
    {
        /// <summary>새 단어만 추가한다. 이미 에셋에 있는 단어는 건드리지 않아서 인스펙터에서 고친 값이 유지된다.</summary>
        AddOnly,
        /// <summary>이미 있는 단어도 JSON의 값으로 덮어쓴다(카테고리, 접두어 규칙, 수치). 아이템 스프라이트와 JSON에 없는 단어는 건드리지 않는다.</summary>
        Overwrite,
    }

    /// <summary>가져오기 결과 요약.</summary>
    public sealed class ImportReport
    {
        /// <summary>새로 만든 단어.</summary>
        public readonly List<string> AddedWords = new List<string>();
        /// <summary>값이 바뀐 단어(덮어쓰기에서만. 종류가 바뀌어 다른 목록으로 옮겨진 단어도 포함).</summary>
        public readonly List<string> UpdatedWords = new List<string>();
        /// <summary>JSON과 같거나 건드리지 않은 단어 수.</summary>
        public int Unchanged;
        /// <summary>새로 만든 카테고리.</summary>
        public readonly List<string> NewCategories = new List<string>();
        /// <summary>새로 만든 카테고리 중 종류를 알 수 없어 '일반'으로 둔 것 — 인스펙터에서 종류를 정해야 한다.</summary>
        public readonly List<string> UnknownCategories = new List<string>();

        public int Added => AddedWords.Count;
        public int Updated => UpdatedWords.Count;
    }

    /// <summary>
    /// JSON(<see cref="ItemWordsFile"/>)을 게임 데이터(<see cref="ItemDatabaseData"/>)로 가져온다. 순수 규칙이라 에디터 없이 시험할 수 있다.
    /// 단어는 카테고리의 종류에 따라 알맞은 목록에 들어간다. 카테고리 종류는 에셋에 이미 정해 둔 것 → JSON에 적힌 것(categoryTypes) →
    /// 카테고리 이름으로 짐작한 것 순으로 정한다.
    /// </summary>
    public static class ItemDatabaseImporter
    {
        // 옛 DB에 이미 있던 이름으로 종류를 짐작한다(코드에서 이 이름들을 아는 곳은 여기뿐이고, 가져온 뒤에는 에셋의 카테고리 종류가 쓰인다).
        const string ElementCategoryName = "속성";   // 접두어 카테고리 중 속성 묻히기가 기본인 쪽
        static readonly string[] KnownPlainCategories = { "추가한 단어", "소모품", "재료·잡화" };

        /// <summary>카테고리 이름으로 종류를 짐작한다. 짐작할 수 없는 이름은 일반으로 두고 recognized를 false로 돌려준다.</summary>
        public static CategoryType InferType(string name, out bool recognized)
        {
            recognized = true;
            if (name == ItemKinds.WeaponCategory) return CategoryType.Weapon;
            if (name == ItemKinds.ArmorCategory) return CategoryType.Armor;
            if (name == ItemKinds.FoodCategory) return CategoryType.Food;
            if (ItemWordSet.IsPrefixCategory(name)) return CategoryType.Prefix;
            if (System.Array.IndexOf(KnownPlainCategories, name) >= 0) return CategoryType.Plain;
            recognized = false;
            return CategoryType.Plain;
        }

        public static ImportReport Import(ItemWordsFile file, ItemDatabaseData db, ImportMode mode)
        {
            var report = new ImportReport();
            if (file == null || db == null) return report;

            var fileTypes = new Dictionary<string, CategoryType>();
            if (file.categoryTypes != null)
                foreach (var c in file.categoryTypes)
                    if (c != null && !string.IsNullOrEmpty(c.name) && !fileTypes.ContainsKey(c.name)) fileTypes[c.name] = c.type;

            var stats = new Dictionary<string, ItemStats>();
            if (file.wordStats != null)
                foreach (var w in file.wordStats)
                    if (w != null && w.stats != null && !string.IsNullOrEmpty(w.word) && !stats.ContainsKey(w.word)) stats[w.word] = w.stats;

            // 1) 카테고리: 에셋에 없는 것만 만든다(이미 있는 카테고리의 종류는 건드리지 않는다)
            void EnsureCategory(string name)
            {
                if (string.IsNullOrEmpty(name) || db.FindCategory(name) != null) return;
                CategoryType type;
                bool recognized = true;
                if (!fileTypes.TryGetValue(name, out type)) type = InferType(name, out recognized);
                db.categories.Add(new CategoryDef { name = name, type = type });
                report.NewCategories.Add(name);
                if (!recognized) report.UnknownCategories.Add(name);
            }
            if (file.categories != null) foreach (var name in file.categories) EnsureCategory(name);
            if (file.items != null) foreach (var item in file.items) if (item != null) EnsureCategory(item.category);

            // 2) 단어
            var seen = new HashSet<string>();
            foreach (var item in file.items ?? new List<ItemWordEntry>())
            {
                if (item == null || string.IsNullOrEmpty(item.word) || !seen.Add(item.word)) continue;   // 같은 단어가 또 나오면 처음 것만

                stats.TryGetValue(item.word, out var custom);
                var desired = Build(item, custom, db.TypeOfCategory(item.category));
                var existing = db.Find(item.word);

                if (existing == null) { db.Add(desired); report.AddedWords.Add(item.word); continue; }
                if (mode == ImportMode.AddOnly) { report.Unchanged++; continue; }

                desired.sprite = existing.sprite;   // 스프라이트는 에셋이 주인이다
                if (existing.JsonEquals(desired)) { report.Unchanged++; continue; }

                if (existing.Kind == desired.Kind) existing.CopyJsonFieldsFrom(desired);
                else { db.Remove(existing); db.Add(desired); }   // 카테고리 종류가 달라져 다른 목록으로 옮겨진다
                report.UpdatedWords.Add(item.word);
            }
            return report;
        }

        // JSON의 한 단어를 카테고리 종류에 맞는 기록으로 만든다. custom은 그 단어에 따로 정한 수치(없으면 null = 기본값을 따른다).
        static WordRecord Build(ItemWordEntry item, ItemStats custom, CategoryType type)
        {
            string category = item.category ?? "";
            var clamped = custom?.Clamped();

            switch (type)
            {
                case CategoryType.Weapon:
                {
                    var r = new WeaponWordRecord();
                    FillNormal(r, item, category);
                    if (clamped != null) { r.useDefaults = false; r.stats = WeaponStats.From(clamped); }
                    return r;
                }
                case CategoryType.Food:
                {
                    var r = new FoodWordRecord();
                    FillNormal(r, item, category);
                    if (clamped != null) { r.useDefaults = false; r.stats = FoodStats.From(clamped); }
                    return r;
                }
                case CategoryType.Armor:
                {
                    var r = new ArmorWordRecord();
                    FillNormal(r, item, category);
                    return r;
                }
                case CategoryType.Prefix:
                {
                    // 효과의 종류: 따로 정한 수치가 있으면 그것, 없으면 카테고리로(속성은 속성 묻히기, 그 밖은 대미지 더하기)
                    var kind = clamped != null ? clamped.prefixKind : (category == ElementCategoryName ? PrefixEffectKind.Element : PrefixEffectKind.Damage);
                    if (kind == PrefixEffectKind.Element)
                    {
                        var r = new PrefixElementWordRecord { word = item.word, category = category };
                        if (clamped != null) { r.useDefaults = false; r.effect = PrefixElementEffect.From(clamped); }
                        return r;
                    }
                    else
                    {
                        var r = new PrefixDamageWordRecord { word = item.word, category = category };
                        if (clamped != null) { r.useDefaults = false; r.effect = PrefixDamageEffect.From(clamped); }
                        return r;
                    }
                }
                default:
                {
                    var r = new PlainWordRecord();
                    FillNormal(r, item, category);
                    return r;
                }
            }
        }

        static void FillNormal(NormalWordRecord r, ItemWordEntry item, string category)
        {
            r.word = item.word;
            r.category = category;
            r.acceptsPrefix = item.acceptsPrefix;
            r.prefixes = item.prefixes != null ? new List<string>(item.prefixes.Where(p => !string.IsNullOrEmpty(p))) : new List<string>();
        }
    }
}
