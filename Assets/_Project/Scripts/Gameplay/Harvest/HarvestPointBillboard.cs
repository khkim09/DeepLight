using Project.Data.Harvest;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>회수 포인트 시각물을 표면 노멀 기준으로 유지하되 edge-on 상태를 SO 값으로 보정하는 클래스이다.</summary>
    public class HarvestPointBillboard : MonoBehaviour
    {
        [SerializeField] private Transform normalReference; // 포인트 표면 노멀 기준 Transform
        [SerializeField] private Camera targetCamera;       // 현재 렌더 기준 카메라
        [SerializeField] private HarvestPointBillboardTuningSO tuning; // 가시성 보정 SO

        private Vector3 baseScale = Vector3.one; // 기본 스케일 캐시

        /// <summary>초기 스케일을 캐싱한다.</summary>
        private void Awake()
        {
            baseScale = transform.localScale;
        }

        /// <summary>LateUpdate에서 최종 시각 방향과 스케일을 보정한다.</summary>
        private void LateUpdate()
        {
            if (normalReference == null || targetCamera == null || tuning == null)
                return;

            Vector3 normal = normalReference.forward.normalized;
            Vector3 toCamera = (targetCamera.transform.position - transform.position).normalized;

            // 카메라를 얼마나 정면으로 보는지 계산한다.
            float facing = Mathf.Abs(Vector3.Dot(normal, toCamera));

            // 기본은 표면 노멀 방향으로 정렬한다.
            Quaternion baseRotation = Quaternion.LookRotation(normal, targetCamera.transform.up);
            transform.rotation = baseRotation;
            transform.localScale = baseScale;

            // 거의 edge-on 상태면 카메라 쪽으로 약간 보정하고 확대한다.
            if (facing < tuning.EdgeOnThreshold)
            {
                Vector3 blendedNormal = Vector3.Slerp(normal, toCamera, 0.45f).normalized;
                Quaternion correctedRotation = Quaternion.LookRotation(blendedNormal, targetCamera.transform.up);

                transform.rotation = Quaternion.RotateTowards(
                    baseRotation,
                    correctedRotation,
                    tuning.EdgeJitterAngle);

                transform.localScale = baseScale * tuning.EdgeScaleMultiplier;
            }
        }

        /// <summary>노멀 기준과 카메라를 설정한다.</summary>
        public void Initialize(Transform newNormalReference, Camera newTargetCamera)
        {
            normalReference = newNormalReference;
            targetCamera = newTargetCamera;
        }
    }
}
