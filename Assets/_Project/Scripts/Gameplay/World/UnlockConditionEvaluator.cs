using System;
using Project.Data.World;

namespace Project.Gameplay.World
{
    /// <summary>개별 해금 조건 평가를 담당하는 구현체</summary>
    public class UnlockConditionEvaluator : IUnlockConditionEvaluator
    {
        /// <summary>조건 평가</summary>
        public bool EvaluateCondition(
            UnlockConditionType conditionType,
            string conditionKey,
            int requiredValue,
            IWorldProgressQuery progressQuery)
        {
            if (progressQuery == null)
                throw new ArgumentNullException(nameof(progressQuery));

            switch (conditionType)
            {
                case UnlockConditionType.None:
                    return true; // 조건 없음은 항상 통과

                case UnlockConditionType.HasUpgrade:
                    return !string.IsNullOrEmpty(conditionKey) &&
                           progressQuery.HasUpgrade(conditionKey);

                case UnlockConditionType.HasUpgradeLevel:
                    return !string.IsNullOrEmpty(conditionKey) &&
                           progressQuery.GetUpgradeLevel(conditionKey) >= requiredValue;

                case UnlockConditionType.HasLog:
                    return !string.IsNullOrEmpty(conditionKey) &&
                           progressQuery.HasLog(conditionKey);

                case UnlockConditionType.HasLogCount:
                    return progressQuery.GetLogCount() >= requiredValue;

                case UnlockConditionType.HasNarrativeFlag:
                    return !string.IsNullOrEmpty(conditionKey) &&
                           progressQuery.HasNarrativeFlag(conditionKey);

                case UnlockConditionType.HasTalkState:
                    return !string.IsNullOrEmpty(conditionKey) &&
                           progressQuery.HasTalkState(conditionKey);

                case UnlockConditionType.HasRelic:
                    return !string.IsNullOrEmpty(conditionKey) &&
                           progressQuery.HasRelic(conditionKey);

                case UnlockConditionType.HasHullTier:
                    return progressQuery.GetCurrentHullTier() >= requiredValue;

                case UnlockConditionType.HasDepthLevel:
                    return progressQuery.GetCurrentDepthLevel() >= requiredValue;

                case UnlockConditionType.HasSensorAccuracy:
                    return progressQuery.GetCurrentSensorAccuracy() >= (requiredValue / 100f);

                case UnlockConditionType.HasZoneDiscovered:
                    if (string.IsNullOrEmpty(conditionKey))
                        return false;
                    if (ZoneId.TryParse(conditionKey, out ZoneId zoneId))
                        return progressQuery.IsZoneDiscovered(zoneId);
                    return false;

                case UnlockConditionType.HasZoneUnlocked:
                    if (string.IsNullOrEmpty(conditionKey))
                        return false;
                    if (ZoneId.TryParse(conditionKey, out ZoneId zoneId2))
                        return progressQuery.IsZoneUnlocked(zoneId2);
                    return false;

                default:
                    throw new ArgumentException($"Unknown condition type: {conditionType}");
            }
        }

        /// <summary>조건 실패 이유 설명 (디버그/UI용)</summary>
        public string GetFailureReason(
            UnlockConditionType conditionType,
            string conditionKey,
            int requiredValue,
            IWorldProgressQuery progressQuery)
        {
            if (progressQuery == null)
                return "Progress query not available";

            try
            {
                switch (conditionType)
                {
                    case UnlockConditionType.None:
                        return "No condition required";

                    case UnlockConditionType.HasUpgrade:
                        return $"Upgrade '{conditionKey}' not acquired";

                    case UnlockConditionType.HasUpgradeLevel:
                        int currentLevel = progressQuery.GetUpgradeLevel(conditionKey);
                        return $"Upgrade '{conditionKey}' level insufficient: {currentLevel}/{requiredValue}";

                    case UnlockConditionType.HasLog:
                        return $"Log '{conditionKey}' not found";

                    case UnlockConditionType.HasLogCount:
                        int currentLogs = progressQuery.GetLogCount();
                        return $"Log count insufficient: {currentLogs}/{requiredValue}";

                    case UnlockConditionType.HasNarrativeFlag:
                        return $"Narrative flag '{conditionKey}' not triggered";

                    case UnlockConditionType.HasTalkState:
                        return $"Talk state '{conditionKey}' not reached";

                    case UnlockConditionType.HasRelic:
                        return $"Relic '{conditionKey}' not acquired";

                    case UnlockConditionType.HasHullTier:
                        int currentTier = progressQuery.GetCurrentHullTier();
                        return $"Hull tier insufficient: {currentTier}/{requiredValue}";

                    case UnlockConditionType.HasDepthLevel:
                        int currentDepth = progressQuery.GetCurrentDepthLevel();
                        return $"Depth level insufficient: {currentDepth}/{requiredValue}";

                    case UnlockConditionType.HasSensorAccuracy:
                        float currentAccuracy = progressQuery.GetCurrentSensorAccuracy();
                        float requiredAccuracy = requiredValue / 100f;
                        return $"Sensor accuracy insufficient: {currentAccuracy:P0}/{requiredAccuracy:P0}";

                    case UnlockConditionType.HasZoneDiscovered:
                        if (string.IsNullOrEmpty(conditionKey))
                            return "Zone ID is empty";
                        if (ZoneId.TryParse(conditionKey, out ZoneId zoneId))
                        {
                            bool discovered = progressQuery.IsZoneDiscovered(zoneId);
                            return $"Zone '{zoneId}' not discovered (currently: {discovered})";
                        }
                        return $"Invalid zone ID format: '{conditionKey}'";

                    case UnlockConditionType.HasZoneUnlocked:
                        if (string.IsNullOrEmpty(conditionKey))
                            return "Zone ID is empty";
                        if (ZoneId.TryParse(conditionKey, out ZoneId zoneId2))
                        {
                            bool unlocked = progressQuery.IsZoneUnlocked(zoneId2);
                            return $"Zone '{zoneId2}' not unlocked (currently: {unlocked})";
                        }
                        return $"Invalid zone ID format: '{conditionKey}'";

                    default:
                        return $"Unknown condition type: {conditionType}";
                }
            }
            catch (Exception ex)
            {
                return $"Error evaluating condition: {ex.Message}";
            }
        }

        /// <summary>조건 타입에 따른 기본 표시 이름 생성</summary>
        public static string GetConditionTypeDisplayName(UnlockConditionType conditionType)
        {
            return conditionType switch
            {
                UnlockConditionType.None => "No Condition",
                UnlockConditionType.HasUpgrade => "Upgrade Required",
                UnlockConditionType.HasUpgradeLevel => "Upgrade Level",
                UnlockConditionType.HasLog => "Log Required",
                UnlockConditionType.HasLogCount => "Log Count",
                UnlockConditionType.HasNarrativeFlag => "Story Progress",
                UnlockConditionType.HasTalkState => "Talk State",
                UnlockConditionType.HasRelic => "Relic Required",
                UnlockConditionType.HasHullTier => "Hull Tier",
                UnlockConditionType.HasDepthLevel => "Depth Level",
                UnlockConditionType.HasSensorAccuracy => "Sensor Accuracy",
                UnlockConditionType.HasZoneDiscovered => "Zone Discovered",
                UnlockConditionType.HasZoneUnlocked => "Zone Unlocked",
                _ => conditionType.ToString()
            };
        }
    }
}
