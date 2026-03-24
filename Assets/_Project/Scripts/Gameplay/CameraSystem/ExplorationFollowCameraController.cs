using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>탐사 모드에서 플레이어 추적과 마우스 시야 회전을 담당하는 클래스</summary>
    public class ExplorationFollowCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target; // 추적 대상
        [SerializeField] private Vector3 defaultOffset = new Vector3(0f, 5f, -8f); // 기본 오프셋
        [SerializeField] private float followSmoothTime = 0.1f; // 추적 보간 시간
        [SerializeField] private float mouseSensitivityX = 120f; // 가로 마우스 감도
        [SerializeField] private float mouseSensitivityY = 90f; // 세로 마우스 감도
        [SerializeField] private float minPitch = -20f; // 최소 피치 각도
        [SerializeField] private float maxPitch = 50f; // 최대 피치 각도
        [SerializeField] private bool lockCursorOnPlay = false; // 플레이 시 커서 잠금 여부
        [SerializeField] private KeyCode freeLookKey = KeyCode.LeftAlt; // 자유시야 키

        private Vector3 followVelocity; // 추적 보간 속도
        private float yaw; // 누적 Yaw
        private float pitch = 20f; // 누적 Pitch

        public bool IsFreeLookActive => UnityEngine.Input.GetKey(freeLookKey); // 자유시야 활성 여부

        /// <summary>카메라 추적 대상을 설정한다</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>현재 카메라의 수평 전방 벡터를 반환한다</summary>
        public Vector3 GetPlanarForward()
        {
            Vector3 forward = transform.forward; // 현재 전방 벡터
            forward.y = 0f; // 수평면으로 투영
            if (forward.sqrMagnitude <= 0.0001f)
                return Vector3.forward;

            return forward.normalized;
        }

        /// <summary>초기 시야 상태를 설정한다</summary>
        private void Start()
        {
            if (target == null)
                return;

            // 초기 Yaw 설정
            yaw = target.eulerAngles.y;

            // 커서 잠금 옵션 적용
            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // 시작 위치 즉시 반영
            SnapToTarget();
        }

        /// <summary>카메라를 부드럽게 갱신한다</summary>
        private void LateUpdate()
        {
            if (target == null)
                return;

            // 마우스 입력 읽기
            float mouseX = UnityEngine.Input.GetAxis("Mouse X");
            float mouseY = UnityEngine.Input.GetAxis("Mouse Y");

            // 회전 누적
            yaw += mouseX * mouseSensitivityX * Time.deltaTime;
            pitch -= mouseY * mouseSensitivityY * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f); // 회전 계산
            Vector3 desiredOffset = rotation * defaultOffset; // 오프셋 계산
            Vector3 desiredPosition = target.position + desiredOffset; // 목표 위치 계산

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime);
            transform.LookAt(target.position);
        }

        /// <summary>현재 타깃 위치로 즉시 스냅한다</summary>
        public void SnapToTarget()
        {
            if (target == null)
                return;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f); // 회전 계산
            transform.position = target.position + rotation * defaultOffset; // 위치 반영
            transform.LookAt(target.position);
        }
    }
}
