using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>채집 모드에서 고정된 2.5D 카메라 포즈를 제공하는 클래스</summary>
    public class HarvestCinematicCameraController : MonoBehaviour
    {
        [SerializeField] private bool cacheAuthoredPoseOnAwake = true; // 시작 시 작성된 포즈를 캐시할지 여부

        private Vector3 authoredPosition; // 작성된 고정 위치
        private Quaternion authoredRotation; // 작성된 고정 회전
        private bool hasCachedPose; // 캐시 여부

        /// <summary>초기 고정 포즈를 캐시한다</summary>
        private void Awake()
        {
            // 캐시 옵션 꺼져 있으면 중단
            if (!cacheAuthoredPoseOnAwake) return;

            // 현재 포즈 캐시
            authoredPosition = transform.position;
            authoredRotation = transform.rotation;
            hasCachedPose = true;
        }

        /// <summary>고정된 목표 포즈를 반환한다</summary>
        public void GetDesiredPose(out Vector3 position, out Quaternion rotation)
        {
            // 캐시 포즈가 있으면 반환
            if (hasCachedPose)
            {
                position = authoredPosition;
                rotation = authoredRotation;
                return;
            }

            // 현재 포즈 반환
            position = transform.position;
            rotation = transform.rotation;
        }

        /// <summary>고정 포즈를 즉시 적용한다</summary>
        public void SnapToDesiredPose()
        {
            // 목표 포즈 계산
            GetDesiredPose(out Vector3 position, out Quaternion rotation);

            // 즉시 반영
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}
