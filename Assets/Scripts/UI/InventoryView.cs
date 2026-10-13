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
    /// 모서리 드래그 회전은 M3.
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
        }

        readonly Dictionary<int, CardView> _views = new Dictionary<int, CardView>();
        readonly HoHoldTracker _hold = new HoHoldTracker();
        Image _highlight;
        DragState _drag;
        CardView _last;
        CardView _mergeTarget;
        float _cell;
        bool _dirty = true;

        public InventoryModel Model { get; private set; }

        void Awake()
        {
            if (cardLayer == null) cardLayer = (RectTransform)transform;
            if (fitter == null) fitter = GetComponent<AspectRatioFitter>();

            Model = new InventoryModel(cols, rows);
            Model.Changed += OnChanged;

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
            if (Model != null) Model.Changed -= OnChanged;
        }

        void OnRectTransformDimensionsChange() => _dirty = true;

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

        void Place(CardView v)
        {
            if (_drag != null && _drag.View == v) return;
            v.SetPosition(CellPos(v.Card));
        }

        void Relayout()
        {
            _dirty = false;
            if (fitter != null) fitter.aspectRatio = (float)Model.Grid.Cols / Model.Grid.Rows;

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

        void LateUpdate()
        {
            if (_dirty) Relayout();
        }

        // ───── 모델 변경 → 화면 ─────
        void OnChanged(CardChange ch)
        {
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
                    if (_views.TryGetValue(ch.Card.Id, out var mv)) Place(mv);
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
            v.Init(this, card, font);
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

        public void Drag(PointerEventData e)
        {
            if (_drag == null) return;
            if (_drag.Rot != null) { RotateDrag(e); return; }
            Vector2 local = ToLayer(e.position, e.pressEventCamera);
            Vector2 pos = _drag.Origin + (local - _drag.StartLocal);
            pos.x = Mathf.Clamp(pos.x, 0f, (Model.Grid.Cols - 1) * _cell);
            pos.y = Mathf.Clamp(pos.y, 0f, (Model.Grid.Rows - 1) * _cell);
            _drag.Pos = pos;
            _drag.Moved = true;
            _drag.View.SetPosition(pos);
            UpdateFeedback(0f);
        }

        public void EndDrag(PointerEventData e)
        {
            var d = _drag;
            if (logClickTargets) Debug.Log($"[InventoryView] EndDrag drag={(d == null ? "없음" : "있음")} moved={(d != null && d.Moved)}");
            if (d == null) return;
            _drag = null;

            if (d.Rot != null) { FinishRotateDrag(d); return; }

            bool holdOn = _hold.On;
            ClearFeedback();

            var card = d.View.Card;
            if (!d.Moved || !_views.ContainsKey(card.Id)) { Place(d.View); return; }

            var (col, row) = CellOf(d.Pos);
            var occupant = Model.Grid.CardAt(col, row, card);

            if (card.Kind == CardKind.Jamo && occupant != null)
            {
                switch (Model.TryMerge(card, occupant, holdOn))
                {
                    case CombineOutcome.Ok: return;                       // 뷰 갱신은 Changed 이벤트가 처리
                    case CombineOutcome.Banned:
                        Place(d.View); d.View.Shake(); return;            // 됬 은 거절: 제자리로
                }
            }

            Model.Move(card, col, row);                                   // 빈 칸이면 이동, 점유된 칸이면 자리 교환
            if (occupant != null && _views.TryGetValue(occupant.Id, out var ov)) ov.Pop();
        }

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
        (int col, int row) CellOf(Vector2 topLeft)
        {
            int c = Mathf.FloorToInt((topLeft.x + _cell / 2f) / _cell);
            int r = Mathf.FloorToInt((topLeft.y + _cell / 2f) / _cell);
            return (GridModel.Clamp(c, 0, Model.Grid.Cols - 1), GridModel.Clamp(r, 0, Model.Grid.Rows - 1));
        }

        void UpdateFeedback(float dt)
        {
            if (_drag == null || _drag.Rot != null) return;
            var card = _drag.View.Card;
            var (col, row) = CellOf(_drag.Pos);
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

            if (logClickTargets) LogClickTargets();

            var kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame && _last != null && _views.ContainsKey(_last.Card.Id) && IsInteractive() && !Typing())
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
