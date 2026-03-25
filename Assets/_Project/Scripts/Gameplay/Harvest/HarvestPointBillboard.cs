using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>포인트 시각물을 표면 노멀 기준으로 유지하되 edge-on 상태에서 가시성을 보정하는 클래스</summary>
    public class HarvestPointBillboard : MonoBehaviour
    {
        [SerializeField] private Transform normalReference; // 포인트 노멀 기준 Transform
        [SerializeField] private Camera targetCamera; // 현재 회수 포인트 오버레이 카메라
        [SerializeField][Range(0f, 1f)] private float edgeOnThreshold = 0.2f; // edge-on 판정 기준
        [SerializeField] private float edgeJitterAngle = 28f; // edge-on일 때 카메라 쪽으로 보정할 각도
        [SerializeField] private float edgeScaleMultiplier = 1.6f; // edge-on일 때 가시성 보정 배율

        private Vector3 baseScale = Vector3.one; // 기본 스케일 캐시

        /// <summary>초기 스케일을 캐싱한다</summary>
        private void Awake()
        {
            baseScale = transform.localScale; // 기본 스케일 저장
        }

        /// <summary>LateUpdate에서 최종 시각 방향을 보정한다</summary>
        private void LateUpdate()
        {
            if (normalReference == null || targetCamera == null)
                return;

            Vector3 normal = normalReference.forward.normalized; // 표면 기준 노멀
            Vector3 toCamera = (targetCamera.transform.position - transform.position).normalized; // 포인트에서 카메라 방향
            float facing = Mathf.Abs(Vector3.Dot(normal, toCamera)); // 카메라를 얼마나 정면으로 보는지

            // 기본은 표면 노멀 방향으로 배치
            Quaternion baseRotation = Quaternion.LookRotation(normal, targetCamera.transform.up);
            transform.rotation = baseRotation;
            transform.localScale = baseScale;

            // 거의 수직으로 보여서 선처럼 되면 카메라 쪽으로 살짝 틀고 확대
            if (facing < edgeOnThreshold)
            {
                Vector3 blendedNormal = Vector3.Slerp(normal, toCamera, 0.45f).normalized; // 카메라 방향 일부 혼합
                Quaternion correctedRotation = Quaternion.LookRotation(blendedNormal, targetCamera.transform.up);

                transform.rotation = Quaternion.RotateTowards(baseRotation, correctedRotation, edgeJitterAngle);
                transform.localScale = baseScale * edgeScaleMultiplier;
            }
        }

        /// <summary>노멀 기준과 카메라를 설정한다</summary>
        public void Initialize(Transform newNormalReference, Camera newTargetCamera)
        {
            normalReference = newNormalReference; // 표면 기준 저장
            targetCamera = newTargetCamera; // 카메라 저장
        }
    }
}
