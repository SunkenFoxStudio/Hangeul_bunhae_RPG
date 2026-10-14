using Hangeul.Game.Character;
using UnityEngine;

namespace Hangeul.Game.World
{
    /// <summary>
    /// 체크포인트. 플레이어가 Trigger 콜라이더에 닿으면 켜지고, 죽으면 가장 최근에 켠 체크포인트에서 되살아난다.
    /// 켜진 체크포인트는 색이 바뀐다(그림이 들어오면 <see cref="inactiveColor"/>/<see cref="activeColor"/> 대신 스프라이트를 바꾸는 식으로 확장한다).
    /// 되살아나는 위치는 기본적으로 이 오브젝트의 위치이고, <see cref="respawnPoint"/>를 지정하면 그 위치다.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [Tooltip("되살아날 위치. 비워 두면 이 오브젝트의 위치.")]
        [SerializeField] Transform respawnPoint;
        [SerializeField] SpriteRenderer sprite;
        [SerializeField] Color inactiveColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        [SerializeField] Color activeColor = new Color(1f, 0.85f, 0.3f, 1f);

        public Vector2 RespawnPosition => respawnPoint != null ? (Vector2)respawnPoint.position : (Vector2)transform.position;
        public bool IsActive { get; private set; }

        void Reset()
        {
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;
        }

        void Awake()
        {
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            Apply();
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<PlayerController>() != null) Checkpoints.Activate(this);
        }

        public void SetActive(bool active)
        {
            IsActive = active;
            Apply();
        }

        void Apply()
        {
            if (sprite != null) sprite.color = IsActive ? activeColor : inactiveColor;
        }
    }
}
