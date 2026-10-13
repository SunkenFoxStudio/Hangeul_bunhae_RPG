using Hangeul.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>
    /// 카드 한 장의 화면 표현. 모델(<see cref="Domain.Card"/>)을 그리기만 하고, 입력은 InventoryView에 전달한다.
    /// 음절 카드의 빈 슬롯은 반투명 고스트로, 채워진 슬롯은 RectMask2D로 잘라 그린다(HTML의 clipPath 대응).
    /// </summary>
    public sealed class CardView : MonoBehaviour,
        IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        static readonly Color Ink = new Color(0.17f, 0.11f, 0.07f);
        static readonly Color SylColor = new Color(0.94f, 0.87f, 0.68f);
        static readonly Color ConsColor = new Color(0.86f, 0.72f, 0.52f);
        static readonly Color VowelColor = new Color(0.72f, 0.82f, 0.70f);
        static readonly Color HoldColor = new Color(0.86f, 0.72f, 0.52f);
        static readonly Color TargetColor = new Color(1f, 0.78f, 0.35f);

        const float FaceInset = 0.04f;     // 카드 사이 간격 (칸의 4%씩)
        const float FontRatio = 0.78f;     // HTML의 font-size 78 / 100

        enum Anim { None, Pop, Shake, Rotate }

        InventoryView _owner;
        Font _font;
        RectTransform _rect;
        RectTransform _face;
        Image _bg;
        float _cell;

        Anim _anim;
        float _animT, _animDur, _rotTurns;
        bool _target, _holdOn;

        public Card Card { get; private set; }
        public RectTransform Rect => _rect;

        public void Init(InventoryView owner, Card card, Font font)
        {
            _owner = owner;
            Card = card;
            _font = font;

            _rect = (RectTransform)transform;
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0f, 1f);

            // 루트는 입력만 받고(투명), 보이는 것은 Face (팝/흔들기/회전 연출 대상)
            var hit = gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            // 완전 투명한 그래픽은 기본 설정(Cull Transparent Mesh)에서 레이캐스트 대상에서도 빠진다
            hit.canvasRenderer.cullTransparentMesh = false;

            var faceGo = new GameObject("Face", typeof(RectTransform), typeof(Image));
            faceGo.transform.SetParent(transform, false);
            _face = (RectTransform)faceGo.transform;
            _face.anchorMin = new Vector2(FaceInset, FaceInset);
            _face.anchorMax = new Vector2(1f - FaceInset, 1f - FaceInset);
            _face.offsetMin = _face.offsetMax = Vector2.zero;
            _bg = faceGo.GetComponent<Image>();
            _bg.raycastTarget = false;

        }

        public void SetCell(float cell)
        {
            _cell = cell;
            _rect.sizeDelta = new Vector2(cell, cell);
            Refresh();
        }

        public void SetPosition(Vector2 topLeftInLayer) => _rect.anchoredPosition = new Vector2(topLeftInLayer.x, -topLeftInLayer.y);

        public void SetTarget(bool on)
        {
            if (_target == on) return;
            _target = on;
            ApplyColor();
        }

        /// <summary>ㅗ·ㅇ 홀드 성립 표시: ㅗ 카드가 자음색으로 바뀐다.</summary>
        public void SetHoldOn(bool on)
        {
            if (_holdOn == on) return;
            _holdOn = on;
            ApplyColor();
        }

        void ApplyColor()
        {
            if (_target) { _bg.color = TargetColor; return; }
            if (Card.Kind == CardKind.Syllable) _bg.color = SylColor;
            else if (_holdOn || !Hangul.IsJung(Card.Jamo)) _bg.color = _holdOn ? HoldColor : ConsColor;
            else _bg.color = VowelColor;
        }

        // ───── 그리기 ─────
        public void Refresh()
        {
            if (_face == null || _cell <= 0f) return;
            for (int i = _face.childCount - 1; i >= 0; i--) Destroy(_face.GetChild(i).gameObject);
            ApplyColor();

            float face = _cell * (1f - 2f * FaceInset);

            if (Card.Kind == CardKind.Jamo)
            {
                AddText(_face, Card.Jamo.ToString(), face, Ink);
                return;
            }

            char cho = Card.S.Cho != 0 ? Card.S.Cho : Card.G.Cho;
            char jung = Card.S.Jung != 0 ? Card.S.Jung : Card.G.Jung;
            char jong = Card.S.Jong != 0 ? Card.S.Jong : Card.G.Jong;
            string ch = Hangul.Compose(cho, jung, jong).ToString();

            var layout = SyllableLayout.LayoutOf(jung);
            bool hasJong = jong != 0;
            var filled = new System.Collections.Generic.List<SyllableLayout.Rect>();
            var missing = new System.Collections.Generic.List<SyllableLayout.Rect>();
            foreach (Slot slot in new[] { Slot.Cho, Slot.Jung, Slot.Jong })
                (Card.S[slot] != 0 ? filled : missing).AddRange(SyllableLayout.Regions(layout, hasJong, slot));

            if (missing.Count == 0) { AddText(_face, ch, face, Ink); return; }

            AddClippedText(ch, missing, face, new Color(Ink.r, Ink.g, Ink.b, 0.28f));   // 고스트
            AddClippedText(ch, filled, face, Ink);
        }

        void AddClippedText(string ch, System.Collections.Generic.List<SyllableLayout.Rect> rects, float face, Color color)
        {
            foreach (var r in rects)
            {
                var go = new GameObject("Clip", typeof(RectTransform), typeof(RectMask2D));
                go.transform.SetParent(_face, false);
                var rt = (RectTransform)go.transform;
                rt.anchorMin = new Vector2(r.X / 100f, 1f - (r.Y + r.H) / 100f);
                rt.anchorMax = new Vector2((r.X + r.W) / 100f, 1f - r.Y / 100f);
                rt.offsetMin = rt.offsetMax = Vector2.zero;

                // 안쪽 글자는 카드 전체 크기로 두고, 영역의 왼쪽 위에 맞춰 당겨 온다
                var t = AddText(rt, ch, face, color);
                t.anchorMin = t.anchorMax = t.pivot = new Vector2(0f, 1f);
                t.sizeDelta = new Vector2(face, face);
                t.anchoredPosition = new Vector2(-r.X / 100f * face, r.Y / 100f * face);
            }
        }

        RectTransform AddText(RectTransform parent, string s, float face, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.text = s;
            text.fontSize = Mathf.Max(8, Mathf.RoundToInt(face * FontRatio));
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.color = color;
            text.raycastTarget = false;
            return rt;
        }

        // ───── 연출 ─────
        public void Pop() => Play(Anim.Pop, 0.18f);
        public void Shake() => Play(Anim.Shake, 0.3f);

        /// <summary>회전 직후 호출: 새 모양을 -k·90°에서 0°로 돌려 보여준다(100 + 90·k ms).</summary>
        public void PlayRotate(int quarterTurns)
        {
            _rotTurns = quarterTurns;
            Play(Anim.Rotate, (100f + 90f * quarterTurns) / 1000f);
        }

        void Play(Anim a, float dur)
        {
            _anim = a; _animT = 0f; _animDur = dur;
            if (a == Anim.Rotate) { _rotActive = false; _rotCur = 0f; }
        }

        // ───── 모서리 드래그 회전용 ─────
        float _rotCur, _rotFrom, _rotTo, _rotT, _rotDur;
        bool _rotActive;

        /// <summary>글자면을 시계방향 deg도로 돌린다. seconds가 0이면 즉시, 아니면 그 시간 동안 부드럽게.</summary>
        public void SetRotation(float deg, float seconds)
        {
            if (seconds <= 0f)
            {
                _rotActive = false;
                _rotCur = deg;
                _face.localEulerAngles = new Vector3(0f, 0f, -deg);
                return;
            }
            _rotFrom = _rotCur; _rotTo = deg; _rotT = 0f; _rotDur = seconds; _rotActive = true;
        }

        /// <summary>자모 카드의 모서리(회전 손잡이) 영역인지: 각 모서리 한 변의 26% 정사각형.</summary>
        public bool IsInCorner(Vector2 screenPos, Camera cam)
        {
            if (Card.Kind != CardKind.Jamo) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_face, screenPos, cam, out var p);
            var r = _face.rect;
            float z = r.width * 0.26f, lx = p.x - r.xMin, ly = p.y - r.yMin;
            return (lx < z || lx > r.width - z) && (ly < z || ly > r.height - z);
        }

        public Vector2 ScreenCenter(Camera cam)
            => RectTransformUtility.WorldToScreenPoint(cam, _face.TransformPoint(_face.rect.center));

        void UpdateRotTween()
        {
            if (!_rotActive) return;
            _rotT += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_rotT / _rotDur);
            _rotCur = Mathf.Lerp(_rotFrom, _rotTo, 1f - (1f - u) * (1f - u));
            _face.localEulerAngles = new Vector3(0f, 0f, -_rotCur);
            if (u >= 1f) _rotActive = false;
        }

        void Update()
        {
            // enabled를 끄면 uGUI가 이벤트(클릭/드래그)를 보내지 않으므로, 애니메이션 없을 때는 여기서 바로 빠진다
            UpdateRotTween();
            if (_anim == Anim.None) return;

            _animT += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_animT / _animDur);

            switch (_anim)
            {
                case Anim.Pop:
                    _face.localScale = Vector3.one * (1f + 0.18f * Mathf.Sin(u * Mathf.PI));
                    break;
                case Anim.Shake:
                    _face.anchoredPosition = new Vector2(Mathf.Sin(u * Mathf.PI * 6f) * (1f - u) * _cell * 0.06f, 0f);
                    break;
                case Anim.Rotate:
                    float ease = 1f - (1f - u) * (1f - u);
                    // 새 글자를 이전 모양 방향(HTML의 -k·90° = Unity z축 +k·90°)에서 0°로 시계방향으로 돌려 보여준다
                    _face.localEulerAngles = new Vector3(0f, 0f, _rotTurns * 90f * (1f - ease));
                    break;
            }

            if (u >= 1f)
            {
                _face.localScale = Vector3.one;
                _face.anchoredPosition = Vector2.zero;
                _face.localEulerAngles = Vector3.zero;
                _anim = Anim.None;
            }
        }

        // ───── 입력 (전부 InventoryView로 위임) ─────
        public void OnPointerDown(PointerEventData e) => _owner.OnCardDown(this, e);
        public void OnPointerClick(PointerEventData e) => _owner.OnCardClick(this, e);
        public void OnBeginDrag(PointerEventData e) => _owner.BeginDrag(this, e);
        public void OnDrag(PointerEventData e) => _owner.Drag(e);
        public void OnEndDrag(PointerEventData e) => _owner.EndDrag(e);
    }
}
