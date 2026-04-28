using System.Collections.Generic;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// GeneratedWorldRoot에 붙는 Registry component.
    /// GeneratedWorldZoneNode 100개를 수집하고 ZoneId 기반 조회를 제공한다.
    /// </summary>
    public class GeneratedWorldZoneRegistry : MonoBehaviour
    {
        [Header("Runtime State")]
        [SerializeField] private List<GeneratedWorldZoneNode> nodes = new List<GeneratedWorldZoneNode>();
        [SerializeField] private bool isInitialized;
        [SerializeField] private int nodeCount;

        // Runtime lookup dictionary (Inspector 미표시)
        private Dictionary<ZoneId, GeneratedWorldZoneNode> nodeByZoneId = new Dictionary<ZoneId, GeneratedWorldZoneNode>();

        /// <summary>등록된 모든 ZoneNode 목록 (Inspector 확인용)</summary>
        public IReadOnlyList<GeneratedWorldZoneNode> Nodes => nodes.AsReadOnly();

        /// <summary>초기화 완료 여부</summary>
        public bool IsInitialized => isInitialized;

        /// <summary>등록된 ZoneNode 개수</summary>
        public int NodeCount => nodeCount;

        /// <summary>
        /// ZoneRoots 부모 Transform 하위의 모든 ZoneRoot_XX를 찾아
        /// GeneratedWorldZoneNode를 수집하고 dictionary를 재구축한다.
        /// </summary>
        public void RebuildFromChildren(Transform zoneRootsParent)
        {
            nodes.Clear();
            nodeByZoneId.Clear();

            if (zoneRootsParent == null)
            {
                UnityEngine.Debug.LogWarning("[GeneratedWorldZoneRegistry] zoneRootsParent is null. Cannot rebuild.");
                isInitialized = false;
                nodeCount = 0;
                return;
            }

            // ZoneRootsParent의 모든 자식 중 ZoneRoot_* 패턴을 찾는다
            for (int i = 0; i < zoneRootsParent.childCount; i++)
            {
                Transform child = zoneRootsParent.GetChild(i);
                if (child == null) continue;

                // 이름이 ZoneRoot_로 시작하는지 확인
                if (!child.name.StartsWith("ZoneRoot_")) continue;

                // GeneratedWorldZoneNode 컴포넌트 찾기
                GeneratedWorldZoneNode node = child.GetComponent<GeneratedWorldZoneNode>();
                if (node == null)
                {
                    UnityEngine.Debug.LogWarning($"[GeneratedWorldZoneRegistry] Missing GeneratedWorldZoneNode on {child.name}. Skipping.");
                    continue;
                }

                // 중복 ZoneId 검사
                ZoneId zoneId = node.ZoneId;
                if (nodeByZoneId.ContainsKey(zoneId))
                {
                    UnityEngine.Debug.LogWarning($"[GeneratedWorldZoneRegistry] Duplicate ZoneId '{zoneId}' detected. Keeping first entry, skipping '{child.name}'.");
                    continue;
                }

                nodes.Add(node);
                nodeByZoneId[zoneId] = node;
            }

            nodeCount = nodes.Count;
            isInitialized = true;

            UnityEngine.Debug.Log($"[GeneratedWorldZoneRegistry] Rebuilt: {nodeCount} nodes registered.");
        }

        /// <summary>
        /// ZoneId로 ZoneNode를 조회한다.
        /// </summary>
        public bool TryGetNode(ZoneId zoneId, out GeneratedWorldZoneNode node)
        {
            return nodeByZoneId.TryGetValue(zoneId, out node);
        }

        /// <summary>
        /// 등록된 모든 ZoneNode 목록을 반환한다.
        /// </summary>
        public IReadOnlyList<GeneratedWorldZoneNode> GetAllNodes()
        {
            return nodes.AsReadOnly();
        }

        /// <summary>
        /// 월드 좌표에 가장 가까운 ZoneNode를 찾는다.
        /// </summary>
        public bool TryFindNearestNode(Vector3 worldPosition, out GeneratedWorldZoneNode node)
        {
            node = null;
            if (nodes.Count == 0) return false;

            float closestDistSqr = float.MaxValue;
            GeneratedWorldZoneNode closest = null;

            for (int i = 0; i < nodes.Count; i++)
            {
                GeneratedWorldZoneNode candidate = nodes[i];
                if (candidate == null) continue;

                float distSqr = (candidate.ZoneCenter - worldPosition).sqrMagnitude;
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closest = candidate;
                }
            }

            node = closest;
            return node != null;
        }

        /// <summary>
        /// ZoneResolver를 사용하여 월드 좌표에 해당하는 ZoneNode를 조회한다.
        /// 1순위: ZoneResolver 좌표 계산
        /// 2순위: currentZoneId가 유효하면 해당 Zone 유지
        /// 3순위: 가장 가까운 Zone
        /// </summary>
        public bool TryGetNodeByPosition(ZoneResolver resolver, Vector3 worldPosition, ZoneId? currentZoneId, out GeneratedWorldZoneNode node)
        {
            node = null;

            if (resolver == null) return false;

            // 1순위: ZoneResolver 좌표 계산
            if (resolver.TryResolveZone(worldPosition, currentZoneId, out ZoneId zoneId))
            {
                if (TryGetNode(zoneId, out node))
                {
                    return true;
                }
            }

            // 2순위: currentZoneId가 유효하면 해당 Zone 유지
            if (currentZoneId.HasValue && TryGetNode(currentZoneId.Value, out node))
            {
                return true;
            }

            // 3순위: 가장 가까운 Zone
            return TryFindNearestNode(worldPosition, out node);
        }
    }
}
