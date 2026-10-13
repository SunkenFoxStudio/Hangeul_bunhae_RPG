using Hangeul.Game.Character;

namespace Hangeul.Game.Interaction
{
    /// <summary>플레이어가 바라보고 상호작용 키를 눌러 사용할 수 있는 대상(NPC, 상자, 문 등). 콜라이더가 있는 오브젝트(또는 부모)에 구현한다.</summary>
    public interface IInteractable
    {
        /// <summary>화면에 띄울 안내 문구(예: "열기", "대화").</summary>
        string Prompt { get; }

        /// <summary>지금 상호작용할 수 있는지. false면 감지 대상에서 제외된다.</summary>
        bool CanInteract(PlayerController player);

        /// <summary>
        /// 상호작용 실행. 대화처럼 조작을 막아야 하면 여기서 <see cref="PlayerController.SetControlLocked"/>를 호출한다.
        /// </summary>
        void Interact(PlayerController player);
    }
}
