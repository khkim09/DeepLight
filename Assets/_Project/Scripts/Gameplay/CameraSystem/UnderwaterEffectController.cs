using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>현재 활성 카메라 높이에 따라 수중 풀스크린 효과를 on/off 한다.</summary>
    public class UnderwaterEffectController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform activeCameraTransform; // 현재 수중 판정에 사용할 카메라 Transform
        [SerializeField] private Material underwaterMaterial; // Full Screen Pass에 연결된 공유 머티리얼

        [Header("Water Settings")]
        [SerializeField] private float waterSurfaceY = 0f; // 수면 월드 Y
        [SerializeField] private float enterOffset = 0f;   // 수중 진입 보정
        [SerializeField] private float exitOffset = 0.1f;  // 수중 이탈 보정

        [Header("Debug")]
        [SerializeField] private bool logStateChanges = false; // 상태 변경 로그 여부

        private static readonly int EffectEnabledId = Shader.PropertyToID("_EffectEnabled");

        private bool isUnderwater; // 현재 수중 상태 캐시

        /// <summary>현재 수중 상태를 반환한다.</summary>
        public bool IsUnderwater => isUnderwater;

        /// <summary>현재 수중 판정 기준 카메라를 교체하고 즉시 상태를 반영한다.</summary>
        public void SetActiveCamera(Transform cameraTransform)
        {
            activeCameraTransform = cameraTransform;
            ApplyCurrentState(forceApply: true);
        }

        /// <summary>초기 머티리얼 상태를 동기화한다.</summary>
        private void Awake()
        {
            ApplyCurrentState(forceApply: true);
        }

        /// <summary>매 프레임 활성 카메라 높이를 기준으로 수중 상태를 갱신한다.</summary>
        private void LateUpdate()
        {
            ApplyCurrentState(forceApply: false);
        }

        /// <summary>활성 카메라 높이를 기준으로 수중 상태를 계산하고 머티리얼에 반영한다.</summary>
        private void ApplyCurrentState(bool forceApply)
        {
            if (underwaterMaterial == null || activeCameraTransform == null)
                return;

            float cameraY = activeCameraTransform.position.y;

            // 수면 경계에서 깜빡임을 줄이기 위해 진입/이탈 기준을 분리한다.
            bool nextState = isUnderwater
                ? cameraY < (waterSurfaceY + exitOffset)
                : cameraY < (waterSurfaceY + enterOffset);

            if (!forceApply && nextState == isUnderwater)
                return;

            isUnderwater = nextState;

            // 셰이더의 수중 효과 활성값을 직접 토글한다.
            underwaterMaterial.SetFloat(EffectEnabledId, isUnderwater ? 1f : 0f);

            if (logStateChanges)
            {
                Debug.Log(
                    $"[UnderwaterEffectController] isUnderwater={isUnderwater}, " +
                    $"camera={(activeCameraTransform != null ? activeCameraTransform.name : "None")}, " +
                    $"cameraY={cameraY:F3}, waterSurfaceY={waterSurfaceY:F3}");
            }
        }

        /// <summary>에디터 값 수정 시 머티리얼 기본 상태를 리셋한다.</summary>
        private void OnValidate()
        {
            if (!Application.isPlaying && underwaterMaterial != null)
                underwaterMaterial.SetFloat(EffectEnabledId, 0f);
        }
    }
}
