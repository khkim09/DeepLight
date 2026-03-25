using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>회수 콘솔 모드에서 조종실 1인칭 카메라 포즈를 제공하는 클래스</summary>
    public class HarvestConsoleCameraController : MonoBehaviour
    {
        [SerializeField] private Transform cockpitViewAnchor; // 조종실 시점 앵커
        [SerializeField] private bool followAnchorContinuously = true; // 활성 상태에서 앵커 추종 여부

        /// <summary>조종실 시점 앵커를 설정한다</summary>
        public void SetCockpitViewAnchor(Transform newCockpitViewAnchor)
        {
            cockpitViewAnchor = newCockpitViewAnchor; // 앵커 저장
        }

        /// <summary>현재 기준으로 목표 포즈를 계산한다</summary>
        public void GetDesiredPose(out Vector3 position, out Quaternion rotation)
        {
            // 앵커 없으면 현재 포즈 유지
            if (cockpitViewAnchor == null)
            {
                position = transform.position;
                rotation = transform.rotation;
                return;
            }

            position = cockpitViewAnchor.position; // 앵커 위치 사용
            rotation = cockpitViewAnchor.rotation; // 앵커 회전 사용
        }

        /// <summary>현재 기준 목표 포즈를 즉시 반영한다</summary>
        public void SnapToDesiredPose()
        {
            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;
        }

        /// <summary>활성 상태에서 조종실 앵커 포즈를 유지한다</summary>
        private void LateUpdate()
        {
            if (!followAnchorContinuously)
                return;

            if (!gameObject.activeInHierarchy)
                return;

            if (cockpitViewAnchor == null)
                return;

            GetDesiredPose(out Vector3 position, out Quaternion rotation);
            transform.position = position;
            transform.rotation = rotation;
        }
    }
}
