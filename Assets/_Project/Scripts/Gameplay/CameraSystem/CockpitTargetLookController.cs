using Project.Data.CameraSystem;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>조종실 시점 앵커의 회전만 타깃 방향으로 보정하는 클래스이다.</summary>
    public class CockpitTargetLookController : MonoBehaviour
    {
        [SerializeField] private CockpitLookTuningSO tuning; // 조종실 바라보기 속도 SO

        private Quaternion baseLocalRotation; // 기본 로컬 회전
        private Transform currentTarget;      // 현재 바라볼 타깃
        private bool isActive;                // 보정 활성화 여부

        /// <summary>초기 로컬 회전을 캐싱한다.</summary>
        private void Awake()
        {
            baseLocalRotation = transform.localRotation;
        }

        /// <summary>타깃 바라보기 보정을 시작한다.</summary>
        public void BeginLookAt(Transform targetTransform)
        {
            currentTarget = targetTransform;
            isActive = targetTransform != null;
        }

        /// <summary>타깃 바라보기 보정을 종료한다.</summary>
        public void EndLookAt()
        {
            currentTarget = null;
            isActive = false;
        }

        /// <summary>매 프레임 조종실 앵커 회전을 보간 갱신한다.</summary>
        private void LateUpdate()
        {
            if (tuning == null)
                return;

            if (!isActive || currentTarget == null)
            {
                // 타깃 보정이 없으면 원래 조종실 기준 각도로 복귀한다.
                transform.localRotation = Quaternion.Slerp(
                    transform.localRotation,
                    baseLocalRotation,
                    Time.deltaTime * tuning.RotationSpeed);
                return;
            }

            Vector3 toTarget = currentTarget.position - transform.position;
            if (toTarget.sqrMagnitude <= 0.0001f)
                return;

            Quaternion desiredRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                Time.deltaTime * tuning.RotationSpeed);
        }
    }
}
