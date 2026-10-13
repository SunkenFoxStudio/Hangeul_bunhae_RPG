using Hangeul.App;
using Hangeul.Game.Character;
using Hangeul.UI;
using UnityEngine;
using UnityEngine.Events;

namespace Hangeul.Game.Interaction
{
    /// <summary>
    /// 맵에 놓인 아이템. 가까이 가면 안내 UI에 단어와 "[E] 줍기"가 뜨고, 상호작용하면 그 단어의 글자 카드가 인벤토리 빈 칸에 들어온다.
    /// 카드를 전부 놓을 빈 칸이 없으면 줍지 않고(아이템은 그대로 남음) 안내 문구만 띄운다.
    /// Collider2D가 필요하다(Trigger 가능). 이미지는 같은 오브젝트의 SpriteRenderer에 넣는다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class WorldItem : MonoBehaviour, IInteractable, IInteractableTitle
    {
        [Tooltip("아이템 단어 (한글 음절만). 인벤토리에 이 단어의 글자 카드가 들어온다.")]
        [SerializeField] string word = "검";
        [SerializeField] string verb = "줍기";
        [SerializeField] string inventoryFullMessage = "가방이 가득 찼습니다";
        [Tooltip("비워 두면 씬에서 InventoryView를 찾는다.")]
        [SerializeField] InventoryView inventory;
        [Tooltip("주운 뒤 오브젝트를 없앤다. 끄면 오브젝트는 남고 다시 상호작용할 수 없다(연출을 직접 처리할 때).")]
        [SerializeField] bool destroyOnPickup = true;
        [SerializeField] UnityEvent onPickedUp;
        [SerializeField] UnityEvent onInventoryFull;

        bool _picked;

        public string Word => word;
        public string Title => word;
        public string Prompt => verb;
        public bool CanInteract(PlayerController player) => !_picked && ItemWordSet.IsValidWord(word);

        public void Interact(PlayerController player)
        {
            if (_picked) return;
            if (inventory == null) inventory = FindFirstObjectByType<InventoryView>();
            if (inventory == null)
            {
                Debug.LogWarning($"[WorldItem] 씬에 InventoryView가 없어 '{word}'을(를) 주울 수 없습니다.", this);
                return;
            }

            if (!inventory.Model.CanAddWord(word))
            {
                Debug.Log($"[WorldItem] '{word}': 가방에 빈 칸이 모자라 줍지 못했습니다.", this);
                InteractionFeedback.Show(inventoryFullMessage);
                onInventoryFull.Invoke();
                return;
            }

            inventory.AddWord(word);
            _picked = true;
            Debug.Log($"[WorldItem] '{word}' 획득", this);
            onPickedUp.Invoke();
            if (destroyOnPickup) Destroy(gameObject);
        }

#if UNITY_EDITOR
        // Scene 뷰에서 배치할 때 어떤 단어인지 보이도록
        void OnDrawGizmos()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.25f, word);
        }

        void OnValidate()
        {
            if (!ItemWordSet.IsValidWord(word)) Debug.LogWarning($"[WorldItem] '{word}'은(는) 한글 음절로만 이루어진 단어가 아닙니다.", this);
        }
#endif
    }
}
