using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>Harvest 중 현재 타깃 오브젝트를 마우스 드래그와 A/D 키로 회전시키는 클래스이다.</summary>
    public class HarvestTargetRotationController : MonoBehaviour
    {
        [Header("Rotation")]
        [SerializeField] private float dragYawSpeed = 0.28f; // 드래그 좌우 회전 속도
        [SerializeField] private float dragPitchSpeed = 0.18f; // 드래그 상하 회전 속도
        [SerializeField] private float minPitch = -55f; // 최소 pitch
        [SerializeField] private float maxPitch = 55f; // 최대 pitch

        private Transform boundRotationPivot; // 현재 회전 타깃 피벗
        private float yaw; // 현재 yaw
        private float pitch; // 현재 pitch
        private bool isDragging; // 드래그 회전 중 여부
        private Vector3 lastMousePosition; // 직전 마우스 좌표

        /// <summary>현재 회전할 타깃 피벗을 바인딩한다.</summary>
        public void BindTarget(Transform rotationPivot)
        {
            boundRotationPivot = rotationPivot;
            CacheCurrentAngles();
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

        /// <summary>드래그 이동량으로 타깃을 회전한다.</summary>
        public void RotateByDrag(Vector3 currentMousePosition)
        {
            if (!isDragging || boundRotationPivot == null)
                return;

            Vector3 delta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;

            yaw += delta.x * dragYawSpeed;
            pitch -= delta.y * dragPitchSpeed;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            ApplyRotation();
        }

        /// <summary>현재 바인딩된 타깃 회전을 기본 상태로 되돌린다.</summary>
        public void ResetRotation()
        {
            if (boundRotationPivot == null)
                return;

            yaw = 0f;
            pitch = 0f;
            ApplyRotation();
        }

        /// <summary>회전 값을 실제 피벗에 적용한다.</summary>
        private void ApplyRotation()
        {
            if (boundRotationPivot == null)
                return;

            boundRotationPivot.localRotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        /// <summary>현재 피벗의 로컬 회전값을 캐싱한다.</summary>
        private void CacheCurrentAngles()
        {
            if (boundRotationPivot == null)
                return;

            Vector3 euler = boundRotationPivot.localEulerAngles;
            pitch = NormalizeAngle(euler.x);
            yaw = NormalizeAngle(euler.y);
        }

        /// <summary>각도를 -180~180 범위로 정규화한다.</summary>
        private float NormalizeAngle(float angle)
        {
            while (angle > 180f)
                angle -= 360f;

            while (angle < -180f)
                angle += 360f;

            return angle;
        }
    }
}
