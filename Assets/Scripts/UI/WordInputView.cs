using Hangeul.App;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Hangeul.UI
{
    /// <summary>
    /// 디버그/치트용 단어 입력창(HTML 하단 입력창 대응). 단어를 입력하고 Enter 또는 버튼을 누르면 글자 카드가 격자에 추가된다.
    /// 아이템 획득 연동이 생기면 이 창은 개발 빌드 전용으로 남긴다.
    /// </summary>
    public sealed class WordInputView : MonoBehaviour
    {
        [SerializeField] InventoryView inventory;
        [SerializeField] InputField input;
        [SerializeField] Button addButton;

        InventoryModel _model;

        // 모델은 InventoryView.Awake에서 만들어지므로 Start에서 구독한다. 비활성 오브젝트도 C# 이벤트는 받으므로 스스로를 껐다 켤 수 있다.
        void Start()
        {
            if (inventory == null) return;
            _model = inventory.Model;
            _model.ModeChanged += OnModeChanged;
            OnModeChanged(_model.Mode);
        }

        void OnDestroy()
        {
            if (_model != null) _model.ModeChanged -= OnModeChanged;
        }

        // 단어 입력창은 조립 모드에서만 보인다 (가방 모드에서는 숨김)
        void OnModeChanged(InventoryMode mode) => gameObject.SetActive(mode != InventoryMode.Bag);

        void OnEnable()
        {
            if (addButton != null) addButton.onClick.AddListener(Submit);
            if (input != null) input.onEndEdit.AddListener(OnEndEdit);
        }

        void OnDisable()
        {
            if (addButton != null) addButton.onClick.RemoveListener(Submit);
            if (input != null) input.onEndEdit.RemoveListener(OnEndEdit);
        }

        // onEndEdit은 포커스를 잃을 때도 오므로 Enter를 눌렀을 때만 추가한다
        void OnEndEdit(string _)
        {
            var kb = Keyboard.current;
            if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) Submit();
        }

        public void Submit()
        {
            if (inventory == null || input == null || string.IsNullOrWhiteSpace(input.text)) return;
            var r = inventory.AddWord(input.text.Trim());
            if (r.HadBanned) Debug.Log("그런 글자는 만들 수 없습니다 (됬 ✗ 됐 ○)");
            input.text = "";
            input.ActivateInputField();
        }
    }
}
