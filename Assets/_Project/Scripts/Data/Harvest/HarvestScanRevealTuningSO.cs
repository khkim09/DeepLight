using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>회수 포인트 공개 확률과 클릭 반경 수치를 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestScanRevealTuningSO",
        menuName = "Project/Harvest/Harvest Scan Reveal Tuning")]
    public class HarvestScanRevealTuningSO : ScriptableObject
    {
        [Header("Reveal Chance")]
        [SerializeField] private float minRevealChance = 0.2f; // 최소 공개 확률
        [SerializeField] private float maxRevealChance = 0.8f; // 최대 공개 확률

        [Header("Selection Radius")]
        [SerializeField] private float pointSelectRadiusPixels = 90f; // 좌클릭 선택 허용 반경
        [SerializeField] private float pointHoverRadiusPixels = 120f; // hover 판정 허용 반경

        /// <summary>최소 공개 확률을 반환한다.</summary>
        public float MinRevealChance => Mathf.Clamp01(minRevealChance);

        /// <summary>최대 공개 확률을 반환한다.</summary>
        public float MaxRevealChance => Mathf.Clamp(maxRevealChance, MinRevealChance, 1f);

        /// <summary>선택 허용 반경을 반환한다.</summary>
        public float PointSelectRadiusPixels => Mathf.Max(1f, pointSelectRadiusPixels);

        /// <summary>hover 허용 반경을 반환한다.</summary>
        public float PointHoverRadiusPixels => Mathf.Max(1f, pointHoverRadiusPixels);
    }
}
