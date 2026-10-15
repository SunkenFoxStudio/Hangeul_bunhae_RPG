using UnityEditor;
using UnityEngine;

namespace Hangeul.Game.Editor
{
    /// <summary>
    /// Hangeul ▸ Setup ▸ Window (창 모드, 크기 조절) : 빌드한 게임을 창 모드로 시작하고, 창 가장자리를 드래그해서 크기를 바꿀 수 있게 한다.
    /// Edit ▸ Project Settings ▸ Player ▸ Resolution and Presentation의 값을 코드로 바꿔 주는 것뿐이다(직접 바꿔도 같다).
    /// 게임 화면에서 Alt+Enter로 전체 화면과 창 모드를 오갈 수 있는 설정(Allow Fullscreen Switch)은 그대로 둔다.
    /// </summary>
    public static class SetupWindowMenu
    {
        // 1080p 모니터에서도 제목 표시줄을 포함해 화면 안에 들어오는 시작 크기. 한 번 늘리거나 줄이면 다음 실행부터는 마지막 크기로 열린다.
        const int StartWidth = 1280, StartHeight = 720;

        [MenuItem("Hangeul/Setup/Window (창 모드, 크기 조절)")]
        static void Apply()
        {
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.defaultScreenWidth = StartWidth;
            PlayerSettings.defaultScreenHeight = StartHeight;
            Debug.Log($"[Setup] 창 모드 + 크기 조절 가능으로 바꿨습니다 (시작 크기 {StartWidth}×{StartHeight}). 빌드를 다시 만들어야 반영됩니다.");
        }
    }
}
