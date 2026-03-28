using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Data.CameraSystem;
using Project.Data.Input;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>SO 기반 고정 카메라값과 유저 감도를 사용해 탐사 카메라를 처리하는 클래스이다.</summary>
    public class ExplorationFollowCameraController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform target; // 추적 대상 잠수함

        [Header("Data")]
        [SerializeField] private ExplorationCameraTuningSO cameraTuning; // 탐사 카메라 기본값 SO
        [SerializeField] private GameInputBindingsSO inputBindings;      // 공용 입력 바인딩 SO

        [Header("User Settings")]
        [SerializeField] private ExplorationCameraUserSettings userSettings = default; // 유저 감도 옵션

        private ExplorationCameraRuntimeSettings runtimeSettings;

        private float currentDistanceZ;
        private float distanceVelocity;
        private Quaternion smoothedTargetRotation;
        private float localYaw;
        private float localPitch;

        /// <summary>자유 시야 입력이 유지 중인지 반환한다.</summary>
        public bool IsFreeLookActive => inputBindings != null && Input.GetKey(inputBindings.FreeLookKey);

        /// <summary>연출 등에 의해 카메라 입력이 잠겨있는지 여부이다.</summary>
        public bool IsInputLocked { get; set; }

        /// <summary>카메라가 잠수함과 독립적으로 정렬 중인지 여부이다.</summary>
        public bool IsIndependentAlignment { get; set; }

        /// <summary>새 추적 대상을 설정한다.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;

            if (target != null)
                smoothedTargetRotation = target.rotation;
        }

        /// <summary>카메라의 평면 전방 벡터를 반환한다.</summary>
        public Vector3 GetPlanarForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            return forward.normalized;
        }

        /// <summary>런타임 세팅을 초기화한다.</summary>
        private void Awake()
        {
            RebuildRuntimeSettings();
        }

        /// <summary>초기 카메라 위치를 준비한다.</summary>
        private void Start()
        {
            if (cameraTuning == null)
                return;

            currentDistanceZ = runtimeSettings.IdleDistanceZ;
            SnapToTarget();
        }

        /// <summary>에디터 값 변경 시 런타임 세팅을 다시 계산한다.</summary>
        private void OnValidate()
        {
            RebuildRuntimeSettings();
        }

        /// <summary>매 프레임 후방 추적과 자유시점을 갱신한다.</summary>
        private void LateUpdate()
        {
            if (cameraTuning == null || inputBindings == null)
                return;

            if (target == null || IsIndependentAlignment)
                return;

            if (!IsInputLocked)
            {
                if (IsFreeLookActive)
                {
                    localYaw += Input.GetAxis("Mouse X") * runtimeSettings.MouseSensitivityX * Time.deltaTime;
                    localPitch -= Input.GetAxis("Mouse Y") * runtimeSettings.MouseSensitivityY * Time.deltaTime;
                    localPitch = Mathf.Clamp(localPitch, runtimeSettings.MinPitch, runtimeSettings.MaxPitch);
                }
                else
                {
                    // 자유시점을 떼면 기본 후방 시점으로 자연스럽게 복귀한다.
                    localYaw = Mathf.Lerp(localYaw, 0f, 8f * Time.deltaTime);
                    localPitch = Mathf.Lerp(localPitch, 0f, 8f * Time.deltaTime);
                }
            }

            localYaw = Mathf.Repeat(localYaw + 180f, 360f) - 180f;

            float targetZ = runtimeSettings.IdleDistanceZ;
            bool isBoost = Input.GetKey(inputBindings.BoostKey);

            if (Input.GetKey(inputBindings.MoveForwardKey))
                targetZ = isBoost ? runtimeSettings.BoostForwardDistanceZ : runtimeSettings.NormalForwardDistanceZ;
            else if (Input.GetKey(inputBindings.MoveBackwardKey))
                targetZ = isBoost ? runtimeSettings.BoostBackwardDistanceZ : runtimeSettings.NormalBackwardDistanceZ;

            currentDistanceZ = Mathf.SmoothDamp(
                currentDistanceZ,
                targetZ,
                ref distanceVelocity,
                runtimeSettings.DistanceSmoothTime);

            Vector3 baseOffset = new Vector3(0f, runtimeSettings.DefaultOffsetY, currentDistanceZ);

            float pullInFactor = Mathf.Abs(localYaw) / 180f;
            Vector3 targetLocalOffset = Vector3.Lerp(baseOffset, runtimeSettings.FreeLookTopOffset, pullInFactor);

            float decayFactor = 1f - Mathf.Exp(-runtimeSettings.RotationFollowSpeed * Time.deltaTime);
            smoothedTargetRotation = Quaternion.Slerp(smoothedTargetRotation, target.rotation, decayFactor);

            Quaternion localCamRotation = Quaternion.Euler(localPitch, localYaw, 0f);
            Quaternion finalCamRotation = smoothedTargetRotation * localCamRotation;

            transform.position = target.position + (finalCamRotation * targetLocalOffset);
            transform.LookAt(target.position, finalCamRotation * Vector3.up);
        }

        /// <summary>특정 타깃을 향한 독립 카메라 정렬 연출을 수행한다.</summary>
        public async UniTask AlignToTargetIndependentlyAsync(Transform lookTarget, float duration, CancellationToken token)
        {
            if (lookTarget == null)
                return;

            IsInputLocked = true;
            IsIndependentAlignment = true;

            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                float t = Mathf.Clamp01(elapsed / duration);
                float curveT = Mathf.SmoothStep(0f, 1f, t);

                Vector3 directionToTarget = lookTarget.position - transform.position;
                if (directionToTarget.sqrMagnitude > 0.001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(directionToTarget);
                    transform.rotation = Quaternion.Slerp(startRot, targetRot, curveT);
                    transform.position = startPos;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        /// <summary>카메라를 잠수함 후방 기본 위치로 즉시 배치한다.</summary>
        public void SnapToTarget()
        {
            if (target == null || cameraTuning == null)
                return;

            localYaw = 0f;
            localPitch = 0f;
            currentDistanceZ = runtimeSettings.IdleDistanceZ;
            smoothedTargetRotation = target.rotation;

            Vector3 baseOffset = new Vector3(0f, runtimeSettings.DefaultOffsetY, currentDistanceZ);
            transform.position = target.position + (smoothedTargetRotation * baseOffset);
            transform.LookAt(target.position, smoothedTargetRotation * Vector3.up);
        }

        /// <summary>유저 옵션 감도 배율을 적용한다.</summary>
        public void ApplyUserSettings(ExplorationCameraUserSettings newUserSettings)
        {
            userSettings = newUserSettings;
            RebuildRuntimeSettings();
        }

        /// <summary>SO와 유저 감도를 합쳐 실제 사용 세팅을 다시 계산한다.</summary>
        private void RebuildRuntimeSettings()
        {
            if (cameraTuning == null)
                return;

            runtimeSettings = cameraTuning.BuildRuntimeSettings(userSettings);
        }
    }
}
