using UnityEngine;

namespace Hangeul.Game.Character
{
    public enum PlayerAnim { Idle, Walk, Attack }

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
        [Tooltip("공격 모션(한 번 재생). 프레임이 한 장뿐이면 공격하는 동안 그 장면이 계속 보이고, 비어 있으면 Idle이 보인다.")]
        [SerializeField] DirectionalClip attack = new DirectionalClip { fps = 12f, loop = false };

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

        /// <summary>애니메이션을 바꾼다. 이미 같은 애니메이션이면 그대로 두고, restart가 true면 처음부터 다시 재생한다.</summary>
        public void Play(PlayerAnim anim, bool restart = false)
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

        void Refresh()
        {
            // 걷기/공격 프레임이 비어 있으면 Idle로 대체
            _clip = _anim == PlayerAnim.Walk && walk.HasFrames ? walk
                  : _anim == PlayerAnim.Attack && attack.HasFrames ? attack
                  : idle;
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
