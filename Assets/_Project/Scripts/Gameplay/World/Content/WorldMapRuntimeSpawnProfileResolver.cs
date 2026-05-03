using System;
using System.Text;

namespace Project.Gameplay.World.Content
{
    /// <summary>
    /// WorldMapRuntimePlaceholderBinding 하나를 입력받아 WorldMapRuntimeSpawnProfileCandidate를 반환하는
    /// 순수 C# static resolver 클래스.
    /// 아직 실제 prefab이나 ScriptableObject asset을 연결하지 않으며,
    /// RuntimeCategory/RuntimeKey/MarkerType/SemanticTags 기반으로 "후보 key"만 안정적으로 구성한다.
    /// Unity scene object를 생성/삭제/수정하지 않는다.
    /// </summary>
    public static class WorldMapRuntimeSpawnProfileResolver
    {
        // ===== Public API =====

        /// <summary>
        /// WorldMapRuntimePlaceholderBinding을 해석하여 WorldMapRuntimeSpawnProfileCandidate를 생성한다.
        /// binding이 null이거나 필수 필드가 부족하면 false를 반환하고,
        /// fallback candidate로 생성된 경우에도 true를 반환하되 IsFallbackCandidate=true로 설정한다.
        /// </summary>
        /// <param name="binding">해석할 RuntimePlaceholderBinding</param>
        /// <param name="candidate">해석 결과 (fallback 포함)</param>
        /// <returns>binding이 null이 아니고 최소한의 정보가 있으면 true</returns>
        public static bool TryResolve(WorldMapRuntimePlaceholderBinding binding, out WorldMapRuntimeSpawnProfileCandidate candidate)
        {
            if (binding == null)
            {
                candidate = null;
                return false;
            }

            var note = new StringBuilder();
            bool isFallback = false;

            // 1. ZoneId
            string zoneId = binding.ZoneId;
            if (string.IsNullOrEmpty(zoneId))
            {
                zoneId = "UnknownZone";
                isFallback = true;
                note.Append("[Fallback: ZoneId empty, used 'UnknownZone'] ");
            }
            else
            {
                note.Append($"[ZoneId={zoneId}] ");
            }

            // 2. MarkerId
            string markerId = binding.MarkerId;
            if (string.IsNullOrEmpty(markerId))
            {
                markerId = "UnknownMarker";
                isFallback = true;
                note.Append("[Fallback: MarkerId empty, used 'UnknownMarker'] ");
            }
            else
            {
                note.Append($"[MarkerId={markerId}] ");
            }

            // 3. MarkerType
            WorldMapZoneContentMarkerType markerType = binding.MarkerType;

            // 4. RuntimeCategory 매핑
            string runtimeCategory = ResolveRuntimeCategory(binding);
            if (string.IsNullOrEmpty(runtimeCategory))
            {
                runtimeCategory = "Unknown";
                isFallback = true;
                note.Append("[Fallback: RuntimeCategory empty, used 'Unknown'] ");
            }
            else
            {
                note.Append($"[Category={runtimeCategory}] ");
            }

            // 5. RuntimeKey 결정 (fallback 포함)
            string runtimeKey = ResolveRuntimeKey(binding);
            if (string.IsNullOrEmpty(runtimeKey))
            {
                runtimeKey = markerType.ToString();
                isFallback = true;
                note.Append($"[Fallback: RuntimeKey empty, used MarkerType '{runtimeKey}'] ");
            }
            else
            {
                note.Append($"[Key={runtimeKey}] ");
            }

            // 6. SemanticTags
            string[] semanticTags = binding.SemanticTags;

            // 7. Source summaries
            string sourceRuleSummary = binding.SourceRuleSummary;
            string sourcePlanSummary = binding.SourcePlanSummary;

            // 8. Prototype zone
            bool isPrototypeZone = binding.IsPrototypeZone;

            // 9. ResolverNote
            string resolverNote = note.ToString().Trim();

            candidate = new WorldMapRuntimeSpawnProfileCandidate(
                zoneId: zoneId,
                markerId: markerId,
                markerType: markerType,
                runtimeCategory: runtimeCategory,
                runtimeKey: runtimeKey,
                semanticTags: semanticTags,
                sourceRuleSummary: sourceRuleSummary,
                sourcePlanSummary: sourcePlanSummary,
                isPrototypeZone: isPrototypeZone,
                isFallbackCandidate: isFallback,
                resolverNote: resolverNote
            );

            return true;
        }

        /// <summary>
        /// WorldMapRuntimePlaceholderBinding으로부터 고유한 candidate key 문자열을 생성한다.
        /// 형식: "{RuntimeCategory}/{RuntimeKey}"
        /// fallback 여부와 관계없이 binding의 현재 값을 기준으로 생성한다.
        /// </summary>
        public static string BuildCandidateKey(WorldMapRuntimePlaceholderBinding binding)
        {
            if (binding == null) return "NULL/UNKNOWN";

            string category = !string.IsNullOrEmpty(binding.RuntimeCategory)
                ? NormalizeKey(binding.RuntimeCategory)
                : "UNKNOWN";

            string key = !string.IsNullOrEmpty(binding.RuntimeKey)
                ? NormalizeKey(binding.RuntimeKey)
                : "UNKNOWN";

            return $"{category}/{key}";
        }

        /// <summary>
        /// key 문자열을 정규화한다.
        /// 앞뒤 공백 제거, 연속 공백을 단일 공백으로 축소, null/empty 체크.
        /// </summary>
        public static string NormalizeKey(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            // 앞뒤 공백 제거
            string trimmed = value.Trim();

            // 연속 공백을 단일 공백으로 축소
            while (trimmed.Contains("  "))
            {
                trimmed = trimmed.Replace("  ", " ");
            }

            return trimmed;
        }

        // ===== Internal Resolve Helpers =====

        /// <summary>
        /// binding의 RuntimeCategory가 이미 설정되어 있으면 그대로 사용하고,
        /// 비어 있으면 MarkerType 기반으로 매핑한다.
        /// </summary>
        private static string ResolveRuntimeCategory(WorldMapRuntimePlaceholderBinding binding)
        {
            // 이미 설정된 RuntimeCategory가 있으면 우선 사용
            if (!string.IsNullOrEmpty(binding.RuntimeCategory))
            {
                return binding.RuntimeCategory;
            }

            // MarkerType 기반 매핑
            switch (binding.MarkerType)
            {
                case WorldMapZoneContentMarkerType.Resource:
                    return "HarvestResource";
                case WorldMapZoneContentMarkerType.Hazard:
                    return "Hazard";
                case WorldMapZoneContentMarkerType.Landmark:
                    return "WorldProp";
                case WorldMapZoneContentMarkerType.Narrative:
                    return "NarrativeLog";
                case WorldMapZoneContentMarkerType.Route:
                    return "RouteTrigger";
                case WorldMapZoneContentMarkerType.Debug:
                    return "Debug";
                case WorldMapZoneContentMarkerType.Unknown:
                default:
                    return "Unknown";
            }
        }

        /// <summary>
        /// binding의 RuntimeKey가 이미 설정되어 있으면 그대로 사용하고,
        /// 비어 있으면 SemanticTags 첫 번째 non-empty 값 또는 MarkerType.ToString()을 fallback으로 사용한다.
        /// </summary>
        private static string ResolveRuntimeKey(WorldMapRuntimePlaceholderBinding binding)
        {
            // 이미 설정된 RuntimeKey가 있으면 우선 사용
            if (!string.IsNullOrEmpty(binding.RuntimeKey))
            {
                return binding.RuntimeKey;
            }

            // SemanticTags 첫 번째 non-empty 값 fallback
            if (binding.SemanticTags != null && binding.SemanticTags.Length > 0)
            {
                foreach (string tag in binding.SemanticTags)
                {
                    if (!string.IsNullOrEmpty(tag))
                    {
                        return tag;
                    }
                }
            }

            // 그래도 없으면 MarkerType.ToString() 기반 fallback
            return binding.MarkerType.ToString();
        }
    }
}
