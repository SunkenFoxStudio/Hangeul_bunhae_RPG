using System;
using System.Collections.Generic;
using System.Linq;
using Hangeul.Domain;

namespace Hangeul.App
{
    /// <summary>
    /// 아이템 단어 목록 파일(data/item_words.json, schemaVersion 1)의 구조. JsonUtility로 바로 읽는다.
    /// 파일에 있는 syllables는 호환용 중복 정보라 읽지 않고, 단어에서 다시 계산한다.
    /// </summary>
    /// <summary>
    /// 카테고리가 담는 단어의 종류. 종류가 단어에 어떤 수치 칸이 있는지와 게임에서의 쓰임을 정한다
    /// (무기는 근접 공격 수치, 음식은 회복량, 접두어는 다른 단어 앞에 붙어 효과를 더한다).
    /// </summary>
    public enum CategoryType
    {
        Plain = 0,    // 수치가 없는 일반 단어(재료, 소모품 등)
        Weapon = 1,
        Armor = 2,
        Food = 3,
        Prefix = 4,   // 접두어·속성: 다른 단어 앞에 붙는 단어
        Ammo = 5,     // 탄(화살): 원거리 무기가 쏘는 것. 접두어('불화살')를 받을 수 있다
    }

    /// <summary>카테고리 이름과 그 종류. 파일에 적혀 있으면 카테고리 이름보다 이것이 우선한다(<see cref="ItemWordsFile.categoryTypes"/>).</summary>
    [Serializable]
    public sealed class CategoryTypeEntry
    {
        public string name;
        public CategoryType type;
    }

    [Serializable]
    public sealed class ItemWordsFile
    {
        public int schemaVersion;
        public List<string> categories = new List<string>();
        public List<ItemWordEntry> items = new List<ItemWordEntry>();
        /// <summary>단어별 상세 수치. 수치를 따로 정한 단어만 들어 있다(옛 파일에는 없는 선택 항목이라 schemaVersion은 그대로다).</summary>
        public List<WordStatsEntry> wordStats = new List<WordStatsEntry>();
        /// <summary>
        /// 카테고리별 종류(선택 항목). 적혀 있는 카테고리는 이 종류로 쓰고, 없는 카테고리는 이름으로 짐작한다
        /// (<see cref="ItemKinds.FromCategory"/>, <see cref="ItemWordSet.IsPrefixCategory"/>: 무기·방어구·음식·접두어·속성).
        /// 게임 데이터 에셋(ItemDatabase)이 만든 파일에는 항상 들어 있어서 카테고리 이름을 바꿔도 쓰임이 바뀌지 않는다.
        /// </summary>
        public List<CategoryTypeEntry> categoryTypes = new List<CategoryTypeEntry>();
    }

    [Serializable]
    public sealed class ItemWordEntry
    {
        public string word;
        public string category;

        // 접두어 규칙(DB 관리 씬에서 편집). 접두어 카테고리의 단어는 접두어로 쓰이는 쪽이라 이 값을 쓰지 않는다.
        /// <summary>이 단어 앞에 접두어를 붙일 수 있는지.</summary>
        public bool acceptsPrefix;
        /// <summary>붙일 수 있는 접두어 단어들(접두어 카테고리의 단어). acceptsPrefix가 켜져 있을 때만 의미가 있다.</summary>
        public List<string> prefixes = new List<string>();
    }

    /// <summary>완성 판정에 쓰는 단어 집합. 한글 음절로만 이루어진 단어만 들어간다.</summary>
    public sealed class ItemWordSet
    {
        readonly HashSet<string> _words = new HashSet<string>();

        public int Count => _words.Count;
        public int MaxLength { get; private set; }

        public bool Contains(string word) => word != null && _words.Contains(word);
        public IEnumerable<string> Words => _words;

        /// <summary>한글 음절로만 구성된 비어 있지 않은 문자열인지.</summary>
        public static bool IsValidWord(string word)
        {
            if (string.IsNullOrEmpty(word)) return false;
            foreach (char c in word) if (!Hangul.IsSyllable(c)) return false;
            return true;
        }

        /// <summary>단어를 넣는다. category는 DB 카테고리 이름(종류 판정에 쓴다, 없으면 null).</summary>
        public bool Add(string word, string category = null)
        {
            if (!IsValidWord(word) || !_words.Add(word)) return false;
            MaxLength = Math.Max(MaxLength, word.Length);
            if (category != null) _categoryOf[word] = category;
            return true;
        }

        readonly Dictionary<string, string> _categoryOf = new Dictionary<string, string>();

        /// <summary>단어의 DB 카테고리. 합성 단어('불화살')는 본체('화살')의 카테고리를 따른다. 모르면 null.</summary>
        public string CategoryOf(string word) => word != null && _categoryOf.TryGetValue(word, out var c) ? c : null;

        readonly Dictionary<string, CategoryType> _typeOfCategory = new Dictionary<string, CategoryType>();

        /// <summary>
        /// 단어의 종류(무기/방어구/음식). 카테고리를 모르거나 쓰임이 없는 카테고리면 <see cref="ItemKind.None"/>.
        /// 파일에 카테고리 종류가 적혀 있으면 그것을, 없으면 카테고리 이름으로 짐작한다.
        /// </summary>
        public ItemKind KindOf(string word)
        {
            string category = CategoryOf(word);
            if (category != null && _typeOfCategory.TryGetValue(category, out var type)) return ItemKinds.FromType(type);
            return ItemKinds.FromCategory(category);
        }

        readonly Dictionary<string, ItemStats> _statsOf = new Dictionary<string, ItemStats>();

        /// <summary>
        /// 단어의 상세 수치(범위에 맞춰 둔 값, 고쳐 쓰지 말 것). 수치를 따로 정하지 않은 단어는 null — 게임은 그때 원래 설정을 쓴다.
        /// 합성 단어('불화살')는 본체('화살')의 수치를 따른다.
        /// </summary>
        public ItemStats StatsOf(string word) => word != null && _statsOf.TryGetValue(word, out var s) ? s : null;

        /// <summary>파일 내용으로 집합을 만든다. 버전이 다르거나 비어 있으면 null. 잘못된/중복 단어는 건너뛴다.</summary>
        public static ItemWordSet FromFile(ItemWordsFile file, out int skipped)
        {
            skipped = 0;
            if (file == null || file.schemaVersion != 1 || file.items == null) return null;

            var set = new ItemWordSet();
            if (file.categoryTypes != null)
                foreach (var c in file.categoryTypes)
                    if (c != null && !string.IsNullOrEmpty(c.name) && !set._typeOfCategory.ContainsKey(c.name)) set._typeOfCategory[c.name] = c.type;

            foreach (var item in file.items)
                if (item == null || !set.Add(item.word, item.category)) skipped++;

            // 단어별 상세 수치: 목록에 없는 단어의 것은 무시한다. 같은 단어가 두 번 있으면 먼저 나온 것이 우선.
            if (file.wordStats != null)
                foreach (var entry in file.wordStats)
                    if (entry != null && entry.stats != null && set.Contains(entry.word) && !set._statsOf.ContainsKey(entry.word))
                        set._statsOf[entry.word] = entry.stats.Clamped();

            // 접두어 규칙으로 생기는 합성 단어("불"+"화살" = "불화살")도 하나의 아이템 단어로 등록한다(카테고리는 본체의 것).
            // 이미 파일에 있는 단어와 겹치면 파일의 단어가 우선이다(합성 단어로 세지 않는다).
            foreach (var item in file.items)
                foreach (var (prefix, compound) in PrefixPairsOf(file, item))
                {
                    if (set.Add(compound, item.category))
                    {
                        set._compounds.Add(compound);
                        set._compoundParts[compound] = (prefix, item.word);
                        if (set._statsOf.TryGetValue(item.word, out var baseStats)) set._statsOf[compound] = baseStats;   // 수치도 본체를 따른다
                    }
                    if (!set._prefixesByBase.TryGetValue(item.word, out var list)) set._prefixesByBase[item.word] = list = new List<string>();
                    if (!list.Contains(prefix)) list.Add(prefix);   // 위에 얹어도 인정하는 접두어(MatchScanner의 '위 배치')
                }
            return set;
        }

        readonly HashSet<string> _compounds = new HashSet<string>();
        readonly Dictionary<string, (string prefix, string baseWord)> _compoundParts = new Dictionary<string, (string, string)>();
        readonly Dictionary<string, List<string>> _prefixesByBase = new Dictionary<string, List<string>>();
        static readonly IReadOnlyList<string> NoPrefixes = new string[0];

        /// <summary>이 단어 앞/위에 붙을 수 있는 접두어 단어들(DB의 접두어 규칙). 없으면 빈 목록. 긴 접두어가 먼저 온다.</summary>
        public IReadOnlyList<string> PrefixesFor(string baseWord)
        {
            if (baseWord == null || !_prefixesByBase.TryGetValue(baseWord, out var list)) return NoPrefixes;
            return list.OrderByDescending(p => p.Length).ToList();
        }

        /// <summary>접두어 규칙으로만 만들어진 단어인지(파일에 직접 적힌 단어는 false).</summary>
        public bool IsCompound(string word) => word != null && _compounds.Contains(word);

        /// <summary>합성 단어를 접두어와 본체로 나눈다('화염검' → '화염', '검'). 합성 단어가 아니면 false.</summary>
        public bool TryGetCompound(string word, out string prefix, out string baseWord)
        {
            if (word != null && _compoundParts.TryGetValue(word, out var parts)) { prefix = parts.prefix; baseWord = parts.baseWord; return true; }
            prefix = baseWord = null;
            return false;
        }

        /// <summary>다른 단어 앞에 붙는 단어를 모아 둔 카테고리들(예: 접두어 = 레어·희귀, 속성 = 불·화염·얼음).</summary>
        public static readonly string[] PrefixCategories = { "접두어", "속성" };

        public static bool IsPrefixCategory(string category) => category != null && System.Array.IndexOf(PrefixCategories, category) >= 0;

        // 파일에 카테고리 종류가 적혀 있으면 그것으로, 없으면 이름으로 접두어 카테고리인지 가린다
        static bool IsPrefixCategoryIn(ItemWordsFile file, string category)
        {
            if (category != null && file.categoryTypes != null)
                foreach (var c in file.categoryTypes)
                    if (c != null && c.name == category) return c.type == CategoryType.Prefix;
            return IsPrefixCategory(category);
        }

        /// <summary>
        /// 한 단어에서 접두어 규칙으로 만들어지는 합성 단어들(접두어 + 단어). 접두어를 받는 단어가 아니거나,
        /// 접두어 카테고리의 단어가 아닌 이름은 건너뛴다.
        /// </summary>
        public static IEnumerable<string> CompoundsOf(ItemWordsFile file, ItemWordEntry item)
        {
            foreach (var (_, compound) in PrefixPairsOf(file, item)) yield return compound;
        }

        /// <summary>한 단어에서 접두어 규칙으로 만들어지는 (접두어, 합성 단어) 쌍들.</summary>
        public static IEnumerable<(string prefix, string compound)> PrefixPairsOf(ItemWordsFile file, ItemWordEntry item)
        {
            if (file?.items == null || item == null || !item.acceptsPrefix || item.prefixes == null) yield break;
            if (IsPrefixCategoryIn(file, item.category) || !IsValidWord(item.word)) yield break;

            foreach (var prefix in item.prefixes)
            {
                if (!IsValidWord(prefix)) continue;
                bool isPrefixWord = file.items.Exists(i => i != null && i.word == prefix && IsPrefixCategoryIn(file, i.category));
                if (isPrefixWord) yield return (prefix, prefix + item.word);
            }
        }
    }
}
