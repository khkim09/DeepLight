using Project.Data.Harvest;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.Interaction
{
    /// <summary>채집 타깃의 상호작용 존과 프레이밍 거리를 계산하는 클래스</summary>
    [RequireComponent(typeof(SphereCollider))]
    public class HarvestInteractionZone : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HarvestTargetBehaviour targetBehaviour; // 부모 타깃 참조
        [SerializeField] private HarvestInteractionTuningSO tuning; // 상호작용 존 튜닝

        private SphereCollider cachedSphereCollider; // 스피어 콜라이더 캐시

        public HarvestTargetBehaviour TargetBehaviour => targetBehaviour;
        public IHarvestTarget HarvestTarget => targetBehaviour;

        /// <summary>기본 참조를 자동 보정한다</summary>
        private void Reset()
        {
            cachedSphereCollider = GetComponent<SphereCollider>();
            cachedSphereCollider.isTrigger = true;

            if (targetBehaviour == null)
                targetBehaviour = GetComponentInParent<HarvestTargetBehaviour>();

            RebuildZone();
        }

        /// <summary>런타임 시작 시 존 정보를 갱신한다</summary>
        private void Awake()
        {
            cachedSphereCollider = GetComponent<SphereCollider>();
            cachedSphereCollider.isTrigger = true;

            if (targetBehaviour == null)
                targetBehaviour = GetComponentInParent<HarvestTargetBehaviour>();

            RebuildZone();
        }

        /// <summary>타깃 바운드 기준으로 상호작용 존을 다시 계산한다</summary>
        [ContextMenu("Rebuild Interaction Zone")]
        public void RebuildZone()
        {
            if (cachedSphereCollider == null)
                cachedSphereCollider = GetComponent<SphereCollider>();

            if (tuning == null)
                return;

            Bounds bounds = GetTargetBounds();

            // 타깃 중심을 현재 존 로컬 기준으로 맞춘다.
            Vector3 localCenter = transform.InverseTransformPoint(bounds.center);
            cachedSphereCollider.center = localCenter;

            // 타깃 시각 크기에 맞춰 존 반경을 자동 조절한다.
            float targetRadius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
            float computedRadius = targetRadius * tuning.InteractionRadiusMultiplier + tuning.InteractionRadiusPadding;
            cachedSphereCollider.radius = Mathf.Clamp(computedRadius, tuning.MinInteractionRadius, tuning.MaxInteractionRadius);
        }

        /// <summary>타깃 중심점을 반환한다</summary>
        public Vector3 GetTargetCenter()
        {
            return GetTargetBounds().center;
        }

        /// <summary>타깃 전체 월드 바운드를 반환한다</summary>
        public Bounds GetTargetBounds()
        {
            if (targetBehaviour == null)
                return new Bounds(transform.position, Vector3.one);

            Renderer[] renderers = targetBehaviour.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length > 0)
            {
                Bounds combined = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                    combined.Encapsulate(renderers[i].bounds);

                return combined;
            }

            Collider[] colliders = targetBehaviour.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                Bounds combined = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                    combined.Encapsulate(colliders[i].bounds);

                return combined;
            }

            return new Bounds(targetBehaviour.transform.position, Vector3.one);
        }

        /// <summary>카메라 FOV 기준의 적정 프레이밍 거리를 계산한다</summary>
        public float EvaluateFramingDistance(Camera referenceCamera)
        {
            Bounds bounds = GetTargetBounds();
            float visualRadius = Mathf.Max(bounds.extents.magnitude, 0.5f);

            // FOV 기준으로 화면에 타깃이 적절히 들어오도록 거리 계산
            float fov = referenceCamera != null ? referenceCamera.fieldOfView : 60f;
            float distance = visualRadius / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            if (tuning == null)
                return distance;

            distance += tuning.RadiusPadding;
            return Mathf.Clamp(distance, tuning.MinFramingDistance, tuning.MaxFramingDistance);
        }
    }
}
