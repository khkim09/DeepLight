using System.Collections.Generic;
using UnityEngine;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// Zone Content Marker Registry.
    /// GeneratedWorldRoot 또는 ZoneRoots 상위에 부착되어 하위의 모든 WorldMapZoneContentMarker를 수집/cache한다.
    /// zoneId, markerType 기준 조회 메서드를 제공하며, 중복 markerId 검사 로직을 포함한다.
    /// </summary>
    public class WorldMapZoneContentRegistry : MonoBehaviour
    {
        [Header("Registry State")]
        [SerializeField] private int _cachedMarkerCount;

        private List<WorldMapZoneContentMarker> _markers = new List<WorldMapZoneContentMarker>();
        private Dictionary<string, List<WorldMapZoneContentMarker>> _markersByZone = new Dictionary<string, List<WorldMapZoneContentMarker>>();
        private Dictionary<WorldMapZoneContentMarkerType, List<WorldMapZoneContentMarker>> _markersByType = new Dictionary<WorldMapZoneContentMarkerType, List<WorldMapZoneContentMarker>>();
        private HashSet<string> _markerIds = new HashSet<string>();

        private bool _cacheValid = false;

        /// <summary>모든 등록된 marker의 읽기 전용 리스트</summary>
        public IReadOnlyList<WorldMapZoneContentMarker> Markers
        {
            get
            {
                EnsureCache();
                return _markers;
            }
        }

        /// <summary>캐시된 marker 총 개수</summary>
        public int CachedMarkerCount => _cachedMarkerCount;

        /// <summary>
        /// 지정된 zoneId에 속한 모든 marker를 반환한다.
        /// </summary>
        /// <param name="zoneId">Zone ID (예: "A1", "B5")</param>
        /// <returns>해당 zone의 marker 리스트 (없으면 빈 리스트)</returns>
        public IReadOnlyList<WorldMapZoneContentMarker> GetMarkersByZone(string zoneId)
        {
            EnsureCache();
            if (string.IsNullOrEmpty(zoneId) || !_markersByZone.TryGetValue(zoneId, out var list))
                return System.Array.Empty<WorldMapZoneContentMarker>();
            return list;
        }

        /// <summary>
        /// 지정된 markerType의 모든 marker를 반환한다.
        /// </summary>
        /// <param name="type">marker 타입</param>
        /// <returns>해당 타입의 marker 리스트 (없으면 빈 리스트)</returns>
        public IReadOnlyList<WorldMapZoneContentMarker> GetMarkersByType(WorldMapZoneContentMarkerType type)
        {
            EnsureCache();
            if (!_markersByType.TryGetValue(type, out var list))
                return System.Array.Empty<WorldMapZoneContentMarker>();
            return list;
        }

        /// <summary>
        /// 지정된 zoneId와 markerType을 모두 만족하는 marker를 반환한다.
        /// </summary>
        /// <param name="zoneId">Zone ID (예: "A1", "B5")</param>
        /// <param name="type">marker 타입</param>
        /// <returns>조건을 만족하는 marker 리스트 (없으면 빈 리스트)</returns>
        public IReadOnlyList<WorldMapZoneContentMarker> GetMarkersByZoneAndType(string zoneId, WorldMapZoneContentMarkerType type)
        {
            EnsureCache();
            if (string.IsNullOrEmpty(zoneId))
                return System.Array.Empty<WorldMapZoneContentMarker>();

            if (!_markersByZone.TryGetValue(zoneId, out var zoneMarkers))
                return System.Array.Empty<WorldMapZoneContentMarker>();

            // zoneMarkers 중에서 type이 일치하는 것만 필터링
            List<WorldMapZoneContentMarker> result = new List<WorldMapZoneContentMarker>();
            for (int i = 0; i < zoneMarkers.Count; i++)
            {
                if (zoneMarkers[i] != null && zoneMarkers[i].MarkerType == type)
                {
                    result.Add(zoneMarkers[i]);
                }
            }
            return result;
        }

        /// <summary>
        /// 캐시를 강제로 재구축한다.
        /// 하위 Transform을 재귀적으로 검색하여 모든 WorldMapZoneContentMarker를 수집한다.
        /// </summary>
        public void RebuildCache()
        {
            _markers.Clear();
            _markersByZone.Clear();
            _markersByType.Clear();
            _markerIds.Clear();

            // 하위 모든 WorldMapZoneContentMarker 수집
            GetComponentsInChildren(true, _markers);

            // 딕셔너리 구축
            for (int i = 0; i < _markers.Count; i++)
            {
                WorldMapZoneContentMarker marker = _markers[i];
                if (marker == null) continue;

                // zoneId 기준
                string zoneId = marker.ZoneId;
                if (!string.IsNullOrEmpty(zoneId))
                {
                    if (!_markersByZone.TryGetValue(zoneId, out var zoneList))
                    {
                        zoneList = new List<WorldMapZoneContentMarker>();
                        _markersByZone[zoneId] = zoneList;
                    }
                    zoneList.Add(marker);
                }

                // markerType 기준
                WorldMapZoneContentMarkerType type = marker.MarkerType;
                if (!_markersByType.TryGetValue(type, out var typeList))
                {
                    typeList = new List<WorldMapZoneContentMarker>();
                    _markersByType[type] = typeList;
                }
                typeList.Add(marker);

                // markerId 수집 (중복 검사용)
                string markerId = marker.MarkerId;
                if (!string.IsNullOrEmpty(markerId))
                {
                    _markerIds.Add(markerId);
                }
            }

            _cachedMarkerCount = _markers.Count;
            _cacheValid = true;
        }

        /// <summary>
        /// 모든 markerId가 유일한지 검사한다.
        /// </summary>
        /// <returns>중복이 없으면 true</returns>
        public bool ValidateUniqueMarkerIds()
        {
            EnsureCache();
            var seen = new HashSet<string>();
            bool allUnique = true;

            for (int i = 0; i < _markers.Count; i++)
            {
                if (_markers[i] == null) continue;
                string id = _markers[i].MarkerId;
                if (string.IsNullOrEmpty(id))
                {
                    UnityEngine.Debug.LogWarning($"[WorldMapZoneContentRegistry] Marker at index {i} has null/empty MarkerId.", this);
                    allUnique = false;
                    continue;
                }
                if (!seen.Add(id))
                {
                    UnityEngine.Debug.LogWarning($"[WorldMapZoneContentRegistry] Duplicate MarkerId found: '{id}'.", this);
                    allUnique = false;
                }
            }
            return allUnique;
        }

        /// <summary>
        /// 캐시가 유효하지 않으면 RebuildCache를 호출한다.
        /// </summary>
        private void EnsureCache()
        {
            if (!_cacheValid)
            {
                RebuildCache();
            }
        }

        private void OnValidate()
        {
            // Editor에서 Inspector 값이 변경되면 캐시 무효화
            _cacheValid = false;
        }

        private void Awake()
        {
            // Runtime 시작 시 캐시 구축
            RebuildCache();
        }
    }
}
