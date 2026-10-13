using Hangeul.App;
using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>
    /// '가방 흔들기' 버튼. 누르면 인벤토리의 자모 카드들이 무작위로 회전/조립된다. 조립 모드에서만 보이고, 흔드는 동안에는 누를 수 없다.
    /// </summary>
    public sealed class ShakeBagButton : MonoBehaviour
    {
        [SerializeField] InventoryView inventory;
        [SerializeField] Button button;

        InventoryModel _model;

        // 모델은 InventoryView.Awake에서 만들어지므로 Start에서 구독한다. 비활성 오브젝트도 C# 이벤트는 받는다.
        void Start()
        {
            if (inventory == null) inventory = FindFirstObjectByType<InventoryView>();
            if (inventory == null) return;

            _model = inventory.Model;
            _model.ModeChanged += OnModeChanged;
            if (button != null) button.onClick.AddListener(inventory.ShakeBag);
            OnModeChanged(_model.Mode);
        }

        void OnDestroy()
        {
            if (_model != null) _model.ModeChanged -= OnModeChanged;
            if (button != null && inventory != null) button.onClick.RemoveListener(inventory.ShakeBag);
        }

        void Update()
        {
            if (button != null && inventory != null) button.interactable = !inventory.IsShaking;
        }

        void OnModeChanged(InventoryMode mode) => gameObject.SetActive(mode == InventoryMode.Assemble);
    }
}
