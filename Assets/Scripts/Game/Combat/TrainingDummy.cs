using UnityEngine;

namespace Hangeul.Game.Combat
{
    /// <summary>
    /// 공격을 시험하는 훈련 인형(테스트용). 맞으면 잠깐 색이 변하고 맞은 방향으로 흔들리며, 맞은 내용을 Console에 적는다.
    /// 체력이 0이 되면 사라졌다가 일정 시간 뒤 되살아난다. 적을 만들기 전에 공격의 판정/피해/밀려남을 확인하는 용도다.
    /// 게임 시간(<c>Time.time</c>) 기준이라 일시정지 중에는 멈춘다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        [SerializeField] SpriteRenderer sprite;
        [Tooltip("죽은 뒤 되살아나기까지 걸리는 시간(초).")]
        [SerializeField, Min(0.1f)] float reviveSeconds = 2f;
        [SerializeField] Color hitColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField, Min(0.01f)] float flashSeconds = 0.12f;
        [Tooltip("맞았을 때 맞은 방향으로 흔들리는 거리(유니티 단위).")]
        [SerializeField, Min(0f)] float wobble = 0.05f;

        Health _health;
        Collider2D[] _colliders;
        Vector3 _home;
        Vector2 _kick;
        Color _baseColor = Color.white;
        float _flashUntil, _reviveAt = -1f;

        // 이미 만든 인형의 충돌 영역이 그림보다 커서 "투명한 벽"처럼 막힐 때: 컴포넌트 우클릭 ▸ 이 메뉴
        [ContextMenu("충돌 영역을 그림 크기에 맞추기")]
        void FitColliderToSprite()
        {
            var box = GetComponent<BoxCollider2D>();
            var renderer = sprite != null ? sprite : GetComponentInChildren<SpriteRenderer>();
            if (box == null || renderer == null || renderer.sprite == null) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(box, "Fit Collider");
#endif
            box.size = renderer.sprite.bounds.size;
            box.offset = renderer.sprite.bounds.center;
        }

        void Awake()
        {
            _health = GetComponent<Health>();
            _colliders = GetComponents<Collider2D>();
            if (sprite == null) sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null) _baseColor = sprite.color;
            _home = transform.position;
        }

        void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        void OnDamaged(Health health, DamageInfo info, int taken)
        {
            Debug.Log($"[훈련 인형] {taken} 피해 (남은 체력 {health.Current}/{health.Max}), 밀려남 {info.Knockback}", this);
            _flashUntil = Time.time + flashSeconds;
            _kick = info.Knockback.sqrMagnitude > 0f ? info.Knockback.normalized * wobble : Vector2.zero;
        }

        void OnDied(Health health)
        {
            Debug.Log("[훈련 인형] 쓰러짐", this);
            _reviveAt = Time.time + reviveSeconds;
            foreach (var c in _colliders) c.enabled = false;
            if (sprite != null) sprite.enabled = false;
        }

        void Update()
        {
            if (_reviveAt >= 0f && Time.time >= _reviveAt)
            {
                _reviveAt = -1f;
                _health.Revive();
                foreach (var c in _colliders) c.enabled = true;
                if (sprite != null) sprite.enabled = true;
            }

            bool flashing = Time.time < _flashUntil;
            if (sprite != null) sprite.color = flashing ? hitColor : _baseColor;
            // 맞은 방향으로 밀렸다가 제자리로 돌아오는 작은 흔들림
            float u = flashing ? (_flashUntil - Time.time) / flashSeconds : 0f;
            transform.position = _home + (Vector3)(_kick * u);
        }
    }
}
