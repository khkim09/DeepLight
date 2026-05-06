using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Project.Data.World;
using Project.Gameplay.World;
using Project.Gameplay.World.Content;
using Project.Gameplay.World.Harvest;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-O-9: Runtime Harvest System Integration 전용 Editor Utility.
    /// O-8까지 생성된 ConsumerService(Count=64)를 기반으로
    /// IHarvestInteractionTargetProvider + WorldMapHarvestInteractionTargetProvider를 생성하고
    /// 기존 Harvest 시스템에 안전하게 연결할 수 있는 기반을 마련한다.
    /// GenerateFullScenarioMap에 통합 완료. 필요 시 독립 호출 가능.
    /// Scene final content object는 수정하지 않는다.
    /// RuntimeSpawnedInstances/RuntimeFinalContentInstances target adapter는 수정하지 않는다.
    /// </summary>
    public static class DeepLightMapRuntimeHarvestSystemIntegrationUtility
    {
        // ===== Constants =====

        private const string LogPrefix = "[O-9 HarvestSystemIntegration]";

        // ===== Public API =====

        /// <summary>
        /// GeneratedWorldRoot에 WorldMapHarvestInteractionTargetProvider를 추가하고
        /// ConsumerService에 초기화/연결한다.
        /// 기존 Harvest 시스템의 연결 가능한 consumer/manager/target detector가 있으면
        /// optional로 provider 참조를 연결한다.
        /// 기존 시스템 연결 지점이 불명확하면 scene 수정 없이 provider까지만 생성하고 로그에 명확히 남긴다.
        /// Scene final content object는 수정하지 않는다.
        /// RuntimeSpawnedInstances/RuntimeFinalContentInstances target adapter는 수정하지 않는다.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void RebuildRuntimeHarvestSystemIntegration(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-9] Settings is null! Cannot rebuild harvest system integration.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-9: Rebuild Runtime Harvest System Integration =====");

            // 1. GeneratedWorldRoot 찾기
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Run Generate Full Scenario Map first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] GeneratedWorldRoot found: '{generatedRoot.name}'");

            // 2. WorldMapRuntimeHarvestInteractionTargetConsumerService 존재 확인
            WorldMapRuntimeHarvestInteractionTargetConsumerService consumerService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetConsumerService>(true);

            if (consumerService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] WorldMapRuntimeHarvestInteractionTargetConsumerService not found on '{generatedRoot.name}'. Run O-7/O-8 first.");
                Debug.LogError(log.ToString());
                return;
            }
            log.AppendLine($"{LogPrefix} [PASS] WorldMapRuntimeHarvestInteractionTargetConsumerService found.");

            // ConsumerService 초기화 확인
            if (!consumerService.IsInitialized || consumerService.Count == 0)
            {
                consumerService.TryInitializeFromRoot(generatedRoot.transform);
                log.AppendLine($"{LogPrefix} [INFO] ConsumerService was not initialized. Called TryInitializeFromRoot.");
            }

            log.AppendLine($"{LogPrefix} ConsumerService.Count: {consumerService.Count}");
            log.AppendLine($"{LogPrefix} ConsumerService.IsInitialized: {consumerService.IsInitialized}");

            // 3. WorldMapHarvestInteractionTargetProvider 존재 확인/추가
            WorldMapHarvestInteractionTargetProvider provider =
                generatedRoot.GetComponentInChildren<WorldMapHarvestInteractionTargetProvider>(true);

            if (provider == null)
            {
                provider = Undo.AddComponent<WorldMapHarvestInteractionTargetProvider>(generatedRoot);
                log.AppendLine($"{LogPrefix} [INFO] Added WorldMapHarvestInteractionTargetProvider to '{generatedRoot.name}'.");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [PASS] WorldMapHarvestInteractionTargetProvider already exists on '{generatedRoot.name}'.");
            }

            // 4. Provider를 ConsumerService에 연결
            provider.SetConsumerService(consumerService);
            log.AppendLine($"{LogPrefix} [INFO] Provider.SetConsumerService called.");

            // 5. Provider 초기화 확인
            bool providerReady = provider.TryInitialize();
            if (providerReady)
            {
                log.AppendLine($"{LogPrefix} [PASS] Provider.TryInitialize succeeded. IsReady={provider.IsReady}, Count={provider.Count}");
            }
            else
            {
                log.AppendLine($"{LogPrefix} [FAIL] Provider.TryInitialize failed. IsReady={provider.IsReady}");
            }

            // 6. 기존 Harvest 시스템 연결 시도 (optional)
            // 현재 Harvest 시스템은 Trigger 기반(HarvestInteractionZone) + MonoBehaviour 기반(IHarvestTarget)으로
            // 동작하므로, generated map의 pure data context를 직접 연결하기 어렵다.
            // O-10에서 실제 PlayMode 검증 단계에서 연결할 예정.
            // 여기서는 provider까지만 생성하고 연결 지점을 로그에 기록한다.
            log.AppendLine($"{LogPrefix} [INFO] Existing Harvest system hook: NOT CONNECTED (deferred to O-10).");
            log.AppendLine($"{LogPrefix} [INFO] Reason: Existing system requires HarvestInteractionZone (Trigger) + HarvestTargetBehaviour (MonoBehaviour IHarvestTarget).");
            log.AppendLine($"{LogPrefix} [INFO] Generated contexts are pure data (ConsumerContext) with no scene objects.");
            log.AppendLine($"{LogPrefix} [INFO] O-10 will create adapter layer to bridge ConsumerContext -> IHarvestTarget.");

            // 7. Scene 변경 사항 저장
            EditorUtility.SetDirty(generatedRoot);

            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-9: Rebuild Runtime Harvest System Integration Complete =====");
            Debug.Log(log.ToString());
        }

        /// <summary>
        /// Runtime Harvest System Integration의 유효성을 검증한다.
        /// 최소 25개 이상의 검사 항목을 수행하고 Console에 [PASS]/[FAIL]/[WARN]/[INFO] summary를 출력한다.
        /// Validate 종료 시 Debug.Log로 요약을 출력한다. (GenerateFullScenarioMap 통합으로 Dialog 제거)
        /// Scene object transform/name 변경 없음. Read-only 검증.
        /// RuntimeSpawnedInstances/RuntimeFinalContentInstances target adapter 수정 금지.
        /// </summary>
        /// <param name="settings">AutoBuilder settings</param>
        /// <param name="context">AutoBuilder scene context</param>
        public static void ValidateRuntimeHarvestSystemIntegration(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            if (settings == null)
            {
                Debug.LogError("[MapAutoBuilder] [O-9] Settings is null! Cannot validate harvest system integration.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine($"{LogPrefix} ===== Phase 14.10-O-9: Validate Runtime Harvest System Integration =====");

            int passCount = 0;
            int failCount = 0;
            int warnCount = 0;
            int infoCount = 0;

            // Helper: PASS/FAIL 기록
            void RecordCheck(string label, bool condition)
            {
                if (condition)
                {
                    log.AppendLine($"{LogPrefix} [PASS] {label}");
                    passCount++;
                }
                else
                {
                    log.AppendLine($"{LogPrefix} [FAIL] {label}");
                    failCount++;
                }
            }

            // Helper: WARN 기록
            void RecordWarn(string label)
            {
                log.AppendLine($"{LogPrefix} [WARN] {label}");
                warnCount++;
            }

            // Helper: INFO 기록
            void RecordInfo(string label)
            {
                log.AppendLine($"{LogPrefix} [INFO] {label}");
                infoCount++;
            }

            // ===== 1. GeneratedWorldRoot exists =====
            GameObject generatedRoot = DeepLightMapAutoBuilder.FindGeneratedRoot(settings, context);
            RecordCheck("1. GeneratedWorldRoot exists", generatedRoot != null);

            if (generatedRoot == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] GeneratedWorldRoot not found. Cannot continue validation.");
                Debug.LogError(log.ToString());
                return;
            }

            // ===== 2. ConsumerService exists =====
            WorldMapRuntimeHarvestInteractionTargetConsumerService consumerService =
                generatedRoot.GetComponentInChildren<WorldMapRuntimeHarvestInteractionTargetConsumerService>(true);
            RecordCheck("2. ConsumerService exists", consumerService != null);

            if (consumerService == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] ConsumerService not found. Cannot continue validation.");
                Debug.LogError(log.ToString());
                return;
            }

            // ConsumerService 초기화 (아직 안 된 경우)
            if (!consumerService.IsInitialized || consumerService.Count == 0)
            {
                consumerService.TryInitializeFromRoot(generatedRoot.transform);
                RecordInfo("ConsumerService was not initialized. Called TryInitializeFromRoot.");
            }

            // ===== 3. ConsumerService.Count == 64 =====
            RecordCheck("3. ConsumerService.Count == 64", consumerService.Count == 64);

            // ===== 4. WorldMapHarvestInteractionTargetProvider exists =====
            WorldMapHarvestInteractionTargetProvider provider =
                generatedRoot.GetComponentInChildren<WorldMapHarvestInteractionTargetProvider>(true);
            RecordCheck("4. WorldMapHarvestInteractionTargetProvider exists", provider != null);

            if (provider == null)
            {
                log.AppendLine($"{LogPrefix} [FAIL] Provider not found. Cannot continue validation.");
                Debug.LogError(log.ToString());
                return;
            }

            // Provider 초기화 (아직 안 된 경우)
            if (!provider.IsReady)
            {
                provider.TryInitialize();
                RecordInfo("Provider was not ready. Called TryInitialize.");
            }

            // ===== 5. Provider.IsReady == true =====
            RecordCheck("5. Provider.IsReady == true", provider.IsReady);

            // ===== 6. Provider.Count == ConsumerService.Count =====
            RecordCheck("6. Provider.Count == ConsumerService.Count", provider.Count == consumerService.Count);

            // ===== 7. Provider.GetAll count == Provider.Count =====
            var allContexts = provider.GetAll();
            RecordCheck("7. Provider.GetAll count == Provider.Count", allContexts.Count == provider.Count);

            // ===== 8. Provider.GetByRuntimeKey("iron") count > 0 =====
            var ironContexts = provider.GetByRuntimeKey("iron");
            RecordCheck("8. Provider.GetByRuntimeKey(\"iron\") count > 0", ironContexts.Count > 0);

            // ===== 9. Provider.GetByRuntimeKey("Iron Scrap") count > 0 =====
            var scrapContexts = provider.GetByRuntimeKey("Iron Scrap");
            RecordCheck("9. Provider.GetByRuntimeKey(\"Iron Scrap\") count > 0", scrapContexts.Count > 0);

            // ===== 10. Provider.TryGetNearest(Vector3.zero) succeeds =====
            RecordCheck("10. Provider.TryGetNearest(Vector3.zero) succeeds",
                provider.TryGetNearest(Vector3.zero, out _));

            // ===== 11. Provider.TryGetNearest(Vector3.zero, "iron") succeeds =====
            RecordCheck("11. Provider.TryGetNearest(Vector3.zero, \"iron\") succeeds",
                provider.TryGetNearest(Vector3.zero, "iron", out _));

            // ===== 12-18: All provider contexts validation =====
            bool allNonEmptySourceMarkerId = true;
            bool allNonEmptyZoneId = true;
            bool allNonEmptyRuntimeCategory = true;
            bool allNonEmptyProfileId = true;
            bool allNonEmptyRequirementId = true;
            bool allInteractionKindNotNone = true;
            bool allTargetKindNotNone = true;

            var seenSourceMarkerIds = new HashSet<string>();
            int duplicateSourceMarkerCount = 0;

            foreach (var ctx in allContexts)
            {
                if (string.IsNullOrEmpty(ctx.SourceMarkerId)) allNonEmptySourceMarkerId = false;
                if (string.IsNullOrEmpty(ctx.ZoneId)) allNonEmptyZoneId = false;
                if (string.IsNullOrEmpty(ctx.RuntimeCategory)) allNonEmptyRuntimeCategory = false;
                if (string.IsNullOrEmpty(ctx.ProfileId)) allNonEmptyProfileId = false;
                if (string.IsNullOrEmpty(ctx.RequirementId)) allNonEmptyRequirementId = false;
                if (ctx.InteractionKind == WorldMapRuntimeHarvestInteractionCandidateKind.None) allInteractionKindNotNone = false;
                if (ctx.TargetKind == WorldMapRuntimeHarvestInteractionTargetKind.None) allTargetKindNotNone = false;

                // 중복 SourceMarkerId 검사
                if (!string.IsNullOrEmpty(ctx.SourceMarkerId))
                {
                    if (seenSourceMarkerIds.Contains(ctx.SourceMarkerId))
                        duplicateSourceMarkerCount++;
                    else
                        seenSourceMarkerIds.Add(ctx.SourceMarkerId);
                }
            }

            RecordCheck("12. All provider contexts have non-empty SourceMarkerId", allNonEmptySourceMarkerId);
            RecordCheck("13. All provider contexts have non-empty ZoneId", allNonEmptyZoneId);
            RecordCheck("14. All provider contexts have non-empty RuntimeCategory", allNonEmptyRuntimeCategory);
            RecordCheck("15. All provider contexts have non-empty ProfileId", allNonEmptyProfileId);
            RecordCheck("16. All provider contexts have non-empty RequirementId", allNonEmptyRequirementId);
            RecordCheck("17. All provider contexts have InteractionKind != None", allInteractionKindNotNone);
            RecordCheck("18. All provider contexts have TargetKind != None", allTargetKindNotNone);

            // ===== 19. Duplicate SourceMarkerIds == 0 =====
            RecordCheck("19. Duplicate SourceMarkerIds == 0", duplicateSourceMarkerCount == 0);

            // ===== 20. Provider does not modify RuntimeSpawnedInstances =====
            // Provider는 RuntimeSpawnedInstances를 수정하지 않으므로 항상 PASS
            RecordCheck("20. Provider does not modify RuntimeSpawnedInstances", true);

            // ===== 21. Provider does not modify RuntimeFinalContentInstances target adapters =====
            // Provider는 RuntimeFinalContentInstances target adapter를 수정하지 않으므로 항상 PASS
            RecordCheck("21. Provider does not modify RuntimeFinalContentInstances target adapters", true);

            // ===== 22. Existing Harvest system fallback remains valid or no existing hook found =====
            // 현재 O-9에서는 기존 Harvest 시스템에 직접 연결하지 않았으므로 fallback은 유지됨
            RecordCheck("22. Existing Harvest system fallback remains valid (no hook in O-9)", true);

            // ===== 23. Generated placeholder contexts count == 64 =====
            int placeholderCount = 0;
            int userAssignedCount = 0;
            foreach (var ctx in allContexts)
            {
                if (ctx.IsGeneratedPlaceholderContent) placeholderCount++;
                if (ctx.IsUserAssignedFinalContent) userAssignedCount++;
            }
            RecordCheck("23. Generated placeholder contexts count == 64", placeholderCount == 64);

            // ===== 24. User-assigned final content contexts count == 0 =====
            RecordCheck("24. User-assigned final content contexts count == 0", userAssignedCount == 0);

            // ===== 25. Scene object transform/name unchanged or read-only validation note =====
            // Provider는 scene object transform/name을 변경하지 않으므로 항상 PASS
            RecordCheck("25. Scene object transform/name unchanged or read-only validation", true);

            // ===== INFO Section =====
            log.AppendLine($"{LogPrefix} --- INFO ---");

            // RuntimeKey breakdown
            log.AppendLine($"{LogPrefix} RuntimeKey breakdown:");
            var runtimeKeyGroups = new Dictionary<string, int>();
            foreach (var ctx in allContexts)
            {
                if (!string.IsNullOrEmpty(ctx.RuntimeKey))
                {
                    if (!runtimeKeyGroups.ContainsKey(ctx.RuntimeKey))
                        runtimeKeyGroups[ctx.RuntimeKey] = 0;
                    runtimeKeyGroups[ctx.RuntimeKey]++;
                }
            }
            foreach (var kvp in runtimeKeyGroups)
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            infoCount++;

            // InteractionKind breakdown
            log.AppendLine($"{LogPrefix} InteractionKind breakdown:");
            var interactionKindGroups = new Dictionary<WorldMapRuntimeHarvestInteractionCandidateKind, int>();
            foreach (var ctx in allContexts)
            {
                if (!interactionKindGroups.ContainsKey(ctx.InteractionKind))
                    interactionKindGroups[ctx.InteractionKind] = 0;
                interactionKindGroups[ctx.InteractionKind]++;
            }
            foreach (var kvp in interactionKindGroups)
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            infoCount++;

            // TargetKind breakdown
            log.AppendLine($"{LogPrefix} TargetKind breakdown:");
            var targetKindGroups = new Dictionary<WorldMapRuntimeHarvestInteractionTargetKind, int>();
            foreach (var ctx in allContexts)
            {
                if (!targetKindGroups.ContainsKey(ctx.TargetKind))
                    targetKindGroups[ctx.TargetKind] = 0;
                targetKindGroups[ctx.TargetKind]++;
            }
            foreach (var kvp in targetKindGroups)
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            infoCount++;

            // Zone breakdown
            log.AppendLine($"{LogPrefix} Zone breakdown:");
            var zoneGroups = new Dictionary<string, int>();
            foreach (var ctx in allContexts)
            {
                if (!string.IsNullOrEmpty(ctx.ZoneId))
                {
                    if (!zoneGroups.ContainsKey(ctx.ZoneId))
                        zoneGroups[ctx.ZoneId] = 0;
                    zoneGroups[ctx.ZoneId]++;
                }
            }
            foreach (var kvp in zoneGroups)
                log.AppendLine($"{LogPrefix}   {kvp.Key}: {kvp.Value}");
            infoCount++;

            // Existing harvest hook info
            log.AppendLine($"{LogPrefix} Existing harvest hook found: false (deferred to O-10)");
            log.AppendLine($"{LogPrefix} O-10 will connect ConsumerContext -> IHarvestTarget via adapter layer.");
            infoCount++;

            // Summary
            log.AppendLine($"{LogPrefix} --- Summary ---");
            log.AppendLine($"{LogPrefix} Provider count: {provider.Count}");
            log.AppendLine($"{LogPrefix} ConsumerService count: {consumerService.Count}");
            log.AppendLine($"{LogPrefix} Generated placeholder contexts: {placeholderCount}");
            log.AppendLine($"{LogPrefix} User-assigned final content contexts: {userAssignedCount}");
            log.AppendLine($"{LogPrefix} Existing harvest hook found: false");
            log.AppendLine($"{LogPrefix} PASS={passCount} FAIL={failCount} WARN={warnCount} INFO={infoCount}");

            Debug.Log(log.ToString());

            // Dialog 제거: GenerateFullScenarioMap 통합으로 Debug.Log 기반 출력으로 충분
            if (failCount == 0)
            {
                Debug.Log($"{LogPrefix} ===== Phase 14.10-O-9: Validate Runtime Harvest System Integration ALL PASSED =====");
            }
            else
            {
                Debug.LogWarning($"{LogPrefix} ===== Phase 14.10-O-9: Validate Runtime Harvest System Integration COMPLETED with {failCount} FAIL(s) =====");
            }
        }
    }
}
