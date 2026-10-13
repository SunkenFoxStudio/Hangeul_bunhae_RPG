using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Hangeul.Game.Inventory
{
    /// <summary>
    /// CinemachineCamera의 결과 위치에 가로 오프셋(월드 단위)을 더하는 확장. 값은 직렬화하지 않는 런타임 값이라
    /// Play 중에 바뀌어도 "Cinemachine 설정이 씬에 반영됩니다" 확인창이 뜨지 않는다.
    /// <see cref="InventoryCameraShift"/>가 매 프레임 <see cref="ShiftX"/>를 갱신한다.
    /// </summary>
    [AddComponentMenu("Hangeul/Inventory Camera Offset")]
    public sealed class InventoryCameraOffset : CinemachineExtension
    {
        [NonSerialized] public float ShiftX;

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Aim || Mathf.Approximately(ShiftX, 0f)) return;
            state.PositionCorrection += state.GetCorrectedOrientation() * new Vector3(ShiftX, 0f, 0f);
        }
    }
}
