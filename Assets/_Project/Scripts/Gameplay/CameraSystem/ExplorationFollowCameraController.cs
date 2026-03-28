using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>잠수함의 조작 상태에 따라 거리를 조절하고, 선회 시 측면이 자연스럽게 노출되도록 회전을 부드럽게 지연 추적하는 3인칭 탐사 카메라 클래스이다.</summary>
    public class ExplorationFollowCameraController : MonoBehaviour
    {
        [Header("Target & Base Follow")]
        [SerializeField] private Transform target; // 카메라가 추적할 대상(잠수함) 트랜스폼
        [SerializeField] private float defaultOffsetY = 5f; // 카메라의 기본 Y축 높이 오프셋
        [Tooltip("수치가 작을수록 회전 시 측면이 더 많이 보이며 묵직하게 꼬리를 따라간다.")]
        [SerializeField] private float rotationFollowSpeed = 6f; // 대상의 회전을 따라가는 지연 추적 속도

        [Header("State Based Distance (Z)")]
        [SerializeField] private float idleDistanceZ = -8f; // 정지 상태일 때의 기본 Z축 거리
        [SerializeField] private float normalForwardDistanceZ = -10f; // 전진(W) 입력 시의 목표 Z축 거리
        [SerializeField] private float boostForwardDistanceZ = -14f; // 부스트 전진(Shift+W) 입력 시의 목표 Z축 거리
        [SerializeField] private float normalBackwardDistanceZ = -7f; // 후진(S) 입력 시의 목표 Z축 거리
        [SerializeField] private float boostBackwardDistanceZ = -6f; // 부스트 후진(Shift+S) 입력 시의 목표 Z축 거리
        [SerializeField] private float distanceSmoothTime = 0.3f; // 목표 거리까지 도달하는 데 걸리는 보간 시간

        [Header("Free Look (Alt) Settings")]
        [SerializeField] private Vector3 freeLookTopOffset = new Vector3(0f, 8f, -1f); // 자유 시야 모드 시 적용될 상단 오프셋 위치
        [SerializeField] private float mouseSensitivityX = 120f; // 자유 시야 모드의 마우스 X축 민감도
        [SerializeField] private float mouseSensitivityY = 90f; // 자유 시야 모드의 마우스 Y축 민감도
        [SerializeField] private float minPitch = -30f; // 자유 시야의 최소 상하 하향 각도
        [SerializeField] private float maxPitch = 60f; // 자유 시야의 최대 상하 상향 각도
        [SerializeField] private float returnSpeed = 8f; // 자유 시야 종료 시 원래 시점으로 복귀하는 속도
        [SerializeField] private KeyCode freeLookKey = KeyCode.LeftAlt; // 자유 시야 모드를 활성화하는 입력 키
        [SerializeField] private bool lockCursorOnPlay = false; // 플레이 시작 시 마우스 커서를 잠글지 여부

        private float currentDistanceZ; // 현재 스무딩되어 적용 중인 Z축 거리
        private float distanceVelocity; // 거리 스무딩(SmoothDamp) 연산용 참조 변수
        private Quaternion smoothedTargetRotation; // 대상의 회전을 지연 추적하여 저장하는 쿼터니언 변수
        private float localYaw; // 자유 시야 모드에서 누적된 로컬 요우(좌우) 각도
        private float localPitch; // 자유 시야 모드에서 누적된 로컬 피치(상하) 각도

        /// <summary>자유 시야(Free Look) 입력이 유지되고 있는지 여부를 반환한다.</summary>
        public bool IsFreeLookActive => Input.GetKey(freeLookKey);

        /// <summary>연출 등에 의해 유저의 카메라 조작이 잠겨있는지 여부이다.</summary>
        public bool IsInputLocked { get; set; }

        /// <summary>카메라가 잠수함과 독립적으로 특정 대상을 바라보고 있는지 여부이다.</summary>
        public bool IsIndependentAlignment { get; set; }

        /// <summary>카메라가 추적할 새로운 대상을 설정하고 초기 회전값을 동기화한다.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null) smoothedTargetRotation = target.rotation;
        }

        /// <summary>현재 카메라가 바라보는 방향을 평면(Y축 무시) 기준으로 정규화하여 반환한다.</summary>
        public Vector3 GetPlanarForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f) return Vector3.forward;
            return forward.normalized;
        }

        /// <summary>초기 커서 상태를 설정하고 카메라를 대상의 기본 위치로 즉시 이동시킨다.</summary>
        private void Start()
        {
            // 설정에 따라 마우스 커서를 화면 중앙에 고정하고 숨긴다.
            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // 초기 거리를 정지 상태 거리로 맞추고 대상을 향해 스냅한다.
            currentDistanceZ = idleDistanceZ;
            SnapToTarget();
        }

        /// <summary>매 프레임 대상의 이동 및 회전 상태에 따라 카메라의 위치와 각도를 부드럽게 갱신한다.</summary>
        private void LateUpdate()
        {
            // 추적 대상이 없거나 독립적인 카메라 연출 중일 때는 위치 갱신을 생략한다.
            if (target == null || IsIndependentAlignment) return;

            // 1. 자유 시야 모드(Free Look) 입력 처리 및 시점 복귀 연산
            if (!IsInputLocked)
            {
                if (IsFreeLookActive)
                {
                    localYaw += Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
                    localPitch -= Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;
                    localPitch = Mathf.Clamp(localPitch, minPitch, maxPitch);
                }
                else
                {
                    // 입력이 없으면 원래의 기본 후방 시점으로 부드럽게 복귀한다.
                    localYaw = Mathf.Lerp(localYaw, 0f, returnSpeed * Time.deltaTime);
                    localPitch = Mathf.Lerp(localPitch, 0f, returnSpeed * Time.deltaTime);
                }
            }

            // 요우 각도를 -180 ~ 180도 사이로 정규화한다.
            localYaw = Mathf.Repeat(localYaw + 180f, 360f) - 180f;

            // 2. 입력 상태에 따른 목표 거리(Target Distance) 설정
            float targetZ = idleDistanceZ;
            bool isBoost = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // 전진, 후진, 부스트 입력 상태에 따라 카메라가 떨어지는 거리를 다르게 지정한다.
            if (Input.GetKey(KeyCode.W)) targetZ = isBoost ? boostForwardDistanceZ : normalForwardDistanceZ;
            else if (Input.GetKey(KeyCode.S)) targetZ = isBoost ? boostBackwardDistanceZ : normalBackwardDistanceZ;

            // 현재 거리를 목표 거리를 향해 부드럽게 보간한다.
            currentDistanceZ = Mathf.SmoothDamp(currentDistanceZ, targetZ, ref distanceVelocity, distanceSmoothTime);
            Vector3 baseOffset = new Vector3(0f, defaultOffsetY, currentDistanceZ);

            // 3. 자유 시야 조작 시 카메라 오프셋 블렌딩
            float pullInFactor = Mathf.Abs(localYaw) / 180f;
            Vector3 targetLocalOffset = Vector3.Lerp(baseOffset, freeLookTopOffset, pullInFactor);

            // 4. 주사율에 독립적인 감쇠 공식을 적용하여 선체의 회전을 부드럽게 지연 추적한다.
            // 프레임률이 달라도 일관된 속도로 타겟 회전을 따라가 측면 노출 효과(꼬리 물기)를 유지한다.
            float decayFactor = 1f - Mathf.Exp(-rotationFollowSpeed * Time.deltaTime);
            smoothedTargetRotation = Quaternion.Slerp(smoothedTargetRotation, target.rotation, decayFactor);

            // 5. 추적된 회전값을 기준으로 카메라의 최종 위치와 각도를 계산하고 적용한다.
            Quaternion localCamRotation = Quaternion.Euler(localPitch, localYaw, 0f);
            Quaternion finalCamRotation = smoothedTargetRotation * localCamRotation;

            // Transform 포지션을 갱신하여 대상 주위의 구면 궤적을 유지한다.
            transform.position = target.position + (finalCamRotation * targetLocalOffset);
            transform.LookAt(target.position, finalCamRotation * Vector3.up);
        }

        /// <summary>잠수함과 독립적으로 특정 대상을 향해 카메라를 부드럽게 회전시키는 연출을 수행한다.</summary>
        public async UniTask AlignToTargetIndependentlyAsync(Transform lookTarget, float duration, CancellationToken token)
        {
            if (lookTarget == null) return;

            // 카메라 연출을 위해 유저 입력을 잠그고 독립 모드를 활성화한다.
            IsInputLocked = true;
            IsIndependentAlignment = true;

            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Quaternion startRot = transform.rotation;

            // 지정된 시간(duration) 동안 목표를 향해 카메라를 부드럽게 회전시킨다.
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
                    transform.position = startPos; // 위치는 기존 위치로 고정 유지
                }

                // 코루틴 대신 UniTask를 사용하여 다음 Update 프레임까지 대기한다.
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        /// <summary>카메라를 현재 잠수함의 후방 기본 오프셋 위치로 즉시 스냅시킨다.</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            // 자유 시야 변수 및 거리를 초기화한다.
            localYaw = 0f;
            localPitch = 0f;
            currentDistanceZ = idleDistanceZ;
            smoothedTargetRotation = target.rotation;

            // 대상의 현재 위치와 회전값을 기준으로 카메라를 즉시 배치한다.
            Vector3 baseOffset = new Vector3(0f, defaultOffsetY, currentDistanceZ);
            transform.position = target.position + (smoothedTargetRotation * baseOffset);
            transform.LookAt(target.position, smoothedTargetRotation * Vector3.up);
        }
    }
}
