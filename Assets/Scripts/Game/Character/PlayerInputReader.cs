using UnityEngine;
using UnityEngine.InputSystem;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// Input System 액션 에셋(기본: Assets/InputSystem_Actions)의 Player/Move 값을 읽어 주는 얇은 래퍼.
    /// 입력 장치(키보드/패드)와 상태 머신을 분리하기 위한 지점이다.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        [SerializeField] InputActionAsset actions;
        [SerializeField] string moveAction = "Player/Move";

        [SerializeField] string interactAction = "Player/Interact";
        [SerializeField] string inventoryAction = "Player/Inventory";
        [SerializeField] string attackAction = "Player/Attack";

        InputAction _move;
        InputAction _interact;
        InputAction _inventory;
        InputAction _attack;

        public Vector2 Move => _move != null ? _move.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>이번 프레임에 상호작용 키를 눌렀는지. (Interact는 Hold 없이 짧게 누르는 방식이다)</summary>
        public bool InteractPressed => _interact != null && _interact.WasPressedThisFrame();

        /// <summary>이번 프레임에 인벤토리 열기/닫기 키(Tab)를 눌렀는지.</summary>
        public bool InventoryPressed => _inventory != null && _inventory.WasPressedThisFrame();

        /// <summary>이번 프레임에 공격 입력(기본: 마우스 왼쪽 버튼)을 눌렀는지.</summary>
        public bool AttackPressed => _attack != null && _attack.WasPressedThisFrame();

        /// <summary>마우스 포인터의 화면 좌표(마우스가 없으면 화면 가운데).</summary>
        public Vector2 PointerScreenPosition
            => Mouse.current != null ? Mouse.current.position.ReadValue() : new Vector2(Screen.width / 2f, Screen.height / 2f);

        /// <summary>안내 UI에 띄울 상호작용 키 이름(예: "E"). 키보드 바인딩을 우선하고, 없으면 첫 바인딩.</summary>
        public string InteractKeyLabel
        {
            get
            {
                if (_interact == null || _interact.bindings.Count == 0) return "E";
                int index = 0;
                for (int i = 0; i < _interact.bindings.Count; i++)
                    if (_interact.bindings[i].effectivePath.StartsWith("<Keyboard>")) { index = i; break; }
                string label = _interact.GetBindingDisplayString(index);
                return string.IsNullOrEmpty(label) ? "E" : label;
            }
        }

        void Awake()
        {
            if (actions == null) { Debug.LogError("PlayerInputReader: Input Actions 에셋이 지정되지 않았습니다.", this); return; }
            _move = actions.FindAction(moveAction, throwIfNotFound: false);
            if (_move == null) Debug.LogError($"PlayerInputReader: '{moveAction}' 액션을 찾을 수 없습니다.", this);
            _interact = actions.FindAction(interactAction, throwIfNotFound: false);
            if (_interact == null) Debug.LogError($"PlayerInputReader: '{interactAction}' 액션을 찾을 수 없습니다.", this);
            _inventory = actions.FindAction(inventoryAction, throwIfNotFound: false);
            if (_inventory == null) Debug.LogError($"PlayerInputReader: '{inventoryAction}' 액션을 찾을 수 없습니다.", this);
            _attack = actions.FindAction(attackAction, throwIfNotFound: false);
            if (_attack == null) Debug.LogError($"PlayerInputReader: '{attackAction}' 액션을 찾을 수 없습니다.", this);
        }

        void OnEnable() { _move?.Enable(); _interact?.Enable(); _inventory?.Enable(); _attack?.Enable(); }
        void OnDisable() { _move?.Disable(); _interact?.Disable(); _inventory?.Disable(); _attack?.Disable(); }

#if UNITY_EDITOR
        // 컴포넌트를 추가할 때 기본 액션 에셋을 자동으로 연결
        void Reset()
        {
            actions = UnityEditor.AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        }
#endif
    }
}
