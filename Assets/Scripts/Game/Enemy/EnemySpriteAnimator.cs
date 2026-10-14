using Hangeul.Game.Character;
using UnityEngine;

namespace Hangeul.Game.Enemy
{
    public enum EnemyAnim { Idle, Move, Hurt, Dead }

    /// <summary>
    /// 적의 방향별 스프라이트 재생(플레이어의 <see cref="PlayerSpriteAnimator"/>와 같은 방식, Animator Controller 불필요).
    /// 슬롯이 비어 있으면 Idle로, Idle도 비어 있으면 SpriteRenderer에 넣어 둔 스프라이트 한 장이 그대로 보인다 —
    /// 지금처럼 방향별 첫 프레임만 있어도, 한 장짜리 스프라이트만 있어도 동작한다.
    /// 맞았을 때 잠깐 색이 변하는 효과(<see cref="Flash"/>)와 사라지는 효과(<see cref="SetAlpha"/>)도 여기서 처리한다.
    /// </summary>
    public sealed class EnemySpriteAnimator : MonoBehaviour
    {
        [SerializeField] SpriteRenderer target;
        [SerializeField] DirectionalClip idle = new DirectionalClip { fps = 6f };
        [SerializeField] DirectionalClip move = new DirectionalClip { fps = 8f };
        [SerializeField] DirectionalClip hurt = new DirectionalClip { fps = 10f, loop = false };
        [SerializeField] DirectionalClip dead = new DirectionalClip { fps = 10f, loop = false };
        [SerializeField] Color flashColor = new Color(1f, 0.4f, 0.4f, 1f);

        EnemyAnim _anim = EnemyAnim.Idle;
        Facing _facing = Facing.Down;
        float _time;
        Sprite[] _frames;
        DirectionalClip _clip;
        Color _baseColor = Color.white;
        float _flashUntil;
        float _alpha = 1f;

        void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            if (target != null) _baseColor = target.color;
            Refresh();
        }

        public void Play(EnemyAnim anim, bool restart = false)
        {
            if (anim == _anim && _clip != null && !restart) return;
            _anim = anim;
            _time = 0f;
            Refresh();
        }

        public void SetFacing(Facing facing)
        {
            if (facing == _facing) return;
            _facing = facing;
            Refresh();
        }

        /// <summary>잠깐 색을 바꾼다(맞았을 때).</summary>
        public void Flash(float seconds) => _flashUntil = Time.time + seconds;

        /// <summary>투명도(1 = 그대로, 0 = 사라짐). 죽을 때 서서히 사라지는 데 쓴다.</summary>
        public void SetAlpha(float alpha) => _alpha = Mathf.Clamp01(alpha);

        void Refresh()
        {
            var wanted = _anim == EnemyAnim.Move ? move : _anim == EnemyAnim.Hurt ? hurt : _anim == EnemyAnim.Dead ? dead : idle;
            _clip = wanted.HasFrames ? wanted : idle;     // 비어 있으면 Idle로 대체
            _frames = _clip.HasFrames ? _clip.Get(_facing) : null;
            Apply();
        }

        void Update()
        {
            if (_frames != null && _frames.Length > 0)
            {
                _time += Time.deltaTime;
                Apply();
            }

            if (target == null) return;
            var c = Time.time < _flashUntil ? flashColor : _baseColor;
            c.a *= _alpha;
            target.color = c;
        }

        void Apply()
        {
            if (target == null || _frames == null || _frames.Length == 0) return;   // 프레임이 없으면 SpriteRenderer의 스프라이트를 그대로 둔다
            int i = Mathf.FloorToInt(_time * Mathf.Max(0.01f, _clip.fps));
            i = _clip.loop ? i % _frames.Length : Mathf.Min(i, _frames.Length - 1);
            target.sprite = _frames[i];
        }
    }
}
