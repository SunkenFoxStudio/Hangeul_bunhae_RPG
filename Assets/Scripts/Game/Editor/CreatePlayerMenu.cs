using Hangeul.Game.Character;
using Hangeul.Game.Combat;
using UnityEditor;
using UnityEngine;

namespace Hangeul.Game.Editor
{
    /// <summary>GameObject ▸ Hangeul ▸ Player : 플레이어 오브젝트를 올바른 설정으로 한 번에 만든다.</summary>
    public static class CreatePlayerMenu
    {
        [MenuItem("GameObject/Hangeul/Player", false, 10)]
        static void CreatePlayer()
        {
            var root = new GameObject("Player");
            Undo.RegisterCreatedObjectUndo(root, "Create Player");
            int playerLayer = LayerMask.NameToLayer(CombatLayers.Player);   // Hangeul ▸ Setup ▸ Combat Layers를 실행했다면 Player 레이어로
            if (playerLayer >= 0) root.layer = playerLayer;

            // 탑다운: 중력 없음, 회전 고정, 보간
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 발밑 충돌 영역(프레임 48x64px, PPU 100 기준 추정값). 실제 스프라이트에 맞춰 조정할 것.
            var box = root.AddComponent<BoxCollider2D>();
            box.size = new Vector2(0.16f, 0.08f);
            box.offset = new Vector2(0f, -0.10f);

            // Reset()이 Input Actions 에셋을 자동 연결한다
            root.AddComponent<PlayerInputReader>();
            root.AddComponent<Hangeul.Game.Interaction.InteractionSensor>();

            // 시각 요소는 자식으로 분리
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0;
            var animator = visual.AddComponent<PlayerSpriteAnimator>();

            CreateCombatMenus.AddPlayerHealth(root);         // 체력(하트 방식 정수). 인스펙터에서 최대 체력/무적 시간을 조절한다
            CreateCombatMenus.AddPlayerAttack(root);         // 공격 설정(판정/시간/피해)과 베기 이펙트 슬롯
            CreateCombatMenus.AddPlayerHurtbox(root);        // 피격 판정(몸 모양, 벽 충돌 콜라이더와 따로). Scene 뷰에서 크기를 맞춘다
            root.AddComponent<PlayerHitFeedback>();          // 맞았을 때 번쩍임/깜빡임/히트스톱/화면 흔들림

            var controller = root.AddComponent<PlayerController>();
            var so = new SerializedObject(controller);
            so.FindProperty("spriteAnimator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            PlayerAnimationSetup.AddPlayerShadow(root);   // 발밑 그림자

            Selection.activeGameObject = root;
        }
    }
}
