using System;
using Hangeul.Game.Character;
using UnityEngine;

namespace Hangeul.Game.Interaction
{
    /// <summary>
    /// 플레이어 중심의 원형 범위 안에 들어온 <see cref="IInteractable"/> 중 가장 가까운 것을 찾는다(방향 무관).
    /// 대상이 바뀌면 <see cref="CurrentChanged"/>가 발생하므로 "E 열기" 같은 안내 UI는 여기에 연결하면 된다.
    /// </summary>
    public sealed class InteractionSensor : MonoBehaviour
    {
        [SerializeField] Vector2 originOffset;
        [SerializeField, Min(0.01f)] float radius = 0.35f;
        [SerializeField] LayerMask layers = ~0;

        [Header("Gizmo")]
        [Tooltip("켜면 오브젝트를 선택하지 않아도 Scene 뷰에 상호작용 범위를 항상 표시한다.")]
        [SerializeField] bool alwaysShowGizmo = true;

        readonly Collider2D[] _hits = new Collider2D[16];
        ContactFilter2D _filter;
        PlayerController _player;

        public IInteractable Current { get; private set; }
        public event Action<IInteractable> CurrentChanged;

        void Awake()
        {
            _player = GetComponent<PlayerController>();
            _filter = new ContactFilter2D { useTriggers = true };
            _filter.SetLayerMask(layers);
        }

        void Update()
        {
            var found = Find();
            if (ReferenceEquals(found, Current)) return;
            Current = found;
            CurrentChanged?.Invoke(Current);
        }

        IInteractable Find()
        {
            if (_player == null) return null;

            Vector2 center = Center;
            int count = Physics2D.OverlapCircle(center, radius, _filter, _hits);

            IInteractable best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var target = _hits[i].GetComponentInParent<IInteractable>();
                if (target == null || !target.CanInteract(_player)) continue;

                float d = ((Vector2)_hits[i].ClosestPoint(center) - center).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = target; }
            }
            return best;
        }

        Vector2 Center => (Vector2)transform.position + originOffset;

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (alwaysShowGizmo) DrawRange();
        }

        void OnDrawGizmosSelected()
        {
            if (!alwaysShowGizmo) DrawRange();
        }

        void DrawRange()
        {
            // 대상이 잡히면 초록, 아니면 노랑
            Gizmos.color = Current != null ? Color.green : Color.yellow;
            Gizmos.DrawWireSphere(Center, radius);

            Gizmos.color = new Color(1f, 1f, 1f, 0.5f);
            Gizmos.DrawLine(transform.position, Center);   // 오프셋 확인용
        }
#endif
    }
}
