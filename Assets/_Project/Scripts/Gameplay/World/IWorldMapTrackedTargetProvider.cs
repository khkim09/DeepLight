using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 월드맵 시스템이 추적할 대상(Transform/위치)을 제공하는 추상화.
    ///
    /// 책임:
    /// - 현재 추적 대상의 Transform 반환
    /// - 대상이 유효한지 여부 보고
    /// - 대상이 아직 준비되지 않은 경우 late binding 지원
    ///
    /// 구현체:
    /// - DebugTrackedTargetProvider: 인스펙터 할당, 태그 검색, 명시적 Transform
    /// - GameplayTrackedTargetProvider: 실제 플레이어/잠수함 Transform
    /// </summary>
    public interface IWorldMapTrackedTargetProvider
    {
        /// <summary>추적 대상의 현재 Transform (null 가능)</summary>
        Transform CurrentTarget { get; }

        /// <summary>추적 대상이 현재 유효한지 여부</summary>
        bool IsTargetAvailable { get; }

        /// <summary>추적 대상의 현재 월드 위치 (대상이 없으면 Vector3.zero)</summary>
        Vector3 CurrentWorldPosition { get; }

        /// <summary>대상이 아직 없을 때 대기 중인지 여부 (late binding)</summary>
        bool IsWaitingForTarget { get; }

        /// <summary>대상 획득 시도 (late binding용, 매 프레임 호출 가능)</summary>
        /// <returns>이번 호출에서 새로 획득했으면 true</returns>
        bool TryAcquireTarget();
    }
}
