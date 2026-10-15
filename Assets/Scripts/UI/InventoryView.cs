using System.Collections.Generic;
using Hangeul.App;
using Hangeul.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>
    /// 카드 격자 화면. 모델(<see cref="InventoryModel"/>)의 Changed 이벤트를 받아 CardView를 만들고 지우고 옮기며,
    /// 드래그 입력을 모델 명령으로 바꾼다.
    /// 조작: 우클릭 드래그 = 카드 이동/결합, 음절 좌클릭 드래그 = 분리(그 카드를 계속 끌고 감), 자모 좌클릭 드래그 = 이동/결합,
    /// 우클릭 = 회전 우선 변환, 더블클릭 = 분리 우선 변환, R 키 = 마지막으로 만진 카드 변환.
    /// 모서리 드래그 회전: 자모 카드 모서리를 좌클릭으로 잡고 돌린다.
    /// 가방 모드: 완성된 단어는 한 덩어리(어디를 잡든 통째로 이동), 단어가 아닌 카드는 어둡게 물음표. 분해·결합·회전은 막힌다.
    /// </summary>
    public sealed class InventoryView : MonoBehaviour
    {
        static readonly Color EmptyColor = new Color(0.3f, 0.8f, 0.4f, 0.4f);
        static readonly Color SwapColor = new Color(0.3f, 0.55f, 1f, 0.4f);
        static readonly Color MergeColor = new Color(1f, 0.6f, 0.2f, 0.45f);

        [Tooltip("카드가 놓이는 레이어. 격자 영역 전체를 덮도록 stretch 되어 있어야 한다.")]
        [SerializeField] RectTransform cardLayer;
        [Tooltip("격자 비율(가로:세로)을 맞추는 AspectRatioFitter. 없으면 비율을 맞추지 않는다.")]
        [SerializeField] AspectRatioFitter fitter;
        [SerializeField] Font font;
        [SerializeField, Range(1, 20)] int cols = 5;
        [SerializeField, Range(1, 20)] int rows = 5;
        [Header("테마 (스프라이트)")]
        [Tooltip("격자 칸/카드 배경 스프라이트 모음. 비우면 코드로 그린 기본 모양을 쓴다.")]
        [SerializeField] InventoryTheme theme;

        [Header("격자 표시 (코드로 그리는 기본 모양 — 테마에 Cell 스프라이트가 있으면 쓰지 않는다)")]
        [SerializeField] bool showGrid = true;
        [Tooltip("칸 구분선 색")]
        [SerializeField] Color gridLineColor = new Color(0.25f, 0.16f, 0.08f, 0.55f);
        [Tooltip("칸 구분선 굵기 (캔버스 단위)")]
        [SerializeField, Min(0f)] float gridLineWidth = 2f;
        [Tooltip("한 칸 걸러 깔리는 체커 무늬 색. 투명(알파 0)이면 끈다.")]
        [SerializeField] Color gridAlternateColor = new Color(0.25f, 0.16f, 0.08f, 0.10f);

        [Header("단어 / 쓰레기통")]
        [Tooltip("아이템 단어 목록(item_words.json). 비워 두면 Resources/item_words를 찾는다.")]
        [SerializeField] TextAsset itemWords;
        [Tooltip("카드를 끌어다 놓으면 삭제되는 영역. 없으면 삭제 기능이 없다.")]
        [SerializeField] TrashZone trashZone;

        [Header("저장")]
        [Tooltip("켜면 시작할 때 저장 파일을 불러오고(있으면), 바뀔 때마다 자동 저장한다. 저장된 격자 크기가 위 Cols/Rows보다 우선한다.")]
        [SerializeField] bool persist = true;
        [SerializeField] string saveFileName = InventorySaveStore.DefaultFileName;

        [Header("가방 흔들기 (Unity Physics2D)")]
        [Tooltip("부딪힌 두 카드가 합칠 수 있을 때 실제로 조립될 확률(0~1). 새로 부딪힐 때마다 한 번 굴린다. 완성된 단어의 카드는 항상 제외.")]
        [SerializeField, Range(0f, 1f)] float shakeMergeChance = 1f;
        [Tooltip("카드를 던져 올리는 시간(초). 이 시간 동안 Toss Interval마다 던진다.")]
        [SerializeField, Min(0.1f)] float shakeSeconds = 1.6f;
        [Tooltip("흔들기가 끝난 뒤 카드가 멈추기를 기다리는 최대 시간(초). 이 안에 멈추지 않아도 칸에 맞춘다.")]
        [SerializeField, Min(0f)] float shakeSettleSeconds = 2.5f;
        [Tooltip("던질 때 카드가 위로 튀어 오르는 속도(칸/초). 클수록 높이 튄다. 0이면 던지지 않고 중력으로 떨어지기만 한다.")]
        [SerializeField, Min(0f)] float shakeTossSpeed = 9f;
        [Tooltip("던지는 간격(초). 던지는 사이에는 중력으로 떨어지며 서로 부딪힌다.")]
        [SerializeField, Min(0.2f)] float shakeTossInterval = 0.7f;
        [Tooltip("던질 때 좌우로 흩어지는 정도(위로 던지는 속도에 대한 비율). 0이면 똑바로 위로.")]
        [SerializeField, Range(0f, 1f)] float shakeSideways = 0.35f;
        [Tooltip("던질 때 카드가 도는 세기(도/초). 0이면 돌지 않는다(충돌로 도는 것은 그대로).")]
        [SerializeField, Min(0f)] float shakeTossSpin = 360f;
        [Tooltip("아래로 당기는 중력(칸/초²). 0이면 둥둥 떠다닌다. 중력이 있으면 끝났을 때 카드가 아래쪽에 쌓인다.")]
        [SerializeField, Min(0f)] float shakeGravity = 12f;
        [Tooltip("카드와 벽이 튕기는 정도(0이면 붙고 1이면 그대로 튕김).")]
        [SerializeField, Range(0f, 1f)] float shakeBounce = 0.35f;
        [Tooltip("카드와 벽 사이 마찰.")]
        [SerializeField, Range(0f, 1f)] float shakeFriction = 0.35f;

        [Header("디버그")]
        [Tooltip("켜면 마우스를 누를 때마다 그 위치에서 클릭을 받는 UI 목록(위→아래)을 Console에 출력한다.")]
        [SerializeField] bool logClickTargets;

        sealed class DragState
        {
            public CardView View;
            public Vector2 Origin;       // 드래그 시작 때 카드 왼쪽 위 (레이어 좌표, y 아래 방향)
            public Vector2 StartLocal;   // 누른 지점
            public Vector2 Pos;          // 현재 카드 왼쪽 위
            public bool Moved;

            // 모서리 드래그 회전 (Rot != null이면 이동이 아니라 회전 드래그)
            public RotationSession Rot;
            public Vector2 Center;       // 카드 중심(화면 좌표)
            public float PrevAngle;      // 직전 포인터 각도 (시계방향 +, 라디안)
            public float Total;          // 누적 각도

            public bool OverTrash;       // 포인터가 쓰레기통 위에 있음

            // 가방 모드 덩어리 드래그 (Group != null이면 이 카드들을 통째로 옮긴다)
            public List<CardView> Group;
            public List<Card> GroupCards;
            public List<Vector2> GroupOrigins;
            public int LeadIndex;        // 잡은 카드의 묶음 안 순서
            public ChunkRef Chunk;       // 끌고 있는 단어 덩어리(단어가 아닌 카드 한 장이면 null)
        }

        // 가방 모드에서 한 단어로 묶인 카드들 (카드 아이디 → 소속 묶음과 그 안의 순서)
        sealed class ChunkRef
        {
            public List<Card> Cards;     // 읽는 순서(접두어를 위에 얹은 단어면 접두어 → 본체)
            public int Index;
            public bool LinkRight;       // 오른쪽 이웃과 이어짐(같은 줄 안)
            public bool LinkDown;        // 아래 카드와 이어짐(위에 얹은 접두어의 첫 글자 → 본체의 첫 글자)
            public WordMatch Match;      // 이 덩어리가 이루는 완성 단어
            public ItemKind Kind;        // 무기/방어구/음식(쓰임이 없으면 None)
            public bool Equipped;        // 장착 중인 무기인지
            public ItemFrame Frame;      // 이 카드 몫의 아이템 테두리
        }

        const float FrameThickness = 0.06f;           // 아이템 테두리 굵기 (칸 대비)
        const float FrameThicknessEquipped = 0.10f;   // 장착 중이면 더 굵게

        readonly Dictionary<int, CardView> _views = new Dictionary<int, CardView>();
        readonly HoHoldTracker _hold = new HoHoldTracker();
        readonly Dictionary<int, ChunkRef> _chunks = new Dictionary<int, ChunkRef>();
        readonly System.Random _rng = new System.Random();
        bool _shaking;
        Image _highlight;
        GridLinesGraphic _gridLines;
        RectTransform _cellRoot;
        readonly List<Image> _cellImages = new List<Image>();
        DragState _drag;
        CardView _last;
        CardView _mergeTarget;
        float _cell;
        bool _dirty = true;
        bool _saveDirty;
        float _lastChange;
        float _scanAt = -1f;            // 조작 뒤 단어 완성을 확인할 시각 (HTML처럼 60ms 디바운스)
        const float ScanDelay = 0.06f;
        const float SaveDelay = 0.5f;   // 마지막 변경 뒤 이 시간이 지나면 저장 (드래그 중에는 저장하지 않는다)

        public InventoryModel Model { get; private set; }

        /// <summary>인벤토리가 쓰는 글꼴(프로젝트 글꼴). 다른 화면 UI가 같은 글꼴을 쓰고 싶을 때 받아 간다.</summary>
        public Font Font => font;

        /// <summary>
        /// 가방 모드에서 완성 단어를 끌어다 놓을 수 있는 월드 쪽 대상(플레이어). 게임 쪽이 등록한다 — 없으면 끌어다 놓기가 없다.
        /// 무기는 장착/해제, 음식은 먹고 카드가 사라진다. 쓸 수 없으면 카드는 제자리로 돌아온다.
        /// </summary>
        public IItemDropTarget DropTarget { get; set; }

        void Awake()
        {
            if (cardLayer == null) cardLayer = (RectTransform)transform;
            if (fitter == null) fitter = GetComponent<AspectRatioFitter>();

            Model = new InventoryModel(cols, rows);
            Model.Changed += OnChanged;
            Model.WordCompleted += OnWordCompleted;
            Model.ModeChanged += OnModeChanged;
            Model.Scanned += RefreshBag;
            Model.EquipmentChanged += OnEquipmentChanged;
            // 쓰레기통이 카드 뒤에 그려져야 카드가 그 위로 지나가고, 달라붙을 때도 보인다
            if (trashZone != null) trashZone.transform.SetAsFirstSibling();
            LoadWords();
            if (persist) LoadSaved();
            RefreshTrashVisibility();

            // 칸 구분선: 카드 뒤(맨 아래)에 깐다
            var lines = new GameObject("GridLines", typeof(RectTransform), typeof(CanvasRenderer), typeof(GridLinesGraphic));
            lines.transform.SetParent(cardLayer, false);
            var lrt = (RectTransform)lines.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            _gridLines = lines.GetComponent<GridLinesGraphic>();
            _gridLines.raycastTarget = false;
            lines.transform.SetAsFirstSibling();

            var hl = new GameObject("CellHighlight", typeof(RectTransform), typeof(Image));
            hl.transform.SetParent(cardLayer, false);
            var rt = (RectTransform)hl.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            _highlight = hl.GetComponent<Image>();
            _highlight.raycastTarget = false;
            _highlight.enabled = false;
        }

        void OnDestroy()
        {
            if (_shakeWorld != null) _shakeWorld.Dispose();   // 흔드는 도중에 사라지면 물리 월드도 치운다
            if (Model == null) return;
            Model.Changed -= OnChanged;
            Model.WordCompleted -= OnWordCompleted;
            Model.ModeChanged -= OnModeChanged;
            Model.Scanned -= RefreshBag;
            Model.EquipmentChanged -= OnEquipmentChanged;
            if (persist && _saveDirty) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (Model != null && persist && _saveDirty) SaveNow();
        }

        // ───── 단어 / 완성 ─────
        void LoadWords()
        {
            // 빌드에서는 실행 파일 옆의 목록 파일(DB 관리 씬이 고친 것)이 우선이고, 에디터에서는 직접 넣은 파일이나 Resources의 것을 쓴다
            string json = ItemWordsStore.ReadCurrent(itemWords);
            if (json == null)
            {
                Debug.LogWarning("[InventoryView] 아이템 단어 목록(item_words.json)이 없어 단어 완성 판정을 하지 않습니다.");
                return;
            }
            ItemWordsFile file;
            try { file = JsonUtility.FromJson<ItemWordsFile>(json); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[InventoryView] 아이템 단어 목록(item_words.json)을 읽을 수 없어 단어 완성 판정을 하지 않습니다: " + e.Message);
                return;
            }
            var set = ItemWordSet.FromFile(file, out int skipped);
            if (set == null) { Debug.LogWarning("[InventoryView] 아이템 단어 목록 형식이 올바르지 않습니다 (schemaVersion 1 필요)."); return; }
            if (skipped > 0) Debug.LogWarning($"[InventoryView] 아이템 단어 {skipped}개를 건너뛰었습니다 (한글 음절이 아니거나 중복).");
            Model.SetWords(set);
        }

        void OnWordCompleted(WordMatch m)
        {
            Debug.Log($"[InventoryView] 단어 완성: {m.Word}");
            if (_cell <= 0f || Model.Mode == InventoryMode.Bag) return;   // 가방 모드에서는 완성 글로우를 띄우지 않는다
            CompletionOverlay.Spawn(cardLayer, new Vector2(m.Col * _cell, m.Row * _cell), new Vector2(m.Length * _cell, _cell));
            if (m.IsStacked)   // 위에 얹은 접두어 줄도 함께 빛낸다
                CompletionOverlay.Spawn(cardLayer, new Vector2(m.Col * _cell, (m.Row - 1) * _cell), new Vector2(m.PrefixLength * _cell, _cell));
        }

        // ───── 저장 ─────
        void LoadSaved()
        {
            if (!InventorySaveStore.Exists(saveFileName)) return;
            var r = InventorySaveStore.Load(Model, saveFileName);
            if (!r.Ok) Debug.LogWarning($"[InventoryView] 인벤토리 불러오기 실패: {r.Error}");
            else if (r.Skipped > 0 || r.Relocated > 0)
                Debug.LogWarning($"[InventoryView] 인벤토리 불러오기: 카드 {r.Loaded}장, 버린 카드 {r.Skipped}장, 옮긴 카드 {r.Relocated}장");
            _saveDirty = false;
            _dirty = true;
        }

        [ContextMenu("지금 저장")]
        public void SaveNow()
        {
            try { InventorySaveStore.Save(Model, saveFileName); _saveDirty = false; }
            catch (System.Exception e) { Debug.LogWarning("[InventoryView] 인벤토리 저장 실패: " + e.Message); }
        }

        [ContextMenu("저장 파일 삭제")]
        void DeleteSaveFile()
        {
            InventorySaveStore.Delete(saveFileName);
            Debug.Log("[InventoryView] 저장 파일을 삭제했습니다: " + InventorySaveStore.PathOf(saveFileName));
        }

        [ContextMenu("저장 폴더 열기")]
        void OpenSaveFolder() => Application.OpenURL("file://" + Application.persistentDataPath);

        void OnRectTransformDimensionsChange() => _dirty = true;

        // ───── 가방 흔들기 (조립 모드, Unity Physics2D) ─────
        public bool IsShaking => _shaking;
        ShakeSession _shakeSession;
        ShakeWorld _shakeWorld;

        /// <summary>
        /// 가방을 흔든다: 주사위를 쥐고 위아래로 흔들듯이 카드들이 Unity 물리로 튕기고 구르며, 부딪혀서 합칠 수 있으면 조립된다
        /// (ShakeWorld + ShakeSession). 끝나면 가장 가까운 빈 칸에 맞춘다. 완성된 단어의 카드는 가만히 있는다.
        /// 흔드는 동안에는 카드를 잡을 수 없다. 조립 모드에서만 동작한다.
        /// </summary>
        public void ShakeBag()
        {
            if (_shaking || _drag != null || Model.Mode != InventoryMode.Assemble) return;
            if (_dirty) Relayout();
            if (_cell <= 0f) return;

            ClearFeedback();
            _shakeSession = new ShakeSession(Model, new ShakeSettings
            {
                MergeChance = shakeMergeChance,
                ShakeSeconds = shakeSeconds,
                MaxSettleSeconds = shakeSettleSeconds,
                TossSpeed = shakeTossSpeed,
                TossInterval = shakeTossInterval,
                Sideways = shakeSideways,
                Spin = shakeTossSpin,
                Gravity = shakeGravity,
                Bounce = shakeBounce,
                Friction = shakeFriction,
            }, _rng);
            _shaking = true;

            if (_shakeSession.Finished) { UpdateShake(); return; }   // 흔들 카드가 없다
            _shakeWorld = ShakeWorld.Create(_shakeSession, Model.Grid.Cols, Model.Grid.Rows);
        }

        // 물리를 한 프레임 진행해 세션에 반영하고, 카드 화면을 그 위치/회전에 맞춘다. 끝나면 정리한다.
        void UpdateShake()
        {
            var session = _shakeSession;
            if (session == null) return;

            if (_shakeWorld != null)
            {
                if (Model.Mode != InventoryMode.Assemble) _shakeWorld.Abort();   // 모드가 바뀌면 그 자리에서 멈추고 칸에 맞춘다
                else _shakeWorld.Tick(Time.unscaledDeltaTime);   // 게임 시간이 멈춰 있어도(인벤토리 열림) 흔들리도록 멈추지 않는 시간으로 진행
            }

            foreach (var body in session.Bodies)
            {
                if (body.Fixed || body.Dead || !_views.TryGetValue(body.Card.Id, out var v)) continue;
                if (session.Finished)   // 마지막 칸으로 부드럽게 자리잡고 글자는 똑바로 선다
                {
                    // 몇 바퀴를 돌았든 가장 가까운 방향으로 돌아와 선다
                    v.SetRotation(Mathf.DeltaAngle(0f, body.DisplayAngleDeg), 0f);
                    v.SetRotation(0f, 0.2f);
                    v.MoveTo(CellPos(body.Card), 0.12f);
                    continue;
                }
                v.SetPosition(new Vector2((body.X - 0.5f) * _cell, (body.Y - 0.5f) * _cell));
                v.SetRotation(body.DisplayAngleDeg, 0f);
            }

            if (!session.Finished) return;
            Debug.Log($"[InventoryView] 가방 흔들기: 조립 {session.Merged}장");
            if (_shakeWorld != null) _shakeWorld.Dispose();
            _shakeWorld = null;
            _shakeSession = null;
            _shaking = false;
        }

        // ───── 모드 ─────
        public InventoryMode Mode => Model.Mode;
        public void SetMode(InventoryMode mode) => Model.SetMode(mode);
        public void ToggleMode() => Model.SetMode(Model.Mode == InventoryMode.Bag ? InventoryMode.Assemble : InventoryMode.Bag);

        void OnModeChanged(InventoryMode mode)
        {
            _saveDirty = true;
            _lastChange = Time.unscaledTime;
            _last = null;
            RefreshBag();
            RefreshTrashVisibility();
        }

        // 쓰레기통은 조립 모드에서만 보인다
        void RefreshTrashVisibility()
        {
            if (trashZone != null) trashZone.gameObject.SetActive(Model.Mode == InventoryMode.Assemble);
        }

        // 가방 모드: MatchScanner.Shown(겹치지 않는 가장 긴 단어들)을 읽어 카드마다 모양을 정한다.
        //  - 단어에 속한 카드: 글자 그대로. 이웃한 글자와 이어지는 카드에 연결 동그라미(옆은 오른쪽 경계, 위에 얹은 접두어는 아래쪽 경계).
        //  - 단어에 속하지 않은 카드(미완성/자모/없는 글자): 어둡게 물음표.
        void RefreshBag()
        {
            _chunks.Clear();
            bool bag = Model.Mode == InventoryMode.Bag;
            if (bag)
            {
                foreach (var m in Model.Scanner.Shown)
                {
                    var cards = new List<Card>();
                    foreach (var (col, row) in m.Cells())
                    {
                        var c = Model.Grid.CardAt(col, row);
                        if (c == null) { cards = null; break; }
                        cards.Add(c);
                    }
                    if (cards == null) continue;

                    // 쓸 수 있는 아이템(무기/방어구/음식)이면 덩어리를 감싸는 테두리를 카드마다 나눠 그린다
                    var kind = Model.KindOf(m);
                    bool equipped = kind == ItemKind.Weapon && Model.IsEquipped(m);
                    HashSet<(int, int)> cells = null;   // 바깥 가장자리를 가리는 칸 집합: 테두리를 그릴 때만 필요하다
                    if (kind != ItemKind.None)
                    {
                        cells = new HashSet<(int, int)>();
                        foreach (var c in cards) cells.Add((c.Col, c.Row));
                    }

                    int p = m.PrefixLength;   // cards[0..p)가 접두어(윗줄), cards[p..)가 본체
                    for (int k = 0; k < cards.Count; k++)
                    {
                        bool inPrefix = k < p;
                        bool lastOfRun = inPrefix ? k == p - 1 : k == cards.Count - 1;
                        _chunks[cards[k].Id] = new ChunkRef
                        {
                            Cards = cards,
                            Index = k,
                            LinkRight = !lastOfRun,
                            LinkDown = p > 0 && k == 0,
                            Match = m,
                            Kind = kind,
                            Equipped = equipped,
                            Frame = kind == ItemKind.None ? ItemFrame.None : BuildFrame(cards, k, kind, equipped, cells),
                        };
                    }
                }
            }

            foreach (var v in _views.Values)
            {
                _chunks.TryGetValue(v.Card.Id, out var cr);
                v.SetBag(bag, bag && cr == null, bag && cr != null && cr.LinkRight, bag && cr != null && cr.LinkDown);
                v.SetItemFrame(bag && cr != null ? cr.Frame : ItemFrame.None);
            }
        }

        // 한 카드의 테두리: 덩어리 바깥쪽 가장자리에만 선을 긋는다. 첫 카드에는 종류 이름표, 장착 중이면 마지막 카드에 체크 배지.
        static ItemFrame BuildFrame(List<Card> cards, int index, ItemKind kind, bool equipped, HashSet<(int, int)> cells)
        {
            var c = cards[index];
            var color = ItemFrame.KindColor(kind);
            if (equipped) color = Color.Lerp(color, Color.white, 0.25f);   // 장착 중이면 더 밝고 굵게
            return new ItemFrame(
                top: !cells.Contains((c.Col, c.Row - 1)),
                right: !cells.Contains((c.Col + 1, c.Row)),
                bottom: !cells.Contains((c.Col, c.Row + 1)),
                left: !cells.Contains((c.Col - 1, c.Row)),
                color: color,
                thickness: equipped ? FrameThicknessEquipped : FrameThickness,
                label: index == 0 ? ItemKinds.Label(kind) : null,
                badge: equipped && index == cards.Count - 1);
        }

        void OnEquipmentChanged()
        {
            _saveDirty = true;               // 장착 상태도 인벤토리 저장에 들어간다
            _lastChange = Time.unscaledTime;
            RefreshBag();
        }

        // ───── 외부 API ─────
        public AddWordResult AddWord(string word, (int col, int row)? anchor = null) => Model.AddWord(word, anchor);

        /// <summary>격자 크기를 바꾼다. 확인창 UI가 생기기 전까지는 넘치는 카드를 바로 폐기한다.</summary>
        public ResizeResult Resize(int newCols, int newRows)
        {
            var r = Model.Resize(newCols, newRows, allowDiscard: true);
            _dirty = true;
            return r;
        }

        // ───── 배치 ─────
        float ComputeCell()
        {
            var r = cardLayer.rect;
            return Mathf.Min(r.width / Model.Grid.Cols, r.height / Model.Grid.Rows);
        }

        Vector2 CellPos(Card c) => new Vector2(c.Col * _cell, c.Row * _cell);

        bool IsDragging(CardView v) => _drag != null && (_drag.View == v || (_drag.Group != null && _drag.Group.Contains(v)));

        void Place(CardView v)
        {
            if (IsDragging(v) || _shakeSession != null) return;   // 흔드는 동안 위치는 시뮬레이션이 정한다
            v.SetPosition(CellPos(v.Card));
        }

        void Relayout()
        {
            _dirty = false;
            if (fitter != null) fitter.aspectRatio = (float)Model.Grid.Cols / Model.Grid.Rows;
            RefreshGridLines();

            float cell = ComputeCell();
            if (cell <= 0f) return;
            bool sizeChanged = !Mathf.Approximately(cell, _cell);
            _cell = cell;

            foreach (var v in _views.Values)
            {
                if (sizeChanged) v.SetCell(_cell);
                Place(v);
            }
            if (_drag != null) UpdateFeedback(0f);
        }

        void RefreshGridLines()
        {
            bool sprites = theme != null && theme.cell != null;
            if (_gridLines != null) _gridLines.gameObject.SetActive(showGrid && !sprites);
            if (_gridLines != null && !sprites)
                _gridLines.Set(Model.Grid.Cols, Model.Grid.Rows, gridLineWidth, gridLineColor, gridAlternateColor);
            RefreshCellImages(sprites);
        }

        // 테마에 칸 스프라이트가 있으면 칸마다 Image 하나를 깐다(한 칸 걸러 Alt). 칸 수만큼만 만들어 재사용한다.
        void RefreshCellImages(bool sprites)
        {
            if (_cellRoot == null)
            {
                var go = new GameObject("GridCells", typeof(RectTransform));
                go.transform.SetParent(cardLayer, false);
                _cellRoot = (RectTransform)go.transform;
                _cellRoot.anchorMin = Vector2.zero;
                _cellRoot.anchorMax = Vector2.one;
                _cellRoot.offsetMin = _cellRoot.offsetMax = Vector2.zero;
                _cellRoot.SetAsFirstSibling();
            }
            _cellRoot.gameObject.SetActive(sprites);
            if (!sprites) return;

            var grid = Model.Grid;
            int need = grid.Cols * grid.Rows;
            while (_cellImages.Count < need)
            {
                var go = new GameObject("Cell", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_cellRoot, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                _cellImages.Add(img);
            }

            float cell = ComputeCell();
            for (int i = 0; i < _cellImages.Count; i++)
            {
                var img = _cellImages[i];
                bool used = i < need;
                img.gameObject.SetActive(used);
                if (!used) continue;

                int col = i % grid.Cols, row = i / grid.Cols;
                var rt = (RectTransform)img.transform;
                rt.sizeDelta = new Vector2(cell, cell);
                rt.anchoredPosition = new Vector2(col * cell, -row * cell);
                bool alt = ((col + row) & 1) == 1 && theme.cellAlt != null;
                img.sprite = alt ? theme.cellAlt : theme.cell;
                img.type = img.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }
        }

        // 인스펙터에서 색/굵기를 바꾸면 Play 중에도 바로 반영한다
        void OnValidate()
        {
            if (Application.isPlaying && Model != null) _dirty = true;
        }

        void LateUpdate()
        {
            if (_dirty) Relayout();
        }

        // ───── 모델 변경 → 화면 ─────
        void OnChanged(CardChange ch)
        {
            _saveDirty = true;
            _lastChange = Time.unscaledTime;
            _scanAt = Time.unscaledTime + ScanDelay;

            switch (ch.Type)
            {
                case ChangeType.Added:
                    var v = CreateView(ch.Card);
                    v.Pop();
                    break;
                case ChangeType.Removed:
                case ChangeType.Discarded:
                    if (_views.TryGetValue(ch.Card.Id, out var gone))
                    {
                        _views.Remove(ch.Card.Id);
                        if (_last == gone) _last = null;
                        Destroy(gone.gameObject);
                    }
                    break;
                case ChangeType.Updated:
                    if (_views.TryGetValue(ch.Card.Id, out var upd)) { upd.Refresh(); upd.Pop(); }
                    break;
                case ChangeType.Rotating:
                    if (_views.TryGetValue(ch.Card.Id, out var rot)) rot.Refresh();
                    break;
                case ChangeType.Moved:
                    if (_views.TryGetValue(ch.Card.Id, out var mv) && !IsDragging(mv))
                        mv.MoveTo(CellPos(mv.Card), 0.1f);   // 놓은 칸/밀려난 칸으로 부드럽게 자리잡는다
                    break;
                case ChangeType.Rejected:
                    if (_views.TryGetValue(ch.Card.Id, out var rj)) rj.Shake();
                    break;
            }
        }

        CardView CreateView(Card card)
        {
            if (_cell <= 0f) _cell = ComputeCell();
            var go = new GameObject("Card " + card.Id, typeof(RectTransform));
            go.transform.SetParent(cardLayer, false);
            var v = go.AddComponent<CardView>();
            v.Init(this, card, font, theme);
            _views[card.Id] = v;
            v.SetCell(Mathf.Max(_cell, 1f));
            Place(v);
            return v;
        }

        // ───── 입력: 카드에서 전달 ─────
        public void OnCardDown(CardView v, PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left && e.button != PointerEventData.InputButton.Right) return;
            _last = v;
            v.transform.SetAsLastSibling();
        }

        public void OnCardClick(CardView v, PointerEventData e)
        {
            // 우클릭 드래그를 놓을 때는 EndDrag보다 먼저 Click이 오므로, 드래그였던 입력은 클릭으로 치지 않는다
            if (e.dragging || _drag != null) return;
            if (Model.Mode == InventoryMode.Bag || _shaking) return;
            if (e.button == PointerEventData.InputButton.Right) QuickTransform(v, false);
            else if (e.button == PointerEventData.InputButton.Left && e.clickCount >= 2) QuickTransform(v, true);
        }

        void QuickTransform(CardView v, bool splitFirst)
        {
            _last = v;
            if (Model.Transform(v.Card, splitFirst, out int k) == TransformResult.Rotated) v.PlayRotate(k);
        }

        public void BeginDrag(CardView v, PointerEventData e)
        {
            _drag = null;
            if (_shaking) return;
            bool right = e.button == PointerEventData.InputButton.Right;
            if (logClickTargets) Debug.Log($"[InventoryView] BeginDrag {v.name} button={e.button}");
            if (!right && e.button != PointerEventData.InputButton.Left) return;

            if (Model.Mode == InventoryMode.Bag) { BeginBagDrag(v, e); return; }

            var state = new DragState
            {
                View = v,
                StartLocal = ToLayer(e.pressPosition, e.pressEventCamera),
                Origin = CellPos(v.Card),
            };

            // 자모 카드의 모서리를 좌클릭으로 잡으면 이동이 아니라 회전
            if (!right && v.IsInCorner(e.pressPosition, e.pressEventCamera))
            {
                state.Rot = Model.BeginRotation(v.Card);
                if (state.Rot != null)
                {
                    state.Center = v.ScreenCenter(e.pressEventCamera);
                    state.PrevAngle = AngleOf(e.pressPosition, state.Center);
                    _drag = state;
                    _last = v;
                    RotateDrag(e);
                    return;
                }
            }

            // 음절 카드를 좌클릭으로 끌면 분리: 끌어당긴 방향 쪽 슬롯이 떨어져 나오고, 그 카드를 계속 끈다
            if (!right && v.Card.Kind == CardKind.Syllable)
            {
                Vector2 d = e.position - e.pressPosition;
                var dir = Mathf.Abs(d.x) > Mathf.Abs(d.y) ? (d.x < 0 ? PullDir.L : PullDir.R) : (d.y > 0 ? PullDir.U : PullDir.D);
                var detached = Model.Detach(v.Card, dir);
                if (logClickTargets) Debug.Log($"[InventoryView] 분리 dir={dir} → {(detached == null ? "실패" : "새 카드 " + detached.Id)}");
                if (detached == null || !_views.TryGetValue(detached.Id, out var nv)) return;   // 못 뗐으면 흔들기만 하고 끝
                state.View = nv;
            }

            _drag = state;
            _last = state.View;
            _highlight.transform.SetAsLastSibling();
            state.View.transform.SetAsLastSibling();
            Drag(e);
        }

        // 가방 모드: 어디를 잡든 그 카드가 속한 단어 덩어리(없으면 그 카드 한 장)를 통째로 끈다
        void BeginBagDrag(CardView v, PointerEventData e)
        {
            _chunks.TryGetValue(v.Card.Id, out var cr);
            var cards = cr != null ? cr.Cards : new List<Card> { v.Card };
            int lead = cr != null ? cr.Index : 0;

            var group = new List<CardView>();
            var origins = new List<Vector2>();
            foreach (var c in cards)
            {
                if (!_views.TryGetValue(c.Id, out var gv)) return;
                group.Add(gv);
                origins.Add(CellPos(c));
            }

            _drag = new DragState
            {
                View = v,
                StartLocal = ToLayer(e.pressPosition, e.pressEventCamera),
                Origin = origins[lead],
                Group = group,
                GroupCards = new List<Card>(cards),
                GroupOrigins = origins,
                LeadIndex = lead,
                Chunk = cr,
            };
            _last = v;
            _highlight.transform.SetAsLastSibling();
            foreach (var gv in group) gv.transform.SetAsLastSibling();
            Drag(e);
        }

        public void Drag(PointerEventData e)
        {
            if (_drag == null) return;
            if (_drag.Rot != null) { RotateDrag(e); return; }
            Vector2 local = ToLayer(e.position, e.pressEventCamera);
            Vector2 pos = _drag.Origin + (local - _drag.StartLocal);   // 격자에 묶이지 않고 포인터를 그대로 따라간다

            // 쓰레기통 위에 있으면 포인터를 따라가지 않고 쓰레기통 칸 가운데에 붙어 살짝 작아진다
            // 쓰레기통은 조립 모드에서만 쓴다 (가방 모드에서는 보이지도 않는다)
            bool over = trashZone != null && Model.Mode == InventoryMode.Assemble && trashZone.Contains(e.position, e.pressEventCamera);
            if (over != _drag.OverTrash)
            {
                _drag.OverTrash = over;
                trashZone.SetHover(over);
                _drag.View.SetShrunk(over);
            }
            if (over) pos = TrashTopLeft(e.pressEventCamera);
            _drag.Pos = pos;
            _drag.Moved = true;
            _drag.View.SetPosition(pos);
            if (_drag.Group != null)
                for (int i = 0; i < _drag.Group.Count; i++)
                    if (_drag.Group[i] != _drag.View) _drag.Group[i].SetPosition(_drag.GroupOrigins[i] + (pos - _drag.Origin));
            UpdateFeedback(0f);
            if (_drag.Group != null) UpdateDropTarget(e.position);
        }

        // ───── 가방 모드: 플레이어에게 끌어다 놓기 (무기 장착 / 음식 먹기) ─────
        static ItemDrop ToDrop(ChunkRef c) => new ItemDrop(c.Match.Word, c.Kind, c.Equipped);

        // 이 덩어리를 플레이어에게 놓을 수 있는지(쓸 수 있는 아이템이고 대상이 등록돼 있는지)
        bool CanDropOnTarget(DragState d) => d.Chunk != null && d.Chunk.Kind != ItemKind.None && DropTarget != null;

        // 포인터가 플레이어 위에 있는지 확인하고, 놓으면 어떻게 되는지 안내 문구를 덩어리 위에 띄운다
        void UpdateDropTarget(Vector2 screenPosition)
        {
            var d = _drag;
            var action = ItemUseAction.None;
            bool over = CanDropOnTarget(d) && DropTarget.Contains(screenPosition);
            if (over) action = DropTarget.Preview(ToDrop(d.Chunk));
            ShowHint(over ? ItemUseRules.Hint(action) : null, d.Pos, action == ItemUseAction.Blocked);
        }

        // 놓았다: 게임 쪽(플레이어)이 효과를 적용하고, 인벤토리는 장착 표시나 카드 없애기를 맡는다. 먹은 게 아니면 카드는 제자리로.
        void UseItem(DragState d)
        {
            var match = d.Chunk.Match;
            if (!Model.IsShown(match)) { foreach (var gv in d.Group) ReturnHome(gv); return; }   // 그새 단어가 깨졌으면 아무것도 하지 않는다

            switch (DropTarget.Use(ToDrop(d.Chunk)))
            {
                case ItemUseAction.Equip:
                    Model.EquipWeapon(match);
                    foreach (var gv in d.Group) gv.Pop();
                    break;
                case ItemUseAction.Unequip:
                    Model.UnequipWeapon();
                    foreach (var gv in d.Group) gv.Pop();
                    break;
                case ItemUseAction.Eat:
                    if (Model.ConsumeWord(match)) return;   // 카드가 사라진다 — 돌아갈 카드가 없다
                    break;
                default:   // 쓸 수 없다(방어구, 체력이 가득 찬 음식 등)
                    foreach (var gv in d.Group) gv.Shake();
                    break;
            }
            foreach (var gv in d.Group) ReturnHome(gv);
        }

        RectTransform _hintRect;
        Image _hintBg;
        Text _hintText;
        string _hintShownText;      // 마지막으로 글자 배치를 계산한 문구·색·칸 크기(같으면 다시 계산하지 않는다)
        bool _hintShownWarn;
        float _hintShownCell;

        // 끌고 있는 덩어리에서 잡은 카드(topLeft, 레이어 좌표) 위에 짧은 안내 문구를 띄운다. text가 비면 숨긴다.
        void ShowHint(string text, Vector2 topLeft, bool warn)
        {
            if (string.IsNullOrEmpty(text) || _cell <= 0f)
            {
                if (_hintRect != null) _hintRect.gameObject.SetActive(false);
                return;
            }
            if (_hintRect == null)
            {
                var go = new GameObject("DropHint", typeof(RectTransform), typeof(Canvas), typeof(Image));
                go.transform.SetParent(cardLayer, false);
                var hintCanvas = go.GetComponent<Canvas>();   // 카드 이름표(CardView, 맨 위 Canvas)보다도 위에 그린다
                hintCanvas.overrideSorting = true;
                var rootCanvas = cardLayer.GetComponentInParent<Canvas>();
                hintCanvas.sortingOrder = (rootCanvas != null ? rootCanvas.rootCanvas.sortingOrder : 0) + 2;
                _hintRect = (RectTransform)go.transform;
                _hintRect.anchorMin = _hintRect.anchorMax = new Vector2(0f, 1f);
                _hintRect.pivot = new Vector2(0.5f, 0f);
                _hintBg = go.GetComponent<Image>();
                _hintBg.raycastTarget = false;
                _hintBg.color = new Color(0.1f, 0.07f, 0.04f, 0.88f);

                var textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textGo.transform.SetParent(go.transform, false);
                var trt = (RectTransform)textGo.transform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = trt.offsetMax = Vector2.zero;
                _hintText = textGo.GetComponent<Text>();
                _hintText.alignment = TextAnchor.MiddleCenter;
                _hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
                _hintText.verticalOverflow = VerticalWrapMode.Overflow;
                _hintText.raycastTarget = false;
            }

            // 이 함수는 끄는 동안 포인터가 움직일 때마다 불린다. 문구·색·칸 크기가 그대로면 글자 배치를 다시 계산하지 않고 위치만 따라간다.
            if (text != _hintShownText || warn != _hintShownWarn || !Mathf.Approximately(_cell, _hintShownCell))
            {
                float h = _cell * 0.42f;
                _hintText.font = font;
                _hintText.fontSize = Mathf.Max(8, Mathf.RoundToInt(h * 0.6f));
                _hintText.color = warn ? new Color(1f, 0.72f, 0.5f) : Color.white;
                _hintText.text = text;
                _hintRect.sizeDelta = new Vector2(_hintText.preferredWidth + h * 0.8f, h);
                _hintShownText = text;
                _hintShownWarn = warn;
                _hintShownCell = _cell;
            }
            _hintRect.anchoredPosition = new Vector2(topLeft.x + _cell * 0.5f, -topLeft.y + _cell * 0.1f);   // 첫 카드 바로 위
            if (!_hintRect.gameObject.activeSelf)
            {
                _hintRect.SetAsLastSibling();   // 자기 Canvas(정렬 순서 +2)로 그려서 형제 순서는 켤 때만 맞추면 된다
                _hintRect.gameObject.SetActive(true);
            }
        }

        public void EndDrag(PointerEventData e)
        {
            var d = _drag;
            if (logClickTargets) Debug.Log($"[InventoryView] EndDrag drag={(d == null ? "없음" : "있음")} moved={(d != null && d.Moved)}");
            if (d == null) return;
            _drag = null;
            ShowHint(null, Vector2.zero, false);

            if (d.Rot != null) { FinishRotateDrag(d); return; }

            if (trashZone != null) trashZone.SetHover(false);
            var card = d.View.Card;
            if (!_views.ContainsKey(card.Id)) { ClearFeedback(); return; }

            // 쓰레기통 위에서 놓으면 삭제 (이미 칸 가운데에 붙어 있다)
            if (d.OverTrash)
            {
                ClearFeedback();
                Model.Discard(card);
                return;
            }

            bool holdOn = _hold.On;
            ClearFeedback();

            if (!d.Moved)
            {
                if (d.Group != null) foreach (var gv in d.Group) Place(gv);
                else Place(d.View);
                return;
            }

            if (d.Group != null) { EndBagDrag(d, e.position); return; }

            // 카드 중심이 격자 안일 때만 놓을 수 있다. 밖이면 제자리로 돌아간다
            if (!TryCellOf(d.Pos, out int col, out int row)) { ReturnHome(d.View); return; }
            var occupant = Model.Grid.CardAt(col, row, card);

            if (card.Kind == CardKind.Jamo && occupant != null)
            {
                switch (Model.TryMerge(card, occupant, holdOn))
                {
                    case CombineOutcome.Ok: return;                       // 뷰 갱신은 Changed 이벤트가 처리
                    case CombineOutcome.Banned:
                        ReturnHome(d.View); d.View.Shake(); return;       // 됬 은 거절: 제자리로
                }
            }

            Model.Move(card, col, row);                                   // 빈 칸이면 이동, 점유된 칸이면 자리 교환
            if (occupant != null && _views.TryGetValue(occupant.Id, out var ov)) ov.Pop();
        }

        // 가방 모드: 플레이어 위에서 놓으면 아이템 사용(장착/먹기). 아니면 잡은 카드의 중심 칸을 기준으로 덩어리 전체(모양 그대로)가
        // 빈 칸에 들어갈 때만 옮긴다(교환 없음). 아니면 모두 제자리로.
        void EndBagDrag(DragState d, Vector2 screenPosition)
        {
            if (CanDropOnTarget(d) && DropTarget.Contains(screenPosition)) { UseItem(d); return; }

            var lead = d.View.Card;   // 잡은 카드가 놓인 칸을 기준으로 덩어리를 모양 그대로 옮긴다
            if (TryCellOf(d.Pos, out int col, out int row) && Model.ShiftGroup(d.GroupCards, col - lead.Col, row - lead.Row)) return;
            foreach (var gv in d.Group) ReturnHome(gv);
        }

        void ReturnHome(CardView v) => v.MoveTo(CellPos(v.Card), 0.15f);

        // 쓰레기통 칸 가운데에 카드 중심이 오도록 한 카드 왼쪽 위 (격자 레이어 좌표)
        Vector2 TrashTopLeft(Camera cam)
            => ToLayer(trashZone.ScreenCenter(cam), cam) - new Vector2(_cell, _cell) / 2f;

        // ───── 모서리 드래그 회전 ─────
        // 포인터가 카드 중심을 기준으로 돈 각도를 누적해 90° 단위로 반올림한다. 경계를 넘는 순간 자모가 바뀐다.
        static float AngleOf(Vector2 screen, Vector2 center)
            => Mathf.Atan2(-(screen.y - center.y), screen.x - center.x);   // 화면 y는 위가 +라서 부호를 뒤집어 시계방향이 +가 되게 한다

        void RotateDrag(PointerEventData e)
        {
            var d = _drag;
            float a = AngleOf(e.position, d.Center);
            float delta = a - d.PrevAngle;
            if (delta > Mathf.PI) delta -= 2f * Mathf.PI;
            else if (delta < -Mathf.PI) delta += 2f * Mathf.PI;
            d.Total += delta;
            d.PrevAngle = a;

            int steps = Mathf.RoundToInt(d.Total / (Mathf.PI / 2f));
            if (steps == d.Rot.Steps) return;

            // 자모가 바뀌면 이전 모습에서 이어지도록 시작 각도를 맞춘 뒤 목표 각도로 돌린다
            if (Model.ApplyRotation(d.View.Card, d.Rot, steps)) d.View.SetRotation(d.Rot.JumpAngleDeg, 0f);
            d.View.SetRotation(d.Rot.TargetAngleDeg, 0.12f);
        }

        void FinishRotateDrag(DragState d)
        {
            bool mapped = Model.FinishRotation(d.View.Card, d.Rot);
            d.View.SetRotation(0f, mapped ? 0f : 0.18f);   // 확정이면 각도 초기화, 글자가 안 되는 각도면 원위치로 돌아간다
        }

        // ───── 드래그 중 피드백 ─────
        /// <summary>카드 중심이 놓인 칸. 중심이 격자 밖이면 false.</summary>
        bool TryCellOf(Vector2 topLeft, out int col, out int row)
        {
            float cx = topLeft.x + _cell / 2f, cy = topLeft.y + _cell / 2f;
            col = row = -1;
            if (cx < 0f || cy < 0f || cx >= Model.Grid.Cols * _cell || cy >= Model.Grid.Rows * _cell) return false;
            col = Mathf.FloorToInt(cx / _cell);
            row = Mathf.FloorToInt(cy / _cell);
            return true;
        }

        void UpdateFeedback(float dt)
        {
            if (_drag == null || _drag.Rot != null) return;
            if (_drag.Group != null) { UpdateBagFeedback(); return; }
            if (_drag.OverTrash || !TryCellOf(_drag.Pos, out int col, out int row))
            {
                ClearFeedback();      // 놓을 수 없는 곳: 칸 강조/결합 표시를 모두 끈다
                return;
            }
            var card = _drag.View.Card;
            var occupant = Model.Grid.CardAt(col, row, card);

            CardView target = null;
            if (card.Kind == CardKind.Jamo && occupant != null
                && CardOps.TryCombine(card, occupant, false, out _) != CombineOutcome.Rejected)
                _views.TryGetValue(occupant.Id, out target);

            if (_mergeTarget != target)
            {
                if (_mergeTarget != null) _mergeTarget.SetTarget(false);
                _mergeTarget = target;
                if (_mergeTarget != null) _mergeTarget.SetTarget(true);
            }

            _hold.Tick(card, target != null ? occupant : null, dt);
            SetHoldVisual(card, occupant, _hold.On);

            var rt = (RectTransform)_highlight.transform;
            rt.sizeDelta = new Vector2(_cell, _cell);
            rt.anchoredPosition = new Vector2(col * _cell, -row * _cell);
            _highlight.color = target != null ? MergeColor : occupant != null ? SwapColor : EmptyColor;
            _highlight.enabled = true;
        }

        // 가방 모드: 덩어리가 들어갈 자리(모양 그대로)를 놓을 수 있을 때만 칸마다 초록으로 보여준다
        void UpdateBagFeedback()
        {
            var d = _drag;
            if (d.OverTrash || !TryCellOf(d.Pos, out int col, out int row)) { ClearFeedback(); return; }

            var lead = d.View.Card;
            int dCol = col - lead.Col, dRow = row - lead.Row;
            if (!Model.CanShiftGroup(d.GroupCards, dCol, dRow)) { HideGroupHighlights(); _highlight.enabled = false; return; }

            for (int i = 0; i < d.GroupCards.Count; i++)
            {
                var img = i == 0 ? _highlight : GroupHighlight(i - 1);
                var rt = (RectTransform)img.transform;
                rt.sizeDelta = new Vector2(_cell, _cell);
                rt.anchoredPosition = new Vector2((d.GroupCards[i].Col + dCol) * _cell, -(d.GroupCards[i].Row + dRow) * _cell);
                img.color = EmptyColor;
                img.enabled = true;
            }
            for (int i = d.GroupCards.Count - 1; i < _extraHighlights.Count; i++) _extraHighlights[i].enabled = false;
        }

        // 덩어리의 2번째 이후 카드 칸을 표시하는 강조 이미지(필요할 때 만들어 재사용)
        readonly List<Image> _extraHighlights = new List<Image>();

        Image GroupHighlight(int index)
        {
            while (_extraHighlights.Count <= index)
            {
                var go = new GameObject("CellHighlightExtra", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(cardLayer, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
                var img = go.GetComponent<Image>();
                img.raycastTarget = false;
                img.enabled = false;
                _extraHighlights.Add(img);
            }
            var h = _extraHighlights[index];
            h.transform.SetSiblingIndex(_highlight.transform.GetSiblingIndex());   // 카드보다 아래, 첫 강조 바로 뒤
            return h;
        }

        void HideGroupHighlights()
        {
            foreach (var h in _extraHighlights) h.enabled = false;
        }

        CardView _holdVisual;

        void SetHoldVisual(Card dragged, Card occupant, bool on)
        {
            CardView vowel = null;
            if (on)
            {
                var ho = dragged.Jamo == 'ㅗ' ? dragged : occupant;
                if (ho != null) _views.TryGetValue(ho.Id, out vowel);
            }
            if (_holdVisual == vowel) return;
            if (_holdVisual != null) _holdVisual.SetHoldOn(false);
            _holdVisual = vowel;
            if (_holdVisual != null) _holdVisual.SetHoldOn(true);
        }

        void ClearFeedback()
        {
            if (_mergeTarget != null) _mergeTarget.SetTarget(false);
            _mergeTarget = null;
            if (_holdVisual != null) _holdVisual.SetHoldOn(false);
            _holdVisual = null;
            _hold.Reset();
            _highlight.enabled = false;
            HideGroupHighlights();
        }

        // ───── 매 프레임 ─────
        void Update()
        {
            if (_drag != null) UpdateFeedback(Time.unscaledDeltaTime);
            if (_shakeSession != null) UpdateShake();

            // 흔드는 동안에는 카드 칸이 그대로라서 단어 확인/저장을 미룬다(끝난 뒤 한 번에)
            if (_scanAt >= 0f && Time.unscaledTime >= _scanAt && !_shaking)
            {
                _scanAt = -1f;
                Model.Scan(notify: true);
            }

            if (logClickTargets) LogClickTargets();
            if (persist && _saveDirty && _drag == null && !_shaking && Time.unscaledTime - _lastChange > SaveDelay) SaveNow();

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && Model.Mode == InventoryMode.Assemble && _last != null && _views.ContainsKey(_last.Card.Id) && IsInteractive() && !Typing() && !_shaking)
                QuickTransform(_last, false);
        }

        void LogClickTargets()
        {
            var mouse = Mouse.current;
            if (mouse == null || !(mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)) return;
            if (EventSystem.current == null) { Debug.Log("[InventoryView] EventSystem이 없습니다."); return; }

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = mouse.position.ReadValue() }, results);
            var names = new System.Text.StringBuilder();
            foreach (var r in results) names.Append(r.gameObject.name).Append(" > ");
            Debug.Log($"[InventoryView] 클릭 위치 UI: {(results.Count == 0 ? "(없음 — 아무 UI도 이 위치를 받지 않음)" : names.ToString())}");
        }

        bool IsInteractive()
        {
            var g = GetComponentInParent<CanvasGroup>();
            return g == null || g.interactable;
        }

        static bool Typing()
        {
            var sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == null) return false;
            var input = sel.GetComponent<InputField>();
            return input != null && input.isFocused;
        }

        Vector2 ToLayer(Vector2 screen, Camera cam)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(cardLayer, screen, cam, out var p);
            var r = cardLayer.rect;
            return new Vector2(p.x - r.xMin, r.yMax - p.y);
        }
    }
}
