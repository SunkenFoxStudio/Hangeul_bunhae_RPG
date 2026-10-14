using Hangeul.App;
using UnityEngine;

namespace Hangeul.Game.Character
{
    public enum PlayerAnim { Idle, Walk, Attack, Hurt, Dead }

    /// <summary>
    /// 방향별 스프라이트 프레임을 직접 재생하는 가벼운 애니메이터(Animator Controller 불필요).
    /// 상태(FSM)가 <see cref="Play"/>로 애니메이션 종류를, 컨트롤러가 <see cref="SetFacing"/>으로 방향을 지정한다.
    /// 방향만 바뀔 때는 재생 시간을 유지해서 걷는 도중 방향을 틀어도 발이 끊기지 않는다.
    /// 슬롯(<c>Idle / Walk / Attack / Hurt / Dead</c>)에는 방향마다 프레임 여러 장을 넣는다 — 시트의 프레임을 모두 선택해
    /// 배열 칸에 끌어다 놓거나, 메뉴 <c>Hangeul ▸ Setup ▸ Player Animation</c>이 자동으로 채운다. 한 장뿐이어도 동작한다.
    /// 비어 있는 슬롯은 Idle로 대체한다. 프레임 수와 상관없이 정해진 시간에 맞춰 재생하려면 <see cref="PlayFitted"/>를 쓴다.
    /// </summary>
    public sealed class PlayerSpriteAnimator : MonoBehaviour, IPlayerAnimator
    {
        [SerializeField] SpriteRenderer target;
        [SerializeField] DirectionalClip idle = new DirectionalClip { fps = 6f };
        [SerializeField] DirectionalClip walk = new DirectionalClip { fps = 10f };
        [Tooltip("공격 모션(한 번 재생). 공격 시간표에 맞춰 전체가 그 시간 안에 재생된다. 비어 있으면 Idle이 보인다.")]
        [SerializeField] DirectionalClip attack = new DirectionalClip { fps = 12f, loop = false };
        [Tooltip("맞았을 때 모션(한 번 재생). 경직 시간에 맞춰 재생된다. 비어 있으면 Idle이 보인다.")]
        [SerializeField] DirectionalClip hurt = new DirectionalClip { fps = 10f, loop = false };
        [Tooltip("쓰러지는 모션(한 번 재생하고 마지막 장면에 머문다). 비어 있으면 Idle이 보인다.")]
        [SerializeField] DirectionalClip dead = new DirectionalClip { fps = 10f, loop = false };

        PlayerAnim _anim = PlayerAnim.Idle;
        int _frameIndex;
        Facing _facing = Facing.Down;
        float _time;
        float _fitSeconds;          // 0보다 크면 이 시간 안에 전체를 한 번 재생하도록 속도를 맞춘다
        Sprite[] _frames;
        DirectionalClip _clip;

        /// <summary>지금 재생 중인 애니메이션과 방향, 보이는 프레임 번호(그림자처럼 같은 모션을 따라가는 것들이 읽는다).</summary>
        public PlayerAnim CurrentAnim => _anim;
        public Facing Facing => _facing;
        public int CurrentFrameIndex => _frameIndex;

        void Awake()
        {
            if (target == null) target = GetComponentInChildren<SpriteRenderer>();
            Refresh();
        }

        /// <summary>애니메이션을 바꾼다. 이미 같은 애니메이션이면 그대로 두고, restart가 true면 처음부터 다시 재생한다. 슬롯의 fps로 재생한다.</summary>
        public void Play(PlayerAnim anim, bool restart = false)
        {
            if (anim == _anim && _clip != null && !restart && _fitSeconds <= 0f) return;
            _anim = anim;
            _time = 0f;
            _fitSeconds = 0f;
            Refresh();
        }

        /// <summary>
        /// 처음부터 재생하되, 프레임이 몇 장이든 전체가 정확히 seconds초 안에 한 번 재생되도록 속도를 맞춘다
        /// (공격, 피격처럼 동작 시간이 시간표로 정해진 모션). 한 번 재생하는 슬롯(loop 꺼짐)에 쓴다.
        /// </summary>
        public void PlayFitted(PlayerAnim anim, float seconds)
        {
            _anim = anim;
            _time = 0f;
            // 그 슬롯이 비어 있어 Idle로 대체되는 경우에는 시간에 맞추지 않는다(Idle이 엄청 빠르게 돌지 않게)
            _fitSeconds = ClipFor(anim).HasFrames ? Mathf.Max(0.01f, seconds) : 0f;
            Refresh();
        }

        public void SetFacing(Facing facing)
        {
            if (facing == _facing) return;
            _facing = facing;
            Refresh();
        }

        DirectionalClip ClipFor(PlayerAnim anim)
        {
            switch (anim)
            {
                case PlayerAnim.Walk: return walk;
                case PlayerAnim.Attack: return attack;
                case PlayerAnim.Hurt: return hurt;
                case PlayerAnim.Dead: return dead;
                default: return idle;
            }
        }

        void Refresh()
        {
            var wanted = ClipFor(_anim);
            _clip = wanted.HasFrames ? wanted : idle;   // 비어 있는 슬롯은 Idle로 대체
            _frames = _clip.Get(_facing);
            Apply();
        }

        void Update()
        {
            if (_frames == null || _frames.Length == 0) return;
            _time += Time.deltaTime;   // 게임 시간 기준: 일시정지·히트스톱 중에는 멈춘다
            Apply();
        }

        void Apply()
        {
            if (target == null || _frames == null || _frames.Length == 0) return;
            float fps = _fitSeconds > 0f ? ClipMath.FpsToFit(_frames.Length, _fitSeconds) : _clip.fps;
            _frameIndex = ClipMath.FrameIndex(_time, fps, _frames.Length, _clip.loop);
            target.sprite = _frames[_frameIndex];
        }
    }
}
