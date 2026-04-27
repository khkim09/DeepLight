using Project.Data.World;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 씬에 배치되는 트리거 볼륨 MonoBehaviour.
    /// ZoneRoot 또는 ZoneDataSO를 참조하여 플레이어/타겟 Transform의 진입/퇴장을 감지한다.
    /// 아직 월드 상태의 권위적인 결정자는 WorldMapService(좌표 계산 기반)이며,
    /// 이 트리거는 씬 배치 검증 및 디버그 용도이다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ZoneTriggerRelay : MonoBehaviour
    {
        [Header("Zone Reference")]
        [SerializeField] private ZoneRoot zoneRoot; // 연결된 ZoneRoot (우선)
        [SerializeField] private ZoneDataSO zoneData; // 직접 ZoneDataSO 참조 (fallback)

        [Header("Detection Settings")]
        [SerializeField] private string targetTag = "Player"; // 감지할 태그
        [SerializeField] private Transform explicitTrackedTransform; // 명시적 추적 Transform (태그 대체)
        [SerializeField] private bool debugLogOnEnterExit = true;

        [Header("Events")]
        [SerializeField] private UnityEvent<ZoneId> onZoneEntered;
        [SerializeField] private UnityEvent<ZoneId> onZoneExited;

        // 내부 상태
        private bool _isInside;
        private Collider _triggerCollider;

        /// <summary>연결된 ZoneRoot</summary>
        public ZoneRoot ZoneRoot => zoneRoot;

        /// <summary>직접 참조된 ZoneDataSO</summary>
        public ZoneDataSO ZoneData => zoneData ?? (zoneRoot != null ? zoneRoot.GetZoneData() : null);

        /// <summary>이 트리거가 나타내는 ZoneId</summary>
        public ZoneId GetZoneId()
        {
            if (zoneRoot != null)
                return zoneRoot.GetZoneId();
            if (zoneData != null)
                return zoneData.ZoneId;
            return default;
        }

        /// <summary>현재 감지된 타겟이 트리거 내부에 있는지 여부</summary>
        public bool IsInside => _isInside;

        private void Awake()
        {
            // Collider를 트리거로 설정
            _triggerCollider = GetComponent<Collider>();
            _triggerCollider.isTrigger = true;

            // ZoneData fallback: ZoneRoot에서 가져오기
            if (zoneData == null && zoneRoot != null)
            {
                zoneData = zoneRoot.GetZoneData();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            // 태그 필터
            if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
                return;

            // 명시적 Transform이 설정되어 있으면 해당 Transform만 허용
            if (explicitTrackedTransform != null && other.transform != explicitTrackedTransform)
                return;

            _isInside = true;

            ZoneId zoneId = GetZoneId();

            if (debugLogOnEnterExit)
            {
                UnityEngine.Debug.Log($"[ZoneTriggerRelay] Entered zone '{zoneId}' (trigger: {name})", this);
            }

            onZoneEntered?.Invoke(zoneId);
        }

        private void OnTriggerExit(Collider other)
        {
            // 태그 필터
            if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag))
                return;

            // 명시적 Transform이 설정되어 있으면 해당 Transform만 허용
            if (explicitTrackedTransform != null && other.transform != explicitTrackedTransform)
                return;

            _isInside = false;

            ZoneId zoneId = GetZoneId();

            if (debugLogOnEnterExit)
            {
                UnityEngine.Debug.Log($"[ZoneTriggerRelay] Exited zone '{zoneId}' (trigger: {name})", this);
            }

            onZoneExited?.Invoke(zoneId);
        }

        /// <summary>에디터 Gizmo: 트리거 볼륨 시각화</summary>
        private void OnDrawGizmosSelected()
        {
            Collider col = GetComponent<Collider>();
            if (col == null) return;

            Gizmos.color = new Color(0f, 0.8f, 1f, 0.3f);

            if (col is BoxCollider box)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, transform.lossyScale);
                Gizmos.DrawCube(box.center, box.size);
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireCube(box.center, box.size);
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                Gizmos.DrawSphere(sphere.center, sphere.radius);
                Gizmos.color = new Color(0f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
        }

        /// <summary>컨텍스트 메뉴: ZoneRoot 자동 연결 (부모 검색)</summary>
        [ContextMenu("Auto-Assign ZoneRoot from Parent")]
        private void AutoAssignZoneRootFromParent()
        {
            zoneRoot = GetComponentInParent<ZoneRoot>();
            if (zoneRoot != null)
            {
                zoneData = zoneRoot.GetZoneData();
                UnityEngine.Debug.Log($"[ZoneTriggerRelay] {name}: Auto-assigned ZoneRoot '{zoneRoot.name}'", this);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[ZoneTriggerRelay] {name}: No ZoneRoot found in parent hierarchy.", this);
            }
        }
    }
}
