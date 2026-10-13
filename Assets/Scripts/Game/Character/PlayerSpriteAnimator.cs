using UnityEngine;

namespace Hangeul.Game.Character
{
    public enum PlayerAnim { Idle, Walk }

    /// <summary>
    /// 방향별 스프라이트 프레임을 직접 재생하는 가벼운 애니메이터(Animator Controller 불필요).
    /// 상태(FSM)가 <see cref="Play"/>로 애니메이션 종류를, 컨트롤러가 <see cref="SetFacing"/>으로 방향을 지정한다.
    /// 방향만 바뀔 때는 재생 시간을 유지해서 걷는 도중 방향을 틀어도 발이 끊기지 않는다.
    /// </summary>
    public sealed class PlayerSpriteAnimator : MonoBehaviour
    {
        [SerializeField] SpriteRenderer target;
        [SerializeField] DirectionalClip idle = new DirectionalClip { fps = 6f };
        [SerializeField] DirectionalClip walk = new DirectionalClip { fps = 10f };

        PlayerAnim _anim = PlayerAnim.Idle;
        Facing _facing = Facing.Down;
        float _time;
        Sprite[] _frames;
        DirectionalClip _clip;

        void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            Refresh();
        }

        public void Play(PlayerAnim anim)
        {
            if (anim == _anim && _clip != null) return;
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

        void Refresh()
        {
            _clip = _anim == PlayerAnim.Walk && walk.HasFrames ? walk : idle;   // 걷기 프레임이 비어 있으면 Idle로 대체
            _frames = _clip.Get(_facing);
            Apply();
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0) return;
            _time += Time.deltaTime;
            Apply();
        }

        void Apply()
        {
            if (target == null || _frames == null || _frames.Length == 0) return;
            int i = Mathf.FloorToInt(_time * Mathf.Max(0.01f, _clip.fps));
            i = _clip.loop ? i % _frames.Length : Mathf.Min(i, _frames.Length - 1);
            target.sprite = _frames[i];
        }
    }
}
