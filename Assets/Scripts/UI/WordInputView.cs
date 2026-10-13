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
