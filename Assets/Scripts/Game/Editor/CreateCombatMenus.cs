using System.Linq;
using Hangeul.Game.Character;
using Hangeul.Game.Combat;
using UnityEditor;
using UnityEngine;

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
