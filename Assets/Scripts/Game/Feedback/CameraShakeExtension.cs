using Unity.Cinemachine;
using UnityEngine;

namespace Hangeul.Game.Feedback
{
    /// <summary>
    /// CinemachineCamera의 결과 위치를 잠깐 흔드는 확장. 값은 직렬화하지 않는 런타임 값이라 Play 중에 바뀌어도
    /// "Cinemachine 설정이 씬에 반영됩니다" 확인창이 뜨지 않는다(<see cref="Hangeul.Game.Inventory.InventoryCameraOffset"/>과 같은 방식).
    /// 씬에 없으면 처음 흔들 때 CinemachineCamera에 자동으로 붙는다(Hangeul ▸ Setup ▸ Combat HUD &amp; Feedback 메뉴로 미리 붙일 수 있다).
    /// 흔들림 시간은 멈추지 않는 시간으로 재서, 히트스톱으로 게임이 멈춘 동안에도 흔들린다.
    /// </summary>
    [AddComponentMenu("Hangeul/Camera Shake Extension")]
    public sealed class CameraShakeExtension : CinemachineExtension
    {
        static CameraShakeExtension _instance;

        float _strength, _duration, _until;

        public static CameraShakeExtension Instance
        {
            get
            {
                if (_instance == null) _instance = FindFirstObjectByType<CameraShakeExtension>();
                if (_instance == null)
                {
                    var cam = FindFirstObjectByType<CinemachineCamera>();
                    if (cam != null) _instance = cam.gameObject.AddComponent<CameraShakeExtension>();
                }
                return _instance;
            }
        }

        public void Begin(float strength, float seconds)
        {
            float remaining = Mathf.Max(0f, _until - Time.unscaledTime);
            float current = _duration > 0f ? _strength * (remaining / _duration) : 0f;
            _strength = Mathf.Max(strength, current);   // 이미 흔들리는 중이면 더 센 쪽으로
            _duration = seconds;
            _until = Time.unscaledTime + seconds;
        }

        protected override void PostPipelineStageCallback(
            CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
        {
            if (stage != CinemachineCore.Stage.Aim) return;
            float remaining = _until - Time.unscaledTime;
            if (remaining <= 0f || _duration <= 0f) return;

            Vector2 jitter = Random.insideUnitCircle * (_strength * (remaining / _duration));
            state.PositionCorrection += state.GetCorrectedOrientation() * new Vector3(jitter.x, jitter.y, 0f);
        }
    }
}
