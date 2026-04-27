using Project.Data.World;
using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// IWorldAmbientApplier의 디버그 구현체.
    /// ZoneAmbientProfileSO의 값을 받아 Camera.backgroundColor, RenderSettings.fog 등을 직접 변경한다.
    /// 실제 프로덕션에서는 이 클래스를 ProductionAmbientApplier로 교체하거나,
    /// 내부 로직을 확장하여 FogVolume / AudioManager / PostProcessVolume에 연결한다.
    ///
    /// 안전 설계:
    /// - Camera 참조가 없으면 아무것도 하지 않음
    /// - RenderSettings.fog 변경은 applyFog 옵션으로 제어 가능
    /// - BGM 키는 실제 오디오 재생 대신 로그만 출력
    /// - 모든 프로퍼티는 nullable-safe
    /// </summary>
    public class DebugWorldAmbientApplier : IWorldAmbientApplier
    {
        [Header("References")]
        private Camera _targetCamera; // 적용 대상 카메라 (null이면 Camera.main 사용)
        private bool _applyFog = true; // RenderSettings.fog 변경 허용 여부
        private bool _applyAmbientLight = true; // RenderSettings.ambientLight 변경 허용 여부
        private bool _logBgmChanges = true; // BGM 키 변경 로그 출력 여부

        // 내부 상태
        private string _currentProfileId = ""; // 현재 적용된 프로필 ID (중복 적용 방지)
        private bool _isInitialized;

        /// <summary>DebugWorldAmbientApplier 생성</summary>
        public DebugWorldAmbientApplier(Camera targetCamera = null, bool applyFog = true, bool applyAmbientLight = true, bool logBgmChanges = true)
        {
            _targetCamera = targetCamera;
            _applyFog = applyFog;
            _applyAmbientLight = applyAmbientLight;
            _logBgmChanges = logBgmChanges;
        }

        /// <summary>Applier 초기화 (Camera.main fallback)</summary>
        public void Initialize()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
                if (_targetCamera == null)
                {
                    UnityEngine.Debug.LogWarning("[DebugWorldAmbientApplier] No camera assigned and Camera.main not found. Ambient visual changes will be skipped.");
                }
            }

            _isInitialized = true;
            UnityEngine.Debug.Log("[DebugWorldAmbientApplier] Initialized.");
        }

        /// <summary>주어진 AmbientProfile을 씬에 적용</summary>
        public void ApplyProfile(ZoneAmbientProfileSO profile)
        {
            // null 프로필은 fallback 처리
            if (profile == null || !profile.IsValid())
            {
                UnityEngine.Debug.LogWarning("[DebugWorldAmbientApplier] Attempted to apply null/invalid profile. Falling back to neutral.");
                ApplyNeutralDefaults();
                return;
            }

            // 중복 적용 방지: 같은 프로필이면 스킵
            if (_currentProfileId == profile.ProfileId)
                return;

            _currentProfileId = profile.ProfileId;

            UnityEngine.Debug.Log($"[DebugWorldAmbientApplier] Applying profile: '{profile.ProfileId}' ({profile.DisplayName})");

            // 1. Camera 배경색 적용
            if (_targetCamera != null)
            {
                _targetCamera.backgroundColor = profile.BackgroundColor;
            }

            // 2. RenderSettings 안개 적용
            if (_applyFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = profile.FogColor;
                RenderSettings.fogDensity = profile.FogDensity;
            }

            // 3. RenderSettings 앰비언트 라이트 적용
            if (_applyAmbientLight)
            {
                RenderSettings.ambientLight = profile.AmbientLightColor;
            }

            // 4. BGM 키 로그 출력 (실제 오디오 재생은 아직 미연동)
            if (_logBgmChanges && !string.IsNullOrEmpty(profile.BgmStateKey))
            {
                UnityEngine.Debug.Log($"[DebugWorldAmbientApplier] BGM state key would change to: '{profile.BgmStateKey}' (audio system not yet connected)");
            }

            // 5. 위험 경고 정보 로그
            if (profile.ShowDangerWarning)
            {
                UnityEngine.Debug.LogWarning($"[DebugWorldAmbientApplier] DANGER WARNING ACTIVE: Intensity={profile.RiskOverlayIntensity:P0}, Color={profile.RiskOverlayColor}");
            }
        }

        /// <summary>Out-of-bounds fallback 프로필 적용</summary>
        public void ApplyOutOfBoundsFallback()
        {
            // 이미 fallback 상태면 중복 적용 방지
            if (_currentProfileId == "__out_of_bounds_fallback")
                return;

            _currentProfileId = "__out_of_bounds_fallback";

            UnityEngine.Debug.Log("[DebugWorldAmbientApplier] Applying out-of-bounds fallback (neutral/dim).");

            ApplyNeutralDefaults();
        }

        /// <summary>현재 적용된 프로필 ID 반환</summary>
        public string GetCurrentProfileId()
        {
            return _currentProfileId;
        }

        /// <summary>Applier 활성화 여부</summary>
        public bool IsActive => _isInitialized;

        /// <summary>중립 기본값으로 리셋 (Camera.backgroundColor, RenderSettings 초기화)</summary>
        private void ApplyNeutralDefaults()
        {
            if (_targetCamera != null)
            {
                // 중립적인 수중 배경색 (진한 청록)
                _targetCamera.backgroundColor = new Color(0.05f, 0.08f, 0.12f);
            }

            if (_applyFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.1f, 0.12f, 0.15f);
                RenderSettings.fogDensity = 0.015f;
            }

            if (_applyAmbientLight)
            {
                RenderSettings.ambientLight = new Color(0.2f, 0.22f, 0.25f);
            }

            if (_logBgmChanges)
            {
                UnityEngine.Debug.Log("[DebugWorldAmbientApplier] BGM state key would reset to neutral (audio system not yet connected)");
            }
        }
    }
}
