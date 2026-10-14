# 한글 분해·조합 인벤토리 — Unity 이식 구조도 (초안)

> 대상: 2D JRPG의 **플레이어 인벤토리** 기믹
> 원본 레퍼런스: **`inventory_medieval.html`(기능·비주얼 기준, 유일한 레퍼런스)**. 이 문서의 `inventory_test.html` 언급은 삭제된 파일의 옛 이름이며 같은 코드로 읽는다. (`index.html`은 자유 배치 원형)
> 목표: HTML 프로토타입과 **조작감·규칙·연출이 거의 동일**하게 Unity에서 재현한다.

---

## 1. 핵심 방향

| 원칙 | 내용 |
|---|---|
| 규칙과 표현의 분리 | 한글 규칙(분해/결합/회전)·그리드 로직은 **UnityEngine에 의존하지 않는 순수 C#**로 작성. UI는 그 결과를 그리기만 한다. |
| HTML 동작이 곧 명세 | 애매한 동작은 HTML 코드를 정답으로 본다. 포팅 시 함수 단위로 대응시킨다(§3 표). |
| 데이터 주도 | 결합표·회전표·카테고리·아이템 단어는 코드가 아닌 데이터(ScriptableObject/JSON)로. |
| 테스트 가능성 | 순수 로직은 EditMode 테스트로 HTML과 같은 입력→같은 출력을 검증한다. |

---

## 2. 레이어 구조

```
┌──────────────────────────────────────────────────────────┐
│ Presentation (uGUI / UI Toolkit 중 택1, 아래 §6)          │
│  InventoryView · CardView · WordPanelView · MakeablePanel │
│  CompletionOverlay · BagFrame(테마)         │
├──────────────────────────────────────────────────────────┤
│ Interaction (입력 → 명령)                                 │
│  DragController · RotateController · ChipDragController   │
│  (Input System: 마우스 / 터치 / 패드 커서)                │
├──────────────────────────────────────────────────────────┤
│ Application (상태 + 명령)                                 │
│  InventoryModel · CardOps(분해/결합/회전) · WordBook      │
│  ArrangementService · MatchScanner · SaveService          │
├──────────────────────────────────────────────────────────┤
│ Domain (순수 C#, Unity 무의존)                            │
│  Hangul · JamoTables · Card · GridMath · Arrangement      │
└──────────────────────────────────────────────────────────┘
```

의존 방향은 **위 → 아래 한 방향**. Domain/Application은 `UnityEngine` 참조 금지(asmdef로 강제).

### Assembly Definition

| asmdef | 포함 | 참조 허용 |
|---|---|---|
| `Hangeul.Domain` | Hangul, 표, Card, GridMath. 위치: `Assets/Scripts/Domain` (M0 구현: `Hangul`, `JamoTables`, `RotationTable`, `SyllableLayout` — 표는 정적 클래스에 내장, 데이터 분리는 추후) | 없음 (noEngineReferences) |
| `Hangeul.App` | InventoryModel, Ops, Services. 위치: `Assets/Scripts/App` (M1 구현: `InventoryModel` — AddWord/Detach/SplitJamo/Transform/TryMerge/Move/Resize/Discard, M3: `RotationSession` + `BeginRotation/ApplyRotation/FinishRotation`, M4: `ToSave/Load` + `InventorySaveData`, M7: `MatchScanner`, `ItemWordSet`, `WordCompleted` 이벤트). Domain 쪽 M1: `Card`, `GridModel`, `CardOps`(결합), `DetachRules`, `ForbiddenSyllables` | Domain |
| `Hangeul.UI` | View, Controller, 테마. 위치: `Assets/Scripts/UI` (M2 구현: `InventoryView`, `CardView`, `WordInputView`; M7: `TrashZone`, `CompletionOverlay`, 단어 목록은 `Assets/Resources/item_words.json`(= `hangeul-bunhae/data`의 복사본); M4: `InventorySaveStore` — 개발용 JSON 저장소, `persistentDataPath/inventory.json`, 변경 0.5초 뒤 자동 저장 — 글자는 uGUI `Text` + Nanum Myeongjo TTF, TMP 전환은 M8) | Domain, App, Unity |
| `Hangeul.Game` | 플레이어 캐릭터, FSM, 상호작용(§13). 위치: `Assets/Scripts/Game` | Unity.InputSystem |
| `Hangeul.Game.Editor` | 에디터 전용 생성 메뉴 | Hangeul.Game |
| `Hangeul.Tests` | EditMode/PlayMode 테스트. 위치: `Assets/Tests/EditMode` | 전부 |

---

## 3. HTML → Unity 대응표

| HTML (inventory_test) | Unity 대응 | 레이어 |
|---|---|---|
| CHO/JUNG/JONG 배열, 유니코드 산술 | `Hangul.Compose/Decompose` | Domain |
| CC/VV 결합표, SPLIT | `JamoTables` (데이터 로드) | Domain |
| ROT4 회전표, `nextStep` | `RotationTable.Next(jamo, quarterTurns)` | Domain |
| 카드 `{id,kind,jamo,s,g,col,row,x,y,el}` | `Card`(데이터) + `CardView`(`el` 대체) | Domain / UI |
| `isDone`, `letterOf` | `Card.IsComplete`, `Card.Letter` | Domain |
| `COLS/ROWS`, `cellOf`, `cardAt`, `emptyNear`, `findRun` | `GridModel` | Domain |
| `addWord`, `setGridSize`, `layoutGrid` | `InventoryModel.AddWord/Resize` + `InventoryView.Layout` | App / UI |
| 카드 detach / 병합 드롭 | `CardOps.Detach / TryMerge` | App |
| `transform()`(R키·더블클릭·우클릭) | `RotateController.QuickRotate` → `CardOps.Rotate` | UI → App |
| `applyRot` / `finishRotate` (모서리 드래그) | `RotateController.Drag` (미리보기 상태 + 확정) | UI |
| `computeArrangements` (`FULL_LEN`, `ARR_CAP`, `SUBSEQ_MAX`) | `ArrangementService` (비동기/코루틴 분할) | App |
| `placedInfo` (오른쪽 패널) | `MakeablePanelView` + `PlacedInfo` | UI / App |
| `cats`, `wordCat`, `order`, `DEFAULT_CAT`, `openCat` | `WordBook`(모델) + `WordPanelView` | App / UI |
| 칩 드래그, `#dropdock`, `moveWord`, `newCategory` | `ChipDragController` + `DropDockView` | UI |
| `scanMatches` / `showDone` | `MatchScanner` + `CompletionOverlay` | App / UI |
| 저장/불러오기(JSON, schemaVersion 1) | `SaveService` | App |
| `BANNED`, `isBanned`, `rage()` | `ForbiddenWords` (금지 조합 검사만, 특수 연출 없음) | App / UI |
| medieval 테마 CSS, `drawBag` SVG | `ThemeProfile`(SO) + `BagFrame` | UI |
| `combine`, `findTarget`, `apply` | `CardOps.TryCombine / TryMerge` (§4.4) | Domain / App |
| `pickSlot`, `PULL`, `PREF`, `detach`, `splitJamo` | `DetachRules`, `CardOps.Detach / SplitJamo` (§4.4) | Domain / App |
| `hold`, `HOLD_MS`, `isHoPair` | `HoHoldTracker` (§4.4, 시간 입력은 외부 주입) | App |
| `applyRot`, `finishRotate`, `d.orig/steps/rn` | `RotationSession` (§4.5) | App |
| `discardCard(s)`, `sel`, 삭제 모드 | `InventoryModel.Discard`, `DiscardController` (§5.6) | App / UI |
| `setUserWord`, `removedBase`, `applyExport` | `WordBook.SetEnabled / Import` (§5.2) | App |
| `layoutGrid`, `BAG`, 좌우 패널 표시 조건 | `InventoryLayout` (§6.5) | UI |
| 하단 입력창 `#dock`, `start`, `shakeInput` | `WordInputView` (디버그/치트용, §6.2) | UI |

> `inventory_test.html`과 `inventory_medieval.html`은 스크립트가 사실상 동일하다. 차이는 가방 SVG·격자 여백(`BAG`)·카드 그라데이션의 `--rot` 보정뿐이며, 기능 명세는 어느 쪽을 봐도 같다.

---

## 4. Domain 설계

### 4.1 한글 규칙 (`Hangeul.Domain`)

```csharp
public static class Hangul {
    public static bool IsSyllable(char c);
    public static (int cho, int jung, int jong) Decompose(char syllable);
    public static char Compose(int cho, int jung, int jong);
}

public sealed class JamoTables {            // JSON/SO에서 로드
    public bool TryCombine(char a, char b, out char result);  // CC(ㄱ+ㄱ=ㅋ…) / VV(ㅗ+ㅏ=ㅘ…)
    public bool TrySplit(char combined, out (char, char) parts);
}

public static class RotationTable {
    public static char Rotate(char jamo, int quarterTurns); // ROT4: 없으면 자기 자신 유지
}
```

- 회전은 **90° 단위 인덱스(`quarterTurns` 0..3)** 가 기준. 드래그 중 연속 각도는 UI가 `Round(angle/90)`로 환산해 넘긴다.
- 표의 정답은 HTML과 동일: 예) `ㅏ` 90°→`ㅜ`, 180°→`ㅓ`, 270°→`ㅗ`. 회전 후 매핑이 없으면 **원래 자모 유지**(`ㄱ` 90°=`ㄱ`, 180°=`ㄴ`).
- ROT4 전체 (시계방향 0/90/180/270°): `ㅏ→ㅏㅜㅓㅗ`, `ㅜ→ㅜㅓㅗㅏ`, `ㅓ→ㅓㅗㅏㅜ`, `ㅗ→ㅗㅏㅜㅓ`, `ㅑ→ㅑㅠㅕㅛ`(같은 순환), `ㅣ↔ㅡ`(90°마다 교대), `ㄱ→[ㄱ,-,ㄴ,-]`, `ㄴ→[ㄴ,-,ㄱ,-]` (`-`=매핑 없음).
- HTML 소스에는 테스트 로그가 없으므로 **골든 데이터는 위 표에서 생성**한다(`Tests/Golden/rot4.json`: 모든 자모 × 0..3 → 기대 결과). `nextStep` 골든도 함께 만든다.

### 4.2 카드

```csharp
public enum CardKind { Syllable, Jamo }

public sealed class Card {
    public int Id;
    public CardKind Kind;
    public char Jamo;            // Kind==Jamo
    public Syllable Parts;       // Kind==Syllable: 초/중/종 슬롯 (빈 슬롯 = 고스트)
    public int Col, Row;         // 그리드 좌표
    public int Rotation;         // 현재 표시용 각도(도), 회전 확정 시 정규화
    public bool IsComplete;      // = isDone
    public char? Letter;         // = letterOf
}
```

- `Card`는 **순수 데이터**. 화면 요소(`el`)는 `Dictionary<int, CardView>`로 UI가 보관.
- 음절 카드는 HTML처럼 슬롯 `s`(채워진 자모)와 고스트 `g`(떼어낸 자모의 흔적)를 따로 가진다. 빈 슬롯은 반투명 글자로 그려진다.
- **고스트 규칙**: 초성·중성을 떼면 `g[slot]=떼어낸 자모`(반투명 표시), **종성을 떼면 `g.jong=''`**(고스트 없음 → 남은 카드는 받침 없는 온전한 글자).
- 남은 슬롯이 1개가 되면 카드는 `Kind=Jamo`로 변환되고 `s`, `g`는 초기화된다.
- **`IsComplete`** = `Kind==Syllable && s.cho && s.jung && (s.jong || !g.jong)`. 초성·중성이 있고 받침 고스트가 없는 것.
- **`Letter`** = `Compose(s.cho, s.jung, s.jong)`. 완성 카드에만 의미가 있다.
- 슬롯 영역 비율표(`regions`: 모음 형태 V/H/C × 받침 유무)와 드래그 방향별 분리 규칙(`PULL`)은 UI에서도 쓰이므로 **Domain의 `SyllableLayout`** 에 코드/데이터로 둔다. (마스크 클리핑 좌표 = `regions`의 0~100 좌표계)

### 4.3 그리드

```csharp
public sealed class GridModel {
    public int Cols, Rows;                       // 1..20
    public Card CardAt(int col, int row);
    public bool TryFindEmptyNear(int col, int row, out (int, int) cell);
    public bool TryFindRun(int n, (int col,int row)? anchor, out (int,int) start); // findRun: 한 줄에 n칸 연속 빈 자리
    public ResizeResult Resize(int cols, int rows);     // 정책은 §5.1 (HTML 확정 동작)
}
```

- `TryFindEmptyNear(col,row,prefs)`: **우선순위 상대좌표 `prefs`를 먼저** 보고, 없으면 맨해튼 거리가 가장 가까운 빈 칸(동률이면 행→열 순서로 먼저 만난 칸).
- `TryFindRun(n, anchor)`: anchor가 없으면 격자 중앙 줄부터, 있으면 anchor 행·열과 가까운 순으로 탐색.
- 칸 계산은 카드 **중심**이 속한 칸(`cellOf`)이며 격자 밖은 clamp.

### 4.4 카드 연산 규칙 (`CardOps`, 순수 로직)

**결합 `TryCombine(dragged:Jamo, target:Card)`** — 드래그한 것은 항상 자모 카드이고, 판정 대상은 **드래그 카드 중심이 놓인 칸의 카드**다(거리 기반이 아님).

| target | 규칙 |
|---|---|
| 자모 | 모음+모음 → `VV`(양방향), 자음+자음 → `CC`(양방향). 없으면 초성+중성 → 음절 카드(`s={cho,jung}`). 순서 무관 |
| 자모, 둘이 ㅗ·ㅇ | 기본은 `오`(초성 ㅇ + 중성 ㅗ). **0.6초 홀드가 성립했을 때만 `ㅎ`** |
| 음절 | 모음: 중성이 비었으면 채움, 이미 있으면 `VV[s.jung+j]`로 합성 가능할 때만. 자음: 초성이 비면 초성으로(초성 가능한 자모만), 초성만 있고 중성이 없으면 거절, 받침이 비면 받침으로, 받침이 있으면 `CC[s.jong+j]`가 받침 가능 자모일 때 겹받침, 마지막으로 `CC[s.cho+j]`가 초성 가능일 때 쌍자음 초성 |

- 결합 성공 시 드래그 카드는 제거되고 target이 변환된다(`pop` 연출). 채운 슬롯의 고스트 `g[slot]`은 지운다.
- **금지어 검사**: 결합 결과가 금지 음절(`됬`)이면 결합을 거절하고 드래그 카드를 **원래 칸으로 되돌린다**(특수 연출 없음, 가벼운 거절 피드백만).

**분리 `Detach(card, slot, dir)`**
- 방향 `dir`(L/R/U/D)은 첫 이동 벡터 중 큰 축. 이동 14px 이상에서 판정한다(`PULL[layout][dir]`의 앞에서부터 **채워져 있는** 슬롯).
- 뽑을 슬롯이 없으면 흔들기(`shake`) 후 종료. 빈 칸이 없으면 **아무것도 바꾸지 않고** 흔들기.
- 새 자모 카드는 **끌어당긴 방향 쪽 인접 칸 우선**(`PREF`: L=[좌,상,하,우], R=[우,상,하,좌], U=[상,좌,우,하], D=[하,좌,우,상]), 없으면 가장 가까운 빈 칸.
- 카드의 **그립(상단 손잡이)** 을 잡으면 분리가 아니라 카드 이동이다. 우클릭 드래그도 분리하지 않고 이동만 한다.

**겹자모 분리 `SplitJamo(card)`** (더블클릭 / `transform(splitFirst)`)
- 새 카드는 원본 오른쪽 우선(`PREF.R`). `ㅎ`은 **세로로 쌓기**: 아래 칸이 비면 원본=ㅗ(위)·새=ㅇ(아래), 아래가 막혀 위가 비면 원본=ㅇ·새=ㅗ(위), 둘 다 막히면 가장 가까운 빈 칸. 칸이 없으면 흔들기.
- **분리 vs 회전 우선순위**: 더블클릭은 분리 우선(`splitFirst=true`), R키/우클릭은 회전 우선. 회전이 불가능한 자모는 분리, 둘 다 불가능하면 흔들기.

**ㅗ+ㅇ 홀드 (`HoHoldTracker`)**: 드래그 중인 카드가 상대 카드 위에 **같은 쌍으로 연속해 머문 시간**이 0.6초를 넘으면 `On=true`(모음 ㅗ 카드가 자음색으로 전이). 대상이 바뀌거나 이탈하면 초기화. 시간은 `Tick(float dt)` 또는 타임스탬프를 외부에서 주입해 EditMode 테스트가 가능하게 한다.

### 4.5 회전 세션 (`RotationSession`) — 드래그 회전의 정확한 규칙

단발 회전(`QuickRotate`: R/우클릭/더블클릭)은 `RotationTable`의 `nextStep(j)`(모양이 바뀌는 최소 시계 회전 횟수 k)만 쓴다. 모서리 드래그는 **세션 상태**가 필요하다.

```csharp
public sealed class RotationSession {
    public char Orig;    // 드래그 시작 시점의 자모 (항상 이 기준으로 재계산)
    public int  Steps;   // 누적 90° 횟수 (음수 가능)
    public int  Rn;      // 현재 화면의 자모가 Orig를 몇 번 돌린 모양인지
    public char Current; // 현재 자모
    public bool Apply(int steps);   // 90° 경계 통과 시 호출. 자모가 바뀌었는지 반환
    public char Finish();           // 매핑 없는 각도면 Orig로 복귀
}
```
- `Apply(st)`: `n=((st%4)+4)%4`, `want = ROT4[Orig][n] ?? Orig`. `want != Current`면 자모 교체 + `Rn = (want==Orig ? 0 : n)`. **화면 각도 = (st − Rn)·90°**로 이어 붙여 시각적으로 끊기지 않게 한다.
- 드래그 중 누적 각도는 이전 각도와의 차이를 ±π로 보정해 합산하고 `Round(total/90°)`로 스텝화. 반대로 돌려도(음수) 같은 규칙.
- **회전 핸들 영역**: 자모 카드 모서리 4곳, 각 한 변의 **26%** 정사각 영역. 이 영역에 포인터가 있으면 회전 커서 표시. 음절 카드엔 없다.
- 단발 회전 애니메이션: 즉시 새 자모로 교체 후 `-k·90°→0°`, 지속 `100 + 90·k` ms.

---

## 5. Application 설계

### 5.1 InventoryModel (단일 진실 소스)

```csharp
public sealed class InventoryModel {
    public GridModel Grid;
    public event Action<CardChange> Changed;     // View는 이 이벤트만 구독
    public CardChange AddWord(string word);       // 빈 칸 탐색 후 낱자 카드로 배치
    public CardChange Detach(Card c, Slot slot);  // 음절 → 자모
    public CardChange TryMerge(Card dragged, Card target);
    public CardChange Rotate(Card c, int quarterTurns);
    public CardChange Move(Card c, int col, int row);
}
```

**확정된 동작 규칙 (HTML 기준)**

- **이동/교환 `Move`**: *(게임 확정 변경: HTML과 다름)* 드래그 중 카드는 **격자에 묶이지 않고 화면 어디든 포인터를 따라간다.** 놓을 때 **카드 중심이 격자 안일 때만** 그 칸에 배치(스냅)되고, 격자 밖이면 **원래 칸으로 부드럽게 돌아간다.** 쓰레기통 위에서 놓으면 쓰레기통 칸 가운데로 달라붙은 뒤 삭제(§5.6). (HTML은 드래그 중에도 격자 영역으로 clamp.) 칸에 카드가 있고 결합 불가이면 **자리 교환**(상대 카드는 내 원래 칸으로, `pop` 연출). 결합 가능이면 교환 대신 결합. 드래그 중 칸 하이라이트: 빈 칸=초록, 교환=파랑, 결합=주황/진홍.
- **`AddWord(word, anchor?)`**: 한글 음절만 추출 → 금지 음절은 건너뜀 → **남은 칸 수만큼**만 추가(`COLS*ROWS - 카드 수`). `COLS`글자씩 끊어 한 줄에 연속 배치(`TryFindRun`, anchor 없으면 중앙 줄부터). 한 줄이 안 나오면 `TryFindEmptyNear`로 흩어서 배치. 반환값=실제 추가 수; 요청보다 적으면(가득 참) 입력창 흔들기. 추가 직후 `MatchScanner.Scan(notify:false)`를 불러 **입력만으로 이미 완성된 단어는 알리지 않는다**.
- **칩을 격자에 드롭**: 단어 칩을 격자 위에 놓으면 그 칸을 anchor로 `AddWord`. 드래그 중에는 놓일 자리(`min(글자수, COLS)`칸 폭)를 미리 하이라이트.
- **`Resize(cols, rows)`**: 1..20으로 clamp. 용량 부족(`카드 수 > cols*rows`)이면 **확인창** → 승인 시 (행→열 순) 마지막 카드부터 초과분을 폐기(연출 포함), 거부 시 값 원복. 이후 새 격자 밖이거나 겹친 카드는 **가장 가까운 빈 칸으로 이동**. 끝나면 `Scan(notify:false)`. 값은 `inv_grid`처럼 별도 설정으로 저장.
- **카드 크기는 화면에서 유도**: `CARD = max(28, floor(min((W - 2*mx)/COLS, (H - top - bot - 8)/ROWS)))`. 별도 슬라이더 없음(§6.5).
- **금지 음절 `됬`**: `AddWord`는 해당 음절을 건너뛰고, 결합은 거절한다(§4.4). 특수 연출 없음.

- 모든 변경은 **Command 객체**로 만들어 `Changed`로 통지 → View가 애니메이션 재생. (Undo는 후순위 옵션.)
- `Rotate`는 HTML 요구사항 그대로: **회전 시작(R 입력/드래그 중 90° 경계 통과) 시점에 즉시 자모를 교체**하고, 애니메이션은 `-k*90° → 0°`로 보정 재생.

### 5.2 WordBook (왼쪽 "아이템 단어" 패널 모델)

- 필드: `categories`(순서 보장), `wordCategory`(단어→카테고리), `order`.
- 기본 카테고리 `추가한 단어`(`DEFAULT_CAT`): **항상 목록에 노출**, 다른 카테고리는 3열 카드형 "폴더"로 표시 후 클릭 시 내부 진입.
- 폴더 안에 있는 동안 기본 카테고리 목록은 숨김.
- 단어 이동·새 카테고리 생성: `MoveWord`, `NewCategory`.
- `MoveWord(w, cat)`: 분류 변경 + `order`의 **맨 뒤로** 이동. `NewCategory`: 이름 입력(최대 12자, 공백 제거, 중복이면 기존 분류 사용) 후 이동.
- **분류 삭제는 비어 있는 분류만**, 그리고 삭제 모드가 켜졌을 때만 × 노출. `추가한 단어`는 삭제 불가. 단어 × 삭제도 삭제 모드에서만.
- 폴더 안에서 칩을 끌면 `DropDock`에 "다른 폴더 목록 + 새 분류(＋)"가 나타난다(열린 폴더 자신은 제외). 폴더 목록에서 끌면 "새 분류를 만들려면 여기에 놓으세요".
- 패널 가장자리(34px 이내)에서 칩을 끌면 목록 자동 스크롤(프레임당 12px).
- **단어 활성화 개념**: HTML은 `BASE`(기본 단어) / `userWords` / `removedBase`로 나눠 저장했지만, Unity에서는 이를 **단일 `ItemWordDB`(JSON) + `enabled` 목록**으로 단순화한다. 오른쪽 "만들 수 있는 글자"의 체크박스 = `WordBook.SetEnabled(word, on)`: 켜면 목록에 추가(기본 분류 `추가한 단어`), 끄면 제거. 한글 음절로만 구성된 단어만 허용.
- **Import(`applyExport`)** = 파일 내용으로 목록 **통째 교체**: `schemaVersion==1` 검증, 유효하지 않은/중복 단어 제외, `categories`에 없는 분류는 뒤에 추가, `추가한 단어`가 없으면 맨 앞에 삽입.
- 단어가 바뀌면 `ArrangementService` 재계산 + `MatchScanner.Scan(false)`가 따라야 한다.

### 5.3 ArrangementService (오른쪽 "만들 수 있는 글자")

HTML에서 렉이 났던 지점이라 처음부터 **성능 상한을 설계에 포함**한다.

- 규칙: 완성 글자 길이 `FULL_LEN=2` 이하만 경우의 수 전개, **3글자 이상은 현재 보유 중인 것만 표시**.
- 상한: `ARR_CAP=3000`, 부분수열 길이 `SUBSEQ_MAX=14`.
- **입력 범위**: `IsComplete`인 **음절 카드만** 사용. 낱개 자모 카드는 쓰지 않고, 빈 슬롯이 있는 음절 카드는 제외하되 그 수(`partial`)를 안내 문구로 표시. 카드는 위→아래, 왼쪽→오른쪽 순서로 `seq`를 만들고, 같은 줄에서 열이 연속된 구간을 `runs`(offset 포함)로 묶는다.
- **`listed`(목록에 있는 단어)**: 현재 단어 목록 전체를 전수 검사해 글자 **다중집합(개수 포함)** 으로 만들 수 있는 단어. 정렬 = 티어(0:붙은 연속부분 → 1:배치 순서 유지한 건너뛴 부분수열 → 2:그 밖) → 첫 위치 → 긴 단어 우선.
- **`groups`(그 밖의 배열)**: 글자 수별로 `len=1..n`. 1순위(붙은 연속부분)는 상한에 걸려도 계속 채우고, 2순위(부분수열)는 `len<=FULL_LEN && n<=SUBSEQ_MAX`, 3순위(순열 DFS)는 `len<=FULL_LEN`. **이미 목록에 있는 단어는 제외**, 중복 제거, 새 단어가 `ARR_CAP`개에 닿으면 `truncated=true`로 중단하고 UI에 "처음 3000개만 표시" 안내.
- **재계산 트리거**: `placedInfo.key`(runs의 `offset:문자열`) + `partial` 수가 바뀔 때만. 즉 낱개 자모 이동만으로는 재계산하지 않는다.
- 구현: 순수 함수 `Compute(PlacedInfo)`. 인벤토리 변경 시 **디바운스(HTML은 60ms, Unity는 60~100ms)** 후 호출, 무거우면 프레임 분할 코루틴/`Task.Run`(Domain은 스레드 안전하게 불변 입력 사용).
- 결과는 티어별 리스트(`tiers`)로 반환 → View는 스크롤 뷰에 **풀링**하여 표시.

### 5.4 MatchScanner

- `scanMatches`: **같은 행에서 왼쪽→오른쪽으로 연속된 `IsComplete` 음절 카드**의 글자를 이어 붙여 단어 목록과 대조(모든 시작 열에서 전개, 한 칸이라도 비거나 미완성이면 끊김). 세로/대각선은 인정하지 않는다.
- **접두어 위 배치 (예외, 확정 2026-10-14)**: DB의 접두어 규칙(`ItemWordSet.PrefixesFor`)에 있는 접두어 단어는 본체 단어의 **바로 윗줄에, 접두어의 첫 글자를 본체의 첫 글자 바로 위에 맞춰(왼쪽 정렬)** 얹어도 한 덩어리 단어로 인정한다(`WordMatch.PrefixLength > 0`; `Row/Col/Length`는 본체, 접두어는 `Row-1`줄의 `Col`부터). 접두어가 본체보다 길어 오른쪽으로 삐져나가도 된다. 그 밖의 세로/대각선 배치는 여전히 인정하지 않는다. **일렬(접두어가 본체 왼쪽에 이어 붙음)과 위 배치가 겹치면 일렬이 우선**한다: 위 배치는 본체가 "혼자 보이는 일렬 단어"일 때만 성립하고, 접두어 칸이 더 긴 일렬 단어에 쓰이고 있으면 거절된다. 접두어 글자를 혼자 이루던 일렬 단어(예: '불')는 합쳐지며 사라진다.
- 표시 대상은 **겹치지 않는 가장 긴 매치**(길이 내림차순 → 행 → 열 순으로 셀 점유 검사; `술병` 완성 시 `술`, `병`은 숨김).
- **새로 완성된 것만 알림**: 이전 스캔의 매치 키(`행:열:단어:카드id들`) 집합 `prevMatches`와 비교. `prevMatches`는 숨겨진 매치까지 **전체 기준**으로 저장해, 이미 완성돼 있던 것이 다시 알림되지 않는다. `AddWord`, `Resize`, 단어 목록 변경 시에는 `notify:false`로 호출.
- 알림 시 `CompletionOverlay`(해당 칸 폭만큼 초록 글로우, 약 1초) + **왼쪽 패널의 해당 칩(`flash`)과 닫힌 폴더가 반짝**. **완성돼도 카드는 소비하지 않고 유지한다**(HTML과 동일, §10-9 확정). 완성 단어의 결과 표시는 가방 모드에서 스프라이트로 대체된다(§5.7).
- 게임 쪽에는 `OnWordCompleted(word, cells)` 이벤트만 노출(JRPG 본편이 구독해 아이템 지급 등 처리).

### 5.5 SaveService

- 포맷은 HTML과 동일 스키마 유지: `schemaVersion: 1`, 카드 글자 키 `letter`. → **HTML에서 저장한 `data/item_words.json`을 Unity에서 그대로 불러올 수 있어야 한다**(상호 호환 목표).
- 구현: `JsonUtility` 또는 `Newtonsoft`(Unity Package) 중 택1. 파일 위치 `Application.persistentDataPath`.
- **세이브 수명 (확정 방향)**: 현재 `InventorySaveStore`의 자동 저장·자동 불러오기는 개발 테스트용이다. 실제 빌드에서 인벤토리 데이터는 **'이어하기'를 위한 게임 세이브의 일부**여야 한다. 따라서 본편 연동(M9) 때 다음을 지원한다.
  - **새 게임 시작**: 해당 세이브(슬롯)의 인벤토리 데이터를 지우고 빈 인벤토리로 시작.
  - **세이브 슬롯**: 슬롯마다 별도 파일(또는 슬롯 데이터 안의 `inventory` 필드). `InventoryView.saveFileName`을 슬롯별로 바꾸는 방식으로 대응하고, `InventorySaveStore.Delete(fileName)`이 그 삭제 수단이다.
  - 본편 세이브 시스템이 생기면 `InventoryModel.ToSave()/Load()`만 그쪽에서 호출하고, 개발용 자동 저장(`persist`)은 끈다.
- 마이그레이션 훅: `schemaVersion` 분기.
- 그리드 크기 설정은 별도 설정 파일/PlayerPrefs(`inv_grid` 대응).
- HTML이 저장하는 것은 아이템 단어 목록뿐이고 격자 위 카드 배치는 저장하지 않는다. **게임에서는 인벤토리이므로 카드 배치를 반드시 세이브에 포함한다**(§10-8 확정): `inventory{cols,rows,mode,cards[{id,kind,jamo,s,g,col,row}]}`를 게임 세이브 안에 두고 `schemaVersion`을 단어 목록 파일과 분리한다. 불러올 때 범위 밖/중복 칸 카드는 `Resize`의 재배치 규칙으로 보정한다.
- 불러온 파일 검증: `schemaVersion==1`, `items`/`categories` 배열, 단어는 한글 음절만, 중복 제거. 신뢰할 수 없는 `syllables`는 무시하고 **`word`에서 `Decompose`로 재계산**한다(저장 파일의 `syllables`는 호환용 중복 정보).
- 저장 시 `items`는 분류 순서대로, 같은 분류 안에서는 `order` 순서대로 출력. 덮어쓰기 전 "아이템 목록 파일이 아닌 것 같다"는 확인(에디터 툴 한정, 런타임 불필요).

### 5.7 조립 모드 / 가방 모드

인벤토리는 두 가지 모드를 가진다(`InventoryMode { Assemble, Bag }`, 세이브에 저장).

| | 조립(Assemble) | 가방(Bag) |
|---|---|---|
| 용도 | 카드 분해·결합·회전·이동으로 단어를 만든다 | 일반 인벤토리처럼 소지품을 확인/사용한다 |
| 카드 조작 | HTML과 동일(§4.4, §7) | 비활성(읽기 전용, 선택/사용만) |
| 완성된 단어 | 초록 오버레이로 알림 | 완성 단어가 차지한 칸들(`MatchScanner` 결과 `cells`)을 **그 단어의 스프라이트 하나로 대체** 표시 |

- **러프 구현 (현재, `InventoryModel.Mode`/`ModeToggleView`)**: 스프라이트 대체 이전 단계로 다음과 같이 보여준다.
  - 단어(`MatchScanner.Shown`)에 속한 카드는 글자 그대로. **2글자 이상이면 글자 사이에 카드와 같은 색의 작은 동그라미**(`CardView` 연결 표시)를 덧대 이어진 것처럼 보인다. 한 글자 단어는 그대로.
  - 단어에 속하지 않은 카드(미완성 음절·낱개 자모·목록에 없는 글자)는 **어둡게 물음표 하나**.
  - **접두어를 위에 얹은 단어**(§5.4)는 한 덩어리로 보이며, 연결 동그라미는 같은 줄 안에서는 오른쪽 경계, 위에 얹은 접두어의 첫 글자와 본체의 첫 글자 사이에는 **아래쪽 경계**에 놓인다(`CardView.SetBag`의 `linkDown`).
  - 조작: **어디를 잡든 그 단어 덩어리를 통째로 이동**(`ShiftGroup`: 모양 그대로 평행 이동, 잡은 카드가 놓인 칸 기준). 목적지가 모두 격자 안의 빈 칸이어야 하며(교환 없음) 아니면 제자리로 복귀. 놓을 수 있는 자리는 칸마다 초록으로 표시한다. 분해·결합·회전·모서리 회전·R 키는 막힌다. 쓰레기통은 **가방 모드에서 보이지 않고 쓸 수 없다**(조립 모드에서만). 같은 모드에서 단어 입력창(개발용)과 초록 완성 글로우도 숨긴다(`WordCompleted` 이벤트는 그대로 발생). 가방 모드의 카드는 단어 카드도 물음표 카드와 같은 어두운 색이다.
  - 모드는 세이브(`mode`: `assemble`/`bag`)에 저장된다. 완성 단어를 스프라이트로 대체하는 것은 `ItemSpriteDB`가 준비된 뒤 이 표현을 교체한다.
- 모드 전환은 모델 상태를 바꾸지 않고 **View만 교체**한다: 카드 데이터(`cards`)는 유지, 가방 모드 렌더러가 `Scan` 결과를 읽어 칸에 스프라이트를 그린다. 조립 모드로 돌아오면 카드 그대로 복원.
- 스프라이트 매핑: `단어 → Sprite` 테이블(`ItemSpriteDB`, ScriptableObject). 매핑이 없는 단어는 카드 그대로(또는 기본 아이콘) 표시. 단어 → 아이템 정의 DB(§10-5 구 항목)와 같은 키를 쓰는 것을 권장.
- 스프라이트가 차지하는 영역: 단어 길이만큼 가로로 이어진 칸(예: `도끼` = 2칸). 겹치는 매치는 `Scan`의 "겹치지 않는 가장 긴 매치" 규칙을 그대로 따른다.
- 가방 모드에서 카드 이동을 허용할지(스프라이트 단위 재배치)는 후속 결정.

### 5.6a 가방 흔들기 (게임 전용 기능, 조립 모드) — Unity Physics2D (2026-10-14)
- 조립 모드의 **'가방 흔들기' 버튼**을 누르면 **주사위를 쥐고 위아래로 흔들듯이** 인벤토리의 카드들이 **Unity 물리엔진(Physics2D)** 으로 튕기고 구르며 서로 부딪히고, 부딪혀서 합칠 수 있으면 조립된다. 가방 모드에서는 버튼이 숨겨진다.
- **구조**: `ShakeWorld`(`Hangeul.UI`, Unity 물리) + `ShakeSession`(`Hangeul.App`, 규칙) + `InventoryView.UpdateShake`(화면).
  - `ShakeWorld`: 카드마다 `Rigidbody2D` + `BoxCollider2D`(사각형 그대로 구르고 쌓임, 한 칸 = 1 유니티 단위), 격자 영역은 정적 벽 4개(두께 3). **독립된 물리 씬**(`LocalPhysicsMode.Physics2D`)에 만들어 게임 월드의 물체와 절대 부딪히지 않고, **1/120초 걸음으로 직접 진행**한다(`PhysicsScene2D.Simulate`, 매 프레임 `InventoryView`가 `Tick(Time.unscaledDeltaTime)`). 독립 물리 씬은 자동으로 시뮬레이션되지 않으므로, 인벤토리를 열어 게임 시간이 멈춰 있어도(`Time.timeScale = 0`, §13.9) 흔들기 물리는 돈다. 끝나면 씬째 지운다.
  - **흔드는 방법 (2026-10-14 수정)**: 카드를 계속 떠는 힘(사인파 진동)은 **주지 않는다** — 작은 진폭의 진동처럼 보여서 뺐다. 대신 `ShakeSeconds` 동안 `TossInterval`(기본 0.7초)마다 모든 카드를 **위로 던져 올리고**(`TossSpeed` 칸/초에 카드마다 ±25%, 좌우로 `Sideways`만큼 흩어짐, 회전 `Spin`), 던지는 사이에는 `Gravity`로 떨어지며 서로 부딪힌다(주사위를 쥐고 위로 흔들듯이). `TossSpeed`를 0으로 하면 던지지 않고 중력으로 떨어지기만 한다. 벽은 움직이지 않아 카드가 화면의 격자 밖으로 나가지 않는다. 프로젝트의 Physics2D 중력 설정과 무관하게 직접 힘을 준다(`gravityScale = 0`).
  - `ShakeSession`: 물리가 알려 주는 것을 모델에 반영하는 순수 C#. 부딪힘 → 조립(`TryMerge`), 도는 각도 → 자모 모양(`SetPose`), 끝 → 칸 맞추기(`Finish`). 물리 없이도 EditMode에서 테스트한다.
- **움직이는 카드**: **자모 카드와 아직 단어가 아닌 음절 카드 전부**. **이미 완성된 단어를 이루는 카드**(`InventoryModel.CompletedWordCardIds`, 접두어를 위에 얹은 단어 포함)는 **가만히 있고 합쳐지지도 않는다**(리지드바디 없는 정적 충돌체 = 장애물).
- **조립**: 두 카드가 **새로 부딪히는 순간**(`OnCollisionEnter2D`, `ShakeContact`) 합칠 수 있으면(`CardOps.TryCombine`, 금지 음절 `됬` 제외) `MergeChance`(기본 **1.0**, 인스펙터에서 조절) 확률로 조립한다. 한쪽 카드가 사라지고 남은 카드는 두 카드의 평균 속도로 이어서 움직이며 `Pop`. 연쇄 조립이 일어날 수 있다.
- **회전**: 카드가 물리로 도는 각도가 누적되고(`ShakeBody.Angle`), **자모 카드는 90°를 넘을 때마다 글자가 바뀐다**(`RotationSession`: `ㅏ→ㅜ→ㅓ→ㅗ`, `ㄱ↔ㄴ` 등. 모양이 없는 자모는 보기만 돈다). 글자가 바뀐 만큼(Rn·90°)을 뺀 나머지 각도만 화면에서 돌려 그리므로 물리 각도 그대로 보이고 글자가 바뀌어도 끊기지 않는다(`DisplayAngleDeg`). 음절 카드도 물리 각도 그대로 구르지만 글자는 바뀌지 않는다. 조립 직후 새 카드는 그 각도에서부터 다시 센다(`AngleBase`).
- **끝날 때**: 흔드는 시간(`ShakeSeconds`)이 지난 뒤 모든 카드가 멈추거나(`MaxSettleSeconds` 경과, 또는 물리가 안 돌 때를 대비한 실시간 한도) 끝난다. 도는 자모를 가장 가까운 90° 모양으로 확정하고(`FinishRotation`), **각 카드를 자기 위치에서 가장 가까운 빈 칸에 맞춘다**(`InventoryModel.ApplyLayout`로 한꺼번에 이동, 칸이 바뀐 카드마다 `Moved`). 글자는 0.2초 동안 똑바로 선다. **중력이 있으면(기본 12) 카드가 아래쪽에 쌓인 채로 칸에 맞춰진다** — 위치를 유지하고 싶으면 `shakeGravity`를 0으로.
- **모델과 화면**: 흔드는 동안 카드의 칸(`Col/Row`)은 그대로이고 화면 위치만 물리가 정한다. 모델이 바뀌는 것은 조립·회전 확정·마지막 칸 배치뿐이다. 흔드는 동안에는 **카드 조작, 단어 완성 확인, 자동 저장을 미루고**(끝난 뒤 한 번에), 흔들다 만들어진 **단어는 끝난 뒤 평소처럼 알림**(글로우, `WordCompleted`)이 간다. 모드가 가방으로 바뀌면 그 자리에서 멈추고 칸에 맞춘다.
- 인스펙터(`InventoryView` ▸ 가방 흔들기): `shakeMergeChance`, `shakeSeconds`, `shakeSettleSeconds`, `shakeTossSpeed`, `shakeTossInterval`, `shakeSideways`, `shakeTossSpin`, `shakeGravity`, `shakeBounce`, `shakeFriction`.
- 이전 방식들은 모두 없앴다: 카드가 제자리에서 흔들린 뒤 결과를 한 번에 적용하던 순간 이동식 `InventoryModel.ShakeBag`(와 그 테스트), 직접 만든 간단한 원 충돌 시뮬레이션. 지금은 물리로 흔들리는 것만 보이고, 카드가 스스로 떠는 연출은 없다. 효과음·부딪힐 때 흔들림 같은 연출은 후속.

### 5.6 삭제(버리기) — 게임에서는 쓰레기통 방식 (확정)
> 아래 'HTML 삭제 모드'(토글·다중 선택)는 **게임에서 쓰지 않는다.** 게임은 **인벤토리 창 왼쪽의 쓰레기통(`TrashZone`)에 카드를 끌어다 놓으면 삭제**한다.
> - 아이콘은 `TrashZone/Icon` Image의 Sprite에 나중에 넣는다(자리만 먼저 배치). 포인터가 쓰레기통 위에 있으면 강조되고, 그 위에서는 카드가 격자 밖으로도 포인터를 따라간다.
> - 판정은 카드 위치가 아니라 **포인터 위치** 기준. 좌/우클릭 드래그, 분리 직후 드래그 모두 가능. 회전 드래그는 해당 없음.
> - 삭제는 `InventoryModel.Discard` 호출 → `Discarded` 이벤트. 확인창·되돌리기는 없다(필요하면 후속 결정).
> - 삭제 직후 `MatchScanner.Scan(notify:true)`는 디바운스로 자동 수행된다.

#### (참고) HTML의 삭제 모드
- 토글을 켜면 카드 클릭이 선택/해제가 된다(회전·분리·드래그 비활성). 선택된 카드는 반투명 + 진홍 외곽선. `선택 삭제 (N)` 버튼 또는 `Delete` 키로 일괄 삭제하고 **모드는 자동으로 꺼진다**. `모두 지우기`는 모드와 무관하게 전체 삭제.
- 삭제 연출: 살짝 커졌다 작아지며 회전·페이드(0.55초). 여러 장은 격자 순서(위→아래, 왼→오)로 **장당 40ms 시차, 최대 600ms**. 모델 상태는 즉시 비우고 화면에서만 잠시 남긴다(`Changed` 이벤트로 View가 처리).
- 삭제 직후 `ArrangementService` 갱신 + `MatchScanner.Scan`.

---

## 6. Presentation 설계

### 6.1 UI 기술 선택

| 항목 | uGUI (Canvas) | UI Toolkit |
|---|---|---|
| 카드 개별 회전/그림자/그라데이션 | 쉬움 (RectTransform, Shader/Material) | 제약 많음 |
| 드래그 앤 드롭, 커스텀 효과 | 자료 풍부 | 가능하나 번거로움 |
| 권장 | **✅ uGUI + TextMeshPro** | 보조(설정 화면 등) |

→ **uGUI + TextMeshPro 권장.** 카드 중심 연출이 핵심이므로.

### 6.2 화면 구성 (Canvas)

```
InventoryScreen
├─ WordPanel (왼쪽)        ← ScrollRect: 폴더 그리드 3열 / 열린 폴더 / '추가한 단어' 항상 노출
│   └─ DropDock (고정 높이, 보더 상시 표시, 칩 드래그 중에만 내용 표시)
├─ BagFrame + GridArea     ← 가방 SVG 대체 스프라이트 + 카드가 놓이는 영역
│   └─ CardLayer (카드 부모, 드래그 시 최상단 레이어로 승격)
├─ MakeablePanel (오른쪽)  ← 만들 수 있는 글자 (풀링 리스트)
├─ TopBar                  ← 모두 지우기 · 격자 COLS×ROWS(1–20) · 카드 버리기 토글 · 선택 삭제(N) · 목록 저장/불러오기(에디터/디버그)
├─ WordInputView           ← 하단 입력창+추가 버튼 (개발/치트용. 본편에서는 전리품 → AddWord로 대체)
├─ SettingsPopup           ← 그리드 크기 COLS/ROWS (1–20) (TopBar에 두지 않을 경우)
└─ OverlayLayer            ← CompletionOverlay
```

- **좌우 패널(WordPanel/DropDock, MakeablePanel)은 게임에서 제외**(§10-13 확정). 위 트리의 해당 항목은 HTML 프로토타입 기준 구조이며, 게임 빌드에서는 만들지 않고 필요하면 에디터/디버그 도구로만 둔다. 따라서 `ArrangementService`, 패널 숨김 규칙, 칩 드래그(`ChipDragController`)도 게임 필수 범위가 아니다. 단, `WordBook`(단어 DB)과 `MatchScanner`는 완성 판정을 위해 계속 필요하다.
- **카드 크기 슬라이더도 제외**(§10-12). 칸 크기는 §5.1의 화면 유도식만 사용한다.
- 입력창 규칙: 한글 음절이 하나도 없거나(IME 조합 중 Enter 제외) 일부만 들어가면 입력창 흔들기. 입력 후 비우고 포커스 유지.

### 6.3 CardView

- 구조는 HTML의 `.card`(그림자) + `.face`(회전) 2단 구조를 그대로:
  - `CardView`(루트, 위치/그림자) → `Face`(회전 대상) → `Glyph`(TMP).
  - 음절 카드는 초·중·종 **고스트 슬롯**을 `Mask`/스텐실(또는 SDF 셰이더)로 클리핑해 재현(HTML의 SVG clipPath 대체).
- **그라데이션은 회전에 영향받지 않아야 함**(중세 테마 요구): 그라데이션을 `Face`의 자식이 아닌 **회전하지 않는 형제 오브젝트**에 두거나, 셰이더에서 `rotation` uniform을 상쇄(`180° - rot`)한다.
- 색상 전이(ㅗ+ㅇ→ㅎ 자음색으로 천천히 변화 + 금색 테두리): `Face` 머티리얼 컬러를 **트위닝**(DOTween 권장 또는 자체 코루틴). 중간에 **투명해지지 않도록 항상 불투명 배경 + 오버레이 페이드**로 구현, 금색 링은 오버레이 위에 별도 Image.

### 6.4 애니메이션

- 라이브러리: **DOTween**(또는 PrimeTween). 모든 연출은 `View` 쪽에서 `Changed` 이벤트를 받아 재생.
- 목록: 이동(스냅, 0.12초), 회전 `-k*90→0`, 색 전이, 완성 오버레이, 폐기(§5.6), `pop`(생성/결합 시 1.18배, 0.28초), `shake`(거절, 0.3초), 칸 하이라이트(`#cell`), 칩 `flash`.
- **연출 제외**: 분노(rage) 연출은 **구현하지 않는다**(§10-6 확정). 금지 조합은 거절 + 카드 원위치만.
- 드래그 중인 카드는 이동 트랜지션 없음, 그림자 강화, 최상단 z-order(`front`).
- `prefers-reduced-motion` 상당 설정(흔들림·연출 축소)을 옵션으로 고려.

### 6.5 레이아웃 (`InventoryLayout`)

- 보드 영역 안에 가방(격자)을 **중앙 정렬**. `BAG={mx:40, top:104, bot:46}`는 격자 둘레 여백(좌우/위=손잡이+덮개/아래).
- `CARD`(칸 크기) = 보드 크기와 `COLS/ROWS`에서 유도(§5.1). 창 크기가 바뀌면 `relayout`: **애니메이션 없이** 모든 카드를 새 칸에 스냅.
- 가방 프레임은 격자 크기에 따라 늘어나야 하므로 9-slice(§8). 손잡이/덮개/잠금쇠는 상단 고정 장식, 양옆 가죽끈·버클은 격자 높이의 30% 지점.
- 격자 배경: 칸 선(1px)이 보이는 양피지 질감, 안쪽 그림자(inset) 포함.

### 6.6 카드 색 규칙

| 종류 | 글자색 | 바탕 |
|---|---|---|
| 모음 자모 | 적갈색 `#7d2217` | 따뜻한 베이지 그라데이션 |
| 자음 자모 | 청색 `#1c3a66` | 회청색 그라데이션 |
| 음절 카드 | 먹색 `#2a1a0e` | 양피지 + 금테 |

- 자모 카드 네 모서리에 작은 점(회전 손잡이 표시). 음절 카드는 상단에 그립(폭 37.5%, 높이 12.5%)이 있다.
- `asC`(ㅗ가 ㅇ 위에서 0.6초 경과): ㅗ 카드가 **자음색으로 0.3초 동안 전이**(§6.3의 불투명 오버레이 방식).

---

## 7. 입력 / 인터랙션

| 동작 | HTML | Unity |
|---|---|---|
| 카드 드래그 이동 | pointer events | `IBeginDragHandler/IDragHandler/IEndDragHandler` |
| 병합 | 카드 위에 드롭 | 드롭 시 `GraphicRaycaster`/거리 기반 대상 탐색 → `TryMerge` |
| 분해 | 음절 카드의 슬롯 드래그 | 슬롯별 드래그 핸들 → `Detach` |
| 빠른 회전 | R키 / 더블클릭 / 우클릭 | Input System 액션 `Rotate`(R, 우클릭, 패드 버튼), 더블탭 |
| 연속 회전 | 모서리 드래그(`applyRot`) | 모서리 핸들 드래그 → 각도 → 90° 경계 통과 시 `Rotate` 즉시 반영(손을 떼지 않아도) |
| 칩(단어) 드래그 | pointer events | `ChipDragController`, 드롭 대상은 폴더/DropDock |

- **버튼별 동작 (확정)**: 좌클릭 드래그는 분리·회전이 일어날 수 있으므로, **카드 이동의 기본 수단은 우클릭 드래그**(해당 카드 하나 이동, 분리 안 함)다. 좌클릭 드래그 = (음절 본체) 분리 / (자모 모서리) 회전. 움직임 없이 우클릭만 하면 회전. *(HTML 코드는 좌클릭으로도 그립/자모 본체를 끌면 이동되지만, 혼동을 막기 위해 Unity에서는 이동을 우클릭 드래그로 통일할지 최종 확인 필요 — 현재 방침: 우클릭 이동 기본, 좌클릭 이동은 그립/자모 본체에 한해 유지.)*
- 패드/터치 대응 시 "우클릭 이동"을 대체할 입력(길게 누르기 등)이 필요하다(§10-3).
- 이동 임계값: 분리 판정 14px, 이동 시작 4px. 칩 드래그 시작 6px.
- 칩 드래그 중 `Esc` 또는 포인터 취소 → 드롭 취소.
- 카드 선택(`lastTouched`)이 R 키 회전 대상. 입력창에 포커스가 있으면 R/Delete 무시.
- Input System의 Action Map: `Inventory`(Move cursor, Select, Rotate, Cancel).
- **패드 지원**은 후순위이나, 커서 모드(가상 커서)로 확장 가능하도록 `IPointerSource` 추상화를 둔다.
- 회전 중 규칙: 마우스를 떼든 말든 **경계를 넘는 순간 자모가 바뀐다**. 놓을 때 `finishRotate`에서 `d.orig`/`d.rn` 기준으로 정규화(HTML 로직 그대로, 위 §4.1 골든 데이터로 검증).

---

## 8. 폰트 / 테마

- 폰트: **한글 11,172자 전체를 지원하는 폰트만** 사용 (검증 완료: Gowun Batang, Nanum Myeongjo, Noto Serif KR, Nanum Brush Script). 중세 테마는 Nanum Myeongjo(+영문 Cinzel).
- TMP Font Asset은 **Dynamic SDF**(Atlas Population Mode: Dynamic)로 생성 → 전체 글리프 사전 베이킹 없이 필요한 글자만 즉석 생성. (정적 베이킹은 용량이 커서 비권장.)
- 숫자가 많은 요소는 Cinzel 대신 Nanum Myeongjo(Cinzel의 `1`이 `I`로 보이는 문제).
- **구현 현황 (스프라이트 UI 준비)**: `InventoryTheme`(ScriptableObject, `Create ▸ Hangeul ▸ Inventory Theme`)에 격자 칸(`cell`/`cellAlt`)·카드 배경(`cardSyllable/Consonant/Vowel`) 스프라이트 슬롯이 있고 `InventoryView.theme`에 연결한다. 비워 두면 코드로 그린 기본 모양(`GridLinesGraphic` 선·체커, 카드 색 칸)을 쓴다. 가방 프레임·쓰레기통 아이콘 등 씬에 놓인 Image는 Sprite를 직접 넣는다. 픽셀 아트는 Filter Mode = Point·Compression = None, 늘어나는 요소는 9-slice(Border) 권장.
- 테마는 `ThemeProfile`(ScriptableObject)로 분리: 색 팔레트, 폰트, 카드 스프라이트, 가방 프레임, 사운드. 기본 테마/중세 테마를 프로필 교체로 전환.
- 가방 프레임: SVG 그대로 쓰기보다 **9-slice 스프라이트** 또는 Vector Graphics 패키지로 임포트. 그리드 크기가 가변(1–20)이므로 프레임이 늘어나도록 9-slice 필수. HTML의 `BAG={mx:40, top:104, bot:46}` 값은 9-slice 보더 값의 시작점으로 사용.

---

## 9. 폴더 구조 (제안)

```
Assets/
├─ _Project/
│  ├─ Scripts/
│  │  ├─ Domain/          (Hangeul.Domain.asmdef)
│  │  ├─ App/             (Hangeul.App.asmdef)
│  │  ├─ UI/              (Hangeul.UI.asmdef)
│  │  └─ Game/            (JRPG 본편과의 접점: 이벤트 브리지)
│  ├─ Data/
│  │  ├─ JamoTables.json  (CC/VV/SPLIT/ROT4)
│  │  ├─ ItemWords.json   (HTML의 data/item_words.json과 동일 스키마)
│  │  └─ Themes/          (ThemeProfile SO)
│  ├─ Prefabs/UI/         (CardView, WordChip, FolderCard, Overlay…)
│  ├─ Fonts/              (TMP Font Asset)
│  └─ Art/                (가방 프레임, 카드 텍스처)
└─ Tests/
   ├─ EditMode/           (Hangul, 회전 골든, Grid, Arrangement, Save)
   └─ PlayMode/           (드래그/회전 시나리오, 풀링 성능)
```

---

## 10. 미해결 / 결정 필요 사항

1. ~~그리드 축소 시 범위 밖 카드~~ → **확정**: HTML 동작 그대로 (용량 부족이면 확인 후 초과분 폐기, 아니면 가까운 빈 칸으로 재배치). §5.1 참조.
2. **게임 본편 연동**: 인벤토리에 단어가 들어오는 경로(전리품 → `AddWord`)와 완성 시 보상(`OnWordCompleted`)의 정확한 계약.
3. **터치/패드 대응 범위**: PC 마우스 우선인지, 콘솔/모바일까지 고려하는지.
4. **DOTween 도입 여부**(유료 Pro 불필요, 무료판으로 충분) vs 자체 트윈.
5. **아이템 단어 데이터**: 단어 → 실제 아이템 정의(스탯 등)와의 매핑 테이블 위치(ScriptableObject DB 추천).
6. ~~이스터에그~~ → **확정**: `됬` 조합은 금지(생성 불가)만 하고 특수 연출은 없음. 금지어는 `ForbiddenWords` 데이터로 두어 필요 시 추가 가능.
7. ~~로컬라이징~~ → **확정**: 고려하지 않음. 한국어 고정.
8. ~~카드 배치 저장~~ → **확정**: 게임의 인벤토리는 일반적인 인벤토리이므로 **카드(단어)가 계속 보존**된다. 카드 배치를 세이브에 포함한다(§5.5).
9. ~~단어 완성 시 카드 처리~~ → **확정**: 완성돼도 카드는 **유지**(소비 없음). **조립 모드 → 가방 모드**로 전환하면 완성된 단어 자리에 단어별 **스프라이트**가 표시된다(§5.7).
10. **단어 목록 모델 단순화**: HTML의 `BASE/userWords/removedBase` 3중 구조를 단일 DB로 합칠지 — **보류**(결정 전까지 §5.2 제안 유지, 구현 착수 안 함).
11. ~~우클릭 드래그 의미~~ → **확정**: 좌클릭 드래그는 분리·회전이 일어날 수 있으므로 **카드 이동은 우클릭 드래그**로 한다(HTML 힌트의 "모든 카드 이동"이 아니라 *해당 카드 이동*). §7 참조.
12. ~~카드 크기 슬라이더~~ → **확정**: 게임에서는 **불필요**(제외).
13. ~~좌우 패널~~ → **확정**: 게임에서는 **불필요**(제외). 좌측 WordPanel/DropDock, 우측 MakeablePanel은 에디터/디버그 도구로만 남기거나 구현하지 않는다(§6.2).

---

> **실제 폴더 위치**: 프로젝트에서는 `_Project` 없이 `Assets/Scripts/Game`(플레이어·FSM·상호작용)을 쓰고 있다. 나머지 레이어(Domain/App/UI)를 만들 때 같은 `Assets/Scripts/` 아래에 두는 것을 기준으로 한다.

---

## 11. 구현 로드맵 (제안)

| 단계 | 내용 | 완료 기준 |
|---|---|---|
| P0 | 플레이어 이동(FSM)·Cinemachine 추적·충돌·상호작용 (§13) — **구현됨**, Y 정렬은 보류 | 이동·충돌·상호작용 동작 |
| M0 | 프로젝트/asmdef 셋업, JamoTables 이식 | EditMode: 분해·결합·회전표 테스트 통과 (HTML 골든 일치) |
| M1 | Domain/App 이식: Card, Grid, InventoryModel, CardOps | 콘솔 시나리오로 병합/분해/회전 재현 |
| M2 | 최소 UI: CardView, 드래그 이동·병합·분해 | HTML `index.html` 수준의 자유 조작 |
| M3 | 회전 UX: R키/더블클릭/모서리 드래그, 즉시 자모 교체 (`RotationSession`, §4.5) | §7 회전 규칙 일치, 애니메이션 보정 |
| M4 | 인벤토리 그리드 + 설정(1–20) + 저장/불러오기 | JSON 상호 호환 확인 |
| M5 | WordBook 패널(폴더/칩 드래그/DropDock) | 폴더 UX가 HTML과 동일 |
| M6 | MakeablePanel + ArrangementService + 성능 상한 | 카드 다량 시 프레임 드랍 없음 |
| M7 | 완성 연출(MatchScanner/Overlay/폐기), 금지 조합(`됬`) 차단 | `OnWordCompleted` 이벤트 발행 |
| M8 | 중세 테마: 폰트, 가방 프레임, 그라데이션 고정, 색 전이 | `inventory_medieval.html`과 시각 비교 |
| M9 | JRPG 본편 연동, 폴리싱 | 통합 빌드 |

---

## 12. 리스크

- **회전 중 그라데이션/색 전이 연출**은 셰이더 작업이 필요할 수 있음 → M3~M8에서 가장 먼저 프로토타입.
- **경우의 수 폭증**(렉): 처음부터 캡+디바운스+풀링 적용.
- **TMP Dynamic 폰트의 첫 글리프 생성 히칭**: 로딩 시 자주 쓰는 자모/단어 프리워밍.
- **HTML과의 동작 편차**: 포팅 중 불일치가 생기면 HTML이 정답이므로, `index.html`/`inventory_test.html`을 레퍼런스 빌드로 계속 보관.

---

## 13. 플레이어 캐릭터 (게임 본편)

위치: `Assets/Scripts/Game` (`Hangeul.Game` asmdef). 2D 탑다운, 4방향, Rigidbody2D 이동.

### 13.1 구조

```
PlayerController (루트: 입력·설정·FSM 소유)
 ├─ PlayerInputReader      ← Input System: Player/Move, Player/Interact (InputSystem_Actions)
 ├─ PlayerSpriteAnimator   ← 방향별 스프라이트 프레임 재생 (Animator Controller 없음)
 ├─ InteractionSensor      ← 플레이어 중심 원형 범위(방향 무관)에서 IInteractable 탐색 (없으면 자동 추가)
 └─ StateMachine           ← Idle / Move / Interact / Locked
```

| 파일 | 역할 |
|---|---|
| `FSM/IState`, `StateMachine` | 범용 FSM. Enter/Exit 중 전이 요청은 대기열에 넣어 안전 처리 |
| `Character/PlayerStates.cs` | `PlayerState` 기반 + 상태 4종 |
| `Character/Facing.cs` | `Facing {Down, Up, Left, Right}`, 이동 벡터 → 방향 변환 |
| `Character/DirectionalClip.cs` | 방향별 `Sprite[]` + fps/loop. 인스펙터에서 시트를 끌어다 채움. 빈 방향은 Down → 다른 방향으로 대체 |
| `Interaction/IInteractable` | `Prompt`, `CanInteract(player)`, `Interact(player)` |
| `Interaction/InteractableObject` | 인스펙터만으로 쓰는 기본 구현(UnityEvent) |
| `Editor/CreatePlayerMenu` | `GameObject ▸ Hangeul ▸ Player` 생성 메뉴 |

### 13.2 상태 전이

```
Idle ──입력──▶ Move ──입력 없음──▶ Idle
 │               │
 └──E키+대상──▶ Interact ──0.25초──▶ Idle
                  │ (대상이 SetControlLocked(true) 호출 시)
                  ▼
               Locked ──SetControlLocked(false)──▶ Idle
```

- `Locked`는 인벤토리 열림·대화·컷신 중 조작 차단. **인벤토리 UI는 열 때 `SetControlLocked(true)`, 닫을 때 `false`** 를 호출한다(인벤토리와의 접점).
- `Interact` 상태: 진입 시 `sensor.Current.Interact(player)` 호출, 이동 정지(감속), 짧게 대기 후 Idle.
- 상태 추가: `PlayerState` 상속 → `PlayerController.Awake`에서 인스턴스 생성 → 다른 상태의 `Tick`에서 `Fsm.ChangeState`.

### 13.3 이동·방향
- 속도 `moveSpeed`(기본 1.5 unit/s), 가속·감속은 `MoveTowards`로 `FixedUpdate`에서 `Rigidbody2D.linearVelocity` 갱신. 입력 데드존 0.15.
- 방향 규칙: **위쪽 절반(대각선 포함)=Up**, 정확히 좌우(±5°)=Left/Right, 아래쪽은 180°~240° Left, 240°~300° Down, 300°~360° Right.
- 스프라이트: 프레임 48×64px, PPU 100, Point 필터. 에셋: `Assets/Sprites/The Female Adventurer - Free` (임시, 시트를 인스펙터에 지정).

### 13.4 상호작용
- `InteractionSensor`: 루트 위치 + `originOffset`을 중심으로 반지름 `radius`(0.35)의 **원 하나**를 검사한다(바라보는 방향과 무관). 에디터에서는 `Always Show Gizmo`로 범위를 항상 표시(대상이 있으면 초록). 레이어 마스크로 필터, 트리거 포함. 후보 중 `CanInteract`가 true인 것 중 가장 가까운 것을 `Current`로 한다.
- `CurrentChanged` 이벤트 → "E 열기" 안내 UI를 연결할 지점(아직 UI 없음).
- 입력: `Player/Interact`(E / 패드 North). 에셋의 Interact에는 `Hold`가 설정돼 있지만 `WasPressedThisFrame`로 누르는 순간을 쓴다.
- 대상 만들기: 콜라이더가 있는 오브젝트에 `InteractableObject`를 붙이거나 `IInteractable`을 직접 구현(NPC, 상자 등).

### 13.5 카메라·렌더링
- 카메라: Cinemachine 3 `CinemachineCamera`가 Player 추적(Lens Orthographic Size는 스프라이트 확대 표시 때문에 약 7).
- 충돌: 벽 Tilemap(`Tilemap Collider 2D` + Composite), 플레이어는 발밑 BoxCollider2D.
- **Y 정렬(보류)**: `Renderer2D`의 Transparency Sort Mode를 Custom Axis(0,1,0)로 설정했다. 하지만 Sorting Layer가 `Grounds < Obstacle < Enemy < Player`로 나뉘어 있어 Player가 항상 Obstacle 위에 그려진다. 앞뒤 정렬을 쓰려면 Player·Enemy·벽을 같은 레이어로 모으고, 벽 Tilemap Renderer는 `Individual` 모드, 플레이어 정렬 기준점을 발밑으로 맞춰야 한다. 현재는 미적용.

### 13.6 인벤토리 열기 (확정 방침)
- **키**: `Tab`(패드 Select), 입력 액션 `Player/Inventory`. 열려 있으면 다시 Tab으로 닫는다.
- **표시 위치**: 인벤토리 패널은 **화면 오른쪽**에 표시한다. 열리면 카메라가 오른쪽으로 부드럽게 이동해 플레이어가 **왼쪽 빈 공간**에 보인다(`InventoryCameraShift`: Cinemachine Position Composer의 `TargetOffset`을 화면 너비 비율(기본 25%)만큼 SmoothDamp, 0.35초).
- **조작 잠금**: 열면 `PlayerController.SetControlLocked(true)`, 닫으면 `false`. 상호작용·대화 등 다른 상태 중(`IsFree`가 false)에는 열리지 않는다.
- `InventoryToggle`이 위를 묶고, 패널 on/off는 `onToggled(bool)` UnityEvent에 연결한다.
- **패널 뼈대**: `GameObject ▸ Hangeul ▸ Inventory UI` 메뉴가 `InventoryCanvas`(1920×1080 스케일) ▸ `Panel`(우측 40%, `InventoryPanelView`) ▸ `Header` / `BagFrame` ▸ `GridArea`(정사각 비율 유지) ▸ `CardLayer`를 만든다. 패널은 열릴 때 오른쪽에서 슬라이드하며 페이드 인(0.35초, 카메라 이동과 동일 길이).
- 이 방침으로 §6.2 화면 구성은 "전체 화면 인벤토리"가 아니라 **우측 패널형**이 된다(좌우 패널 제외 결정과 함께 UI 설계 시 반영).

### 13.7 월드 아이템 줍기 (확정 방침)
- 맵의 아이템(`WorldItem`)은 **단어 하나**를 가진다. 가까이 가면 안내 UI(`InteractionPromptView`)가 아이템 위에 **단어(제목)와 `[키] 줍기`**를 띄운다. 키 이름은 입력 설정의 Interact 바인딩에서 읽는다(`PlayerInputReader.InteractKeyLabel`).
- 상호작용하면 그 단어의 **글자 카드가 인벤토리의 빈 칸에 들어온다**(`InventoryModel.AddWord`, 중앙 줄부터 연속 배치).
  - **전부 놓을 빈 칸이 있을 때만 줍는다**(`CanAddWord`). 모자라면 줍지 않고 아이템은 그대로 남으며 안내 문구("가방이 가득 찼습니다")만 띄운다. 일부만 들어가는 일은 없다.
  - 한글 음절이 아닌 단어나 금지 음절(`됬`)이 섞인 아이템은 상호작용 대상에서 제외된다.
  - 주우면 오브젝트를 없앤다(옵션으로 유지 가능). 이미 주운 아이템의 영속(재진입 시 다시 나타나지 않게)은 월드 세이브와 함께 후속 과제.
- 안내 UI는 **이름 칸과 안내 칸 두 칸**이며 각 칸의 폭/높이/위치는 **실행할 때 `InteractionPromptView`가 글자 길이에 맞춰 직접 정한다**(안내 칸이 맨 아래, 그 위에 이름 칸). 씬에는 `LayoutGroup`/`ContentSizeFitter`를 두지 않는다 — 두면 플레이를 끝낼 때마다 에디터가 RectTransform 크기를 다시 계산해 씬이 "변경됨"으로 표시된다.
- 안내 UI는 조작 가능 상태(Idle/Move)일 때만 보인다(인벤토리를 열었거나 상호작용 중이면 숨김). 제목이 없는 일반 대상(`InteractableObject`)은 `[키] Prompt`만 보인다(`IInteractableTitle`로 제목 지원).
- 구현 위치: `Assets/Scripts/Game/Interaction`. Game 어셈블리는 App/Domain/UI를 참조한다(통합 계층).
- 메뉴: `GameObject ▸ Hangeul ▸ Interaction Prompt`(씬에 한 번), `World Item`(아이템마다).

### 13.7a 아이템 의미와 사용 (기획 방침, 2026-10-14)
- **단어 하나 = 아이템 하나**(확정). 수량 개념은 기본적으로 없다.
- **아이템 사용 — 음식·무기·장비품 (방향 확정, 미구현)**: 완성한 단어 카드를 **인벤토리 창 밖의 플레이어 캐릭터에 드래그해서 놓으면** 사용/장착한다. 카드는 격자 밖 자유 드래그가 이미 되므로(놓을 곳이 아니면 제자리 복귀) 플레이어 위 "놓을 수 있는 곳" 판정만 추가하면 된다. 아이템 종류(음식/무기/장비) 정의는 아이템 DB와 함께 정한다.
- **키 아이템 — 열쇠 등 (메모만, 미구현)**: 해당 아이템과 상호작용할 수 있는 **포인트 구역(월드 위 영역)에 카드를 드래그해서 놓는** 방식을 고려 중. 카드를 월드 좌표로 판정해야 하고, 맞는 구역이 아니면 제자리로 돌아간다. 구체 설계는 만들기 전에 다시 정한다.
- **같은 글자 카드 겹치기 (아이디어 메모, 채택 미정)**: 같은 글자 카드를 겹치면 카드 모서리에 **개수 숫자**를 표시하고 카드 한 장만 남기는 규칙. 게임의 방향성에 맞을지 고민 중이라 **구현하지 않는다**. 채택하게 되면 영향 범위: 카드 모델에 개수 필드, 세이브 스키마(schemaVersion 상향), 분해/회전/조립 시 개수 처리, 단어 완성 판정은 그대로, 가방 흔들기와의 상호작용.
- **보류 목록(사용자 결정)**: 세이브 구조(슬롯·월드 세이브·주운 아이템 영속)는 테스트 단계라 지금 하지 않는다(§5.5 참고). Y 정렬은 에셋이 만들어진 뒤에 한다.

### 13.7b 아이템 DB 관리 씬 (개발용, 러프)
- 게임 씬에 항상 떠 있는 `DbSceneButton`(왼쪽 위 "DB 관리")이 `ItemDbScene`으로 이동시킨다. 에디터/Development Build에서만 보이고 릴리스에서는 스스로 사라진다.
- `ItemDbScene`의 `ItemDbManagerView`(UI는 코드로 생성)는 `Resources/item_words.json`을 읽어 **카테고리 목록(필터·추가·빈 카테고리 삭제)**, **단어 목록(카테고리 변경: 누르면 옮길 카테고리를 고르는 창, 삭제)**, **단어 추가**(한글 음절만, 금지 음절·중복 거부)를 제공한다. 단어는 **왼쪽에서 지금 선택한 카테고리에 들어간다**(별도 카테고리 선택 버튼 없음, "전체"를 보고 있으면 먼저 카테고리를 고르라고 안내). 인벤토리 그리드는 없다.
- 바꿀 때마다 에디터에서는 파일에 즉시 저장한다(빌드에서는 저장 안 함). 저장하면 파일에서 `savedAt`과 호환용 `syllables`가 빠진다(게임은 읽지 않음). HTML 쪽 원본 `hangeul-bunhae/data/item_words.json`은 자동으로 갱신되지 않는다.
- **접두어 규칙**: `접두어`(예: 레어, 희귀)와 `속성`(예: 불, 화염, 얼음) 두 카테고리의 단어가 다른 단어 앞에 붙는 접두어다(`ItemWordSet.PrefixCategories`; 선택 창에는 두 카테고리가 묶여 나온다). 접두어가 아닌 각 단어는 `acceptsPrefix`(접두어 가능 체크박스)와 `prefixes`(붙을 수 있는 접두어 단어들, 선택 창에서 여러 개 고르기)를 가진다(예: '화살'은 불·얼음 가능, '사과'는 불가). 접두어 단어 자신에게는 이 칸이 없다. 접두어 카테고리에서 빠지거나 삭제된 단어는 저장할 때 각 단어의 `prefixes`에서 자동으로 지워진다. 필드는 `ItemWordEntry`에 있고 기존 파일(필드 없음)은 불가·빈 목록으로 읽힌다. 
- **접두어가 붙으면 하나의 새 단어(= 새 아이템)가 된다(확정)**: '불' + '화살' = '불화살'. 파일을 읽을 때(`ItemWordSet.FromFile`) 접두어 규칙으로 생기는 합성 단어를 단어 집합에 자동으로 넣으므로, 카드 4장(불·화·살 → 불화살)이 한 줄에 놓이면 **하나의 완성 단어**로 인식된다(기존 "겹치면 긴 단어 우선"으로 '화살'로 쪼개 보이지 않는다). 파일에 같은 단어가 직접 있으면 그쪽이 우선이다(`IsCompound`로 구분). 체크가 꺼져 있거나 접두어 카테고리의 단어가 아닌 이름은 무시한다.
- **게임 의도**: 속성이 붙은 쪽이 더 강한 대신 **인벤토리 칸을 더 쓴다**는 선택(위험/보상)을 노린다. 합성 아이템의 실제 효과·수치는 아이템 DB 설계에서 정한다(미정).
- 메뉴: `GameObject ▸ Hangeul ▸ Create Item DB Scene`(씬 생성 + Build Settings 등록), `DB Scene Button`(게임 씬에 한 번).
- 코드: `Assets/Scripts/Game/DevTools`. 단어별 종류(음식/무기/장비)·효과 필드는 아직 없다(§13.7a, 아이템 DB 설계 때 추가).

### 13.8b 전투 기반 (2026-10-14)
- **체력은 정수**: `HitPoints`(`Hangeul.App`, 순수 규칙 — 깎기/회복/부활/최대치, EditMode 테스트)를 `Health`(`Hangeul.Game.Combat`)가 감싼다. 하트처럼 1, 2씩 깎는다(`DamageInfo.Amount`).
- **피해 흐름**: 공격하는 쪽이 `DamageInfo`(피해량, 편 `Team`, 밀려나는 속도, 경직 시간, 속성 `Element`, 때린 오브젝트)를 만들어 맞은 콜라이더에서 찾은 `IDamageable.TakeDamage`를 부른다(`GetComponentInParent<IDamageable>()`). `Health`는 같은 편(Neutral 제외)·무적·이미 죽음이면 거절하고, 맞으면 `Damaged`(이후 체력이 0이면 `Died`)와 `Changed`를 발생시킨다.
- **무적**: 한 번 맞으면 `invincibleSeconds` 동안 더 맞지 않는다(게임 시간 기준이라 일시정지 중에는 흐르지 않는다). 부활 직후에도 잠깐 무적(`GrantInvincibility`).
- **플레이어**: `Health`가 없으면 `PlayerController`가 설정값(`maxHealth` 6, 무적 1초)으로 자동으로 붙인다. 맞으면 `PlayerHurtState`(경직 + 공격이 정한 속도로 밀려난 뒤 `hurtDeceleration`으로 멈춤), 체력이 0이면 `PlayerDeadState`(`respawnDelay` 뒤 `Respawn()`). 지금은 쓰러진 자리에서 되살아나며, **체크포인트로 돌아가는 것은 체크포인트를 만들 때 `Respawn()`에 넣는다**(B 단계).
- **레이어**: `Hangeul ▸ Setup ▸ Combat Layers` 메뉴가 `Player`, `Enemy`, `PlayerAttack`, `EnemyAttack` 레이어를 추가하고 충돌 매트릭스를 정리한다(공격은 자기 편 몸·다른 공격과 부딪히지 않음, 벽/상대 몸과는 부딪힘). 씬의 플레이어를 Player 레이어로 옮긴다. `CombatLayers.TargetMask(team)`이 그 편의 공격이 맞출 몸의 마스크(Default 포함)를 준다. 벽 같은 월드는 Default.
- **테스트**: `GameObject ▸ Hangeul ▸ Damage Zone (테스트용)`이 닿으면 피해를 주는 구역(`DamageZone`)을 만든다. 함정으로도 쓴다. 피해량/밀려남/경직은 인스펙터에서 조절.
- 플레이어 피격 영역은 지금 발밑 작은 콜라이더다. 적의 공격 판정을 만들 때 몸통 크기의 피격 영역(Hurtbox)을 따로 두는 것을 검토한다.

### 13.8c 플레이어 공격 (2026-10-14)
- **입력**: `Player/Attack` 액션(기본: **마우스 왼쪽 버튼**). 템플릿에 있던 Enter 키 바인딩은 뺐다(게임패드/터치 바인딩은 템플릿 그대로 두었고 게임패드는 나중에 정한다). UI 위에서 누른 클릭은 공격으로 치지 않는다(`EventSystem.IsPointerOverGameObject`). Idle/Move 상태에서만 시작한다.
- **방향**: 4방향. 기본은 바라보던 방향(`PlayerController.Facing`), `PlayerMeleeAttack.attackTowardsMouse`를 켜면 마우스가 있는 쪽의 상하좌우로 공격한다. 시작할 때 정해지면 끝까지 유지된다.
- **시간표**: 준비(`windup` 0.08초) → 판정(`active` 0.12초) → 마무리(`recovery` 0.18초) (`AttackTimeline`, `Hangeul.App`, EditMode 테스트). **시간(초)으로만 정하므로 애니메이션 프레임 수와 무관**하다 — 지금처럼 방향별 첫 프레임 한 장뿐이어도 같은 타이밍으로 동작하고, 나중에 프레임 애니메이션이 들어와도 그대로 쓴다.
- **판정**: 판정 구간에 공격 시작점(`originOffset`) 앞쪽으로 `reach` × `width`인 상자(`MeleeBox`)를 `Physics2D.OverlapBox`로 훑어, 상자 안의 `IDamageable`을 **공격 한 번에 한 번씩** 때린다(이미 때린 대상은 건너뜀, 자기 자신 제외, 같은 편은 `Health`가 거절). `CombatLayers.TargetMask(Team.Player)`(Enemy + Default)로 거른다. 피해량 `damage`(1), 밀려남 `knockbackSpeed`(공격 방향), 경직 `stunSeconds`는 `PlayerMeleeAttack` 인스펙터에서 조절. Scene 뷰에서 플레이어를 선택하면 4방향 판정 상자가 노란 선으로 보인다.
- **공격 손맛(그래픽이 없어도)**: 준비·판정 동안 공격하는 쪽으로 살짝 나아가고(`lungeSpeed`) 마무리에서 멈춘다. 맞으면 `Hurt`가 공격을 끊는다.
- **그래픽 슬롯**: ① `PlayerSpriteAnimator ▸ Attack`(방향별 공격 모션, 비어 있으면 Idle이 보임, 한 장이면 공격하는 동안 그 장면이 계속 보임) ② `Slash`(플레이어 자식) `AttackEffect ▸ Slash`(방향별 베기 이펙트, 프레임이 여러 장이면 판정 시간에 펼쳐 재생, 한 장이면 판정 시간 동안 보였다 사라짐; 스프라이트 Pivot이 판정 상자 중심에 놓이고 `Sprite Offset`으로 더 옮김). 이펙트 슬롯이 비어 있으면 판정 범위를 보여주는 **임시 반투명 사각형**이 나온다.
- **메뉴**: `Hangeul ▸ Setup ▸ Player Attack`(씬의 플레이어에 `PlayerMeleeAttack` + `Slash` 붙이기, 컴포넌트를 인스펙터에서 보이게), `GameObject ▸ Hangeul ▸ Training Dummy (테스트용)`(맞으면 색이 변하고 Console에 피해가 적히는 표적, 죽으면 2초 뒤 되살아남).
- 아직 없는 것: 연속 공격 입력 예약, 공격 중 방향 전환, 무기에 따른 피해/범위 변경(무기 단어는 E 단계), 히트스톱·화면 흔들림(D 단계).

### 13.8d 적: 근접 추격형 샘플 (2026-10-14)
- **구조**: `EnemyController`(루트: 이동·감지·피격 설정, FSM 소유) + `EnemyState` 상속 상태들(`Enemy/EnemyStates.cs`) + `EnemySpriteAnimator`(방향별 스프라이트 슬롯). 플레이어와 같은 방식의 단순 FSM(`FSM/StateMachine` 재사용). 위치: `Assets/Scripts/Game/Enemy`.
- **상태**: `Idle`(대기·배회: 잠깐 서 있다가 원래 자리 `Home` 근처 `wanderRadius` 안의 한 점으로 천천히 걷기를 반복) → 플레이어가 **감지 거리** 안이면 `Chase`(플레이어 쪽으로 곧장 걷기, 벽에 닿으면 미끄러짐) → **포기 거리**보다 멀어지거나 플레이어가 죽으면 `Idle`. 맞으면 `Hurt`(공격이 정한 경직 시간과 밀려남 속도, 이 동안 접촉 피해 꺼짐, 끝나면 맞았으니 거리와 상관없이 알아채고 `Chase`), 체력이 0이면 `Dead`(충돌·접촉 피해를 끄고 드롭을 떨어뜨린 뒤 `deathSeconds` 동안 서서히 사라지고 오브젝트 삭제).
- **감지**: `AggroTracker`(`Hangeul.App`, 순수 규칙·EditMode 테스트): 감지 거리 안에서 쫓기 시작하고 포기 거리보다 멀어져야 놓친다(두 거리가 달라 경계에서 깜빡이지 않음). Scene 뷰에서 적을 선택하면 감지(주황)/포기(노랑) 거리와 배회 범위(파랑)가 원으로 보인다.
- **접촉 피해**: 자식 `ContactZone`의 `DamageZone`(Trigger, 몸보다 살짝 큼, EnemyAttack 레이어)이 닿은 `IDamageable`(플레이어)에 피해(기본 1)와 밀려남을 준다. 맞는 쪽의 무적 시간(`Health`) 때문에 그 간격으로만 맞는다. `EnemyController.contactZone`을 비우면 접촉 피해가 없다.
- **피격 효과**: 맞으면 `EnemySpriteAnimator.Flash`로 잠깐 색이 변한다(`hitFlashSeconds`).
- **그래픽 슬롯**: `EnemySpriteAnimator ▸ Idle / Move / Hurt / Dead`(방향별). 비어 있으면 Idle로, Idle도 비어 있으면 `Visual`의 `SpriteRenderer`에 넣은 스프라이트 한 장이 그대로 보인다 — 방향별 첫 프레임뿐인 지금 상태에서도 동작한다.
- **드롭**: `EnemyController.drops`(프리팹 + 확률). 단어 아이템을 떨어뜨리려면 단어가 들어 있는 `WorldItem` 오브젝트를 프리팹으로 만들어 넣는다.
- **메뉴**: `GameObject ▸ Hangeul ▸ Enemy (슬라임 샘플)`. 시간은 `Time.deltaTime`/`fixedDeltaTime` 기준이라 인벤토리를 열면 같이 멈춘다.
- 아직 없는 것: 공격 전조가 있는 공격(돌진 등), 원거리 공격, 길 찾기(벽을 돌아 쫓아오기), 체력 바.

### 13.8a 일시정지 (확정, 2026-10-14)
- **인벤토리를 열면 게임 시간이 멈춘다**(젤다식): `GamePause.Set(PauseReason.Inventory, true)` → `Time.timeScale = 0`. 플레이어·적·투사체·애니메이션·게임 월드 물리가 모두 멈춘다. 정지 이유는 플래그(`PauseReason`: Inventory, Dialogue, Menu)이고 하나라도 켜져 있으면 멈춘다(`GamePause`, `Hangeul.Game`).
- **멈춘 동안에도 움직여야 하는 것은 `Time.unscaledDeltaTime`을 쓴다**: 인벤토리 패널 슬라이드, 카메라 이동(`InventoryCameraShift`), UI 연출(카드 등), 안내 문구, **가방 흔들기 물리**(독립 물리 씬을 수동 진행, §5.6a).
- **게임 코드의 규칙**: 플레이어/적/투사체/게임 연출은 `Time.deltaTime`·`Time.fixedDeltaTime`(멈추면 같이 멈춤)을 쓰면 별도 처리 없이 정지가 적용된다. 정지 중에도 돌아야 하는 UI/연출만 `unscaled`를 쓴다.
- 인벤토리가 열린 채 사라져도 정지가 남지 않게 `InventoryToggle.OnDestroy`에서 해제하고, 플레이를 시작할 때 `GamePause`를 초기화한다.

### 13.8 미해결
- Interact 애니메이션(현재 Idle 재생).
- Walk 스프라이트 연결(현재 비어 있으면 Idle 재생).
- 패널 내부: 제목 텍스트(TMP Essential Resources + 한글 Dynamic SDF 폰트 필요), 가방 프레임 스프라이트(9-slice), 카드 뷰(CardView). 현재는 빈 뼈대만 있다.
