using System.Collections.Generic;
using System.Linq;
using Hangeul.App;
using Hangeul.Domain;

namespace Hangeul.UI
{
    public enum IssueLevel
    {
        Error,     // 고쳐야 한다: 단어가 쓰이지 않거나 규칙이 깨져 있다
        Warning,   // 확인해 볼 것: 게임에서 의도와 다르게 동작할 수 있다
        Info,      // 알려 주는 것
    }

    /// <summary>검증에서 찾은 문제 하나. Code는 문제의 종류를 가리키는 고정된 이름(시험과 도구가 쓴다).</summary>
    public sealed class ValidationIssue
    {
        public IssueLevel Level;
        public string Code;
        public string Message;
        /// <summary>문제가 있는 단어(특정 단어의 문제가 아니면 null). 인스펙터에서 그 단어를 찾아 보여 주는 데 쓴다.</summary>
        public string Word;
    }

    public sealed class ValidationReport
    {
        public readonly List<ValidationIssue> Issues = new List<ValidationIssue>();

        public int Count(IssueLevel level) => Issues.Count(i => i.Level == level);
        public bool HasErrors => Issues.Any(i => i.Level == IssueLevel.Error);
        public bool Has(string code) => Issues.Any(i => i.Code == code);
        public IEnumerable<ValidationIssue> Of(string code) => Issues.Where(i => i.Code == code);

        internal void Add(IssueLevel level, string code, string message, string word = null)
            => Issues.Add(new ValidationIssue { Level = level, Code = code, Message = message, Word = word });
    }

    /// <summary>
    /// 게임 데이터(<see cref="ItemDatabaseData"/>)를 훑어 문제를 찾는다(순수 규칙). 찾는 것: 중복·빈·쓸 수 없는 단어, 카테고리 목록에 없는 카테고리,
    /// 종류와 맞지 않는 목록의 단어, 접두어 규칙의 구멍(접두어 단어에 없는 접두어, 이미 단어로 있는 합성 단어), 범위를 벗어난 수치와 틀린 색,
    /// 스프라이트가 없는 단어, 단어가 없는 카테고리. 결과는 오류 → 경고 → 안내 순서로 모은다.
    /// </summary>
    public static class ItemDatabaseValidator
    {
        const int ExampleCount = 8;

        public static ValidationReport Validate(ItemDatabaseData db)
        {
            var report = new ValidationReport();
            if (db == null) return report;

            CheckCategories(db, report);
            CheckWords(db, report);
            CheckDefaults(db, report);
            CheckSprites(db, report);

            // 오류 → 경고 → 안내 (같은 단계 안에서는 찾은 순서 그대로)
            var sorted = report.Issues.OrderBy(i => i.Level).ToList();
            report.Issues.Clear();
            report.Issues.AddRange(sorted);
            return report;
        }

        // ───── 카테고리 ─────
        static void CheckCategories(ItemDatabaseData db, ValidationReport report)
        {
            var seen = new HashSet<string>();
            foreach (var c in db.categories)
            {
                if (c == null) { report.Add(IssueLevel.Warning, "empty-entry", "카테고리 목록에 비어 있는 항목이 있습니다."); continue; }
                if (string.IsNullOrWhiteSpace(c.name)) { report.Add(IssueLevel.Error, "empty-category-name", "이름이 비어 있는 카테고리가 있습니다."); continue; }
                if (!seen.Add(c.name)) report.Add(IssueLevel.Error, "duplicate-category", $"같은 이름의 카테고리가 여러 개 있습니다: {c.name}");
            }

            var empty = db.categories.Where(c => c != null && !string.IsNullOrWhiteSpace(c.name) && db.WordCountIn(c.name) == 0).Select(c => c.name).Distinct().ToList();
            if (empty.Count > 0) report.Add(IssueLevel.Info, "empty-category", $"단어가 없는 카테고리 {empty.Count}개: {string.Join(", ", empty)}");
        }

        // ───── 단어 ─────
        static void CheckWords(ItemDatabaseData db, ValidationReport report)
        {
            int nullEntries = db.weapons.Count(r => r == null) + db.foods.Count(r => r == null) + db.armors.Count(r => r == null)
                              + db.plains.Count(r => r == null) + db.prefixDamages.Count(r => r == null) + db.prefixElements.Count(r => r == null);
            if (nullEntries > 0) report.Add(IssueLevel.Warning, "empty-entry", $"단어 목록에 비어 있는 항목이 {nullEntries}개 있습니다.");

            var words = db.AllWords().ToList();

            // 중복
            foreach (var group in words.Where(r => !string.IsNullOrEmpty(r.word)).GroupBy(r => r.word).Where(g => g.Count() > 1))
                report.Add(IssueLevel.Error, "duplicate-word", $"같은 단어가 {group.Count()}번 있습니다: {group.Key}", group.Key);

            var prefixWords = new HashSet<string>(words.Where(r => r.Kind == WordKind.PrefixDamage || r.Kind == WordKind.PrefixElement)
                                                       .Select(r => r.word).Where(w => !string.IsNullOrEmpty(w)));

            foreach (var r in words)
            {
                // 단어 자체
                if (string.IsNullOrWhiteSpace(r.word))
                {
                    report.Add(IssueLevel.Error, "empty-word", $"단어가 비어 있는 항목이 있습니다 (카테고리: {r.category}).");
                    continue;
                }
                if (!ItemWordSet.IsValidWord(r.word))
                    report.Add(IssueLevel.Error, "invalid-word", $"한글 음절로만 이루어져야 쓸 수 있습니다: '{r.word}'", r.word);
                else
                {
                    foreach (char ch in r.word)
                        if (ForbiddenSyllables.IsBanned(ch))
                        {
                            report.Add(IssueLevel.Warning, "banned-syllable", $"금지 음절('{ch}')이 들어 있어 카드로 만들 수 없습니다: {r.word}", r.word);
                            break;
                        }
                }

                // 카테고리와 목록
                bool knownCategory = db.FindCategory(r.category) != null;
                if (!knownCategory)
                    report.Add(IssueLevel.Error, "unknown-category", $"'{r.word}': 카테고리 '{r.category}'가 카테고리 목록에 없습니다(일반 단어로 취급됩니다).", r.word);
                else if (ItemDatabaseData.KindFor(db.TypeOfCategory(r.category), r.Kind) != r.Kind)
                    report.Add(IssueLevel.Warning, "wrong-list", $"'{r.word}': 카테고리 '{r.category}'의 종류와 맞지 않는 목록에 있습니다 — '카테고리 종류에 맞게 단어 정리'를 실행하세요.", r.word);

                if (r is NormalWordRecord normal) CheckPrefixRules(db, normal, prefixWords, report);
                CheckStats(r, report);
            }
        }

        static void CheckPrefixRules(ItemDatabaseData db, NormalWordRecord r, HashSet<string> prefixWords, ValidationReport report)
        {
            var list = r.prefixes ?? new List<string>();

            if (r.acceptsPrefix && list.Count == 0)
                report.Add(IssueLevel.Warning, "prefix-rules-empty", $"'{r.word}': 접두어 가능인데 붙일 접두어가 없습니다.", r.word);
            if (!r.acceptsPrefix && list.Count > 0)
                report.Add(IssueLevel.Info, "prefix-rules-off", $"'{r.word}': 접두어 가능이 꺼져 있어 접두어 목록 {list.Count}개가 쓰이지 않습니다.", r.word);

            var seen = new HashSet<string>();
            foreach (var p in list)
            {
                if (string.IsNullOrEmpty(p)) { report.Add(IssueLevel.Warning, "prefix-empty", $"'{r.word}'의 접두어 목록에 빈 칸이 있습니다.", r.word); continue; }
                if (!seen.Add(p)) report.Add(IssueLevel.Warning, "prefix-duplicate", $"'{r.word}'의 접두어 목록에 '{p}'가 두 번 있습니다.", r.word);
                if (!prefixWords.Contains(p))
                {
                    report.Add(IssueLevel.Error, "missing-prefix-word", $"'{r.word}': 접두어 '{p}'가 접두어 단어에 없습니다(붙여도 아무 일도 없습니다).", r.word);
                    continue;
                }

                // 접두어 + 단어가 이미 단어로 있으면 합성 단어로 세지 않는다(파일의 단어가 우선)
                string compound = p + r.word;
                if (r.acceptsPrefix && db.Find(compound) != null)
                    report.Add(IssueLevel.Warning, "compound-exists", $"'{r.word}': '{p}' + '{r.word}' = 합성 단어 '{compound}'이(가) 이미 단어로 있습니다(합성 단어로 세지 않고 있는 단어가 쓰입니다).", r.word);
            }
        }

        // 수치가 범위를 벗어나면 게임이 자동으로 맞춘다 — 입력 실수일 수 있으니 알려 준다
        static void CheckStats(WordRecord r, ValidationReport report)
        {
            ItemStats stats = null;
            string color = null;
            switch (r)
            {
                case WeaponWordRecord w when !w.useDefaults: stats = w.stats?.ToItemStats(); break;
                case FoodWordRecord f when !f.useDefaults: stats = f.stats?.ToItemStats(); break;
                case PrefixDamageWordRecord d when !d.useDefaults: stats = d.effect?.ToItemStats(); break;
                case PrefixElementWordRecord e when !e.useDefaults: stats = e.effect?.ToItemStats(); color = e.effect?.color; break;
            }
            if (stats == null) return;

            CheckStatsRange(stats, $"'{r.word}'", r.word, report);
            if (color != null) CheckColor(color, $"'{r.word}'", r.word, report);
        }

        static void CheckStatsRange(ItemStats stats, string label, string word, ValidationReport report)
        {
            var probe = stats.Clone();
            probe.elementColor = ColorHex.NormalizeOr(probe.elementColor, ColorHex.DefaultElement);   // 색은 따로 검사한다
            if (!probe.SameAs(probe.Clamped()))
                report.Add(IssueLevel.Warning, "stats-out-of-range", $"{label}의 수치가 범위를 벗어나 게임에서 자동으로 맞춰집니다(피해량·회복량 1 이상, 판정 시간 0.01초 이상, 확률 0~100 등).", word);
        }

        static void CheckColor(string color, string label, string word, ValidationReport report)
        {
            if (!ColorHex.TryNormalize(color, out _))
                report.Add(IssueLevel.Warning, "bad-color", $"{label}의 글자 색 '{color}'은(는) #RRGGBB 형식이 아니어서 기본 색(주황)으로 쓰입니다.", word);
        }

        // ───── 기본값 ─────
        static void CheckDefaults(ItemDatabaseData db, ValidationReport report)
        {
            if (db.defaultWeapon != null) CheckStatsRange(db.defaultWeapon.ToItemStats(), "무기 기본값", null, report);
            if (db.defaultFood != null) CheckStatsRange(db.defaultFood.ToItemStats(), "음식 기본값", null, report);
            if (db.defaultPrefixDamage != null) CheckStatsRange(db.defaultPrefixDamage.ToItemStats(), "접두어(대미지) 기본값", null, report);
            if (db.defaultPrefixElement != null)
            {
                CheckStatsRange(db.defaultPrefixElement.ToItemStats(), "접두어(속성) 기본값", null, report);
                CheckColor(db.defaultPrefixElement.color, "접두어(속성) 기본값", null, report);
            }
        }

        // ───── 스프라이트 ─────
        static void CheckSprites(ItemDatabaseData db, ValidationReport report)
        {
            var missing = db.AllWords().Where(r => !string.IsNullOrEmpty(r.word) && r.sprite == null).Select(r => r.word).ToList();
            if (missing.Count == 0) return;
            string examples = string.Join(", ", missing.Take(ExampleCount)) + (missing.Count > ExampleCount ? " …" : "");
            report.Add(IssueLevel.Info, "no-sprite", $"아이템 스프라이트가 없는 단어 {missing.Count}개 (예: {examples}). 인스펙터의 \"스프라이트 없는 단어만\"으로 모아 볼 수 있습니다.");
        }
    }
}
