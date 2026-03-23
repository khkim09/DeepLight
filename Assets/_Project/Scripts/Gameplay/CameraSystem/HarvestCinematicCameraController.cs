using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>채집 모드에서 사이드뷰 시네마틱 추적을 담당하는 클래스</summary>
    public class HarvestCinematicCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target; // 추적 대상
        [SerializeField] private Vector3 baseOffset = new Vector3(10f, 1f, 0f); // 기본 옆면 오프셋
        [SerializeField] private float followSmoothTimeX = 0.15f; // X축 추적 보간 시간
        [SerializeField] private float followSmoothTimeY = 0.25f; // Y축 추적 보간 시간
        [SerializeField] private float followSmoothTimeZ = 0.15f; // Z축 추적 보간 시간
        [SerializeField] private float leftBiasWorld = 1.5f; // 왼쪽 1/3 느낌 보정값
        [SerializeField] private bool useRotationLock = true; // 회전 고정 여부
        [SerializeField] private Vector3 lockedEulerAngles = new Vector3(0f, -90f, 0f); // 고정 회전값

        private Vector3 currentVelocity; // 추적 속도

        /// <summary>카메라 추적 대상을 설정한다</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>활성화 시 현재 타깃 위치로 즉시 스냅한다</summary>
        private void OnEnable()
        {
            SnapToTarget();
        }

        /// <summary>채집 카메라를 갱신한다</summary>
        private void LateUpdate()
        {
            if (target == null) return;

            // 목표 위치 계산
            Vector3 desiredPosition = target.position + baseOffset;

            // 좌측 1/3 느낌 보정
            desiredPosition.z += leftBiasWorld;

            // 각 축별 부드러운 추적
            float nextX = Mathf.SmoothDamp(transform.position.x, desiredPosition.x, ref currentVelocity.x, followSmoothTimeX);
            float nextY = Mathf.SmoothDamp(transform.position.y, desiredPosition.y, ref currentVelocity.y, followSmoothTimeY);
            float nextZ = Mathf.SmoothDamp(transform.position.z, desiredPosition.z, ref currentVelocity.z, followSmoothTimeZ);

            transform.position = new Vector3(nextX, nextY, nextZ);

            // 회전 처리
            if (useRotationLock)
            {
                transform.rotation = Quaternion.Euler(lockedEulerAngles);
                return;
            }

            // 타깃 쪽으로 회전
            transform.LookAt(target.position);
        }

        /// <summary>현재 타깃 위치로 즉시 스냅한다</summary>
        public void SnapToTarget()
        {
            if (target == null) return;

            // 기본 위치 반영
            Vector3 snappedPosition = target.position + baseOffset;
            snappedPosition.z += leftBiasWorld;
            transform.position = snappedPosition;

            // 회전 반영
            if (useRotationLock)
                transform.rotation = Quaternion.Euler(lockedEulerAngles);
            else
                transform.LookAt(target.position);
        }
    }
}
