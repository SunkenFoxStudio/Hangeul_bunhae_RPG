# data/

## item_words.json

`inventory_test.html` 의 **아이템 단어 목록**을 저장한 파일입니다. 유니티에서 `JsonUtility` 로 바로 읽을 수 있게
배열과 중첩 클래스만 사용했습니다. (딕셔너리 없음)

### 저장 / 불러오기 (페이지에서)

- 상단 `목록 저장` : 현재 목록을 이 파일에 씁니다.
  - **처음 한 번**만 파일 선택 창이 뜹니다. 이 폴더의 `item_words.json` 을 고르세요. 이후에는 브라우저가 그 파일을 기억해서
    버튼만 누르면 같은 파일을 덮어씁니다. (브라우저를 다시 열면 "파일 수정 허용" 확인이 한 번 뜰 수 있습니다.)
  - `Shift+클릭` : 저장할 파일을 다시 고릅니다.
  - 선택한 파일이 아이템 목록 형식이 아니면 덮어쓰기 전에 확인을 받습니다.
- 상단 `목록 불러오기` : 이 파일의 내용으로 현재 목록을 **통째로 바꿉니다.**
- Chrome / Edge 같은 Chromium 계열 브라우저에서만 덮어쓰기가 됩니다. 그 외 브라우저에서는 파일을 내려받는 방식으로 동작합니다.

### 구조 (schemaVersion 1)

```jsonc
{
  "schemaVersion": 1,
  "savedAt": "2026-10-12T07:22:36.430Z",     // 저장 시각 (UTC)
  "categories": ["추가한 단어", "무기", ...],  // 분류 목록 (표시 순서). 비어 있는 분류도 포함
  "items": [                                  // 분류 순서대로, 같은 분류 안에서는 목록 순서대로
    {
      "word": "검",                           // 아이템 단어 (한글 음절로만 구성, 목록 안에서 유일)
      "category": "무기",
      "syllables": [                          // 글자별 자모 분해
        { "letter": "검", "cho": "ㄱ", "jung": "ㅓ", "jong": "ㅁ" }   // 받침이 없으면 "jong": ""
      ]
    }
  ]
}
```

`cho`/`jung`/`jong` 은 호환 자모(ㄱ, ㅏ, ㄳ …) 문자입니다. 쌍자음(ㄲ)과 겹받침(ㄳ)은 한 글자로 들어 있습니다.

### 유니티에서 읽기

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class Syllable { public string letter, cho, jung, jong; }
[Serializable] public class ItemWord { public string word, category; public List<Syllable> syllables; }
[Serializable] public class ItemWordData {
    public int schemaVersion;
    public string savedAt;
    public List<string> categories;
    public List<ItemWord> items;
}

// 예: Resources/item_words.json 또는 StreamingAssets 에 복사해서 사용
var data = JsonUtility.FromJson<ItemWordData>(Resources.Load<TextAsset>("item_words").text);
```

> `Resources` 로 읽으려면 파일을 `Assets/Resources/` 아래로 복사하세요. (확장자 `.json` 은 로드 이름에서 뺍니다.)
