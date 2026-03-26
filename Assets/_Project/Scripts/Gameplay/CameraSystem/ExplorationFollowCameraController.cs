using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>탐사 모드에서 잠수함을 추적하고, 동적 간격 조절이 포함된 자유 시야를 제공하는 카메라 클래스</summary>
    public class ExplorationFollowCameraController : MonoBehaviour
    {
        [Header("Target & Base Follow")]
        [SerializeField] private Transform target; // 추적 대상 (잠수함 본체)
        [SerializeField] private Vector3 defaultOffset = new Vector3(0f, 5f, -8f); // 평상시 유지할 기본 간격 (거리)
        [SerializeField] private float followSmoothTime = 0.08f; // 위치 추적 보간 시간 (카메라가 따라붙는 속도)

        [Header("Free Look (Alt) Settings")]
        [Tooltip("180도 회전 시 도달할 잠수함 바로 위쪽(정수리) 오프셋입니다.")]
        [SerializeField] private Vector3 freeLookTopOffset = new Vector3(0f, 8f, -1f);
        [SerializeField] private float mouseSensitivityX = 120f; // 마우스 가로 회전 감도
        [SerializeField] private float mouseSensitivityY = 90f; // 마우스 세로 회전 감도
        [SerializeField] private float minPitch = -30f; // 자유 시야 하단 꺾임 제한
        [SerializeField] private float maxPitch = 60f; // 자유 시야 상단 꺾임 제한
        [SerializeField] private float returnSpeed = 8f; // Alt 키를 뗐을 때 원상복귀되는 속도
        [SerializeField] private KeyCode freeLookKey = KeyCode.LeftAlt; // 자유 시야 발동 키
        [SerializeField] private bool lockCursorOnPlay = false; // 시작 시 커서 잠금 여부

        private Vector3 followVelocity; // SmoothDamp 속도 캐싱용 참조 변수
        private float yaw; // 카메라의 현재 가로 각도 (Yaw)
        private float pitch = 20f; // 카메라의 현재 세로 각도 (Pitch)

        /// <summary>자유 시야(Alt) 입력이 유지되고 있는지 반환한다</summary>
        public bool IsFreeLookActive => Input.GetKey(freeLookKey);

        /// <summary>연출 등에 의해 유저의 마우스 입력이 잠겨있는지 여부</summary>
        public bool IsInputLocked { get; set; }

        /// <summary>카메라가 잠수함 추적을 멈추고 독립적으로 타겟을 바라보고 있는지 여부 (Phase 1 연출용)</summary>
        public bool IsIndependentAlignment { get; set; }

        /// <summary>카메라의 추적 대상을 변경한다</summary>
        public void SetTarget(Transform newTarget) => target = newTarget;

        /// <summary>현재 카메라가 바라보는 평면(Y축 무시) 전방 벡터를 반환한다</summary>
        public Vector3 GetPlanarForward()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude <= 0.0001f) return Vector3.forward;
            return forward.normalized;
        }

        /// <summary>초기 커서 상태 설정 및 카메라 시작 위치를 스냅한다</summary>
        private void Start()
        {
            if (target == null) return;
            yaw = target.eulerAngles.y;

            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            SnapToTarget();
        }

        /// <summary>매 프레임 위치 및 회전 목표를 추적하여 카메라를 갱신한다</summary>
        private void LateUpdate()
        {
            // 독립 연출(Phase 1) 중에는 잠수함 꽁무니 추적을 무시함
            if (target == null || IsIndependentAlignment) return;

            // 잠수함 본체의 현재 회전값 캐싱 및 정규화 (-180 ~ 180도)
            float targetYaw = target.eulerAngles.y;
            float targetPitch = target.eulerAngles.x;
            if (targetPitch > 180f) targetPitch -= 360f;

            if (!IsInputLocked)
            {
                if (IsFreeLookActive)
                {
                    // 1. 자유 시야 모드: 잠수함 각도와 무관하게 마우스 입력으로 카메라의 Yaw/Pitch를 독자 제어
                    yaw += Input.GetAxis("Mouse X") * mouseSensitivityX * Time.deltaTime;
                    pitch -= Input.GetAxis("Mouse Y") * mouseSensitivityY * Time.deltaTime;
                    pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
                }
                else
                {
                    // 2. 평상시 모드: 카메라의 각도를 잠수함 본체의 각도(전투기 방향)로 부드럽게 일치시킴
                    yaw = Mathf.LerpAngle(yaw, targetYaw, returnSpeed * Time.deltaTime);
                    pitch = Mathf.LerpAngle(pitch, targetPitch, returnSpeed * Time.deltaTime);
                }
            }

            // 3. 다이내믹 오프셋 계산 (거리 당기기 핵심 알고리즘)
            // 잠수함이 바라보는 정면(targetYaw)과 현재 카메라 시야(yaw)의 각도 차이를 절대값으로 구함
            float angleDifference = Mathf.Abs(Mathf.DeltaAngle(targetYaw, yaw));
            float pullInFactor = angleDifference / 180f; // 0(완전 정면) ~ 1(180도 뒤돌아봄) 비율로 변환

            // 카메라가 뒤를 돌아볼수록 defaultOffset(후방)에서 freeLookTopOffset(정수리)으로 간격(위치)을 부드럽게 당겨줌
            Vector3 currentOffset = Vector3.Lerp(defaultOffset, freeLookTopOffset, pullInFactor);

            // 4. 최종 위치 계산 및 적용
            Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredPosition = target.position + camRotation * currentOffset;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime);
            transform.LookAt(target.position); // 카메라는 거리가 당겨져도 항상 잠수함을 응시함
        }

        /// <summary>잠수함과 독립적으로 특정 대상을 향해 카메라를 회전시키는 연출을 수행한다</summary>
        public async UniTask AlignToTargetIndependentlyAsync(Transform lookTarget, float duration, CancellationToken token)
        {
            if (lookTarget == null) return;

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
                    // 위치는 그 자리에 고정하고 시선만 부드럽게 돌아감
                    transform.rotation = Quaternion.Slerp(startRot, targetRot, curveT);
                    transform.position = startPos;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }
        }

        /// <summary>카메라를 현재 잠수함의 후방 기본 오프셋 위치로 즉시 스냅시킨다</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            float targetPitch = target.eulerAngles.x;
            if (targetPitch > 180f) targetPitch -= 360f;

            yaw = target.eulerAngles.y;
            pitch = targetPitch;

            Quaternion camRotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = target.position + camRotation * defaultOffset;
            transform.LookAt(target.position);
        }
    }
}
