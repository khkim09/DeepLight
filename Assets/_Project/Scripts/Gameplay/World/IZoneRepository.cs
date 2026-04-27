using System.Collections.Generic;
using Project.Data.World;

namespace Project.Gameplay.World
{
    /// <summary>존 데이터 저장소 인터페이스 (읽기 전용 접근)</summary>
    public interface IZoneRepository
    {
        /// <summary>ZoneId로 ZoneDataSO 조회 시도</summary>
        bool TryGetZoneData(ZoneId zoneId, out ZoneDataSO zoneData);

        /// <summary>ZoneId로 ZoneDataSO 조회, 없으면 기본값 반환</summary>
        ZoneDataSO GetZoneDataOrDefault(ZoneId zoneId);

        /// <summary>해당 ZoneId에 명시적 데이터가 있는지 확인</summary>
        bool HasExplicitZoneData(ZoneId zoneId);

        /// <summary>모든 등록된 ZoneDataSO 목록 반환</summary>
        IReadOnlyList<ZoneDataSO> GetAllZoneData();
    }
}
