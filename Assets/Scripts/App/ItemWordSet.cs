using System;
using System.Collections.Generic;
using Hangeul.Domain;

namespace Hangeul.App
{
    /// <summary>
    /// 아이템 단어 목록 파일(data/item_words.json, schemaVersion 1)의 구조. JsonUtility로 바로 읽는다.
    /// 파일에 있는 syllables는 호환용 중복 정보라 읽지 않고, 단어에서 다시 계산한다.
    /// </summary>
    [Serializable]
    public sealed class ItemWordsFile
    {
        public int schemaVersion;
        public List<string> categories = new List<string>();
        public List<ItemWordEntry> items = new List<ItemWordEntry>();
    }

    [Serializable]
    public sealed class ItemWordEntry
    {
        public string word;
        public string category;
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

        public bool Add(string word)
        {
            if (!IsValidWord(word) || !_words.Add(word)) return false;
            MaxLength = Math.Max(MaxLength, word.Length);
            return true;
        }

        /// <summary>파일 내용으로 집합을 만든다. 버전이 다르거나 비어 있으면 null. 잘못된/중복 단어는 건너뛴다.</summary>
        public static ItemWordSet FromFile(ItemWordsFile file, out int skipped)
        {
            skipped = 0;
            if (file == null || file.schemaVersion != 1 || file.items == null) return null;

            var set = new ItemWordSet();
            foreach (var item in file.items)
                if (item == null || !set.Add(item.word)) skipped++;
            return set;
        }
    }
}
