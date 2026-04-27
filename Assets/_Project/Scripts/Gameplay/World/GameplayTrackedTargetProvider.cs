using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 실제 게임플레이용 추적 대상 제공자.
    /// 외부에서 Transform을 주입받아 사용 (DeepLightTestBedBootstrapper 등에서 할당).
    ///
    /// 사용 시나리오:
    /// - WorldMapBootstrapBridge가 플레이어/잠수함 Transform을 주입
    /// - 인스펙터에서 직접 할당
    /// - Player 태그 fallback (디버그/테스트 호환성)
    ///
    /// DebugTrackedTargetProvider와의 차이점:
    /// - 명시적 주입을 기본으로 함 (태그 검색은 fallback)
    /// - 게임플레이 흐름에서 SetTarget() 호출을 기대
    /// - late binding 시 스팸 로그 없이 조용히 대기
    /// </summary>
    public class GameplayTrackedTargetProvider : IWorldMapTrackedTargetProvider
    {
        private Transform _target;
        private bool _hasLoggedAcquired;

        /// <summary>GameplayTrackedTargetProvider 생성</summary>
        /// <param name="initialTarget">초기 Transform (null 허용, 나중에 SetTarget()으로 설정 가능)</param>
        public GameplayTrackedTargetProvider(Transform initialTarget = null)
        {
            _target = initialTarget;
        }

        /// <summary>추적 대상 설정 (late binding 지원, 재설정 가능)</summary>
        public void SetTarget(Transform target)
        {
            if (_target != target)
            {
                _target = target;
                _hasLoggedAcquired = false;

                if (target != null)
                {
                    UnityEngine.Debug.Log($"[GameplayTrackedTargetProvider] Target set: {target.name}");
                }
            }
        }

        public Transform CurrentTarget => _target;

        public bool IsTargetAvailable => _target != null;

        public Vector3 CurrentWorldPosition => _target != null ? _target.position : Vector3.zero;

        /// <summary>대상이 아직 없어 대기 중인지 여부 (로그 스팸 없이 실제 상태 반영)</summary>
        public bool IsWaitingForTarget => _target == null;

        /// <summary>대상 획득 시도 (late binding, 매 프레임 호출 가능, 로그 스팸 없음)</summary>
        public bool TryAcquireTarget()
        {
            if (_target != null)
            {
                if (!_hasLoggedAcquired)
                {
                    UnityEngine.Debug.Log($"[GameplayTrackedTargetProvider] Target acquired: {_target.name}");
                    _hasLoggedAcquired = true;
                }
                return false;
            }

            return false;
        }
    }
}
