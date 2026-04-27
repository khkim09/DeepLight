using System.Collections.Generic;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>테스트용 Mock 진행도 질의 구현체</summary>
    public class MockWorldProgressQuery : IWorldProgressQuery
    {
        private readonly Dictionary<string, int> _upgrades = new(); // upgradeId -> level
        private readonly HashSet<string> _logs = new();
        private readonly HashSet<string> _narrativeFlags = new();
        private readonly HashSet<string> _talkStates = new();
        private readonly HashSet<string> _relics = new();
        private readonly HashSet<ZoneId> _discoveredZones = new();
        private readonly HashSet<ZoneId> _unlockedZones = new();

        private int _hullTier = 1;
        private int _depthLevel = 1;
        private float _sensorAccuracy = 0.5f;
        private int _logCount = 0;

        /// <summary>업그레이드 추가</summary>
        public void AddUpgrade(string upgradeId, int level = 1)
        {
            _upgrades[upgradeId] = level;
        }

        /// <summary>업그레이드 레벨 설정</summary>
        public void SetUpgradeLevel(string upgradeId, int level)
        {
            _upgrades[upgradeId] = level;
        }

        /// <summary>로그 추가</summary>
        public void AddLog(string logId)
        {
            _logs.Add(logId);
            _logCount++;
        }

        /// <summary>서사 플래그 추가</summary>
        public void AddNarrativeFlag(string flagKey)
        {
            _narrativeFlags.Add(flagKey);
        }

        /// <summary>대화 상태 추가</summary>
        public void AddTalkState(string talkStateId)
        {
            _talkStates.Add(talkStateId);
        }

        /// <summary>유물 추가</summary>
        public void AddRelic(string relicId)
        {
            _relics.Add(relicId);
        }

        /// <summary>선체 티어 설정</summary>
        public void SetHullTier(int tier)
        {
            _hullTier = tier;
        }

        /// <summary>깊이 레벨 설정</summary>
        public void SetDepthLevel(int level)
        {
            _depthLevel = level;
        }

        /// <summary>센서 정확도 설정</summary>
        public void SetSensorAccuracy(float accuracy)
        {
            _sensorAccuracy = Mathf.Clamp01(accuracy);
        }

        /// <summary>존 발견 표시</summary>
        public void DiscoverZone(ZoneId zoneId)
        {
            _discoveredZones.Add(zoneId);
        }

        /// <summary>존 해금 표시</summary>
        public void UnlockZone(ZoneId zoneId)
        {
            _unlockedZones.Add(zoneId);
        }

        // IWorldProgressQuery 구현

        public bool HasUpgrade(string upgradeId)
        {
            return _upgrades.ContainsKey(upgradeId);
        }

        public int GetUpgradeLevel(string upgradeId)
        {
            return _upgrades.TryGetValue(upgradeId, out int level) ? level : 0;
        }

        public bool HasLog(string logId)
        {
            return _logs.Contains(logId);
        }

        public int GetLogCount()
        {
            return _logCount;
        }

        public bool HasNarrativeFlag(string flagKey)
        {
            return _narrativeFlags.Contains(flagKey);
        }

        public bool HasTalkState(string talkStateId)
        {
            return _talkStates.Contains(talkStateId);
        }

        public bool HasRelic(string relicId)
        {
            return _relics.Contains(relicId);
        }

        public int GetCurrentHullTier()
        {
            return _hullTier;
        }

        public int GetCurrentDepthLevel()
        {
            return _depthLevel;
        }

        public float GetCurrentSensorAccuracy()
        {
            return _sensorAccuracy;
        }

        public bool IsZoneDiscovered(ZoneId zoneId)
        {
            return _discoveredZones.Contains(zoneId);
        }

        public bool IsZoneUnlocked(ZoneId zoneId)
        {
            return _unlockedZones.Contains(zoneId);
        }

        /// <summary>디버그용 상태 출력</summary>
        public string GetDebugState()
        {
            return $"Upgrades: {_upgrades.Count}, Logs: {_logCount}, Flags: {_narrativeFlags.Count}, " +
                   $"Talk States: {_talkStates.Count}, Relics: {_relics.Count}, " +
                   $"Hull: {_hullTier}, Depth: {_depthLevel}, Sensor: {_sensorAccuracy:P0}, " +
                   $"Discovered Zones: {_discoveredZones.Count}, Unlocked Zones: {_unlockedZones.Count}";
        }
    }
}
