using UnityEngine;

namespace Project.Data.World
{
    /// <summary>개별 존의 정적 데이터와 해금 조건 정의</summary>
    [CreateAssetMenu(
        fileName = "ZoneData_",
        menuName = "Project/World/Zone Data")]
    public class ZoneDataSO : ScriptableObject
    {
        [Header("Zone Identity")]
        [SerializeField] private ZoneId zoneId; // 존 식별자 (예: "E5")
        [SerializeField] private RegionId regionId; // 소속 리전
        [SerializeField] private string displayName; // 표시 이름

        [Header("Unlock Conditions")]
        [SerializeField] private UnlockConditionSetSO unlockConditionSet; // 해금 조건 세트

        [Header("Zone Properties")]
        [SerializeField] private float baseRiskLevel = 0f; // 기본 위험도 (0-1)
        [SerializeField] private bool isStartingZone = false; // 시작 존 여부
        [SerializeField] private bool isAlwaysUnlocked = false; // 항상 해금 여부
        [SerializeField] private bool isStoryCritical = false; // 스토리 핵심 존 여부

        [Header("Visual & Audio")]
        [SerializeField] private Color zoneColor = Color.white; // 디버그/맵 표시용 색상
        [SerializeField] private AudioClip ambientSound; // 존 배경음

        [Header("Ambient Profile (Phase 6)")]
        [SerializeField] private ZoneAmbientProfileSO ambientProfile; // 존 분위기 프로필 (null이면 fallback)

        [Header("Environment Profile (Phase 1)")]
        [SerializeField] private ZoneEnvironmentProfileSO environmentProfile; // 존 환경 프로필 (수심/바이옴/해저/프롭)

        /// <summary>존 식별자 (예: "E5")</summary>
        public ZoneId ZoneId => zoneId;

        /// <summary>소속 리전</summary>
        public RegionId RegionId => regionId;

        /// <summary>표시 이름</summary>
        public string DisplayName => displayName;

        /// <summary>해금 조건 세트</summary>
        public UnlockConditionSetSO UnlockConditionSet => unlockConditionSet;

        /// <summary>기본 위험도 (0-1)</summary>
        public float BaseRiskLevel => Mathf.Clamp01(baseRiskLevel);

        /// <summary>시작 존 여부</summary>
        public bool IsStartingZone => isStartingZone;

        /// <summary>항상 해금 여부</summary>
        public bool IsAlwaysUnlockedField => isAlwaysUnlocked;

        /// <summary>스토리 핵심 존 여부</summary>
        public bool IsStoryCritical => isStoryCritical;

        /// <summary>디버그/맵 표시용 색상</summary>
        public Color ZoneColor => zoneColor;

        /// <summary>존 배경음</summary>
        public AudioClip AmbientSound => ambientSound;

        /// <summary>존 분위기 프로필 (null이면 fallback)</summary>
        public ZoneAmbientProfileSO AmbientProfile => ambientProfile;

        /// <summary>존 환경 프로필 (수심/바이옴/해저/프롭, null이면 fallback)</summary>
        public ZoneEnvironmentProfileSO EnvironmentProfile => environmentProfile;

        /// <summary>존이 항상 해금되어 있는지 확인 (시작 존 또는 항상 해금 설정)</summary>
        public bool IsAlwaysUnlocked()
        {
            return isStartingZone || isAlwaysUnlocked;
        }

        /// <summary>존 데이터 유효성 검사</summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(zoneId.ToString()) &&
                    !string.IsNullOrEmpty(regionId.ToString()) &&
                    !string.IsNullOrEmpty(displayName);
        }

        /// <summary>ZoneCoordinate 계산</summary>
        public ZoneCoordinate GetZoneCoordinate()
        {
            return ZoneCoordinate.FromZoneId(zoneId);
        }

        /// <summary>에디터에서 유효성 검사 및 경고</summary>
        private void OnValidate()
        {
            // 시작 존이 아니고 항상 해금도 아니면서 해금 조건 세트가 없으면 경고
            if (!isStartingZone && !isAlwaysUnlocked && unlockConditionSet == null)
            {
                Debug.LogWarning($"[ZoneDataSO] {name}: Not starting zone, not always unlocked, but unlockConditionSet is null. Zone will be locked.", this);
            }
        }
    }
}
