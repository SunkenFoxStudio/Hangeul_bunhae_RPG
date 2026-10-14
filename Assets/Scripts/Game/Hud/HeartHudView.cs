using System.Collections.Generic;
using Hangeul.App;
using Hangeul.Game.Character;
using Hangeul.Game.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.Game.Hud
{
    /// <summary>
    /// 화면 왼쪽 위의 체력 하트 표시. 하트 한 칸 = 체력 2(반 칸 표시, <see cref="HeartDisplay"/>)이고,
    /// 플레이어의 <see cref="Health"/>가 바뀔 때마다 갱신된다. 하트 이미지는 실행할 때 코드로 만든다(씬에 칸마다 오브젝트를 두지 않는다).
    /// 꽉 찬/반 칸/빈 하트 그림은 슬롯에 넣는다. 비어 있으면 임시 픽셀 하트가 나온다.
    /// 맞아서 체력이 줄면 하트가 잠깐 커졌다 돌아온다.
    /// </summary>
    [AddComponentMenu("Hangeul/Heart HUD")]
    public sealed class HeartHudView : MonoBehaviour
    {
        [Tooltip("체력을 보여줄 대상. 비워 두면 씬의 플레이어를 찾는다.")]
        [SerializeField] Health health;
        [Header("하트 그림 (비어 있어도 된다)")]
        [SerializeField] Sprite fullHeart;
        [SerializeField] Sprite halfHeart;
        [SerializeField] Sprite emptyHeart;
        [Header("배치 (화면 픽셀, 1920×1080 기준)")]
        [SerializeField, Min(8f)] float heartSize = 56f;
        [SerializeField, Min(0f)] float spacing = 8f;
        [Tooltip("화면 왼쪽 위 모서리에서 떨어진 거리.")]
        [SerializeField] Vector2 margin = new Vector2(32f, 32f);

        readonly List<Image> _images = new List<Image>();
        RectTransform _container;
        int _lastCurrent = -1;
        float _popUntil;

        void Start()
        {
            if (health == null)
            {
                var player = FindFirstObjectByType<PlayerController>();
                if (player != null) health = player.Health;
            }
            if (health == null)
            {
                Debug.LogWarning("[HeartHud] 체력을 보여줄 플레이어를 찾지 못했습니다.", this);
                enabled = false;
                return;
            }

            var go = new GameObject("Hearts", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _container = (RectTransform)go.transform;
            _container.anchorMin = _container.anchorMax = new Vector2(0f, 1f);
            _container.pivot = new Vector2(0f, 1f);
            _container.anchoredPosition = new Vector2(margin.x, -margin.y);

            health.Changed += OnChanged;
            Refresh();
        }

        void OnDestroy()
        {
            if (health != null) health.Changed -= OnChanged;
        }

        void OnChanged(Health h) => Refresh();

        void Refresh()
        {
            var states = HeartDisplay.Compute(health.Current, health.Max);
            while (_images.Count < states.Length) _images.Add(CreateImage(_images.Count));
            for (int i = 0; i < _images.Count; i++)
            {
                bool used = i < states.Length;
                _images[i].gameObject.SetActive(used);
                if (used) _images[i].sprite = SpriteFor(states[i]);
            }

            if (_lastCurrent >= 0 && health.Current < _lastCurrent) _popUntil = Time.unscaledTime + 0.25f;   // 맞았다: 하트가 잠깐 커진다
            _lastCurrent = health.Current;
        }

        Sprite SpriteFor(HeartState state)
        {
            switch (state)
            {
                case HeartState.Full: return fullHeart != null ? fullHeart : HeartSprites.Full;
                case HeartState.Half: return halfHeart != null ? halfHeart : HeartSprites.Half;
                default: return emptyHeart != null ? emptyHeart : HeartSprites.Empty;
            }
        }

        Image CreateImage(int index)
        {
            var go = new GameObject("Heart " + index, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_container, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(heartSize, heartSize);
            rt.anchoredPosition = new Vector2(index * (heartSize + spacing), 0f);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            return image;
        }

        void Update()
        {
            if (_container == null) return;
            float u = Mathf.Clamp01((_popUntil - Time.unscaledTime) / 0.25f);
            _container.localScale = Vector3.one * (1f + 0.25f * Mathf.Sin(u * Mathf.PI));
        }
    }
}
