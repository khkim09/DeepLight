using System.Collections.Generic;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>존 데이터 저장소 구현체 (ZoneId → ZoneDataSO 매핑)</summary>
    public class ZoneRepository : IZoneRepository
    {
        private readonly WorldMapConfigSO _config;
        private readonly Dictionary<ZoneId, ZoneDataSO> _zoneDataMap;
        private readonly List<ZoneDataSO> _allZoneData;
        private readonly IReadOnlyList<ZoneDataSO> _allZoneDataReadOnly;

        /// <summary>ZoneRepository 생성</summary>
        /// <param name="config">월드맵 설정 (기본 존 데이터 포함)</param>
        /// <param name="zoneDataAssets">등록할 ZoneDataSO 목록</param>
        public ZoneRepository(WorldMapConfigSO config, IReadOnlyList<ZoneDataSO> zoneDataAssets)
        {
            _config = config ?? throw new System.ArgumentNullException(nameof(config));
            _zoneDataMap = new Dictionary<ZoneId, ZoneDataSO>();
            _allZoneData = new List<ZoneDataSO>();
            BuildLookup(zoneDataAssets);
            // 읽기 전용 뷰를 생성자에서 한 번만 캐시 (매 호출마다 AsReadOnly() 래퍼 생성 방지)
            _allZoneDataReadOnly = _allZoneData.AsReadOnly();
        }

        /// <summary>내부 조회 딕셔너리 구축 (생성자에서 한 번만 실행)</summary>
        private void BuildLookup(IReadOnlyList<ZoneDataSO> zoneDataAssets)
        {
            _zoneDataMap.Clear();
            _allZoneData.Clear();

            if (zoneDataAssets == null || zoneDataAssets.Count == 0)
            {
                UnityEngine.Debug.LogWarning("[ZoneRepository] No zone data assets provided. Only fallback data will be available.");
                return;
            }

            foreach (ZoneDataSO zoneData in zoneDataAssets)
            {
                if (zoneData == null)
                {
                    UnityEngine.Debug.LogWarning("[ZoneRepository] Skipping null ZoneDataSO entry.");
                    continue;
                }

                ZoneId zoneId = zoneData.ZoneId;

                // 중복 ZoneId 처리: 첫 번째 등록 유지, 경고 로그 출력
                if (_zoneDataMap.ContainsKey(zoneId))
                {
                    UnityEngine.Debug.LogWarning($"[ZoneRepository] Duplicate ZoneId '{zoneId}' detected. Keeping first entry '{_zoneDataMap[zoneId].name}', skipping '{zoneData.name}'.");
                    continue;
                }

                _zoneDataMap[zoneId] = zoneData;
                _allZoneData.Add(zoneData);
            }

            UnityEngine.Debug.Log($"[ZoneRepository] Initialized with {_zoneDataMap.Count} zone data entries.");

        }

        /// <summary>ZoneId로 ZoneDataSO 조회 시도</summary>
        public bool TryGetZoneData(ZoneId zoneId, out ZoneDataSO zoneData)
        {
            if (_zoneDataMap.TryGetValue(zoneId, out zoneData))
            {
                return true;
            }

            zoneData = null;
            return false;
        }

        /// <summary>ZoneId로 ZoneDataSO 조회, 없으면 config의 기본값 반환</summary>
        public ZoneDataSO GetZoneDataOrDefault(ZoneId zoneId)
        {
            if (_zoneDataMap.TryGetValue(zoneId, out ZoneDataSO zoneData))
            {
                return zoneData;
            }

            // 명시적 데이터가 없으면 config의 기본 존 데이터 반환
            if (_config != null && _config.DefaultZoneData != null)
            {
                return _config.DefaultZoneData;
            }

            UnityEngine.Debug.LogWarning($"[ZoneRepository] No zone data found for '{zoneId}' and no fallback available.");
            return null;
        }

        /// <summary>해당 ZoneId에 명시적 데이터가 있는지 확인</summary>
        public bool HasExplicitZoneData(ZoneId zoneId)
        {
            return _zoneDataMap.ContainsKey(zoneId);
        }

        /// <summary>모든 등록된 ZoneDataSO 목록 반환 (생성자에서 캐시된 읽기 전용 뷰)</summary>
        public IReadOnlyList<ZoneDataSO> GetAllZoneData()
        {
            return _allZoneDataReadOnly;
        }
    }
}
