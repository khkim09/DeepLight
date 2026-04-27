using Project.Core.Events;
using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>런타임 월드맵 서비스 구현체 (현재 존 추적, 상태 캐시, 질의, 이벤트 발행)</summary>
    public class WorldMapService : IWorldMapService
    {
        private readonly IZoneResolver _zoneResolver;
        private readonly IZoneRepository _zoneRepository;
        private readonly IZoneStateEvaluator _zoneStateEvaluator;
        private readonly IWorldProgressQuery _progressQuery;

        private ZoneId _currentZoneId;
        private RegionId _currentRegionId;
        private ZoneRuntimeState _currentZoneState;
        private bool _hasCurrentZone;

        // Anti-spam: 마지막으로 발행한 상태 스냅샷 (같은 존+같은 상태면 재발행 방지)
        private ZoneAccessibility _lastPublishedAccessibility;
        private float _lastPublishedRiskLevel;
        private bool _lastPublishedIsDiscovered;
        private bool _lastPublishedIsUnlocked;

        /// <summary>WorldMapService 생성</summary>
        /// <param name="zoneResolver">존 좌표 변환기</param>
        /// <param name="zoneRepository">존 데이터 저장소</param>
        /// <param name="zoneStateEvaluator">존 상태 평가기</param>
        /// <param name="progressQuery">진행도 질의 인터페이스</param>
        public WorldMapService(
            IZoneResolver zoneResolver,
            IZoneRepository zoneRepository,
            IZoneStateEvaluator zoneStateEvaluator,
            IWorldProgressQuery progressQuery)
        {
            _zoneResolver = zoneResolver ?? throw new System.ArgumentNullException(nameof(zoneResolver));
            _zoneRepository = zoneRepository ?? throw new System.ArgumentNullException(nameof(zoneRepository));
            _zoneStateEvaluator = zoneStateEvaluator ?? throw new System.ArgumentNullException(nameof(zoneStateEvaluator));
            _progressQuery = progressQuery; // null 허용 (진행도 없이 기본 상태만 평가)

            _hasCurrentZone = false;
        }

        /// <summary>현재 플레이어가 위치한 ZoneId</summary>
        public ZoneId CurrentZoneId => _currentZoneId;

        /// <summary>현재 플레이어가 위치한 RegionId</summary>
        public RegionId CurrentRegionId => _currentRegionId;

        /// <summary>현재 존이 유효하게 설정되었는지 여부</summary>
        public bool HasCurrentZone => _hasCurrentZone;

        /// <summary>서비스 초기화 (초기 월드 위치로 현재 존 설정 + 이벤트 발행)</summary>
        public void Initialize(Vector3 initialWorldPosition)
        {
            // 월드 경계 내에 있는지 확인
            if (!_zoneResolver.IsWorldPositionInBounds(initialWorldPosition))
            {
                UnityEngine.Debug.LogWarning($"[WorldMapService] Initialize: Position {initialWorldPosition} is out of world bounds. Service stays uninitialized.");
                _hasCurrentZone = false;
                return;
            }

            // 안전하게 ZoneId 해석
            if (!_zoneResolver.TryGetZoneIdFromWorldPosition(initialWorldPosition, out ZoneId zoneId))
            {
                UnityEngine.Debug.LogWarning($"[WorldMapService] Initialize: Failed to resolve zone at position {initialWorldPosition}. Service stays uninitialized.");
                _hasCurrentZone = false;
                return;
            }

            // 존 데이터 조회
            ZoneDataSO zoneData = _zoneRepository.GetZoneDataOrDefault(zoneId);

            // RegionId 설정 (존 데이터가 있으면 해당 리전, 없으면 기본값)
            RegionId regionId = zoneData != null ? zoneData.RegionId : new RegionId("Unknown");

            // 상태 평가
            ZoneRuntimeState state = _zoneStateEvaluator.EvaluateZoneState(zoneId, zoneData, _progressQuery);

            // 현재 상태 업데이트
            _currentZoneId = zoneId;
            _currentRegionId = regionId;
            _currentZoneState = state;
            _hasCurrentZone = true;

            // Anti-spam 캐시 초기화
            _lastPublishedAccessibility = state.Accessibility;
            _lastPublishedRiskLevel = state.CurrentRiskLevel;
            _lastPublishedIsDiscovered = state.IsDiscovered;
            _lastPublishedIsUnlocked = state.IsUnlocked;

            UnityEngine.Debug.Log($"[WorldMapService] Initialized at zone '{zoneId}' (Region: {regionId}), State: {state.Accessibility}");

            // 초기 존 이벤트 발행 (이전 존 없음 → 빈 문자열)
            EventBus.Publish(new ZoneChangedEvent(
                previousZoneId: string.Empty,
                currentZoneId: zoneId.ToString(),
                previousRegionId: string.Empty,
                currentRegionId: regionId.ToString(),
                accessibility: (int)state.Accessibility,
                riskLevel: state.CurrentRiskLevel,
                isDiscovered: state.IsDiscovered,
                isUnlocked: state.IsUnlocked,
                trackedPositionX: initialWorldPosition.x,
                trackedPositionZ: initialWorldPosition.z
            ));
        }

        /// <summary>현재 존 갱신 (월드 위치 기반, 실제 변경 시에만 true 반환 + 이벤트 발행)</summary>
        public bool RefreshCurrentZone(Vector3 worldPosition)
        {
            // 월드 경계 밖 처리
            if (!_zoneResolver.IsWorldPositionInBounds(worldPosition))
            {
                // 이전에 존이 있었으면 클리어 이벤트 발행
                if (_hasCurrentZone)
                {
                    ZoneId lastZoneId = _currentZoneId;
                    RegionId lastRegionId = _currentRegionId;

                    _hasCurrentZone = false;

                    UnityEngine.Debug.Log($"[WorldMapService] Zone cleared: '{lastZoneId}' (out of bounds)");

                    // CurrentZoneClearedEvent 발행
                    EventBus.Publish(new CurrentZoneClearedEvent(
                        lastZoneId: lastZoneId.ToString(),
                        lastRegionId: lastRegionId.ToString(),
                        reason: "OutOfBounds"
                    ));
                }

                return false;
            }

            // 안전하게 ZoneId 해석
            if (!_zoneResolver.TryGetZoneIdFromWorldPosition(worldPosition, out ZoneId newZoneId))
            {
                return false;
            }

            // 존 데이터 조회
            ZoneDataSO zoneData = _zoneRepository.GetZoneDataOrDefault(newZoneId);

            // RegionId 설정
            RegionId newRegionId = zoneData != null ? zoneData.RegionId : new RegionId("Unknown");

            // 상태 평가
            ZoneRuntimeState newState = _zoneStateEvaluator.EvaluateZoneState(newZoneId, zoneData, _progressQuery);

            // === 존 변경 감지 ===
            bool zoneChanged = !_hasCurrentZone || !_currentZoneId.Equals(newZoneId);

            if (zoneChanged)
            {
                // 이전 존 ID 저장 (변경 감지용)
                ZoneId previousZoneId = _currentZoneId;
                RegionId previousRegionId = _currentRegionId;
                bool hadPreviousZone = _hasCurrentZone;

                // 현재 상태 업데이트
                _currentZoneId = newZoneId;
                _currentRegionId = newRegionId;
                _currentZoneState = newState;
                _hasCurrentZone = true;

                // Anti-spam 캐시 업데이트
                _lastPublishedAccessibility = newState.Accessibility;
                _lastPublishedRiskLevel = newState.CurrentRiskLevel;
                _lastPublishedIsDiscovered = newState.IsDiscovered;
                _lastPublishedIsUnlocked = newState.IsUnlocked;

                // 변경 로그
                if (hadPreviousZone)
                {
                    UnityEngine.Debug.Log($"[WorldMapService] Zone changed: '{previousZoneId}' -> '{newZoneId}' (Region: {newRegionId})");
                }
                else
                {
                    UnityEngine.Debug.Log($"[WorldMapService] Zone set: '{newZoneId}' (Region: {newRegionId})");
                }

                // ZoneChangedEvent 발행
                EventBus.Publish(new ZoneChangedEvent(
                    previousZoneId: hadPreviousZone ? previousZoneId.ToString() : string.Empty,
                    currentZoneId: newZoneId.ToString(),
                    previousRegionId: hadPreviousZone ? previousRegionId.ToString() : string.Empty,
                    currentRegionId: newRegionId.ToString(),
                    accessibility: (int)newState.Accessibility,
                    riskLevel: newState.CurrentRiskLevel,
                    isDiscovered: newState.IsDiscovered,
                    isUnlocked: newState.IsUnlocked,
                    trackedPositionX: worldPosition.x,
                    trackedPositionZ: worldPosition.z
                ));

                return true;
            }

            // === 같은 존이지만 상태가 의미 있게 변경되었는지 확인 (anti-spam) ===
            bool stateMeaningfullyChanged =
                newState.Accessibility != _lastPublishedAccessibility ||
                Mathf.Abs(newState.CurrentRiskLevel - _lastPublishedRiskLevel) > 0.01f ||
                newState.IsDiscovered != _lastPublishedIsDiscovered;

            if (stateMeaningfullyChanged)
            {
                // 이전 상태 저장
                ZoneAccessibility prevAccessibility = _lastPublishedAccessibility;
                float prevRiskLevel = _lastPublishedRiskLevel;
                bool prevIsDiscovered = _lastPublishedIsDiscovered;

                // 현재 상태 업데이트
                _currentZoneState = newState;

                // Anti-spam 캐시 업데이트
                _lastPublishedAccessibility = newState.Accessibility;
                _lastPublishedRiskLevel = newState.CurrentRiskLevel;
                _lastPublishedIsDiscovered = newState.IsDiscovered;
                _lastPublishedIsUnlocked = newState.IsUnlocked;

                UnityEngine.Debug.Log($"[WorldMapService] State changed in zone '{newZoneId}': {prevAccessibility}->{newState.Accessibility}, Risk {prevRiskLevel:F2}->{newState.CurrentRiskLevel:F2}");

                // CurrentZoneStateChangedEvent 발행
                EventBus.Publish(new CurrentZoneStateChangedEvent(
                    zoneId: newZoneId.ToString(),
                    previousAccessibility: (int)prevAccessibility,
                    currentAccessibility: (int)newState.Accessibility,
                    previousRiskLevel: prevRiskLevel,
                    currentRiskLevel: newState.CurrentRiskLevel,
                    previousIsDiscovered: prevIsDiscovered,
                    currentIsDiscovered: newState.IsDiscovered
                ));
            }

            return false;
        }

        /// <summary>현재 존의 런타임 상태 조회 시도</summary>
        public bool TryGetCurrentZoneState(out ZoneRuntimeState zoneState)
        {
            if (_hasCurrentZone && _currentZoneState != null)
            {
                zoneState = _currentZoneState;
                return true;
            }

            zoneState = null;
            return false;
        }

        /// <summary>특정 ZoneId의 런타임 상태 조회 시도</summary>
        public bool TryGetZoneState(ZoneId zoneId, out ZoneRuntimeState zoneState)
        {
            // 존 데이터 조회 (fallback 포함)
            ZoneDataSO zoneData = _zoneRepository.GetZoneDataOrDefault(zoneId);

            if (zoneData == null)
            {
                // fallback조차 없으면 상태 평가 불가
                zoneState = null;
                return false;
            }

            // 상태 평가
            zoneState = _zoneStateEvaluator.EvaluateZoneState(zoneId, zoneData, _progressQuery);
            return true;
        }

        /// <summary>특정 ZoneId의 런타임 상태 평가 (데이터 없어도 안전한 폴백 상태 반환)</summary>
        public ZoneRuntimeState GetZoneStateOrEvaluate(ZoneId zoneId)
        {
            // 존 데이터 조회 (fallback 포함)
            ZoneDataSO zoneData = _zoneRepository.GetZoneDataOrDefault(zoneId);

            if (zoneData == null)
            {
                // fallback조차 없으면 안전한 잠금 상태 생성
                UnityEngine.Debug.LogWarning($"[WorldMapService] No zone data or fallback for '{zoneId}'. Creating safe locked fallback state.");
                ZoneRuntimeState fallbackState = new ZoneRuntimeState(zoneId, null)
                {
                    Accessibility = ZoneAccessibility.Locked,
                    LockReason = ZoneLockReason.Multiple,
                    CurrentRiskLevel = 1f,
                    IsDiscovered = false
                };
                return fallbackState;
            }

            // 정상 평가
            return _zoneStateEvaluator.EvaluateZoneState(zoneId, zoneData, _progressQuery);
        }

        /// <summary>현재 존이 특정 ZoneId와 일치하는지 확인</summary>
        public bool IsCurrentZone(ZoneId zoneId)
        {
            return _hasCurrentZone && _currentZoneId.Equals(zoneId);
        }
    }
}
