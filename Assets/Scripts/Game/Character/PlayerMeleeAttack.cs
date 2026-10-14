using System;
using System.Collections.Generic;
using Hangeul.App;
using Hangeul.Game.Combat;
using UnityEngine;

namespace Hangeul.Game.Character
{
    /// <summary>
    /// 플레이어의 4방향 근접 공격 설정과 판정. 공격 한 번의 흐름(준비 → 판정 → 마무리)은 <see cref="PlayerAttackState"/>가 정하고,
    /// 이 컴포넌트는 "얼마나 세게, 어디를, 언제까지" 때리는지와 실제 판정(<see cref="Strike"/>)을 맡는다.
    /// 판정은 시간(초)으로만 정하므로 공격 애니메이션 프레임이 한 장이어도, 나중에 애니메이션이 들어와도 똑같이 동작한다.
    /// Scene 뷰에서 이 컴포넌트가 붙은 오브젝트를 선택하면 4방향의 판정 상자가 노란 선으로 보인다.
    /// 무기 아이템이 생기면 피해량/범위는 장착한 무기가 덮어쓴다(후속).
    /// </summary>
    [AddComponentMenu("Hangeul/Player Melee Attack")]
    public sealed class PlayerMeleeAttack : MonoBehaviour
    {
        [Header("피해")]
        [SerializeField, Min(1)] int damage = 1;
        [Tooltip("맞은 쪽이 공격 방향으로 밀려나는 속도(유니티 단위/초).")]
        [SerializeField, Min(0f)] float knockbackSpeed = 4f;
        [Tooltip("맞은 쪽이 움직일 수 없는 시간(초).")]
        [SerializeField, Min(0f)] float stunSeconds = 0.3f;

        [Header("판정 범위 (유니티 단위, 1유닛 = 100px)")]
        [Tooltip("공격 시작점에서 앞쪽으로 뻗는 길이.")]
        [SerializeField, Min(0.01f)] float reach = 0.4f;
        [Tooltip("앞쪽 상자의 폭(공격 방향의 수직 방향 길이).")]
        [SerializeField, Min(0.01f)] float width = 0.4f;
        [Tooltip("공격 시작점: 플레이어 위치에서의 오프셋. 보통 몸통 가운데쯤.")]
        [SerializeField] Vector2 originOffset = new Vector2(0f, -0.05f);

        [Header("시간 (초) — 애니메이션 프레임 수와 무관")]
        [Tooltip("준비 동작. 이 동안은 아직 맞지 않는다.")]
        [SerializeField, Min(0f)] float windupSeconds = 0.08f;
        [Tooltip("판정이 켜져 있는 시간.")]
        [SerializeField, Min(0.01f)] float activeSeconds = 0.12f;
        [Tooltip("마무리 동작. 판정은 끝났지만 아직 움직이거나 다시 공격할 수 없다.")]
        [SerializeField, Min(0f)] float recoverySeconds = 0.18f;

        [Header("움직임 / 방향")]
        [Tooltip("준비·판정 동안 공격하는 쪽으로 살짝 나아가는 속도(유니티 단위/초). 공격 동작 그림이 없어도 손맛이 나게 한다.")]
        [SerializeField, Min(0f)] float lungeSpeed = 0.8f;
        [Tooltip("켜면 마우스가 있는 쪽(상하좌우 중 가장 가까운 방향)으로 공격한다. 끄면 바라보던 방향으로 공격한다.")]
        [SerializeField] bool attackTowardsMouse;

        [Header("그래픽 (비어 있어도 된다)")]
        [Tooltip("베기 이펙트를 그리는 오브젝트. 비워 두면 실행할 때 임시 이펙트(반투명 사각형)를 자동으로 만든다.")]
        [SerializeField] AttackEffect effect;

        readonly Collider2D[] _buffer = new Collider2D[24];

        /// <summary>공격이 누군가를 실제로 때렸을 때(인자: 때린 대상 수). 타격감 연출(히트스톱, 화면 흔들림)이 받는다.</summary>
        public event Action<int> Struck;

        public AttackTimeline Timeline => new AttackTimeline(windupSeconds, activeSeconds, recoverySeconds);
        public float LungeSpeed => lungeSpeed;

        /// <summary>이번 공격의 방향: 마우스 조준이 켜져 있으면 마우스 쪽, 아니면 지금 바라보는 방향.</summary>
        public Facing ChooseFacing(PlayerController player)
        {
            if (!attackTowardsMouse) return player.Facing;

            var cam = Camera.main;
            if (cam == null) return player.Facing;
            Vector3 mouse = cam.ScreenToWorldPoint(player.Input.PointerScreenPosition);
            Vector2 toMouse = (Vector2)mouse - (Vector2)transform.position;
            return toMouse.sqrMagnitude < 1e-4f ? player.Facing : FacingUtil.FromVector(toMouse);
        }

        /// <summary>해당 방향의 판정 상자(월드 좌표)의 중심과 크기.</summary>
        public void GetBox(Facing facing, out Vector2 center, out Vector2 size)
        {
            Vector2 dir = FacingUtil.ToVector(facing);
            Vector2 origin = (Vector2)transform.position + originOffset;
            var box = MeleeBox.Compute(dir.x, dir.y, origin.x, origin.y, reach, width);
            center = new Vector2(box.cx, box.cy);
            size = new Vector2(box.w, box.h);
        }

        /// <summary>
        /// 판정 상자 안의 피해 가능한 대상을 때린다. already에 든 대상은 이번 공격에서 이미 때렸으므로 건너뛰고, 새로 때린 대상은 거기에 넣는다.
        /// 때리는 데 성공한(체력이 깎인) 대상의 수를 돌려준다. 맞은 대상의 무적 때문에 거절된 경우는 세지 않는다.
        /// </summary>
        public int Strike(Facing facing, HashSet<IDamageable> already)
        {
            GetBox(facing, out var center, out var size);

            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true };
            filter.SetLayerMask(CombatLayers.TargetMask(Team.Player));
            int count = Physics2D.OverlapBox(center, size, 0f, filter, _buffer);

            Vector2 dir = FacingUtil.ToVector(facing);
            int hits = 0;
            for (int i = 0; i < count; i++)
            {
                var target = _buffer[i].GetComponentInParent<IDamageable>();
                if (target == null || (target is Component c && c.gameObject == gameObject)) continue;   // 자기 자신은 제외
                if (!already.Add(target)) continue;

                var info = new DamageInfo(damage, Team.Player, dir * knockbackSpeed, stunSeconds, null, gameObject);
                if (target.TakeDamage(info)) hits++;
            }
            if (hits > 0) Struck?.Invoke(hits);
            return hits;
        }

        /// <summary>베기 이펙트를 판정 시간 동안 보여준다.</summary>
        public void ShowEffect(Facing facing)
        {
            if (effect == null) effect = AttackEffect.CreateDefault(transform);
            GetBox(facing, out var center, out var size);
            effect.Play(facing, center, size, activeSeconds + 0.04f);
        }

        public void HideEffect()
        {
            if (effect != null) effect.Hide();
        }

        // Scene 뷰에서 4방향 판정 상자를 보여준다(범위를 조절할 때 편하다)
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.9f);
            foreach (Facing f in System.Enum.GetValues(typeof(Facing)))
            {
                GetBox(f, out var center, out var size);
                Gizmos.DrawWireCube(center, size);
            }
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere((Vector2)transform.position + originOffset, 0.02f);
        }
    }
}
