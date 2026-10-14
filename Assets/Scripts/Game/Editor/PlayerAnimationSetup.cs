using System.Collections.Generic;
using System.Linq;
using Hangeul.Game.Character;
using UnityEditor;
using UnityEngine;

namespace Hangeul.Game.Editor
{
    /// <summary>
    /// Hangeul ▸ Setup ▸ Player Animation : 플레이어의 방향별 애니메이션 슬롯에 시트의 모든 프레임을 자동으로 채운다.
    /// 시트(예: Idle_Down.png)는 이미 프레임별로 잘려 있으므로(Idle_Down_0 … _7) 그 스프라이트를 번호 순서로 슬롯에 넣는다.
    /// 기본 메뉴는 <b>비어 있거나 한 장뿐인 칸만</b> 채워서 직접 넣어 둔 여러 장짜리 애니메이션을 건드리지 않는다.
    /// </summary>
    public static class PlayerAnimationSetup
    {
        const string Root = "Assets/Sprites/The Female Adventurer - Free/";

        // (슬롯 이름, 폴더, 아래, 위, 왼쪽, 오른쪽 시트 파일 이름). 왼쪽/오른쪽은 시트의 Left_Down / Right_Down을 쓴다.
        static readonly (string slot, string folder, string down, string up, string left, string right)[] Sheets =
        {
            ("idle",   "Idle",  "Idle_Down",  "Idle_Up",  "Idle_Left_Down",  "Idle_Right_Down"),
            ("walk",   "Walk",  "walk_Down",  "walk_Up",  "walk_Left_Down",  "walk_Right_Down"),
            ("attack", "Dash",  "Dash_Down",  "Dash_Up",  "Dash_Left_Down",  "Dash_Right_Down"),   // 공격 그림이 없어 임시로 Dash 시트를 쓴다
            ("dead",   "Death", "death_Down", "death_Up", "death_Left_Down", "death_Right_Down"),
        };

        [MenuItem("Hangeul/Setup/Player Animation (프레임 자동 채우기)")]
        static void FillEmptySlots() => Fill(overwrite: false);

        [MenuItem("Hangeul/Setup/Player Animation (프레임 강제로 다시 채우기)")]
        static void FillAllSlots()
        {
            if (!EditorUtility.DisplayDialog("프레임 강제로 다시 채우기",
                    "직접 넣어 둔 것을 포함해 Idle/Walk/Attack/Dead 슬롯의 프레임을 시트의 프레임으로 모두 덮어씁니다. 계속할까요?", "덮어쓰기", "취소"))
                return;
            Fill(overwrite: true);
        }

        static void Fill(bool overwrite)
        {
            var animators = Object.FindObjectsByType<PlayerSpriteAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (animators.Length == 0)
            {
                Debug.LogWarning("[Animation] 씬에서 PlayerSpriteAnimator를 찾지 못했습니다.");
                return;
            }

            int filled = 0, skipped = 0;
            foreach (var animator in animators)
            {
                var so = new SerializedObject(animator);
                foreach (var sheet in Sheets)
                {
                    foreach (var (direction, file) in new[] { ("down", sheet.down), ("up", sheet.up), ("left", sheet.left), ("right", sheet.right) })
                    {
                        var frames = LoadFrames(Root + sheet.folder + "/" + file + ".png");
                        if (frames.Count == 0) continue;   // 시트가 없다(지웠다)

                        var array = so.FindProperty(sheet.slot + "." + direction);
                        if (array == null) continue;
                        if (!overwrite && array.arraySize > 1) { skipped++; continue; }   // 이미 여러 장이 들어 있다

                        array.arraySize = frames.Count;
                        for (int i = 0; i < frames.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                        filled++;
                    }
                }
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(animator);
            }

            Debug.Log($"[Animation] 프레임 {filled}칸을 채웠습니다" + (skipped > 0 ? $" (이미 여러 장이 있는 {skipped}칸은 그대로 둠)" : "") +
                      ". Attack 슬롯은 공격 그림이 없어 임시로 Dash 시트를 썼습니다. 씬을 저장하세요.");
        }

        // ───── 발밑 그림자 ─────
        // 쓰러지는 동안의 그림자 시트: Death_Shadow 폴더(방향별 8프레임)
        static readonly (string direction, string file)[] DeadShadowSheets =
        {
            ("down", "death_normal_down"), ("up", "death_normal_up"),
            ("left", "death_normal_left_down"), ("right", "death_normal_right_down"),
        };

        [MenuItem("Hangeul/Setup/Player Shadow (발밑 그림자)")]
        static void SetupPlayerShadow()
        {
            int made = 0;
            foreach (var animator in Object.FindObjectsByType<PlayerSpriteAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (animator.GetComponentInParent<PlayerController>() == null) continue;
                var player = animator.GetComponentInParent<PlayerController>();
                if (player.GetComponentInChildren<PlayerShadow>(true) != null) continue;
                AddPlayerShadow(player.gameObject);
                made++;
            }
            Debug.Log(made > 0 ? $"[Animation] 플레이어 {made}명에 발밑 그림자(Shadow)를 붙였습니다. 씬을 저장하세요."
                               : "[Animation] 이미 그림자가 있거나 플레이어를 찾지 못했습니다.");
        }

        /// <summary>
        /// 플레이어 자식으로 "Shadow"를 만든다: 캐릭터 그림(Visual)과 같은 위치에, 한 칸 아래 정렬 순서로 놓고,
        /// 그림자 시트(PPU 100)와 캐릭터 시트(PPU 16)의 월드 크기가 같아지도록 배율을 맞춘다.
        /// </summary>
        internal static void AddPlayerShadow(GameObject player)
        {
            var animator = player.GetComponentInChildren<PlayerSpriteAnimator>(true);
            if (animator == null) return;
            var visual = animator.GetComponent<SpriteRenderer>() ?? animator.GetComponentInChildren<SpriteRenderer>();

            var normal = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Shadow.png")
                         ?? AssetDatabase.LoadAllAssetRepresentationsAtPath(Root + "Shadow.png").OfType<Sprite>().FirstOrDefault();
            if (normal == null)
            {
                Debug.LogWarning("[Animation] Shadow.png를 찾지 못했습니다: " + Root + "Shadow.png");
                return;
            }

            // 캐릭터 프레임의 PPU(보이는 그림이 있으면 그것, 없으면 Idle 시트)
            var frameSprite = visual != null ? visual.sprite : null;
            if (frameSprite == null)
                frameSprite = LoadFrames(Root + "Idle/Idle_Down.png").FirstOrDefault();
            float framePpu = frameSprite != null ? frameSprite.pixelsPerUnit : normal.pixelsPerUnit;
            float scale = normal.pixelsPerUnit / framePpu;   // 두 시트의 한 칸이 같은 월드 크기가 되도록

            var go = new GameObject("Shadow");
            Undo.RegisterCreatedObjectUndo(go, "Create Player Shadow");
            go.transform.SetParent(player.transform, false);
            var source = visual != null ? visual.transform : animator.transform;
            go.transform.localPosition = source.localPosition;
            go.transform.localRotation = source.localRotation;
            go.transform.localScale = Vector3.Scale(source.localScale, new Vector3(scale, scale, 1f));

            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = normal;
            if (visual != null)
            {
                renderer.sortingLayerID = visual.sortingLayerID;
                renderer.sortingOrder = visual.sortingOrder - 1;   // 캐릭터 바로 아래
            }

            var shadow = go.AddComponent<PlayerShadow>();
            var so = new SerializedObject(shadow);
            so.FindProperty("animator").objectReferenceValue = animator;
            so.FindProperty("target").objectReferenceValue = renderer;
            so.FindProperty("normal").objectReferenceValue = normal;
            foreach (var (direction, file) in DeadShadowSheets)
            {
                var frames = LoadFrames(Root + "Death_Shadow/" + file + ".png");
                if (frames.Count == 0) continue;
                var array = so.FindProperty("dead." + direction);
                array.arraySize = frames.Count;
                for (int i = 0; i < frames.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 시트에서 잘라 둔 프레임(이름 끝의 번호 순서)
        static List<Sprite> LoadFrames(string path)
        {
            return AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .OfType<Sprite>()
                .OrderBy(s => TrailingNumber(s.name))
                .ToList();
        }

        static int TrailingNumber(string name)
        {
            int i = name.LastIndexOf('_');
            return i >= 0 && int.TryParse(name.Substring(i + 1), out int n) ? n : 0;
        }
    }
}
