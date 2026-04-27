using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 디버그/테스트용 추적 대상 제공자.
    /// 우선순위: 명시적 Transform > Player 태그 검색 > 오브젝트 이름 검색.
    ///
    /// 사용 시나리오:
    /// - 인스펙터에서 Transform 직접 할당 (가장 빠름)
    /// - Player 태그가 붙은 오브젝트 자동 검색 (Start() 이후)
    /// - 특정 이름의 오브젝트 검색 (fallback)
    /// - WorldMapSetupTool의 Quick Setup All에서 사용
    /// </summary>
    public class DebugTrackedTargetProvider : IWorldMapTrackedTargetProvider
    {
        private Transform _explicitTarget;
        private readonly string _fallbackTag = "Player";
        private readonly string _fallbackObjectName;
        private bool _hasLoggedWaiting;
        private bool _hasLoggedAcquired;

        /// <summary>DebugTrackedTargetProvider 생성</summary>
        /// <param name="explicitTarget">명시적으로 할당된 Transform (null 허용)</param>
        /// <param name="fallbackObjectName">태그 검색 실패 시 사용할 오브젝트 이름 (null 허용)</param>
        public DebugTrackedTargetProvider(Transform explicitTarget = null, string fallbackObjectName = null)
        {
            _explicitTarget = explicitTarget;
            _fallbackObjectName = fallbackObjectName;
        }

        /// <summary>명시적 Transform 설정 (late binding 지원)</summary>
        public void SetTarget(Transform target)
        {
            _explicitTarget = target;
            _hasLoggedWaiting = false;
            _hasLoggedAcquired = false;
        }

        public Transform CurrentTarget
        {
            get
            {
                // 1. 명시적 할당
                if (_explicitTarget != null)
                    return _explicitTarget;

                // 2. 태그 검색
                GameObject player = GameObject.FindWithTag(_fallbackTag);
                if (player != null)
                    return player.transform;

                // 3. 오브젝트 이름 검색
                if (!string.IsNullOrEmpty(_fallbackObjectName))
                {
                    GameObject named = GameObject.Find(_fallbackObjectName);
                    if (named != null)
                        return named.transform;
                }

                return null;
            }
        }

        public bool IsTargetAvailable => CurrentTarget != null;

        public Vector3 CurrentWorldPosition => CurrentTarget != null ? CurrentTarget.position : Vector3.zero;

        public bool IsWaitingForTarget => !IsTargetAvailable;

        /// <summary>대상 획득 시도 (late binding, 매 프레임 호출 가능)</summary>
        public bool TryAcquireTarget()
        {
            if (IsTargetAvailable)
            {
                if (!_hasLoggedAcquired)
                {
                    UnityEngine.Debug.Log($"[DebugTrackedTargetProvider] Target acquired: {CurrentTarget.name}");
                    _hasLoggedAcquired = true;
                    _hasLoggedWaiting = false;
                }
                return false; // 이미 있음
            }

            // 아직 없음 — 한 번만 로그
            if (!_hasLoggedWaiting)
            {
                UnityEngine.Debug.Log("[DebugTrackedTargetProvider] Waiting for tracked target (tag: Player, or explicit Transform)...");
                _hasLoggedWaiting = true;
            }

            return false;
        }
    }
}
