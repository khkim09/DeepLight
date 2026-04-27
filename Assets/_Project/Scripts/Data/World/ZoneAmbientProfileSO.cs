using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// 존 기반 분위기/환경 프리젠테이션 프로필.
    /// BGM 키, 안개 색상/밀도, 배경색, 위험 오버레이 등을 정의한다.
    /// 아직 실제 오디오/포스트프로세싱 파이프라인에 연결되지는 않았으며,
    /// DebugWorldAmbientApplier가 이 값을 읽어 디버그 수준으로 적용한다.
    /// 추후 프로덕션에서는 이 프로필을 실제 FogVolume / AudioManager / PostProcessVolume에 매핑한다.
    /// </summary>
    [CreateAssetMenu(
        fileName = "AmbientProfile_",
        menuName = "Project/World/Zone Ambient Profile")]
    public class ZoneAmbientProfileSO : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string profileId; // 프로필 식별자 (예: "hub_safe")
        [SerializeField] private string displayName; // 표시 이름 (디버그용)

        [Header("Visual")]
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.12f, 0.15f); // Camera.backgroundColor 후보
        [SerializeField] private Color fogColor = new Color(0.2f, 0.25f, 0.3f); // RenderSettings.fogColor 후보
        [SerializeField] private float fogDensity = 0.02f; // RenderSettings.fogDensity 후보
        [SerializeField] private Color ambientLightColor = new Color(0.3f, 0.35f, 0.4f); // RenderSettings.ambientLight 후보

        [Header("Audio")]
        [SerializeField] private string bgmStateKey = ""; // BGM 상태 키 (실제 오디오 시스템 연동 전까지 로그만 출력)

        [Header("Danger Warning")]
        [SerializeField] private Color riskOverlayColor = Color.red; // 위험 오버레이 색상
        [SerializeField, Range(0f, 1f)] private float riskOverlayIntensity = 0f; // 위험 오버레이 강도 (0=없음)
        [SerializeField] private bool showDangerWarning = false; // 위험 경고 표시 여부

        [Header("Debug")]
        [SerializeField, TextArea(2, 4)] private string debugDescription = ""; // 디버그 설명

        // ===== Public Accessors =====

        /// <summary>프로필 식별자</summary>
        public string ProfileId => profileId;

        /// <summary>표시 이름 (디버그용)</summary>
        public string DisplayName => displayName;

        /// <summary>Camera.backgroundColor 후보</summary>
        public Color BackgroundColor => backgroundColor;

        /// <summary>RenderSettings.fogColor 후보</summary>
        public Color FogColor => fogColor;

        /// <summary>RenderSettings.fogDensity 후보</summary>
        public float FogDensity => fogDensity;

        /// <summary>RenderSettings.ambientLight 후보</summary>
        public Color AmbientLightColor => ambientLightColor;

        /// <summary>BGM 상태 키 (실제 오디오 시스템 연동 전까지 로그만 출력)</summary>
        public string BgmStateKey => bgmStateKey;

        /// <summary>위험 오버레이 색상</summary>
        public Color RiskOverlayColor => riskOverlayColor;

        /// <summary>위험 오버레이 강도 (0=없음)</summary>
        public float RiskOverlayIntensity => riskOverlayIntensity;

        /// <summary>위험 경고 표시 여부</summary>
        public bool ShowDangerWarning => showDangerWarning;

        /// <summary>디버그 설명</summary>
        public string DebugDescription => debugDescription;

        /// <summary>프로필이 유효한지 확인 (profileId만 있으면 OK)</summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(profileId);
        }
    }
}
