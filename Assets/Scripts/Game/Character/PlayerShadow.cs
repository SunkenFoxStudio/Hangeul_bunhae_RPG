using UnityEngine;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// 캐릭터 발밑 그림자. 평소에는 <see cref="normal"/> 한 장을 보여주고, 쓰러지는 동안에는 캐릭터와 같은 프레임 번호의
    /// <see cref="dead"/> 슬롯 프레임(Death_Shadow 시트: 그림자가 작아지며 몸 모양이 되는 모션)을 따라 보여준다.
    /// 그림자 시트는 캐릭터 프레임과 같은 크기(48×64)라 캐릭터 그림(Visual)과 같은 위치·크기에 놓으면 발밑에 정확히 맞는다 —
    /// 그림 한 칸의 월드 크기가 다르면(그림자는 PPU 100, 캐릭터는 PPU 16) 메뉴가 배율을 맞춰 둔다.
    /// 캐릭터보다 한 칸 아래에 그려지도록 정렬 순서는 메뉴가 정한다.
    /// </summary>
    [AddComponentMenu("Hangeul/Player Shadow")]
    public sealed class PlayerShadow : MonoBehaviour
    {
        [Tooltip("따라갈 캐릭터 애니메이터. 비워 두면 부모에서 찾는다.")]
        [SerializeField] PlayerSpriteAnimator animator;
        [SerializeField] SpriteRenderer target;
        [Tooltip("평소 그림자(Shadow.png).")]
        [SerializeField] Sprite normal;
        [Tooltip("쓰러지는 동안의 그림자(방향별, Death_Shadow 시트). 캐릭터의 쓰러지는 모션과 같은 프레임 번호로 재생된다. 비어 있으면 평소 그림자가 그대로 보인다.")]
        [SerializeField] DirectionalClip dead = new DirectionalClip { fps = 10f, loop = false };

        void Awake()
        {
            if (animator == null) animator = GetComponentInParent<PlayerSpriteAnimator>();
            if (target == null) target = GetComponent<SpriteRenderer>();
        }

        // 캐릭터 애니메이터가 이번 프레임에 정한 모션을 본 뒤에 그림자를 맞추도록 LateUpdate
        void LateUpdate()
        {
            if (target == null) return;

            Sprite sprite = normal;
            if (animator != null && animator.CurrentAnim == PlayerAnim.Dead && dead.HasFrames)
            {
                var frames = dead.Get(animator.Facing);
                if (frames != null && frames.Length > 0) sprite = frames[Mathf.Min(animator.CurrentFrameIndex, frames.Length - 1)];
            }
            if (sprite != null && target.sprite != sprite) target.sprite = sprite;
        }
    }
}
