using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
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

        private float currentDistanceZ;     // 현재 카메라 z 거리
        private float distanceVelocity;     // 거리 스무딩용 ref
        private Quaternion smoothedTargetRotation; // 잠수함 회전 추적용 보간 회전
        private float localYaw;             // 일반 follow 상태 yaw
        private float localPitch;           // 일반 follow 상태 pitch
        private bool isInventoryOpen;       // 인벤토리 열림 여부
        private Vector3 externalShakeLocalOffset; // 외부 연출용 로컬 오프셋

        // ALT 자유시점 상태
        private bool wasFreeLookActive;     // 이전 프레임 자유시점 활성 여부
        private float freeLookOrbitRadius;  // ALT 시작 시 잠수정 중심과 카메라 사이 반지름
        private float freeLookYaw;          // 자유시점 구면 yaw
        private float freeLookPitch;        // 자유시점 구면 pitch

        /// <summary>자유 시야 입력이 유지 중인지 반환한다.</summary>
        public bool IsFreeLookActive => inputBindings != null && Input.GetKey(inputBindings.FreeLookKey);

        /// <summary>연출 등에 의해 카메라 입력이 잠겨있는지 여부이다.</summary>
        public bool IsInputLocked { get; set; }

        /// <summary>카메라가 잠수함과 독립적으로 정렬 중인지 여부이다.</summary>
        public bool IsIndependentAlignment { get; set; }

        /// <summary>외부 연출용 로컬 오프셋을 설정한다.</summary>
        public void SetExternalShakeLocalOffset(Vector3 localOffset)
        {
            externalShakeLocalOffset = localOffset;
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

            if (target != null)
                smoothedTargetRotation = target.rotation;
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

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<InventoryUIToggledEvent>(OnInventoryToggled);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<InventoryUIToggledEvent>(OnInventoryToggled);
        }

        /// <summary>매 프레임 후방 추적과 자유시점을 갱신한다.</summary>
        private void LateUpdate()
        {
            if (cameraTuning == null || inputBindings == null)
                return;

            if (target == null || IsIndependentAlignment)
                return;

            bool isFreeLookActive = IsFreeLookActive && !isInventoryOpen;

            // 잠수함 회전은 일반 follow / ALT free look 모두 기준축 계산용으로 계속 추적한다.
            float decayFactor = 1f - Mathf.Exp(-runtimeSettings.RotationFollowSpeed * Time.deltaTime);
            smoothedTargetRotation = Quaternion.Slerp(smoothedTargetRotation, target.rotation, decayFactor);

            // 일반 탐사 상태의 전후진/부스트에 따라 목표 카메라 거리를 갱신한다.
            UpdateDistance();

            if (isFreeLookActive)
            {
                // ALT 눌린 첫 프레임에는 현재 카메라 위치를 그대로 유지한 채 orbit 기준값만 계산한다.
                if (!wasFreeLookActive)
                    EnterFreeLookFromCurrentPose();

                if (!IsInputLocked)
                    UpdateFreeLookAngles();

                ApplyFreeLookPose();
            }
            else
            {
                // 일반 follow 상태에서는 freelook 각도를 원점으로 되돌린다.
                if (!IsInputLocked)
                    UpdateFollowAngles();

                ApplyFollowPose();
            }

            wasFreeLookActive = isFreeLookActive;
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
            wasFreeLookActive = false;

            ApplyFollowPose();
        }

        /// <summary>유저 옵션 감도 배율을 적용한다.</summary>
        public void ApplyUserSettings(ExplorationCameraUserSettings newUserSettings)
        {
            userSettings = newUserSettings;
            RebuildRuntimeSettings();
        }

        /// <summary>ALT 자유시점 진입 시 현재 카메라 위치를 유지한 채 구면 orbit 기준값을 계산한다.</summary>
        private void EnterFreeLookFromCurrentPose()
        {
            // 배그식 자유시점 기준:
            // 회전 중심은 잠수정 중심점이며, ALT를 누른 순간 현재 카메라 위치는 절대 바꾸지 않는다.
            Vector3 pivotWorldPosition = target.position;
            Vector3 pivotToCamera = transform.position - pivotWorldPosition;

            freeLookOrbitRadius = Mathf.Max(0.01f, pivotToCamera.magnitude);

            Vector3 dir = pivotToCamera.normalized;

            // 현재 카메라 위치를 구면 좌표(yaw / pitch)로 변환한다.
            // 이후에는 마우스 입력이 들어올 때만 이 각도가 변한다.
            freeLookYaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            freeLookPitch = Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>일반 탐사 상태의 yaw/pitch 입력을 갱신한다.</summary>
        private void UpdateFollowAngles()
        {
            // 일반 follow 상태에서는 ALT 각도 잔상을 남기지 않고 0으로 되돌린다.
            localYaw = Mathf.Lerp(localYaw, 0f, runtimeSettings.ReturnSpeed * Time.deltaTime);
            localPitch = Mathf.Lerp(localPitch, 0f, runtimeSettings.ReturnSpeed * Time.deltaTime);

            localYaw = Mathf.Repeat(localYaw + 180f, 360f) - 180f;
            localPitch = Mathf.Clamp(localPitch, runtimeSettings.MinPitch, runtimeSettings.MaxPitch);
        }

        /// <summary>자유시점 orbit용 yaw/pitch 입력을 갱신한다.</summary>
        private void UpdateFreeLookAngles()
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            freeLookYaw += mouseX * runtimeSettings.MouseSensitivityX * Time.deltaTime;
            freeLookPitch -= mouseY * runtimeSettings.MouseSensitivityY * Time.deltaTime;

            // 극점 부근에서 뒤집히는 현상을 막기 위해 89도로 제한한다.
            freeLookPitch = Mathf.Clamp(freeLookPitch, -89f, 89f);
            freeLookYaw = Mathf.Repeat(freeLookYaw + 180f, 360f) - 180f;
        }

        /// <summary>일반 추적 상태의 카메라 포즈를 적용한다.</summary>
        private void ApplyFollowPose()
        {
            Vector3 baseOffset = new Vector3(0f, runtimeSettings.DefaultOffsetY, currentDistanceZ);
            Quaternion localCamRotation = Quaternion.Euler(localPitch, localYaw, 0f);
            Quaternion finalCamRotation = smoothedTargetRotation * localCamRotation;

            // 일반 탐사 상태는 기존 3인칭 후방 추적 카메라 형태를 유지한다.
            transform.position = target.position + (finalCamRotation * (baseOffset + externalShakeLocalOffset));
            transform.LookAt(target.position, finalCamRotation * Vector3.up);
        }

        /// <summary>잠수정 중심을 기준으로 한 구면 orbit 자유시점 포즈를 적용한다.</summary>
        private void ApplyFreeLookPose()
        {
            // 회전 중심은 잠수정 중심이다.
            Vector3 pivotWorldPosition = target.position;

            // yaw = 좌우 원궤도, pitch = 상하 원궤도
            // ALT 시작 시의 반지름을 유지한 채 구 표면을 따라 이동한다.
            float yawRad = freeLookYaw * Mathf.Deg2Rad;
            float pitchRad = freeLookPitch * Mathf.Deg2Rad;

            Vector3 sphereDirection = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad));

            Vector3 worldPosition = pivotWorldPosition + (sphereDirection * freeLookOrbitRadius);

            // 자유시점에서도 shake는 최종 월드 위치에 더해준다.
            transform.position = worldPosition + externalShakeLocalOffset;

            // 카메라는 항상 잠수정 중심을 바라본다.
            Vector3 lookDirection = pivotWorldPosition - transform.position;
            if (lookDirection.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        }

        /// <summary>전후진 상태에 맞는 카메라 거리 목표값을 갱신한다.</summary>
        private void UpdateDistance()
        {
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
        }

        /// <summary>SO와 유저 감도를 합쳐 실제 사용 세팅을 다시 계산한다.</summary>
        private void RebuildRuntimeSettings()
        {
            if (cameraTuning == null)
                return;

            runtimeSettings = cameraTuning.BuildRuntimeSettings(userSettings);
        }

        /// <summary>인벤토리 토글 이벤트를 수신하여 상태를 갱신한다.</summary>
        private void OnInventoryToggled(InventoryUIToggledEvent publishedEvent)
        {
            isInventoryOpen = publishedEvent.IsOpen;
        }
    }
}
