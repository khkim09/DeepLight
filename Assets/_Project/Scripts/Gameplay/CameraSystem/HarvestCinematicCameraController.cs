using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>채집 모드에서 플레이어 기준 2.5D 카메라 포즈를 제공하는 클래스</summary>
    public class HarvestCinematicCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target; // 기준 플레이어
        [SerializeField] private Vector3 playerRelativeOffset = new Vector3(8f, 2f, 3f); // 플레이어 기준 카메라 기본 오프셋
        [SerializeField] private Vector3 lookAtLocalOffset = new Vector3(0f, 0.5f, 2f); // 플레이어 기준 바라볼 오프셋
        [SerializeField] private float screenRightShift = 2.5f; // 현재 앵글 유지 상태에서 카메라를 화면 오른쪽으로 평행 이동하는 값
        [SerializeField] private bool followTargetInHarvest = true; // 채집 중 플레이어 기준 위치 유지 여부

        /// <summary>카메라 기준 대상을 설정한다</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        /// <summary>현재 기준으로 목표 포즈를 계산한다</summary>
        public void GetDesiredPose(out Vector3 position, out Quaternion rotation)
        {
            // 대상 없으면 현재 포즈 반환
            if (target == null)
            {
                position = transform.position;
                rotation = transform.rotation;
                return;
            }

            // 플레이어 로컬 기준 기본 위치 계산
            Vector3 basePosition = target.position + target.TransformDirection(playerRelativeOffset);

            // 플레이어 로컬 기준 바라볼 위치 계산
            Vector3 lookTarget = target.position + target.TransformDirection(lookAtLocalOffset);
            Vector3 lookDirection = lookTarget - basePosition;

            // 기본 회전 계산
            if (lookDirection.sqrMagnitude > 0.0001f)
                rotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            else
                rotation = transform.rotation;

            // 현재 앵글 유지 상태에서 카메라를 오른쪽으로 평행 이동
            position = basePosition + rotation * Vector3.right * screenRightShift;
        }

        /// <summary>현재 기준 목표 포즈를 즉시 반영한다</summary>
        public void SnapToDesiredPose()
        {
            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;
        }

        /// <summary>채집 중 플레이어 기준 포즈를 유지한다</summary>
        private void LateUpdate()
        {
            if (!followTargetInHarvest)
                return;

            if (!gameObject.activeInHierarchy)
                return;

            if (target == null)
                return;

            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}
