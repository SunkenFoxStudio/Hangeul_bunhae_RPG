using Hangeul.Game.Character;
using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.Game.Interaction
{
    /// <summary>
    /// 상호작용 대상 위에 "단어 / [E] 줍기" 안내를 띄운다(화면 좌표계 Canvas가 월드의 대상을 따라간다).
    /// 플레이어의 <see cref="InteractionSensor.Current"/>를 매 프레임 보고, 인벤토리를 열었거나 상호작용 중이면(= 조작 가능 상태가 아니면) 숨긴다.
    /// 제목(<see cref="IInteractableTitle"/>)이 없는 대상은 "[E] Prompt"만 보인다. 결과 문구(가방 가득 참 등)는 <see cref="InteractionFeedback"/>로 받는다.
    /// </summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] PlayerController player;
        [SerializeField] RectTransform panel;
        [SerializeField] CanvasGroup panelGroup;
        [SerializeField] Text titleText;
        [SerializeField] Text hintText;
        [SerializeField] Text messageText;
        [Tooltip("대상 콜라이더 위쪽 가운데에서 화면 픽셀로 얼마나 더 띄울지.")]
        [SerializeField] Vector2 screenOffset = new Vector2(0f, 24f);
        [SerializeField, Min(0.1f)] float messageSeconds = 1.6f;

        Camera _cam;
        IInteractable _shown;
        Component _followTarget;
        float _messageUntil = -1f;

        void OnEnable()
        {
            InteractionFeedback.Message += ShowMessage;
            // 위치는 모든 LateUpdate(= Cinemachine이 카메라를 옮기는 시점)가 끝난 뒤, 캔버스를 그리기 직전에 맞춘다.
            // LateUpdate에서 맞추면 카메라가 갱신되기 전 위치로 계산돼 움직일 때 한 프레임씩 뒤처진다.
            Canvas.willRenderCanvases += FollowTarget;
        }

        void OnDisable()
        {
            InteractionFeedback.Message -= ShowMessage;
            Canvas.willRenderCanvases -= FollowTarget;
        }

        const float BoxSpacing = 4f;   // 이름 칸과 안내 칸 사이 간격

        void Awake()
        {
            // 예전 방식(레이아웃 컴포넌트)으로 만든 안내 UI가 씬에 남아 있어도 이 뷰가 크기를 정하도록 치운다.
            // (씬 파일에서도 지우려면 InteractionCanvas를 지우고 메뉴로 다시 만든다.)
            foreach (var c in GetComponentsInChildren<ContentSizeFitter>(true)) Destroy(c);
            foreach (var c in GetComponentsInChildren<LayoutGroup>(true)) Destroy(c);
        }

        void Start()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (panelGroup != null) panelGroup.alpha = 0f;
            if (messageText != null) messageText.gameObject.SetActive(false);
        }

        void ShowMessage(string message)
        {
            if (messageText == null) return;
            messageText.text = message;
            messageText.gameObject.SetActive(true);
            _messageUntil = Time.unscaledTime + messageSeconds;
        }

        void LateUpdate()
        {
            UpdateMessage();
            if (player == null || panel == null || panelGroup == null) return;

            var current = player.Sensor != null ? player.Sensor.Current : null;
            var target = current as Component;
            bool show = target != null && player.IsFree;

            if (show)
            {
                if (!ReferenceEquals(_shown, current)) Refresh(current);
                _followTarget = target;
            }
            else
            {
                // 사라지는 동안에도 마지막 대상을 계속 따라간다(대상을 놓으면 안내가 화면에 붙어 카메라와 함께 미끄러져 보인다).
                // 대상이 이미 없어졌으면(주워서 파괴됨) FollowTarget이 알아서 멈춘다.
                _shown = null;
            }
            panelGroup.alpha = Mathf.MoveTowards(panelGroup.alpha, show ? 1f : 0f, Time.unscaledDeltaTime * 12f);
            if (!show && panelGroup.alpha <= 0f) _followTarget = null;
        }

        void Refresh(IInteractable current)
        {
            _shown = current;
            string title = (current as IInteractableTitle)?.Title;
            bool hasTitle = !string.IsNullOrEmpty(title);
            if (titleText != null)
            {
                // 이름 칸(배경 포함)을 통째로 켜고 끈다. 칸이 따로 없으면 글자만 토글한다.
                var box = titleText.transform.parent;
                (box != null && box != panel ? box.gameObject : titleText.gameObject).SetActive(hasTitle);
                titleText.text = hasTitle ? title : "";
            }
            if (hintText != null) hintText.text = $"[{player.Input.InteractKeyLabel}] {current.Prompt}";
            LayoutBoxes(hasTitle);
        }

        // 두 칸의 폭/높이를 글자 길이에 맞추고, 안내 칸을 패널 맨 아래에 두고 그 위에 이름 칸을 쌓는다(패널 피벗은 아래 가운데).
        void LayoutBoxes(bool hasTitle)
        {
            float y = 0f;
            if (hintText != null) y += Fit(hintText, y) + BoxSpacing;
            if (titleText != null && hasTitle) Fit(titleText, y);
        }

        // 글자 칸 text가 들어 있는 상자(바로 위 부모)를 글자 크기 + 안쪽 여백에 맞추고 y에 놓는다. 상자의 높이를 돌려준다.
        float Fit(Text text, float y)
        {
            var box = text.transform.parent as RectTransform;
            if (box == null || box == panel) return 0f;   // 상자 없이 패널에 바로 붙은 예전 구조

            var margin = text.rectTransform.offsetMin;    // 글자 칸의 오프셋이 곧 안쪽 여백(가로, 세로)
            var size = new Vector2(Mathf.Ceil(text.preferredWidth) + margin.x * 2f, Mathf.Ceil(text.preferredHeight) + margin.y * 2f);
            box.sizeDelta = size;
            box.anchoredPosition = new Vector2(0f, y);
            return size.y;
        }

        void FollowTarget()
        {
            if (_followTarget == null || panel == null) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;
            var target = _followTarget;

            // 대상 콜라이더의 위쪽 가운데. 콜라이더가 없으면 오브젝트 위치.
            var col = target.GetComponentInChildren<Collider2D>();
            Vector3 world = col != null ? new Vector3(col.bounds.center.x, col.bounds.max.y, target.transform.position.z) : target.transform.position;
            Vector3 screen = _cam.WorldToScreenPoint(world) + (Vector3)screenOffset;
            panel.position = new Vector3(Mathf.Round(screen.x), Mathf.Round(screen.y), screen.z);   // 픽셀에 맞춰 흔들림을 없앤다
        }

        void UpdateMessage()
        {
            if (messageText == null || !messageText.gameObject.activeSelf) return;
            float left = _messageUntil - Time.unscaledTime;
            if (left <= 0f) { messageText.gameObject.SetActive(false); return; }
            var c = messageText.color;
            c.a = Mathf.Clamp01(left / 0.4f);   // 마지막 0.4초 동안 사라진다
            messageText.color = c;
        }
    }
}
