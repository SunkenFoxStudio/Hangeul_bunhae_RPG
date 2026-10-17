using Hangeul.App;
using Hangeul.Game.Combat;
using Hangeul.Game.Feedback;
using Hangeul.UI;
using UnityEngine;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// 플레이어가 인벤토리의 아이템을 쓰는 창구. 인벤토리 가방 모드에서 완성 단어를 플레이어 위로 끌어다 놓으면
    /// 무기는 장착/해제, 음식은 먹고(체력 회복) 카드가 사라진다. 방어구는 아직 쓰지 않는다(라벨만 표시).
    /// 또한 공격 가능 여부를 정한다: <see cref="requireWeapon"/>이 켜져 있으면 무기를 장착해야 공격할 수 있다(<see cref="CanAttack"/>).
    /// 장착한 무기 단어에 상세 수치(DB 관리 씬에서 정한 피해량·범위·시간 등)가 있으면 근접 공격(<see cref="PlayerMeleeAttack"/>)에 적용하고,
    /// 수치를 따로 정하지 않은 무기는 근접 공격 컴포넌트의 기본 값으로 벤다. 접두어가 붙은 무기('화염검')는 접두어의 효과도 함께 넘긴다.
    /// 음식은 단어에 정해진 회복량만큼 회복한다.
    /// 장착한 무기의 공격 방식이 원거리(활)면 가방에 완성돼 있는 탄 단어(화살) 중 격자에서 가장 먼저 읽히는 것을 쏜다(<see cref="TryPickArrow"/>).
    /// 화살이 없으면 활은 쏠 수 없다(<see cref="CanShoot"/>, <see cref="NotifyNoArrow"/>). 화살은 줄지 않는다.
    /// 인벤토리 화면(<see cref="InventoryView"/>)과는 <see cref="IItemDropTarget"/>으로만 이어져 있다.
    /// </summary>
    [AddComponentMenu("Hangeul/Player Item User")]
    public sealed class PlayerItemUser : MonoBehaviour, IItemDropTarget
    {
        [Header("References")]
        [Tooltip("인벤토리 화면. 비워 두면 씬에서 찾는다.")]
        [SerializeField] InventoryView inventory;

        [Header("무기")]
        [Tooltip("켜면 무기를 장착해야 공격할 수 있다(장착하지 않으면 공격 불가). 인벤토리가 없는 씬에서 전투만 시험할 때는 끈다.")]
        [SerializeField] bool requireWeapon = true;

        [Header("무기가 없을 때 알림 (임시)")]
        [Tooltip("무기 없이 공격하려 할 때 머리 위에 잠깐 띄우는 문구(임시).")]
        [SerializeField] string noWeaponMessage = "무기가 없다";
        [Tooltip("알림 그림을 넣는 자리. 비어 있으면 위 문구를 글자 말풍선으로 띄우고, 그림을 넣으면 글자 대신 이 그림을 띄운다.")]
        [SerializeField] Sprite noWeaponIcon;

        [Header("화살이 없을 때 알림 (임시)")]
        [Tooltip("활을 장착했는데 가방에 화살이 없을 때 머리 위에 잠깐 띄우는 문구(임시).")]
        [SerializeField] string noArrowMessage = "화살이 없다";

        [Header("음식")]
        [Tooltip("회복량을 따로 정하지 않은 음식 하나를 먹을 때 회복하는 체력(하트 한 칸 = 체력 2). 단어마다의 회복량은 DB 관리 씬의 '상세 수치'에서 정한다.")]
        [SerializeField, Min(1)] int foodHeal = 2;

        [Header("끌어다 놓기")]
        [Tooltip("플레이어 그림 둘레로 넓혀 주는 여유(화면 픽셀). 클수록 놓기 쉽다.")]
        [SerializeField, Min(0f)] float dropPaddingPixels = 40f;

        Health _health;
        PlayerMeleeAttack _melee;
        PlayerRangedAttack _ranged;
        ItemStats _weaponStats;   // 장착한 무기 단어의 수치(없으면 null)
        InventoryModel _model;
        SpriteRenderer _sprite;
        bool _spriteSearched;
        FloatingNotice _notice;

        Health Health => _health != null ? _health : (_health = GetComponent<Health>());

        /// <summary>무기를 장착하고 있는지.</summary>
        public bool HasWeapon => inventory != null && inventory.Model != null && inventory.Model.EquippedWeapon != null;

        /// <summary>장착 중인 무기 단어(없으면 null).</summary>
        public string WeaponWord => HasWeapon ? inventory.Model.EquippedWeapon.Word : null;

        /// <summary>공격할 수 있는지: 무기 필수 설정이 꺼져 있거나 무기를 장착했을 때.</summary>
        public bool CanAttack => !requireWeapon || HasWeapon;

        /// <summary>장착한 무기가 원거리 무기(활)인지.</summary>
        public bool WeaponIsRanged => HasWeapon && _weaponStats != null && _weaponStats.attackStyle == AttackStyle.Ranged;

        /// <summary>
        /// 활이 쏠 화살을 가방에서 고른다: 완성돼 있는 탄 단어 중 격자에서 가장 먼저 읽히는 것(위 줄 → 왼쪽). 접두어가 붙은 화살('불화살')이면
        /// 그 효과를 modifier로 돌려준다(없으면 null). 가방에 화살이 없으면 false.
        /// </summary>
        public bool TryPickArrow(out string word, out WeaponModifier modifier)
        {
            word = null;
            modifier = null;
            if (_model == null) return false;

            var picked = _model.PickArrow();
            if (picked == null) return false;

            word = picked.Value.Word;
            var words = _model.Words;
            if (words.TryGetCompound(word, out string prefix, out _))   // '불화살' → 접두어 '불'의 효과
                modifier = WeaponModifier.From(prefix, words.StatsOf(prefix));
            return true;
        }

        /// <summary>쏠 수 있는지: 활이 아니면 항상 true(근접), 활이면 가방에 화살이 있을 때.</summary>
        public bool CanShoot => !WeaponIsRanged || TryPickArrow(out _, out _);

        /// <summary>활을 장착했는데 쏠 화살이 없다는 것을 머리 위 알림으로 알린다(임시 글자 말풍선).</summary>
        public void NotifyNoArrow() => ShowNotice(noArrowMessage, null);

        /// <summary>무기 없이 공격하려 했다는 것을 머리 위 알림으로 알린다(임시 글자 말풍선, <c>noWeaponIcon</c>에 그림을 넣으면 그림으로).</summary>
        public void NotifyNoWeapon() => ShowNotice(noWeaponMessage, noWeaponIcon);

        void ShowNotice(string message, Sprite icon)
        {
            if (_notice == null)
            {
                _notice = GetComponent<FloatingNotice>();
                if (_notice == null) _notice = gameObject.AddComponent<FloatingNotice>();
                if (inventory != null) _notice.UseFontIfNone(inventory.Font);   // 프로젝트 글꼴(한글)
            }
            _notice.Show(message, icon);
        }

        void Start()
        {
            if (inventory == null) inventory = FindFirstObjectByType<InventoryView>(FindObjectsInactive.Include);
            if (inventory != null)
            {
                PopupText.DefaultFont = inventory.Font;   // 적 머리 위 속성 글자도 인벤토리와 같은 한글 글꼴로
                inventory.DropTarget = this;
                _model = inventory.Model;
                if (_model != null) _model.EquipmentChanged += ApplyWeaponStats;
                ApplyWeaponStats();   // 저장된 장착이 이미 되살아나 있을 수 있다
            }
            else if (requireWeapon)
                Debug.LogWarning("[PlayerItemUser] 씬에서 InventoryView를 찾지 못했습니다. 무기를 장착할 수 없어 공격할 수 없습니다 (Require Weapon을 끄면 무기 없이 공격합니다).", this);
        }

        void OnDestroy()
        {
            if (_model != null) _model.EquipmentChanged -= ApplyWeaponStats;
            if (inventory != null && ReferenceEquals(inventory.DropTarget, this)) inventory.DropTarget = null;
        }

        // 장착한 무기 단어의 상세 수치를 근접 공격에 넘긴다(수치를 따로 정하지 않았거나 무기를 풀었으면 null → 인스펙터 값).
        // 원거리 무기(활)면 같은 수치를 원거리 공격에도 넘긴다.
        void ApplyWeaponStats()
        {
            if (_melee == null) _melee = GetComponent<PlayerMeleeAttack>();
            if (_ranged == null) _ranged = GetComponent<PlayerRangedAttack>();
            if (_melee == null || _model == null) return;

            var equipped = _model.EquippedWeapon;
            if (equipped == null)
            {
                _weaponStats = null;
                _melee.UseWeapon(null, null);
                if (_ranged != null) _ranged.UseWeapon(null, null);
                return;
            }

            var words = _model.Words;
            WeaponModifier modifier = null;
            if (words.TryGetCompound(equipped.Word, out string prefix, out _))   // '화염검' → 접두어 '화염'의 효과
                modifier = WeaponModifier.From(prefix, words.StatsOf(prefix));
            _weaponStats = words.StatsOf(equipped.Word);
            _melee.UseWeapon(_weaponStats, modifier);
            if (_ranged != null) _ranged.UseWeapon(_weaponStats, modifier);
        }

        // ───── IItemDropTarget ─────
        public bool Contains(Vector2 screenPosition)
        {
            var cam = Camera.main;
            if (cam == null) return false;

            if (_sprite == null && !_spriteSearched)   // 끄는 동안 포인터가 움직일 때마다 불리므로, 못 찾았어도 한 번만 찾는다
            {
                _spriteSearched = true;
                var animator = GetComponentInChildren<PlayerSpriteAnimator>();
                _sprite = animator != null ? animator.GetComponent<SpriteRenderer>() : null;   // 플레이어 그림(공격 이펙트의 것이 아니라)
            }

            Rect rect;
            if (_sprite != null)
            {
                var b = _sprite.bounds;
                Vector2 lo = cam.WorldToScreenPoint(b.min), hi = cam.WorldToScreenPoint(b.max);
                rect = Rect.MinMaxRect(Mathf.Min(lo.x, hi.x), Mathf.Min(lo.y, hi.y), Mathf.Max(lo.x, hi.x), Mathf.Max(lo.y, hi.y));
            }
            else
            {
                Vector2 center = cam.WorldToScreenPoint(transform.position);   // 그림을 못 찾으면 발밑 한 점 둘레
                rect = new Rect(center, Vector2.zero);
            }

            rect.xMin -= dropPaddingPixels;
            rect.xMax += dropPaddingPixels;
            rect.yMin -= dropPaddingPixels;
            rect.yMax += dropPaddingPixels;
            return rect.Contains(screenPosition);
        }

        public ItemUseAction Preview(ItemDrop item) => Decide(item);

        public ItemUseAction Use(ItemDrop item)
        {
            var action = Decide(item);
            if (action == ItemUseAction.Eat) Health.Heal(HealOf(item.Word));
            return action;
        }

        // 그 음식 단어에 정해 둔 회복량, 없으면 기본 회복량
        int HealOf(string word)
        {
            var stats = inventory != null && inventory.Model != null ? inventory.Model.Words.StatsOf(word) : null;
            return stats != null ? stats.heal : foodHeal;
        }

        ItemUseAction Decide(ItemDrop item)
        {
            var health = Health;
            if (health == null || health.IsDead) return ItemUseAction.None;
            return ItemUseRules.Decide(item.Kind, item.Equipped, health.Current, health.Max);
        }
    }
}
