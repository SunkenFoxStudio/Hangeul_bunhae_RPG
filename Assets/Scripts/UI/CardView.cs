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
        // 가방 모드: 단어가 아닌 카드(어둡게 + 물음표)
        static readonly Color UnknownFlat = new Color(0.27f, 0.23f, 0.2f);
        static readonly Color UnknownTint = new Color(0.42f, 0.4f, 0.38f);
        static readonly Color UnknownInk = new Color(0.72f, 0.66f, 0.58f, 0.85f);
        static readonly Color BagInk = new Color(0.95f, 0.9f, 0.78f);   // 어두운 가방 카드 위의 단어 글자
        const float LinkRatio = 0.24f;     // 단어 글자 사이를 잇는 동그라미 지름 (칸 대비)

        const float FaceInset = 0.04f;     // 카드 사이 간격 (칸의 4%씩)
        const float FontRatio = 0.78f;     // HTML의 font-size 78 / 100
        // 글꼴의 위·아래 여백(ascent/descent)이 달라 MiddleCenter만으로는 글자가 위로 치우친다. 카드 크기 비율로 아래로 내린다(눈으로 맞춘 값).
        const float TextDropRatio = 0.06f;

        enum Anim { None, Pop, Shake, Rotate }

        InventoryView _owner;
        InventoryTheme _theme;
        Font _font;
        RectTransform _rect;
        RectTransform _face;
        Image _bg;
        float _cell;

        Anim _anim;
        float _animT, _animDur, _rotTurns;
        bool _target, _holdOn;
        bool _bag, _bagUnknown, _linkRight, _linkDown;
        Image _link, _linkDownImage;

        public Card Card { get; private set; }
        public RectTransform Rect => _rect;

        public void Init(InventoryView owner, Card card, Font font, InventoryTheme theme = null)
        {
            _owner = owner;
            Card = card;
            _font = font;
            _theme = theme;

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

        /// <summary>카드 왼쪽 위를 격자 레이어 좌표(y 아래 방향)로 즉시 옮긴다. 진행 중인 이동 연출은 취소된다.</summary>
        public void SetPosition(Vector2 topLeftInLayer)
        {
            _moveActive = false;
            _rect.anchoredPosition = new Vector2(topLeftInLayer.x, -topLeftInLayer.y);
        }

        Vector2 TopLeft => new Vector2(_rect.anchoredPosition.x, -_rect.anchoredPosition.y);

        // ───── 이동 연출 ─────
        Vector2 _moveFrom, _moveTo;
        float _moveT, _moveDur;
        bool _moveActive;

        /// <summary>현재 위치에서 목표 위치(격자 레이어 좌표)로 부드럽게 옮긴다.</summary>
        public void MoveTo(Vector2 topLeftInLayer, float seconds)
        {
            if ((TopLeft - topLeftInLayer).sqrMagnitude < 0.25f) { SetPosition(topLeftInLayer); return; }
            _moveFrom = TopLeft;
            _moveTo = topLeftInLayer;
            _moveT = 0f;
            _moveDur = Mathf.Max(0.01f, seconds);
            _moveActive = true;
        }

        const float ShrunkScale = 0.75f;
        bool _shrunk;

        /// <summary>쓰레기통 위에 올라와 있는 동안 카드를 살짝 작게 보여준다 (즉시 전환).</summary>
        public void SetShrunk(bool on)
        {
            _shrunk = on;
            _face.localScale = Vector3.one * (on ? ShrunkScale : 1f);
        }

        void UpdateMove()
        {
            if (!_moveActive) return;
            _moveT += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(_moveT / _moveDur);
            float ease = 1f - (1f - u) * (1f - u);
            var p = Vector2.Lerp(_moveFrom, _moveTo, ease);
            _rect.anchoredPosition = new Vector2(p.x, -p.y);
            if (u >= 1f) _moveActive = false;
        }

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
            // 종류별 배경: 스프라이트가 있으면 그것을(색은 흰색으로 곱하기 없음), 없으면 색 칸
            bool vowelLook = Card.Kind == CardKind.Jamo && Hangul.IsJung(Card.Jamo) && !_holdOn;   // 홀드가 성립한 ㅗ는 자음 카드처럼 보인다
            Color flat;
            Sprite sprite = null;
            if (Card.Kind == CardKind.Syllable) { flat = SylColor; sprite = _theme != null ? _theme.cardSyllable : null; }
            else if (vowelLook) { flat = VowelColor; sprite = _theme != null ? _theme.cardVowel : null; }
            else { flat = _holdOn ? HoldColor : ConsColor; sprite = _theme != null ? _theme.cardConsonant : null; }

            _bg.sprite = sprite;
            _bg.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            Color normal = sprite != null ? Color.white : flat;
            Color dark = sprite != null ? UnknownTint : UnknownFlat;
            _bg.color = _target ? TargetColor : (_bag ? dark : normal);   // 가방 모드는 단어 카드도 물음표 카드와 같은 어두운 색
        }

        // ───── 그리기 ─────
        public void Refresh()
        {
            BuildContent();
            UpdateLink();
        }

        // ───── 가방 모드 표현 ─────
        /// <summary>
        /// 가방 모드 표현. bag: 가방 모드의 어두운 카드 색(단어 카드도 물음표 카드와 같은 색). unknown: 단어가 아닌 카드라 글자 대신 물음표.
        /// linkRight: 같은 단어의 다음 글자와 이어지는 카드라 오른쪽 경계에 카드와 같은 색의 작은 동그라미를 덧댄다.
        /// linkDown: 접두어를 위에 얹은 단어에서, 바로 아래 카드(본체의 첫 글자)와 이어지는 카드라 아래쪽 경계에 동그라미를 덧댄다.
        /// 조립 모드에서는 (false, false, false, false).
        /// </summary>
        public void SetBag(bool bag, bool unknown, bool linkRight, bool linkDown = false)
        {
            if (_bag == bag && _bagUnknown == unknown && _linkRight == linkRight && _linkDown == linkDown) return;
            _bag = bag;
            _bagUnknown = unknown;
            _linkRight = linkRight;
            _linkDown = linkDown;
            Refresh();
        }

        void UpdateLink()
        {
            UpdateLinkDot(ref _link, "Link", _linkRight, new Vector2(_cell, -_cell / 2f));           // 오른쪽 가장자리 가운데 = 다음 카드와의 경계
            UpdateLinkDot(ref _linkDownImage, "LinkDown", _linkDown, new Vector2(_cell / 2f, -_cell));   // 아래쪽 가장자리 가운데 = 아래 카드와의 경계
        }

        void UpdateLinkDot(ref Image dot, string name, bool on, Vector2 position)
        {
            if (!on || _cell <= 0f)
            {
                if (dot != null) dot.gameObject.SetActive(false);
                return;
            }
            if (dot == null)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(transform, false);
                dot = go.GetComponent<Image>();
                dot.sprite = CircleSprite();
                dot.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            float d = _cell * LinkRatio;
            var r = (RectTransform)dot.transform;
            r.sizeDelta = new Vector2(d, d);
            r.anchoredPosition = position;
            dot.color = _bg.color;   // 카드 배경색과 똑같이 (가방 모드의 어두운 색)
            dot.gameObject.SetActive(true);
        }

        static Sprite _circle;

        // 흰 원 스프라이트(가장자리 1px 안티앨리어싱). 에셋 없이 한 번만 만들어 공유한다.
        static Sprite CircleSprite()
        {
            if (_circle != null) return _circle;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[n * n];
            float c = (n - 1) / 2f, radius = n / 2f - 1f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(radius - d) * 255f));
                }
            tex.SetPixels32(px);
            tex.Apply();
            _circle = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
            _circle.hideFlags = HideFlags.HideAndDontSave;
            return _circle;
        }

        void BuildContent()
        {
            if (_face == null || _cell <= 0f) return;
            for (int i = _face.childCount - 1; i >= 0; i--) Destroy(_face.GetChild(i).gameObject);
            ApplyColor();

            float face = _cell * (1f - 2f * FaceInset);
            Color ink = _bag ? BagInk : Ink;

            if (_bagUnknown)
            {
                AddText(_face, "?", face, UnknownInk);
                return;
            }

            if (Card.Kind == CardKind.Jamo)
            {
                AddText(_face, Card.Jamo.ToString(), face, ink);
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

            if (missing.Count == 0) { AddText(_face, ch, face, ink); return; }

            AddClippedText(ch, missing, face, new Color(ink.r, ink.g, ink.b, 0.28f));   // 고스트
            AddClippedText(ch, filled, face, ink);
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
                t.anchoredPosition = new Vector2(-r.X / 100f * face, r.Y / 100f * face - face * TextDropRatio);
            }
        }

        RectTransform AddText(RectTransform parent, string s, float face, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            float drop = face * TextDropRatio;
            rt.offsetMin = new Vector2(0f, -drop);
            rt.offsetMax = new Vector2(0f, -drop);

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
            UpdateMove();
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
                _face.localScale = Vector3.one * (_shrunk ? ShrunkScale : 1f);
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
