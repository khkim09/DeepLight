using System.Collections.Generic;

namespace Project.Gameplay.Services
{
    /// <summary>도감 등록 시점과 분석 상태를 인게임 시간 기준으로 관리하는 서비스이다.</summary>
    public class EncyclopediaService
    {
        // 아이템 ID와 해당 아이템을 처음 발견했을 때의 '인게임 누적 시간(Hours)'을 저장
        private readonly Dictionary<string, float> discoveryRecords = new();

        /// <summary>아이템이 도감에 등록되어 있는지 확인한다.</summary>
        public bool IsItemDiscovered(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId)) return false;
            return discoveryRecords.ContainsKey(itemId);
        }

        /// <summary>새로운 아이템을 도감에 등록하고 현재 인게임 시간을 기록한다.</summary>
        public void DiscoverItem(string itemId, float currentInGameHours)
        {
            if (string.IsNullOrWhiteSpace(itemId) || IsItemDiscovered(itemId)) return;
            discoveryRecords.Add(itemId, currentInGameHours);
        }

        /// <summary>유물의 분석이 완료되었는지 확인한다.</summary>
        public bool IsRelicAnalysisComplete(string itemId, float currentInGameHours, float requiredAnalysisHours)
        {
            if (!discoveryRecords.TryGetValue(itemId, out float discoveryTime)) return false;

            // 현재 누적 인게임 시간에서 발견 당시의 시간을 뺀 값이 요구 시간 이상인지 확인
            return (currentInGameHours - discoveryTime) >= requiredAnalysisHours;
        }
    }
}
