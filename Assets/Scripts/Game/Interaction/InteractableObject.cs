using Hangeul.Game.Character;
using UnityEngine;
using UnityEngine.Events;

namespace Hangeul.Game.Interaction
{
    /// <summary>
    /// 코드 없이 인스펙터만으로 쓰는 기본 상호작용 대상. Collider2D가 필요하다(Trigger 가능).
    /// 실제 NPC/상자 등은 이 클래스를 참고해 <see cref="IInteractable"/>을 직접 구현하면 된다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class InteractableObject : MonoBehaviour, IInteractable
    {
        [SerializeField] string prompt = "조사하기";
        [SerializeField] bool singleUse;
        [SerializeField] UnityEvent onInteract;

        bool _used;

        public string Prompt => prompt;
        public bool CanInteract(PlayerController player) => !(singleUse && _used);

        public void Interact(PlayerController player)
        {
            _used = true;
            Debug.Log($"[Interact] {name}", this);
            onInteract.Invoke();
        }
    }
}
