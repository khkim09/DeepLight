using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>회수 포인트 빌보드 가시성 보정 수치를 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestPointBillboardTuningSO",
        menuName = "Project/Harvest/Harvest Point Billboard Tuning")]
    public class HarvestPointBillboardTuningSO : ScriptableObject
    {
        [SerializeField][Range(0f, 1f)] private float edgeOnThreshold = 0.2f; // edge-on 판정 기준
        [SerializeField] private float edgeJitterAngle = 28f;                 // 카메라 쪽 보정 각도
        [SerializeField] private float edgeScaleMultiplier = 1.6f;            // edge-on 상태 확대 배율

        /// <summary>유효한 edge-on 기준값을 반환한다.</summary>
        public float EdgeOnThreshold => Mathf.Clamp01(edgeOnThreshold);

        /// <summary>유효한 edge-on 보정 각도를 반환한다.</summary>
        public float EdgeJitterAngle => Mathf.Max(0f, edgeJitterAngle);

        /// <summary>유효한 edge-on 확대 배율을 반환한다.</summary>
        public float EdgeScaleMultiplier => Mathf.Max(0.01f, edgeScaleMultiplier);
    }
}
