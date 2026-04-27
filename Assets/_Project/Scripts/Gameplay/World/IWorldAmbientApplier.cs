using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// 존 기반 분위기(Ambient)를 실제 씬에 적용하는 추상화 계층.
    /// IWorldAmbientApplier를 구현하는 클래스는 ZoneAmbientProfileSO의 값을 받아
    /// 카메라 배경색, 안개, 조명, BGM, 위험 오버레이 등을 실제로 적용한다.
    ///
    /// 설계 의도:
    /// - DebugWorldAmbientApplier: 디버그/테스트용 (Camera.backgroundColor, RenderSettings 직접 변경)
    /// - ProductionAmbientApplier (미구현): 실제 FogVolume, AudioManager, PostProcessVolume 연동
    ///
    /// 이 인터페이스 덕분에 WorldMapAmbientReactionController는 Applier 구현체를 몰라도 된다.
    /// </summary>
    public interface IWorldAmbientApplier
    {
        /// <summary>Applier 초기화 (Camera 참조 등)</summary>
        void Initialize();

        /// <summary>주어진 AmbientProfile을 씬에 적용</summary>
        void ApplyProfile(ZoneAmbientProfileSO profile);

        /// <summary>Out-of-bounds fallback 프로필 적용</summary>
        void ApplyOutOfBoundsFallback();

        /// <summary>현재 적용된 프로필 ID 반환 (중복 적용 방지용)</summary>
        string GetCurrentProfileId();

        /// <summary>Applier가 활성화되어 있는지 여부</summary>
        bool IsActive { get; }
    }
}
