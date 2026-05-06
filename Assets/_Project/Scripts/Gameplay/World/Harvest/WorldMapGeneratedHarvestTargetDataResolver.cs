using System.Collections.Generic;
using Project.Data.Harvest;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.World.Harvest
{
    /// <summary>
    /// WorldMapGeneratedHarvestTarget에 필요한 실제 Harvest 데이터 또는 fallback 데이터를 resolve한다.
    /// runtimeKey/profileId/interactionKind/targetKind/sourceMarkerId를 기반으로
    /// 기존 HarvestTargetSO 또는 InteractionProfileSO가 있으면 연결하고,
    /// 없으면 runtimeKey 기반 generated fallback data를 제공한다.
    /// MonoBehaviour가 아니며, GeneratedWorldRoot의 단일 인스턴스로 동작한다.
    /// </summary>
    public class WorldMapGeneratedHarvestTargetDataResolver : MonoBehaviour
    {
        // ===== Serialized Fields =====

        [SerializeField, Tooltip("runtimeKey -> HarvestTargetSO 매핑 (Inspector에서 수동 할당)")]
        private List<RuntimeKeyToHarvestTargetMapping> harvestTargetMappings = new();

        [SerializeField, Tooltip("runtimeKey -> fallback display name 매핑")]
        private List<RuntimeKeyToFallbackData> fallbackDataMappings = new();

        [SerializeField, Tooltip("전체 fallback display name (매핑 실패 시 사용)")]
        private string globalFallbackDisplayName = "Unknown Harvest Target";

        [SerializeField, Tooltip("전체 fallback description")]
        private string globalFallbackDescription = "A generated harvest target with no specific profile assigned.";

        [SerializeField, Tooltip("전체 fallback category")]
        private string globalFallbackCategory = "Resource";

        [SerializeField, Tooltip("전체 fallback reward item ID")]
        private string globalFallbackRewardItemId = "unknown_resource";

        [SerializeField, Tooltip("로그 출력 여부")]
        private bool logResolution;

        // ===== Runtime Cache =====

        /// <summary>runtimeKey -> HarvestTargetSO lookup cache (빌드 시점에 구축)</summary>
        private Dictionary<string, HarvestTargetSO> _harvestTargetByRuntimeKey;

        /// <summary>runtimeKey -> fallback data lookup cache</summary>
        private Dictionary<string, RuntimeKeyToFallbackData> _fallbackByRuntimeKey;

        /// <summary>cache 구축 완료 여부</summary>
        private bool _cacheBuilt;

        // ===== Unity Lifecycle =====

        /// <summary>
        /// Awake: runtime cache를 구축한다.
        /// </summary>
        private void Awake()
        {
            BuildCache();
        }

        // ===== Public API =====

        /// <summary>
        /// WorldMapGeneratedHarvestTarget의 runtimeKey/profileId를 기반으로
        /// ResolvedGeneratedHarvestTargetData를 resolve한다.
        /// target이 null이거나 유효하지 않으면 false를 반환한다.
        /// target의 RequirementId/SourceMarkerId/ZoneId/InteractionKind/TargetKind를
        /// resolved data에 보존한다.
        /// </summary>
        /// <param name="target">resolve할 generated harvest target</param>
        /// <param name="data">resolve 결과</param>
        /// <returns>resolve 성공 여부 (fallback 포함)</returns>
        public bool TryResolve(WorldMapGeneratedHarvestTarget target, out ResolvedGeneratedHarvestTargetData data)
        {
            if (target == null)
            {
                data = CreateFallbackData(string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty,
                    WorldMapRuntimeHarvestInteractionCandidateKind.None,
                    WorldMapRuntimeHarvestInteractionTargetKind.None,
                    "Target is null");
                return false;
            }

            // runtimeKey 기반 resolve 시도
            string runtimeKey = target.RuntimeKey ?? string.Empty;
            string profileId = target.ProfileId ?? string.Empty;
            string sourceMarkerId = target.SourceMarkerId ?? string.Empty;
            string zoneId = target.ZoneId ?? string.Empty;
            string requirementId = target.RequirementId ?? string.Empty;
            WorldMapRuntimeHarvestInteractionCandidateKind interactionKind = target.InteractionKind;
            WorldMapRuntimeHarvestInteractionTargetKind targetKind = target.TargetKind;

            return TryResolveByRuntimeKey(runtimeKey, profileId, sourceMarkerId, zoneId, requirementId,
                interactionKind, targetKind, out data);
        }

        /// <summary>
        /// runtimeKey를 기반으로 ResolvedGeneratedHarvestTargetData를 resolve한다.
        /// </summary>
        /// <param name="runtimeKey">resolve할 RuntimeKey</param>
        /// <param name="data">resolve 결과</param>
        /// <returns>resolve 성공 여부 (fallback 포함)</returns>
        public bool TryResolveByRuntimeKey(string runtimeKey, out ResolvedGeneratedHarvestTargetData data)
        {
            return TryResolveByRuntimeKey(runtimeKey, string.Empty, string.Empty, string.Empty, string.Empty,
                WorldMapRuntimeHarvestInteractionCandidateKind.None,
                WorldMapRuntimeHarvestInteractionTargetKind.None,
                out data);
        }

        /// <summary>
        /// profileId를 기반으로 ResolvedGeneratedHarvestTargetData를 resolve한다.
        /// profileId만으로는 정확한 매핑이 어려우므로, fallback data를 반환할 가능성이 높다.
        /// </summary>
        /// <param name="profileId">resolve할 ProfileId</param>
        /// <param name="data">resolve 결과</param>
        /// <returns>resolve 성공 여부 (fallback 포함)</returns>
        public bool TryResolveByProfileId(string profileId, out ResolvedGeneratedHarvestTargetData data)
        {
            if (string.IsNullOrEmpty(profileId))
            {
                data = CreateFallbackData(string.Empty, string.Empty, string.Empty,
                    string.Empty, string.Empty,
                    WorldMapRuntimeHarvestInteractionCandidateKind.None,
                    WorldMapRuntimeHarvestInteractionTargetKind.None,
                    "ProfileId is empty");
                return false;
            }

            // profileId로는 정확한 HarvestTargetSO 매핑이 어려우므로 fallback data 반환
            data = CreateFallbackData(string.Empty, profileId, string.Empty,
                string.Empty, string.Empty,
                WorldMapRuntimeHarvestInteractionCandidateKind.None,
                WorldMapRuntimeHarvestInteractionTargetKind.None,
                "ProfileId-based resolve: no exact SO mapping");
            return true;
        }

        /// <summary>
        /// 현재 resolver 상태의 디버그 요약 문자열을 반환한다.
        /// </summary>
        /// <returns>요약 문자열</returns>
        public string GetDebugSummary()
        {
            if (!_cacheBuilt)
                BuildCache();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== WorldMapGeneratedHarvestTargetDataResolver Debug Summary ===");
            sb.AppendLine($"HarvestTargetSO mappings: {_harvestTargetByRuntimeKey?.Count ?? 0}");
            sb.AppendLine($"Fallback data mappings: {_fallbackByRuntimeKey?.Count ?? 0}");
            sb.AppendLine($"Global fallback display name: {globalFallbackDisplayName}");
            sb.AppendLine($"Global fallback reward item ID: {globalFallbackRewardItemId}");

            if (_harvestTargetByRuntimeKey != null && _harvestTargetByRuntimeKey.Count > 0)
            {
                sb.AppendLine("--- HarvestTargetSO Mappings ---");
                foreach (var kvp in _harvestTargetByRuntimeKey)
                {
                    HarvestTargetSO so = kvp.Value;
                    sb.AppendLine($"  {kvp.Key} -> {(so != null ? so.name : "NULL")}");
                }
            }

            if (_fallbackByRuntimeKey != null && _fallbackByRuntimeKey.Count > 0)
            {
                sb.AppendLine("--- Fallback Data Mappings ---");
                foreach (var kvp in _fallbackByRuntimeKey)
                {
                    sb.AppendLine($"  {kvp.Key} -> {kvp.Value.displayName}");
                }
            }

            return sb.ToString();
        }

        // ===== Private Methods =====

        /// <summary>
        /// runtimeKey 기반 resolve의 내부 구현.
        /// HarvestTargetSO 매핑이 있으면 그것을 우선 사용하고,
        /// 없으면 fallback data mapping을 사용한다.
        /// 둘 다 없으면 runtimeKey 기반 generated fallback을 생성한다.
        /// 모든 경우에 target의 RequirementId/SourceMarkerId/ZoneId/InteractionKind/TargetKind를 보존한다.
        /// </summary>
        private bool TryResolveByRuntimeKey(
            string runtimeKey,
            string profileId,
            string sourceMarkerId,
            string zoneId,
            string requirementId,
            WorldMapRuntimeHarvestInteractionCandidateKind interactionKind,
            WorldMapRuntimeHarvestInteractionTargetKind targetKind,
            out ResolvedGeneratedHarvestTargetData data)
        {
            if (!_cacheBuilt)
                BuildCache();

            if (string.IsNullOrEmpty(runtimeKey))
            {
                // runtimeKey가 없으면 IsReady=false로 반환
                data = CreateFallbackData(string.Empty, profileId, sourceMarkerId, zoneId, requirementId,
                    interactionKind, targetKind,
                    "RuntimeKey is empty");
                data.IsReady = false;
                return false;
            }

            // 1. HarvestTargetSO 매핑 확인
            if (_harvestTargetByRuntimeKey != null &&
                _harvestTargetByRuntimeKey.TryGetValue(runtimeKey, out HarvestTargetSO targetSO) &&
                targetSO != null)
            {
                // HarvestTargetSO가 있으면 그것을 기반으로 data 구성
                data = CreateFromHarvestTargetSO(
                    targetSO,
                    runtimeKey,
                    profileId,
                    sourceMarkerId,
                    zoneId,
                    requirementId,
                    interactionKind,
                    targetKind);
                return true;
            }

            // 2. Fallback data mapping 확인
            if (_fallbackByRuntimeKey != null &&
                _fallbackByRuntimeKey.TryGetValue(runtimeKey, out RuntimeKeyToFallbackData fallbackEntry))
            {
                data = CreateFromFallbackEntry(
                    fallbackEntry,
                    runtimeKey,
                    profileId,
                    sourceMarkerId,
                    zoneId,
                    requirementId,
                    interactionKind,
                    targetKind);
                return true;
            }

            // 3. runtimeKey 기반 generated fallback 생성 (global fallback 대신 runtimeKey 기반)
            string displayName = BuildDisplayNameFromRuntimeKey(runtimeKey);
            string rewardItemId = BuildRewardItemIdFromRuntimeKey(runtimeKey);
            data = CreateFallbackData(runtimeKey, profileId, sourceMarkerId, zoneId, requirementId,
                interactionKind, targetKind,
                $"No mapping found for runtimeKey '{runtimeKey}', using generated fallback");
            data.DisplayName = displayName;
            data.RewardItemId = rewardItemId;
            data.IsReady = true;
            return true;
        }

        /// <summary>
        /// HarvestTargetSO로부터 ResolvedGeneratedHarvestTargetData를 생성한다.
        /// RequirementId를 반드시 저장한다.
        /// </summary>
        private ResolvedGeneratedHarvestTargetData CreateFromHarvestTargetSO(
            HarvestTargetSO targetSO,
            string runtimeKey,
            string profileId,
            string sourceMarkerId,
            string zoneId,
            string requirementId,
            WorldMapRuntimeHarvestInteractionCandidateKind interactionKind,
            WorldMapRuntimeHarvestInteractionTargetKind targetKind)
        {
            if (logResolution)
            {
                UnityEngine.Debug.Log($"[HarvestTargetDataResolver] Resolved '{runtimeKey}' -> HarvestTargetSO '{targetSO.name}'");
            }

            return new ResolvedGeneratedHarvestTargetData
            {
                SourceMarkerId = sourceMarkerId ?? string.Empty,
                ZoneId = zoneId ?? string.Empty,
                RuntimeKey = runtimeKey ?? string.Empty,
                ProfileId = profileId ?? string.Empty,
                RequirementId = requirementId ?? string.Empty,
                InteractionKind = interactionKind,
                TargetKind = targetKind,
                DisplayName = targetSO.BuildPreviewDisplayName(),
                Description = $"Harvest target: {targetSO.TargetId}",
                HarvestCategory = globalFallbackCategory,
                RarityOrDifficulty = targetSO.AdditionalDifficulty,
                RewardItemId = ResolveRewardItemIdFromSO(targetSO),
                HarvestTargetSO = targetSO,
                InteractionProfileId = string.Empty,
                IsFallback = false,
                IsReady = targetSO.IsValid(),
                Reason = targetSO.IsValid() ? "Resolved from HarvestTargetSO" : "HarvestTargetSO is invalid"
            };
        }

        /// <summary>
        /// Fallback entry로부터 ResolvedGeneratedHarvestTargetData를 생성한다.
        /// RequirementId를 반드시 저장한다.
        /// </summary>
        private ResolvedGeneratedHarvestTargetData CreateFromFallbackEntry(
            RuntimeKeyToFallbackData fallbackEntry,
            string runtimeKey,
            string profileId,
            string sourceMarkerId,
            string zoneId,
            string requirementId,
            WorldMapRuntimeHarvestInteractionCandidateKind interactionKind,
            WorldMapRuntimeHarvestInteractionTargetKind targetKind)
        {
            if (logResolution)
            {
                UnityEngine.Debug.Log($"[HarvestTargetDataResolver] Resolved '{runtimeKey}' -> fallback '{fallbackEntry.displayName}'");
            }

            return new ResolvedGeneratedHarvestTargetData
            {
                SourceMarkerId = sourceMarkerId ?? string.Empty,
                ZoneId = zoneId ?? string.Empty,
                RuntimeKey = runtimeKey ?? string.Empty,
                ProfileId = profileId ?? string.Empty,
                RequirementId = requirementId ?? string.Empty,
                InteractionKind = interactionKind,
                TargetKind = targetKind,
                DisplayName = fallbackEntry.displayName,
                Description = fallbackEntry.description,
                HarvestCategory = fallbackEntry.category,
                RarityOrDifficulty = fallbackEntry.difficulty,
                RewardItemId = fallbackEntry.rewardItemId,
                HarvestTargetSO = null,
                InteractionProfileId = fallbackEntry.interactionProfileId,
                IsFallback = true,
                IsReady = true,
                Reason = $"Fallback mapping for runtimeKey '{runtimeKey}'"
            };
        }

        /// <summary>
        /// Fallback data를 생성한다.
        /// SourceMarkerId/ZoneId/RequirementId/InteractionKind/TargetKind를 반드시 보존한다.
        /// runtimeKey가 비어 있으면 IsReady=false로 설정한다.
        /// </summary>
        private ResolvedGeneratedHarvestTargetData CreateFallbackData(
            string runtimeKey,
            string profileId,
            string sourceMarkerId,
            string zoneId,
            string requirementId,
            WorldMapRuntimeHarvestInteractionCandidateKind interactionKind,
            WorldMapRuntimeHarvestInteractionTargetKind targetKind,
            string reason)
        {
            if (logResolution)
            {
                UnityEngine.Debug.Log($"[HarvestTargetDataResolver] Fallback for runtimeKey='{runtimeKey}', profileId='{profileId}': {reason}");
            }

            bool isEmptyKey = string.IsNullOrEmpty(runtimeKey);
            string displayName = isEmptyKey ? globalFallbackDisplayName : BuildDisplayNameFromRuntimeKey(runtimeKey);
            string rewardItemId = isEmptyKey ? globalFallbackRewardItemId : BuildRewardItemIdFromRuntimeKey(runtimeKey);

            return new ResolvedGeneratedHarvestTargetData
            {
                SourceMarkerId = sourceMarkerId ?? string.Empty,
                ZoneId = zoneId ?? string.Empty,
                RuntimeKey = runtimeKey ?? string.Empty,
                ProfileId = profileId ?? string.Empty,
                RequirementId = requirementId ?? string.Empty,
                InteractionKind = interactionKind,
                TargetKind = targetKind,
                DisplayName = displayName,
                Description = globalFallbackDescription,
                HarvestCategory = globalFallbackCategory,
                RarityOrDifficulty = 0f,
                RewardItemId = rewardItemId,
                HarvestTargetSO = null,
                InteractionProfileId = string.Empty,
                IsFallback = true,
                IsReady = !isEmptyKey, // runtimeKey가 비어 있으면 IsReady=false
                Reason = reason
            };
        }

        /// <summary>
        /// HarvestTargetSO의 ItemPool에서 첫 번째 유효한 아이템의 ID를 resolve한다.
        /// </summary>
        private string ResolveRewardItemIdFromSO(HarvestTargetSO targetSO)
        {
            if (targetSO == null)
                return globalFallbackRewardItemId;

            IReadOnlyList<HarvestItemWeightedEntry> pool = targetSO.ItemPool;
            if (pool == null || pool.Count == 0)
                return globalFallbackRewardItemId;

            for (int i = 0; i < pool.Count; i++)
            {
                var entry = pool[i];
                if (entry.Item != null && entry.Item.IsValid())
                    return entry.Item.ItemId;
            }

            return globalFallbackRewardItemId;
        }

        /// <summary>
        /// runtimeKey로부터 표시 이름을 생성한다.
        /// 예: "iron" -> "Iron", "Iron Scrap" -> "Iron Scrap", "sensor" -> "Sensor"
        /// </summary>
        private string BuildDisplayNameFromRuntimeKey(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return globalFallbackDisplayName;

            // 이미 적절한 형식이면 그대로 반환
            if (char.IsUpper(runtimeKey[0]))
                return runtimeKey;

            // 첫 글자만 대문자로 변환
            return char.ToUpper(runtimeKey[0]) + runtimeKey.Substring(1);
        }

        /// <summary>
        /// runtimeKey로부터 보상 아이템 ID를 생성한다.
        /// 예: "iron" -> "iron", "Iron Scrap" -> "iron_scrap", "sensor" -> "sensor"
        /// </summary>
        private string BuildRewardItemIdFromRuntimeKey(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return globalFallbackRewardItemId;

            // 공백을 underscore로 변환하고 소문자로
            return runtimeKey.Replace(" ", "_").ToLowerInvariant();
        }

        /// <summary>
        /// runtimeKey를 정규화한다.
        /// 예: "Iron Scrap" -> "iron_scrap", "Iron" -> "iron"
        /// </summary>
        private string NormalizeRuntimeKey(string runtimeKey)
        {
            if (string.IsNullOrEmpty(runtimeKey))
                return string.Empty;

            return runtimeKey.Replace(" ", "_").ToLowerInvariant();
        }

        /// <summary>
        /// Serialize된 mapping list를 runtime Dictionary cache로 변환한다.
        /// </summary>
        private void BuildCache()
        {
            _harvestTargetByRuntimeKey = new Dictionary<string, HarvestTargetSO>();
            _fallbackByRuntimeKey = new Dictionary<string, RuntimeKeyToFallbackData>();

            // HarvestTargetSO mappings
            for (int i = 0; i < harvestTargetMappings.Count; i++)
            {
                RuntimeKeyToHarvestTargetMapping mapping = harvestTargetMappings[i];
                if (mapping == null || string.IsNullOrEmpty(mapping.runtimeKey))
                    continue;

                if (!_harvestTargetByRuntimeKey.ContainsKey(mapping.runtimeKey))
                {
                    _harvestTargetByRuntimeKey[mapping.runtimeKey] = mapping.harvestTargetSO;
                }
            }

            // Fallback data mappings
            for (int i = 0; i < fallbackDataMappings.Count; i++)
            {
                RuntimeKeyToFallbackData mapping = fallbackDataMappings[i];
                if (mapping == null || string.IsNullOrEmpty(mapping.runtimeKey))
                    continue;

                if (!_fallbackByRuntimeKey.ContainsKey(mapping.runtimeKey))
                {
                    _fallbackByRuntimeKey[mapping.runtimeKey] = mapping;
                }
            }

            _cacheBuilt = true;

            if (logResolution)
            {
                UnityEngine.Debug.Log($"[HarvestTargetDataResolver] Cache built: {_harvestTargetByRuntimeKey.Count} SO mappings, {_fallbackByRuntimeKey.Count} fallback mappings");
            }
        }
    }

    /// <summary>
    /// runtimeKey -> HarvestTargetSO 매핑을 위한 Serialize 가능 클래스.
    /// </summary>
    [System.Serializable]
    public class RuntimeKeyToHarvestTargetMapping
    {
        [SerializeField, Tooltip("매핑할 RuntimeKey (예: iron, copper, sensor)")]
        public string runtimeKey;

        [SerializeField, Tooltip("매핑할 HarvestTargetSO")]
        public HarvestTargetSO harvestTargetSO;
    }

    /// <summary>
    /// runtimeKey -> fallback data 매핑을 위한 Serialize 가능 클래스.
    /// HarvestTargetSO가 없을 때 사용한다.
    /// </summary>
    [System.Serializable]
    public class RuntimeKeyToFallbackData
    {
        [SerializeField, Tooltip("매핑할 RuntimeKey")]
        public string runtimeKey;

        [SerializeField, Tooltip("표시 이름")]
        public string displayName = "Unknown Resource";

        [SerializeField, Tooltip("설명")]
        public string description = "A generated harvest resource.";

        [SerializeField, Tooltip("카테고리")]
        public string category = "Resource";

        [SerializeField, Tooltip("난이도/희귀도 값")]
        public float difficulty;

        [SerializeField, Tooltip("보상 아이템 ID")]
        public string rewardItemId = "unknown_resource";

        [SerializeField, Tooltip("InteractionProfile ID (향후 사용)")]
        public string interactionProfileId;
    }

    /// <summary>
    /// WorldMapGeneratedHarvestTargetDataResolver의 resolve 결과를 보관하는 serializable data class.
    /// MonoBehaviour가 아니며, runtime/editor 양쪽에서 안전하게 사용 가능.
    /// SourceMarkerId/ZoneId/RuntimeKey/ProfileId/RequirementId/InteractionKind/TargetKind를 모두 보존한다.
    /// </summary>
    [System.Serializable]
    public class ResolvedGeneratedHarvestTargetData
    {
        // ===== Identity Fields =====

        [SerializeField, Tooltip("대응하는 source marker의 고유 식별자")]
        public string SourceMarkerId;

        [SerializeField, Tooltip("이 target이 속한 Zone ID")]
        public string ZoneId;

        [SerializeField, Tooltip("RuntimeKey")]
        public string RuntimeKey;

        [SerializeField, Tooltip("ProfileId")]
        public string ProfileId;

        [SerializeField, Tooltip("RequirementId (resolve 시점에 target에서 복사)")]
        public string RequirementId;

        [SerializeField, Tooltip("InteractionKind")]
        public WorldMapRuntimeHarvestInteractionCandidateKind InteractionKind;

        [SerializeField, Tooltip("TargetKind")]
        public WorldMapRuntimeHarvestInteractionTargetKind TargetKind;

        // ===== Display Fields =====

        [SerializeField, Tooltip("표시 이름")]
        public string DisplayName;

        [SerializeField, Tooltip("설명")]
        public string Description;

        [SerializeField, Tooltip("HarvestCategory (예: Resource, Scrap, Sensor)")]
        public string HarvestCategory;

        [SerializeField, Tooltip("난이도/희귀도 값")]
        public float RarityOrDifficulty;

        // ===== Reward Fields =====

        [SerializeField, Tooltip("보상 아이템 ID")]
        public string RewardItemId;

        // ===== Reference Fields =====

        [SerializeField, Tooltip("매핑된 HarvestTargetSO (없으면 null)")]
        public HarvestTargetSO HarvestTargetSO;

        [SerializeField, Tooltip("InteractionProfile ID (향후 사용)")]
        public string InteractionProfileId;

        // ===== Status Fields =====

        [SerializeField, Tooltip("이 data가 fallback인지 여부")]
        public bool IsFallback;

        [SerializeField, Tooltip("이 data가 유효한 상태인지 여부")]
        public bool IsReady;

        [SerializeField, Tooltip("data 상태에 대한 설명/이유")]
        public string Reason;

        /// <summary>
        /// 디버그 요약 문자열을 반환한다.
        /// </summary>
        public string GetDebugSummary()
        {
            string fallbackStr = IsFallback ? " [FALLBACK]" : "";
            string readyStr = IsReady ? " [READY]" : " [NOT_READY]";

            return $"[{RuntimeKey}]{fallbackStr}{readyStr} | " +
                $"Marker={SourceMarkerId} " +
                $"Zone={ZoneId} " +
                $"ReqId={RequirementId} " +
                $"InteractionKind={InteractionKind} " +
                $"TargetKind={TargetKind} " +
                $"DisplayName={DisplayName} " +
                $"Category={HarvestCategory} " +
                $"Difficulty={RarityOrDifficulty:F2} " +
                $"Reward={RewardItemId} " +
                $"SO={(HarvestTargetSO != null ? HarvestTargetSO.name : "null")} " +
                $"Reason={Reason}";
        }
    }
}
