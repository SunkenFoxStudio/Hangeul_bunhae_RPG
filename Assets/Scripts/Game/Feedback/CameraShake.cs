using Unity.Cinemachine;
using UnityEngine;

namespace Hangeul.Game.Feedback
{
    /// <summary>화면 흔들림. <c>CameraShake.Shake(세기, 시간)</c>으로 부른다. 세기는 월드 단위(0.03 ≈ 3픽셀), 시간이 갈수록 약해진다.</summary>
    public static class CameraShake
    {
        public static void Shake(float strength, float seconds)
        {
            if (strength <= 0f || seconds <= 0f) return;
            var shaker = CameraShakeExtension.Instance;
            if (shaker != null) shaker.Begin(strength, seconds);
        }
    }
}
