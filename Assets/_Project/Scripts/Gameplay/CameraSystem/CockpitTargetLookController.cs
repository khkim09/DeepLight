using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>조종실 시점 앵커의 위치는 유지하고 회전만 타깃 방향으로 보정하는 클래스</summary>
    public class CockpitTargetLookController : MonoBehaviour
    {
        [SerializeField] private float rotationSpeed = 8f; // 목표 회전까지 보간되는 속도

        private Quaternion baseLocalRotation; // 원래 로컬 회전 저장값
        private Transform currentTarget; // 현재 바라볼 타깃
        private bool isActive; // 회전 보정 활성 여부

        /// <summary>기본 로컬 회전을 캐싱한다</summary>
        private void Awake()
        {
            baseLocalRotation = transform.localRotation;
        }

        /// <summary>타깃 바라보기 보정을 시작한다</summary>
        public void BeginLookAt(Transform targetTransform)
        {
            currentTarget = targetTransform;
            isActive = targetTransform != null;
        }

        /// <summary>타깃 바라보기 보정을 종료하고 원래 시점 각도로 복귀한다</summary>
        public void EndLookAt()
        {
            currentTarget = null;
            isActive = false;
        }

        /// <summary>매 프레임 조종실 앵커 회전을 부드럽게 갱신한다</summary>
        private void LateUpdate()
        {
            if (!isActive || currentTarget == null)
            {
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation,
                    baseLocalRotation,
                    Time.deltaTime * rotationSpeed);
                return;
            }

            Vector3 toTarget = currentTarget.position - transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return;

            Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                Time.deltaTime * rotationSpeed);
        }
    }
}
