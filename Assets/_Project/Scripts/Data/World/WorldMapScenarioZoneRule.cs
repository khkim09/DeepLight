using System;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>
    /// 시나리오 프리셋에서 특정 존(또는 존 그룹)에 적용할 규칙을 정의하는 직렬화 가능 클래스.
    /// ZoneId 배열로 대상 존을 지정하고, 해당 존의 환경/위험/바이옴 등을 오버라이드한다.
    ///
    /// ZoneId와 RegionId는 기존 struct를 그대로 사용한다.
    /// 직렬화 문제가 발생할 경우 string 기반 fallback 필드를 추가로 제공한다.
    /// </summary>
    [Serializable]
    public class WorldMapScenarioZoneRule
    {
        [Header("Rule Identity")]
        [SerializeField] private string ruleName = "New Rule"; // 규칙 이름 (식별용)

        [Header("Target Zones")]
        [SerializeField] private ZoneId[] zoneIds; // 대상 존 ID 배열 (비어있으면 regionId 기준 적용)
        [SerializeField] private RegionId regionId; // 대상 리전 ID (zoneIds가 비어있을 때 사용)

        [Header("Overrides")]
        [SerializeField] private ZoneDepthBand depthBand; // 수심 대역 오버라이드
        [SerializeField] private ZoneBiomeType biomeType; // 바이옴 타입 오버라이드
        [SerializeField] private float minDepth; // 최소 깊이 오버라이드
        [SerializeField] private float maxDepth; // 최대 깊이 오버라이드
        [SerializeField] private float baseRiskLevel; // 기본 위험도 오버라이드 (0-1)
        [SerializeField] private ZoneEnvironmentProfileSO environmentProfile; // 환경 프로필 오버라이드

        [Header("Debug")]
        [SerializeField] private Color debugColor = Color.gray; // 디버그 시각화 색상

        // ===== String-based fallback fields (직렬화 안전성 보장) =====
        [SerializeField] private string[] zoneIdStrings; // ZoneId 문자열 fallback (예: "E5", "F6")
        [SerializeField] private string regionIdString; // RegionId 문자열 fallback (예: "Hub")

        // ===== Public Accessors =====

        /// <summary>규칙 이름 (식별용)</summary>
        public string RuleName => ruleName;

        /// <summary>대상 존 ID 배열</summary>
        public ZoneId[] ZoneIds => zoneIds;

        /// <summary>대상 리전 ID (zoneIds가 비어있을 때 사용)</summary>
        public RegionId RegionId => regionId;

        /// <summary>수심 대역 오버라이드</summary>
        public ZoneDepthBand DepthBand => depthBand;

        /// <summary>바이옴 타입 오버라이드</summary>
        public ZoneBiomeType BiomeType => biomeType;

        /// <summary>최소 깊이 오버라이드</summary>
        public float MinDepth => minDepth;

        /// <summary>최대 깊이 오버라이드</summary>
        public float MaxDepth => maxDepth;

        /// <summary>기본 위험도 오버라이드 (0-1)</summary>
        public float BaseRiskLevel => baseRiskLevel;

        /// <summary>환경 프로필 오버라이드</summary>
        public ZoneEnvironmentProfileSO EnvironmentProfile => environmentProfile;

        /// <summary>디버그 시각화 색상</summary>
        public Color DebugColor => debugColor;

        // ===== Public Methods =====

        /// <summary>
        /// 주어진 ZoneId가 이 규칙의 대상인지 확인한다.
        /// zoneIds 배열을 우선 검사하고, 비어있으면 regionId로 검사한다.
        /// </summary>
        /// <param name="zoneId">검사할 존 ID</param>
        /// <returns>대상이면 true</returns>
        public bool IsZoneMatch(ZoneId zoneId)
        {
            // zoneIds 배열 우선 검사
            if (zoneIds != null && zoneIds.Length > 0)
            {
                foreach (ZoneId targetId in zoneIds)
                {
                    if (targetId.Equals(zoneId))
                        return true;
                }
                return false;
            }

            // zoneIds가 비어있으면 string fallback 검사
            if (zoneIdStrings != null && zoneIdStrings.Length > 0)
            {
                foreach (string idStr in zoneIdStrings)
                {
                    if (ZoneId.TryParse(idStr, out ZoneId parsedId) && parsedId.Equals(zoneId))
                        return true;
                }
                return false;
            }

            // zoneIds도 없고 string fallback도 없으면 regionId 기준 검사
            // regionId가 비어있으면 모든 존 매치
            if (string.IsNullOrEmpty(regionId.Id) && string.IsNullOrEmpty(regionIdString))
                return true;

            // regionId 매치 검사
            if (!string.IsNullOrEmpty(regionId.Id))
                return regionId.Id == zoneId.ToString(); // NOTE: 이 부분은 실제 RegionId 매칭 로직으로 대체 필요

            // string fallback regionId 검사
            if (!string.IsNullOrEmpty(regionIdString))
                return regionIdString == zoneId.ToString();

            return false;
        }

        /// <summary>
        /// ZoneId 배열을 string 배열로 동기화한다.
        /// 에디터에서 ZoneId 직렬화 문제가 있을 경우 string fallback을 사용할 수 있다.
        /// </summary>
        public void SyncZoneIdStrings()
        {
            if (zoneIds != null && zoneIds.Length > 0)
            {
                zoneIdStrings = new string[zoneIds.Length];
                for (int i = 0; i < zoneIds.Length; i++)
                {
                    zoneIdStrings[i] = zoneIds[i].ToString();
                }
            }
        }

        /// <summary>
        /// string 배열을 ZoneId 배열로 복원한다.
        /// </summary>
        public void RestoreZoneIdsFromStrings()
        {
            if (zoneIdStrings != null && zoneIdStrings.Length > 0)
            {
                zoneIds = new ZoneId[zoneIdStrings.Length];
                for (int i = 0; i < zoneIdStrings.Length; i++)
                {
                    if (!ZoneId.TryParse(zoneIdStrings[i], out zoneIds[i]))
                    {
                        Debug.LogWarning($"[WorldMapScenarioZoneRule] Failed to parse ZoneId from string: {zoneIdStrings[i]}");
                    }
                }
            }
        }
    }
}
