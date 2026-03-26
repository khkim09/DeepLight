using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.Interaction
{
    /// <summary>채집 대상 주변의 상호작용 가능 영역과 접근 프레이밍 정보를 제공하는 클래스</summary>
    [RequireComponent(typeof(SphereCollider))]
    public class HarvestInteractionZone : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HarvestTargetBehaviour targetBehaviour; // 부모 채집 타깃 참조

        [Header("Interaction Radius")]
        [SerializeField] private float interactionRadiusMultiplier = 1.35f; // 타깃 반경 대비 상호작용 반경 배율
        [SerializeField] private float interactionRadiusPadding = 1.2f; // 상호작용 반경 추가 여유
        [SerializeField] private float minInteractionRadius = 3f; // 최소 상호작용 반경
        [SerializeField] private float maxInteractionRadius = 10f; // 최대 상호작용 반경

        [Header("Framing Distance")]
        [SerializeField] private float radiusPadding = 1.25f; // 콘솔 진입 연출용 추가 거리
        [SerializeField] private float minFramingDistance = 3.5f; // 최소 접근 거리
        [SerializeField] private float maxFramingDistance = 12f; // 최대 접근 거리

        private SphereCollider cachedSphereCollider; // 트리거 스피어 캐싱

        public HarvestTargetBehaviour TargetBehaviour => targetBehaviour; // 외부 접근용 타깃 참조
        public IHarvestTarget HarvestTarget => targetBehaviour; // 인터페이스 형태의 타깃 참조

        /// <summary>필수 참조를 자동 보정한다</summary>
        private void Reset()
        {
            cachedSphereCollider = GetComponent<SphereCollider>();
            cachedSphereCollider.isTrigger = true;

            if (targetBehaviour == null)
                targetBehaviour = GetComponentInParent<HarvestTargetBehaviour>();

            RebuildZone();
        }

        /// <summary>런타임 시작 시 트리거와 반경을 보정한다</summary>
        private void Awake()
        {
            cachedSphereCollider = GetComponent<SphereCollider>();
            cachedSphereCollider.isTrigger = true;

            if (targetBehaviour == null)
                targetBehaviour = GetComponentInParent<HarvestTargetBehaviour>();

            RebuildZone();
        }

        /// <summary>타깃 바운드를 기준으로 상호작용 반경을 다시 계산한다</summary>
        [ContextMenu("Rebuild Interaction Zone")]
        public void RebuildZone()
        {
            if (cachedSphereCollider == null)
                cachedSphereCollider = GetComponent<SphereCollider>();

            Bounds bounds = GetTargetBounds();

            // 현재 오브젝트 local 기준으로 center 환산
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            cachedSphereCollider.center = localCenter;

            // 시각적 타깃 크기에 맞춰 trigger 반경 계산
            float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            float computedRadius = targetRadius * interactionRadiusMultiplier + interactionRadiusPadding;
            cachedSphereCollider.radius = Mathf.Clamp(computedRadius, minInteractionRadius, maxInteractionRadius);
        }

        /// <summary>타깃 중심점을 반환한다</summary>
        public Vector3 GetTargetCenter()
        {
            return GetTargetBounds().center;
        }

        /// <summary>타깃의 월드 바운드를 반환한다</summary>
        public Bounds GetTargetBounds()
        {
            if (targetBehaviour == null)
                return new Bounds(transform.position, Vector3.one);

            Renderer[] renderers = targetBehaviour.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                return combined;
            }

            Collider[] colliders = targetBehaviour.GetComponentsInChildren<Collider>();
            if (colliders.Length > 0)
            {
                Bounds combined = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    combined.Encapsulate(colliders[i].bounds);

                return combined;
            }

            return new Bounds(targetBehaviour.transform.position, Vector3.one);
        }

        /// <summary>카메라 FOV와 타깃 크기를 기준으로 자연스러운 접근 거리를 계산한다</summary>
        public float EvaluateFramingDistance(Camera referenceCamera)
        {
            Bounds bounds = GetTargetBounds();
            float visualRadius = Mathf.Max(bounds.extents.magnitude, 0.5f);

            float fov = referenceCamera != null ? referenceCamera.fieldOfView : 60f;
            float distance = visualRadius / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);
            distance += radiusPadding;

            return Mathf.Clamp(distance, minFramingDistance, maxFramingDistance);
        }
    }
}
