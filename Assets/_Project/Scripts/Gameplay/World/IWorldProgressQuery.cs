using Project.Data.World;

namespace Project.Gameplay.World
{
    /// <summary>월드 진행도 상태를 질의하는 인터페이스 (업그레이드, 로그, 서사 플래그 등)</summary>
    public interface IWorldProgressQuery
    {
        /// <summary>특정 업그레이드 보유 여부 확인</summary>
        bool HasUpgrade(string upgradeId);

        /// <summary>특정 업그레이드 레벨 확인</summary>
        int GetUpgradeLevel(string upgradeId);

        /// <summary>특정 로그 보유 여부 확인</summary>
        bool HasLog(string logId);

        /// <summary>보유한 로그 총 수량 확인</summary>
        int GetLogCount();

        /// <summary>특정 서사 플래그 달성 여부 확인</summary>
        bool HasNarrativeFlag(string flagKey);

        /// <summary>특정 대화 상태 달성 여부 확인</summary>
        bool HasTalkState(string talkStateId);

        /// <summary>특정 유물 보유 여부 확인</summary>
        bool HasRelic(string relicId);

        /// <summary>현재 선체 티어 확인</summary>
        int GetCurrentHullTier();

        /// <summary>현재 깊이 레벨 확인</summary>
        int GetCurrentDepthLevel();

        /// <summary>현재 센서 정확도 확인 (0-1)</summary>
        float GetCurrentSensorAccuracy();

        /// <summary>존 발견 여부 확인</summary>
        bool IsZoneDiscovered(ZoneId zoneId);

        /// <summary>존 해금 여부 확인 (캐시된 상태)</summary>
        bool IsZoneUnlocked(ZoneId zoneId);
    }
}
