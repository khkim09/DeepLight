using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>채집 상호작용 존 크기와 프레이밍 거리 수치를 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestInteractionTuningSO",
        menuName = "Project/Harvest/Harvest Interaction Tuning")]
    public class HarvestInteractionTuningSO : ScriptableObject
    {
        [Header("Interaction Radius")]
        [SerializeField] private float interactionRadiusMultiplier = 1.25f; // 타깃 반경 곱 배수
        [SerializeField] private float interactionRadiusPadding = 1.2f;     // 추가 반경 여유값
        [SerializeField] private float minInteractionRadius = 1.5f;         // 최소 상호작용 반경
        [SerializeField] private float maxInteractionRadius = 12f;          // 최대 상호작용 반경

        [Header("Framing Distance")]
        [SerializeField] private float radiusPadding = 1.5f;       // 프레이밍 거리 여유값
        [SerializeField] private float minFramingDistance = 3f;    // 최소 프레이밍 거리
        [SerializeField] private float maxFramingDistance = 20f;   // 최대 프레이밍 거리

        /// <summary>상호작용 반경 곱 배수를 반환한다.</summary>
        public float InteractionRadiusMultiplier => Mathf.Max(0f, interactionRadiusMultiplier);

        /// <summary>상호작용 반경 여유값을 반환한다.</summary>
        public float InteractionRadiusPadding => Mathf.Max(0f, interactionRadiusPadding);

        /// <summary>최소 상호작용 반경을 반환한다.</summary>
        public float MinInteractionRadius => Mathf.Max(0.01f, minInteractionRadius);

        /// <summary>최대 상호작용 반경을 반환한다.</summary>
        public float MaxInteractionRadius => Mathf.Max(MinInteractionRadius, maxInteractionRadius);

        /// <summary>프레이밍 거리 여유값을 반환한다.</summary>
        public float RadiusPadding => Mathf.Max(0f, radiusPadding);

        /// <summary>최소 프레이밍 거리를 반환한다.</summary>
        public float MinFramingDistance => Mathf.Max(0.01f, minFramingDistance);

        /// <summary>최대 프레이밍 거리를 반환한다.</summary>
        public float MaxFramingDistance => Mathf.Max(MinFramingDistance, maxFramingDistance);
    }
}
