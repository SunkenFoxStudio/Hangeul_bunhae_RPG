using Hangeul.App;
using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>조립 / 가방 모드 전환 버튼. 누르면 모드가 바뀌고, 라벨에 현재 모드가 표시된다.</summary>
    public sealed class ModeToggleView : MonoBehaviour
    {
        [SerializeField] InventoryView inventory;
        [SerializeField] Button button;
        [SerializeField] Text label;
        [SerializeField] string assembleLabel = "조립 모드";
        [SerializeField] string bagLabel = "가방 모드";

        InventoryModel _model;

        // InventoryView.Awake에서 모델이 만들어지므로 구독은 Start에서 한다
        void Start()
        {
            if (inventory == null) inventory = GetComponentInParent<InventoryView>() ?? FindFirstObjectByType<InventoryView>();
            if (inventory == null) return;

            _model = inventory.Model;
            _model.ModeChanged += OnModeChanged;
            if (button != null) button.onClick.AddListener(inventory.ToggleMode);
            OnModeChanged(_model.Mode);
        }

        void OnDestroy()
        {
            if (_model != null) _model.ModeChanged -= OnModeChanged;
            if (button != null && inventory != null) button.onClick.RemoveListener(inventory.ToggleMode);
        }

        void OnModeChanged(InventoryMode mode)
        {
            if (label != null) label.text = mode == InventoryMode.Bag ? bagLabel : assembleLabel;
        }
    }
}
