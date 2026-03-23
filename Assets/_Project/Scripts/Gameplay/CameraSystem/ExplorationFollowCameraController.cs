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

        private Vector3 followVelocity; // 추적 보간 속도
        private float yaw; // 누적 Yaw
        private float pitch = 20f; // 누적 Pitch

        /// <summary>카메라 추적 대상을 설정한다</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>초기 시야 상태를 설정한다</summary>
        private void Start()
        {
            if (target == null) return;

            // 초기 Yaw 설정
            yaw = target.eulerAngles.y;

            // 커서 잠금 옵션 적용
            if (lockCursorOnPlay)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // 시작 위치를 즉시 맞춤
            SnapToTarget();
        }

        /// <summary>카메라를 부드럽게 갱신한다</summary>
        private void LateUpdate()
        {
            if (target == null) return;

            // 마우스 입력 읽기
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            // 회전 누적
            yaw += mouseX * mouseSensitivityX * Time.deltaTime;
            pitch -= mouseY * mouseSensitivityY * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desiredOffset = rotation * defaultOffset;
            Vector3 desiredPosition = target.position + desiredOffset;

            transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, followSmoothTime);
            transform.LookAt(target.position);
        }

        /// <summary>현재 타깃 위치로 즉시 스냅한다</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

            transform.position = target.position + rotation * defaultOffset;
            transform.LookAt(target.position);
        }
    }
}
