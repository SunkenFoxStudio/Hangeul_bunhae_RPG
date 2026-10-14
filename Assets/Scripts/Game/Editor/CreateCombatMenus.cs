using System.Linq;
using Hangeul.Game.Character;
using Hangeul.Game.Combat;
using Hangeul.Game.Enemy;
using Hangeul.Game.Feedback;
using Hangeul.Game.Hud;
using Hangeul.Game.World;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Hangeul.Game.Editor
{
    /// <summary>
    /// 전투 설정 메뉴.
    /// Hangeul ▸ Setup ▸ Combat Layers : 전투용 레이어(Player/Enemy/PlayerAttack/EnemyAttack)를 프로젝트에 추가하고,
    /// 충돌 매트릭스를 정리하고(공격끼리·같은 편 공격은 서로/자기 편 몸과 부딪히지 않음), 씬의 플레이어를 Player 레이어로 옮긴다.
    /// GameObject ▸ Hangeul ▸ Damage Zone (테스트용) : 닿으면 피해를 주는 구역을 만든다(피격/무적/밀려남/사망 확인용).
    /// </summary>
    public static class CreateCombatMenus
    {
        const string SetupMenu = "Hangeul/Setup/Combat Layers";

        [MenuItem(SetupMenu)]
        static void SetupCombatLayers()
        {
            AddLayers();
            ConfigureCollisionMatrix();
            AssignPlayerLayer();
            EnsurePlayerHealth();
            AssetDatabase.SaveAssets();
            Debug.Log("[Combat] 전투 레이어를 설정했습니다: " + string.Join(", ", CombatLayers.All));
        }

        // 비어 있는 사용자 레이어 칸(8번부터)에 없는 이름만 추가한다
        static void AddLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            foreach (var name in CombatLayers.All)
            {
                bool exists = false;
                for (int i = 0; i < layers.arraySize && !exists; i++) exists = layers.GetArrayElementAtIndex(i).stringValue == name;
                if (exists) continue;

                bool added = false;
                for (int i = 8; i < layers.arraySize && !added; i++)
                {
                    var slot = layers.GetArrayElementAtIndex(i);
                    if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                    slot.stringValue = name;
                    added = true;
                }
                if (!added) Debug.LogWarning($"[Combat] 레이어 '{name}'을 추가할 빈 칸이 없습니다.");
            }
            tagManager.ApplyModifiedProperties();
        }

        // 공격(투사체 등)은 자기 편 몸과 다른 공격에 부딪히지 않는다. 월드(벽)와 상대 편 몸에는 부딪힌다.
        static void ConfigureCollisionMatrix()
        {
            int player = LayerMask.NameToLayer(CombatLayers.Player), enemy = LayerMask.NameToLayer(CombatLayers.Enemy);
            int playerAttack = LayerMask.NameToLayer(CombatLayers.PlayerAttack), enemyAttack = LayerMask.NameToLayer(CombatLayers.EnemyAttack);
            if (new[] { player, enemy, playerAttack, enemyAttack }.Any(l => l < 0))
            {
                Debug.LogWarning("[Combat] 전투 레이어가 모두 만들어지지 않아 충돌 매트릭스는 건드리지 않았습니다. 메뉴를 한 번 더 실행해 보세요.");
                return;
            }

            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/Physics2DSettings.asset");
            if (settings.Length == 0) return;
            var so = new SerializedObject(settings[0]);
            var matrix = so.FindProperty("m_LayerCollisionMatrix");
            if (matrix == null || !matrix.isArray)
            {
                Debug.LogWarning("[Combat] Physics2D 충돌 매트릭스를 찾지 못했습니다. Project Settings ▸ Physics 2D에서 직접 정리하세요.");
                return;
            }

            void Ignore(int a, int b)
            {
                var ea = matrix.GetArrayElementAtIndex(a);
                var eb = matrix.GetArrayElementAtIndex(b);
                ea.uintValue &= ~(1u << b);
                eb.uintValue &= ~(1u << a);
            }

            Ignore(playerAttack, player);        // 내 공격은 내 몸을 때리지 않는다
            Ignore(playerAttack, playerAttack);
            Ignore(playerAttack, enemyAttack);   // 공격끼리는 부딪히지 않는다
            Ignore(enemyAttack, enemy);
            Ignore(enemyAttack, enemyAttack);
            so.ApplyModifiedProperties();
        }

        static void AssignPlayerLayer()
        {
            int layer = LayerMask.NameToLayer(CombatLayers.Player);
            if (layer < 0) return;
            foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.gameObject.layer == layer) continue;
                Undo.RecordObject(player.gameObject, "Set Player Layer");
                player.gameObject.layer = layer;
            }
        }

        // 플레이어에 Health 컴포넌트를 미리 붙여 인스펙터에서 보이게 한다(없어도 PlayerController가 실행할 때 자동으로 붙이지만 눈에 안 보인다).
        static void EnsurePlayerHealth()
        {
            foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (player.GetComponent<Health>() == null) AddPlayerHealth(player.gameObject);
        }

        internal static void AddPlayerHealth(GameObject player)
        {
            var health = Undo.AddComponent<Health>(player);
            var so = new SerializedObject(health);
            so.FindProperty("team").enumValueIndex = (int)Team.Player;
            so.FindProperty("maxHealth").intValue = 6;
            so.FindProperty("invincibleSeconds").floatValue = 1f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ───── 하트 HUD / 타격감 연출 / 체크포인트 ─────
        [MenuItem("Hangeul/Setup/Combat HUD & Feedback")]
        static void SetupHudAndFeedback()
        {
            int made = 0;

            // 플레이어: 맞았을 때 번쩍임/깜빡임/히트스톱/화면 흔들림
            foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.GetComponent<PlayerHitFeedback>() != null) continue;
                Undo.AddComponent<PlayerHitFeedback>(player.gameObject);
                made++;
            }

            // 카메라 흔들림 확장
            foreach (var cam in Object.FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (cam.GetComponent<CameraShakeExtension>() != null) continue;
                Undo.AddComponent<CameraShakeExtension>(cam.gameObject);
                made++;
            }

            // 화면 왼쪽 위 하트
            if (Object.FindFirstObjectByType<HeartHudView>(FindObjectsInactive.Include) == null)
            {
                var go = new GameObject("HeartHUD", typeof(Canvas), typeof(CanvasScaler));
                Undo.RegisterCreatedObjectUndo(go, "Create Heart HUD");
                var canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 3;   // 안내 UI(5), 인벤토리(10)보다 아래
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                go.AddComponent<HeartHudView>();
                made++;
            }

            // 죽어서 돌아갈 때의 화면 페이드
            if (Object.FindFirstObjectByType<ScreenFader>(FindObjectsInactive.Include) == null)
            {
                var go = new GameObject("ScreenFader");
                Undo.RegisterCreatedObjectUndo(go, "Create Screen Fader");
                go.AddComponent<ScreenFader>().EnsureBuilt();
                made++;
            }

            Debug.Log(made > 0 ? $"[Combat] 하트 HUD / 타격감 연출 / 화면 페이드 설정을 {made}개 추가했습니다. 씬을 저장하세요."
                               : "[Combat] 이미 모두 설정되어 있습니다.");
        }

        [MenuItem("GameObject/Hangeul/Checkpoint", false, 20)]
        static void CreateCheckpoint()
        {
            var go = new GameObject("Checkpoint");
            Undo.RegisterCreatedObjectUndo(go, "Create Checkpoint");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            sr.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            go.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;

            var checkpoint = go.AddComponent<Checkpoint>();
            var so = new SerializedObject(checkpoint);
            so.FindProperty("sprite").objectReferenceValue = sr;
            so.ApplyModifiedPropertiesWithoutUndo();

            var view = SceneView.lastActiveSceneView;
            if (view != null) go.transform.position = new Vector3(view.pivot.x, view.pivot.y, 0f);
            Selection.activeGameObject = go;
            Debug.Log("체크포인트를 만들었습니다. 플레이어가 닿으면 켜져 색이 바뀌고, 죽으면 가장 최근에 켠 체크포인트에서 되살아납니다.");
        }

        // ───── 플레이어 공격 ─────
        [MenuItem("Hangeul/Setup/Player Attack")]
        static void SetupPlayerAttack()
        {
            int count = 0;
            foreach (var player in Object.FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (player.GetComponent<PlayerMeleeAttack>() != null) continue;
                AddPlayerAttack(player.gameObject);
                count++;
            }
            Debug.Log(count > 0 ? $"[Combat] 플레이어 {count}명에 공격 설정(PlayerMeleeAttack + Slash)을 붙였습니다. 씬을 저장하세요."
                                 : "[Combat] 이미 모든 플레이어에 공격 설정이 있습니다.");
        }

        // 공격 설정(판정/시간/피해)과 베기 이펙트 오브젝트("Slash")를 붙인다. 인스펙터에서 보이고 슬롯에 그래픽을 넣을 수 있다.
        internal static void AddPlayerAttack(GameObject player)
        {
            var melee = Undo.AddComponent<PlayerMeleeAttack>(player);

            var slash = new GameObject("Slash");
            Undo.RegisterCreatedObjectUndo(slash, "Create Slash");
            slash.transform.SetParent(player.transform, false);
            var renderer = slash.AddComponent<SpriteRenderer>();
            renderer.sortingOrder = 1;
            renderer.enabled = false;
            var effect = slash.AddComponent<AttackEffect>();

            var effectSo = new SerializedObject(effect);
            effectSo.FindProperty("target").objectReferenceValue = renderer;
            effectSo.ApplyModifiedPropertiesWithoutUndo();

            var meleeSo = new SerializedObject(melee);
            meleeSo.FindProperty("effect").objectReferenceValue = effect;
            meleeSo.ApplyModifiedPropertiesWithoutUndo();
        }

        [MenuItem("GameObject/Hangeul/Training Dummy (테스트용)", false, 18)]
        static void CreateTrainingDummy()
        {
            var go = new GameObject("TrainingDummy");
            Undo.RegisterCreatedObjectUndo(go, "Create Training Dummy");
            int enemyLayer = LayerMask.NameToLayer(CombatLayers.Enemy);   // Hangeul ▸ Setup ▸ Combat Layers를 실행했다면 Enemy 레이어
            if (enemyLayer >= 0) go.layer = enemyLayer;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            sr.color = new Color(1f, 0.7f, 0.25f, 1f);
            sr.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

            var box = go.AddComponent<BoxCollider2D>();
            box.size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size : Vector2.one;   // 보이는 크기와 똑같이(콜라이더가 더 크면 투명한 벽처럼 느껴진다)

            var health = go.AddComponent<Health>();
            var so = new SerializedObject(health);
            so.FindProperty("team").enumValueIndex = (int)Team.Enemy;
            so.FindProperty("maxHealth").intValue = 5;
            so.FindProperty("invincibleSeconds").floatValue = 0.2f;
            so.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<TrainingDummy>();

            var view = SceneView.lastActiveSceneView;
            if (view != null) go.transform.position = new Vector3(view.pivot.x, view.pivot.y, 0f);
            Selection.activeGameObject = go;
            Debug.Log("훈련 인형을 만들었습니다. 플레이어로 공격하면 Console에 피해가 적힙니다(체력 5, 죽으면 2초 뒤 되살아남).");
        }

        // ───── 적 ─────
        [MenuItem("GameObject/Hangeul/Enemy (슬라임 샘플)", false, 19)]
        static void CreateSlime()
        {
            var root = new GameObject("Slime");
            Undo.RegisterCreatedObjectUndo(root, "Create Slime");
            int enemyLayer = LayerMask.NameToLayer(CombatLayers.Enemy);          // Hangeul ▸ Setup ▸ Combat Layers를 실행했다면 Enemy 레이어
            if (enemyLayer >= 0) root.layer = enemyLayer;

            // 탑다운: 중력 없음, 회전 고정, 보간
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.mass = 3f;                                                       // 플레이어가 살짝 부딪혀도 많이 밀리지 않게
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // 그림(임시: 초록 동그라미). 실제 스프라이트는 Visual의 SpriteRenderer에 넣는다.
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
            sr.color = new Color(0.45f, 0.85f, 0.4f, 1f);
            var animator = visual.AddComponent<EnemySpriteAnimator>();

            // 몸통(단단한 충돌). 보이는 그림과 같은 크기로
            Vector2 size = sr.sprite != null ? (Vector2)sr.sprite.bounds.size * 0.5f : new Vector2(0.3f, 0.3f);
            var box = root.AddComponent<BoxCollider2D>();
            box.size = size;

            // 몸통 접촉 피해: 몸보다 살짝 큰 Trigger. 피격/죽음 중에는 EnemyController가 끈다.
            var zoneGo = new GameObject("ContactZone");
            zoneGo.transform.SetParent(root.transform, false);
            int attackLayer = LayerMask.NameToLayer(CombatLayers.EnemyAttack);
            if (attackLayer >= 0) zoneGo.layer = attackLayer;
            var zoneCol = zoneGo.AddComponent<BoxCollider2D>();
            zoneCol.isTrigger = true;
            zoneCol.size = size + new Vector2(0.04f, 0.04f);
            var zone = zoneGo.AddComponent<DamageZone>();

            var health = root.AddComponent<Health>();
            var healthSo = new SerializedObject(health);
            healthSo.FindProperty("team").enumValueIndex = (int)Team.Enemy;
            healthSo.FindProperty("maxHealth").intValue = 3;
            healthSo.FindProperty("invincibleSeconds").floatValue = 0.15f;
            healthSo.ApplyModifiedPropertiesWithoutUndo();

            var controller = root.AddComponent<EnemyController>();
            var so = new SerializedObject(controller);
            so.FindProperty("contactZone").objectReferenceValue = zone;
            so.FindProperty("spriteAnimator").objectReferenceValue = animator;
            so.ApplyModifiedPropertiesWithoutUndo();

            var view = SceneView.lastActiveSceneView;
            if (view != null) root.transform.position = new Vector3(view.pivot.x, view.pivot.y, 0f);
            Selection.activeGameObject = root;
            Debug.Log("슬라임 샘플 적을 만들었습니다. 플레이어가 가까이 오면 쫓아오고, 닿으면 피해(1)를 줍니다. Visual의 SpriteRenderer에 그림을 넣으세요.");
        }

        [MenuItem("GameObject/Hangeul/Damage Zone (테스트용)", false, 17)]
        static void CreateDamageZone()
        {
            var go = new GameObject("DamageZone");
            Undo.RegisterCreatedObjectUndo(go, "Create Damage Zone");

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            sr.color = new Color(1f, 0.2f, 0.2f, 0.45f);
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(1f, 1f);

            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1f, 1f);
            go.AddComponent<DamageZone>();

            var view = SceneView.lastActiveSceneView;
            if (view != null) go.transform.position = new Vector3(view.pivot.x, view.pivot.y, 0f);
            Selection.activeGameObject = go;
            Debug.Log("테스트용 피해 구역을 만들었습니다. 플레이어가 들어가면 체력이 깎입니다(Damage Zone ▸ Damage/Knockback 조절).");
        }
    }
}
