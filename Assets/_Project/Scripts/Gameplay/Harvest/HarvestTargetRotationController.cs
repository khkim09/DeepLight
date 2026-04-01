using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>Harvest 중 현재 타깃 오브젝트를 화면 드래그 방향 기준으로 자유 회전시키는 클래스이다.</summary>
    public class HarvestTargetRotationController : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] private float dragYawSpeed = 0.28f; // 화면 좌우 드래그 회전 속도
        [SerializeField] private float dragPitchSpeed = 0.28f; // 화면 상하 드래그 회전 속도
        [SerializeField] private Camera rotationReferenceCamera; // 회전 기준 카메라, 비면 Camera.main 사용

        private Transform boundRotationPivot; // 현재 회전 타깃 피벗
        private bool isDragging; // 드래그 회전 중 여부
        private Vector3 lastMousePosition; // 직전 마우스 좌표

        /// <summary>현재 회전할 타깃 피벗을 바인딩한다.</summary>
        public void BindTarget(Transform rotationPivot)
        {
            boundRotationPivot = rotationPivot;
        }

        /// <summary>현재 타깃 피벗 바인딩을 해제한다.</summary>
        public void UnbindTarget()
        {
            boundRotationPivot = null;
            isDragging = false;
        }

        /// <summary>회전 입력을 시작한다.</summary>
        public void BeginDrag(Vector3 screenPosition)
        {
            if (boundRotationPivot == null)
                return;

            isDragging = true;
            lastMousePosition = screenPosition;
        }

        /// <summary>회전 입력을 종료한다.</summary>
        public void EndDrag()
        {
            isDragging = false;
        }

        /// <summary>드래그 이동량을 화면 기준 상대 회전으로 변환해 타깃에 적용한다.</summary>
        public void RotateByDrag(Vector3 currentMousePosition)
        {
            if (!isDragging || boundRotationPivot == null)
                return;

            Camera referenceCamera = ResolveReferenceCamera();
            if (referenceCamera == null)
                return;

            // 직전 프레임 대비 드래그 방향만 사용한다.
            Vector3 delta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;

            if (Mathf.Approximately(delta.x, 0f) && Mathf.Approximately(delta.y, 0f))
                return;

            // 화면 좌우 드래그는 "현재 카메라의 up 축" 기준 회전
            float yawDelta = -delta.x * dragYawSpeed;

            // 화면 상하 드래그는 "현재 카메라의 right 축" 기준 회전
            float pitchDelta = delta.y * dragPitchSpeed;

            // 현재 카메라 화면축 기준으로 월드 회전을 만든다.
            Quaternion yawRotation = Quaternion.AngleAxis(yawDelta, referenceCamera.transform.up);
            Quaternion pitchRotation = Quaternion.AngleAxis(pitchDelta, referenceCamera.transform.right);

            // 현재 보이는 화면 기준 조작감을 위해 월드축 회전을 앞에서 곱한다.
            boundRotationPivot.rotation = yawRotation * pitchRotation * boundRotationPivot.rotation;
        }

        /// <summary>현재 바인딩된 타깃 회전을 기본 상태로 되돌린다.</summary>
        public void ResetRotation()
        {
            if (boundRotationPivot == null)
                return;

            boundRotationPivot.localRotation = Quaternion.identity;
        }

        /// <summary>회전 기준 카메라를 반환한다.</summary>
        private Camera ResolveReferenceCamera()
        {
            if (rotationReferenceCamera != null)
                return rotationReferenceCamera;

            if (Camera.main != null)
                return Camera.main;

            return null;
        }
    }
}
