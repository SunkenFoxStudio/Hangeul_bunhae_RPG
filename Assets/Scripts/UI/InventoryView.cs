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
        }

        // 가방 모드에서 한 단어로 묶인 카드들 (카드 아이디 → 소속 묶음과 그 안의 순서)
        sealed class ChunkRef
        {
            public List<Card> Cards;
            public int Index;
        }

        readonly Dictionary<int, CardView> _views = new Dictionary<int, CardView>();
        readonly HoHoldTracker _hold = new HoHoldTracker();
        readonly Dictionary<int, ChunkRef> _chunks = new Dictionary<int, ChunkRef>();
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

        void Awake()
        {
            if (cardLayer == null) cardLayer = (RectTransform)transform;
            if (fitter == null) fitter = GetComponent<AspectRatioFitter>();

            Model = new InventoryModel(cols, rows);
            Model.Changed += OnChanged;
            Model.WordCompleted += OnWordCompleted;
            Model.ModeChanged += OnModeChanged;
            Model.Scanned += RefreshBag;
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
            if (Model == null) return;
            Model.Changed -= OnChanged;
            Model.WordCompleted -= OnWordCompleted;
            Model.ModeChanged -= OnModeChanged;
            Model.Scanned -= RefreshBag;
            if (persist && _saveDirty) SaveNow();
        }

        void OnApplicationQuit()
        {
            if (Model != null && persist && _saveDirty) SaveNow();
        }

        // ───── 단어 / 완성 ─────
        void LoadWords()
        {
            var asset = itemWords != null ? itemWords : Resources.Load<TextAsset>("item_words");
            if (asset == null)
            {
                Debug.LogWarning("[InventoryView] 아이템 단어 목록(item_words.json)이 없어 단어 완성 판정을 하지 않습니다.");
                return;
            }
            var set = ItemWordSet.FromFile(JsonUtility.FromJson<ItemWordsFile>(asset.text), out int skipped);
            if (set == null) { Debug.LogWarning("[InventoryView] 아이템 단어 목록 형식이 올바르지 않습니다 (schemaVersion 1 필요)."); return; }
            if (skipped > 0) Debug.LogWarning($"[InventoryView] 아이템 단어 {skipped}개를 건너뛰었습니다 (한글 음절이 아니거나 중복).");
            Model.SetWords(set);
        }

        void OnWordCompleted(WordMatch m)
        {
            Debug.Log($"[InventoryView] 단어 완성: {m.Word}");
            if (_cell <= 0f || Model.Mode == InventoryMode.Bag) return;   // 가방 모드에서는 완성 글로우를 띄우지 않는다
            CompletionOverlay.Spawn(cardLayer, new Vector2(m.Col * _cell, m.Row * _cell), new Vector2(m.Length * _cell, _cell));
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
        //  - 단어에 속한 카드: 글자 그대로. 2글자 이상이면 다음 글자와 이어지는 카드에 연결 동그라미.
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
                    for (int k = 0; k < m.Length; k++)
                    {
                        var c = Model.Grid.CardAt(m.Col + k, m.Row);
                        if (c == null) { cards = null; break; }
                        cards.Add(c);
                    }
                    if (cards == null) continue;
                    for (int k = 0; k < cards.Count; k++) _chunks[cards[k].Id] = new ChunkRef { Cards = cards, Index = k };
                }
            }

            foreach (var v in _views.Values)
            {
                _chunks.TryGetValue(v.Card.Id, out var cr);
                v.SetBag(bag, bag && cr == null, bag && cr != null && cr.Index < cr.Cards.Count - 1);
            }
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
            if (IsDragging(v)) return;
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
            if (Model.Mode == InventoryMode.Bag) return;
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
        }

        public void EndDrag(PointerEventData e)
        {
            var d = _drag;
            if (logClickTargets) Debug.Log($"[InventoryView] EndDrag drag={(d == null ? "없음" : "있음")} moved={(d != null && d.Moved)}");
            if (d == null) return;
            _drag = null;

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

            if (d.Group != null) { EndBagDrag(d); return; }

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

        // 가방 모드: 잡은 카드의 중심 칸을 기준으로 덩어리 전체가 빈 칸에 들어갈 때만 옮긴다(교환 없음). 아니면 모두 제자리로.
        void EndBagDrag(DragState d)
        {
            if (TryCellOf(d.Pos, out int col, out int row) && Model.MoveGroup(d.GroupCards, col - d.LeadIndex, row)) return;
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

        // 가방 모드: 덩어리가 들어갈 자리(가로 n칸)를 놓을 수 있을 때만 초록으로 보여준다
        void UpdateBagFeedback()
        {
            var d = _drag;
            if (d.OverTrash || !TryCellOf(d.Pos, out int col, out int row)) { ClearFeedback(); return; }

            int start = col - d.LeadIndex;
            if (!Model.CanMoveGroup(d.GroupCards, start, row)) { _highlight.enabled = false; return; }

            var rt = (RectTransform)_highlight.transform;
            rt.sizeDelta = new Vector2(_cell * d.GroupCards.Count, _cell);
            rt.anchoredPosition = new Vector2(start * _cell, -row * _cell);
            _highlight.color = EmptyColor;
            _highlight.enabled = true;
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
        }

        // ───── 매 프레임 ─────
        void Update()
        {
            if (_drag != null) UpdateFeedback(Time.unscaledDeltaTime);

            if (_scanAt >= 0f && Time.unscaledTime >= _scanAt)
            {
                _scanAt = -1f;
                Model.Scan(notify: true);
            }

            if (logClickTargets) LogClickTargets();
            if (persist && _saveDirty && _drag == null && Time.unscaledTime - _lastChange > SaveDelay) SaveNow();

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && Model.Mode == InventoryMode.Assemble && _last != null && _views.ContainsKey(_last.Card.Id) && IsInteractive() && !Typing())
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
