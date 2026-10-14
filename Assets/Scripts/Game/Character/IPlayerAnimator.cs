namespace Hangeul.Game.Character
{
    /// <summary>
    /// 플레이어 상태(FSM)가 애니메이션을 다루는 창구. 상태는 이 인터페이스만 알고, 실제로 그림을 바꾸는 방법은 구현이 정한다.
    /// 지금은 코드로 프레임을 재생하는 <see cref="PlayerSpriteAnimator"/>이고, 나중에 유니티 Animator(Animator Controller)로 바꿀 때는
    /// 같은 인터페이스를 구현한 컴포넌트로 교체하면 상태 코드를 고치지 않아도 된다.
    /// </summary>
    public interface IPlayerAnimator
    {
        /// <summary>애니메이션을 바꾼다. 같은 것이면 그대로 두고, restart가 true면 처음부터 다시 재생한다.</summary>
        void Play(PlayerAnim anim, bool restart = false);

        /// <summary>처음부터 재생하되 모션 전체가 정확히 seconds초 안에 끝나도록 속도를 맞춘다(공격, 피격처럼 시간이 정해진 모션).</summary>
        void PlayFitted(PlayerAnim anim, float seconds);

        /// <summary>바라보는 방향(4방향).</summary>
        void SetFacing(Facing facing);
    }
}
