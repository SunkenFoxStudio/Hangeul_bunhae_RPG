using UnityEngine;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// 공격 판정 구간에 판정 상자 위치에 나타나는 베기 이펙트. 방향별 스프라이트를 <see cref="slash"/> 슬롯에 끌어다 놓으면 쓴다.
    /// 프레임이 여러 장이면 판정 시간에 맞춰 펼쳐서 재생하고, 한 장이면 그 장면이 판정 시간 동안 보이다가 사라진다.
    /// 슬롯이 비어 있으면 판정 범위를 보여주는 임시 반투명 사각형이 나온다(그래픽이 들어오면 자동으로 쓰지 않는다).
    /// 스프라이트의 중심(Pivot)이 판정 상자의 중심에 놓인다. 위치를 더 옮기려면 <see cref="spriteOffset"/>을 쓴다.
    /// </summary>
    [AddComponentMenu("Hangeul/Attack Effect")]
    public sealed class AttackEffect : MonoBehaviour
    {
        [SerializeField] SpriteRenderer target;
        [Tooltip("방향별 베기 이펙트 스프라이트(Down/Up/Left/Right). 비어 있으면 임시 사각형이 나온다.")]
        [SerializeField] DirectionalClip slash = new DirectionalClip { fps = 20f, loop = false };
        [Tooltip("판정 상자 중심에서 이펙트 스프라이트를 옮길 거리(월드 단위). 방향마다 따로 맞추려면 스프라이트의 Pivot을 조절한다.")]
        [SerializeField] Vector2 spriteOffset;
        [Tooltip("임시 사각형 색(슬롯이 비어 있을 때만).")]
        [SerializeField] Color placeholderColor = new Color(1f, 1f, 1f, 0.55f);

        static Sprite _whiteSquare;

        Sprite[] _frames;
        bool _placeholder;
        float _time, _duration;
        bool _playing;

        /// <summary>플레이어 아래에 "Slash" 오브젝트를 만들어 붙인다(씬에 미리 만들어 두지 않았을 때의 대비).</summary>
        public static AttackEffect CreateDefault(Transform parent)
        {
            var go = new GameObject("Slash");
            go.transform.SetParent(parent, false);
            var effect = go.AddComponent<AttackEffect>();
            effect.EnsureRenderer();
            return effect;
        }

        void Awake() => EnsureRenderer();

        void EnsureRenderer()
        {
            if (target == null) target = GetComponent<SpriteRenderer>();
            if (target == null) target = gameObject.AddComponent<SpriteRenderer>();
            target.sortingOrder = Mathf.Max(target.sortingOrder, 1);   // 플레이어 위에 그린다
            target.enabled = false;
        }

        /// <summary>
        /// 이펙트를 보여준다. center/size는 판정 상자(월드 좌표), seconds는 보이는 시간.
        /// </summary>
        public void Play(Facing facing, Vector2 center, Vector2 size, float seconds)
        {
            EnsureRenderer();
            _frames = slash.HasFrames ? slash.Get(facing) : null;
            _placeholder = _frames == null || _frames.Length == 0;
            _duration = Mathf.Max(0.02f, seconds);
            _time = 0f;
            _playing = true;

            transform.position = center + spriteOffset;
            transform.rotation = Quaternion.identity;

            if (_placeholder)
            {
                // 임시 사각형: 1유닛짜리 흰 사각형을 판정 상자 크기로 늘린다
                transform.localScale = new Vector3(size.x, size.y, 1f) / Mathf.Max(0.0001f, ParentScale());
                target.sprite = WhiteSquare();
                target.color = placeholderColor;
            }
            else
            {
                transform.localScale = Vector3.one / Mathf.Max(0.0001f, ParentScale());
                target.color = Color.white;
            }
            Apply();
            target.enabled = true;
        }

        public void Hide()
        {
            _playing = false;
            if (target != null) target.enabled = false;
        }

        float ParentScale() => transform.parent != null ? Mathf.Abs(transform.parent.lossyScale.x) : 1f;

        void Update()
        {
            if (!_playing) return;
            _time += Time.deltaTime;   // 게임 시간 기준: 일시정지 중에는 멈춘다
            if (_time >= _duration) { Hide(); return; }
            Apply();
        }

        void Apply()
        {
            float u = Mathf.Clamp01(_time / _duration);
            if (_placeholder)
            {
                var c = placeholderColor;
                c.a *= 1f - u;                    // 서서히 사라진다
                target.color = c;
                return;
            }
            int i = Mathf.Min(_frames.Length - 1, Mathf.FloorToInt(u * _frames.Length));
            target.sprite = _frames[i];
        }

        static Sprite WhiteSquare()
        {
            if (_whiteSquare != null) return _whiteSquare;
            var tex = Texture2D.whiteTexture;
            _whiteSquare = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), tex.width);   // 1유닛
            _whiteSquare.hideFlags = HideFlags.HideAndDontSave;
            return _whiteSquare;
        }
    }
}
