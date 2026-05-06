using Project.Data.World;
using Project.Gameplay.World;
using UnityEngine;

namespace Project.Editor.AutoTool
{
    /// <summary>
    /// Phase 14.10-O-9~O-13: Harvest system stable steps를 실행하는 Pipeline Phase Runner.
    /// AutoBuilder.cs의 GenerateFullScenarioMap 내부에서 단 한 줄로 호출된다.
    /// 각 단계 전후로 Debug.Log phase banner를 출력한다.
    /// </summary>
    public static class DeepLightMapPipelinePhaseO
    {
        /// <summary>
        /// O-9~O-13 stable steps를 순차 실행한다.
        /// AutoBuilder.cs GenerateFullScenarioMap 내부에서 O-8 완료 직후,
        /// Selection 설정 / LogFinalDataCount / 최종 완료 로그 이전에 호출된다.
        /// </summary>
        public static void RunStableOPhase(
            DeepLightMapAutoBuilderSettingsSO settings,
            DeepLightMapAutoBuilderSceneContext context)
        {
            // ===== Phase 14.10-O-9: Runtime Harvest System Integration =====
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-9: Runtime Harvest System Integration =====");
            DeepLightMapAutoBuilder.RebuildRuntimeHarvestSystemIntegration(settings, context);
            DeepLightMapAutoBuilder.ValidateRuntimeHarvestSystemIntegration(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-9: Runtime Harvest System Integration Complete =====");

            // ===== Phase 14.10-O-10: Generated Harvest Target Adapters =====
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-10: Generated Harvest Target Adapters =====");
            DeepLightMapAutoBuilder.RebuildGeneratedHarvestTargetAdapters(settings, context);
            DeepLightMapAutoBuilder.ValidateGeneratedHarvestTargetAdapters(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-10: Generated Harvest Target Adapters Complete =====");

            // ===== Phase 14.10-O-11: Generated Harvest Runtime Hook =====
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-11: Generated Harvest Runtime Hook =====");
            DeepLightMapAutoBuilder.RebuildGeneratedHarvestRuntimeHook(settings, context);
            DeepLightMapAutoBuilder.ValidateGeneratedHarvestRuntimeHook(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-11: Generated Harvest Runtime Hook Complete =====");

            // ===== Phase 14.10-O-12: Generated Harvest Interactor Hooks =====
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-12: Generated Harvest Interactor Hooks =====");
            DeepLightMapAutoBuilder.RebuildGeneratedHarvestInteractorHooks(settings, context);
            DeepLightMapAutoBuilder.ValidateGeneratedHarvestInteractorHooks(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-12: Generated Harvest Interactor Hooks Complete =====");

            // ===== Phase 14.10-O-13: Generated Harvest PlayMode Smoke Test =====
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-13: Generated Harvest PlayMode Smoke Test =====");
            DeepLightMapAutoBuilder.RebuildGeneratedHarvestPlayModeSmokeTest(settings, context);
            DeepLightMapAutoBuilder.ValidateGeneratedHarvestPlayModeSmokeTest(settings, context);
            Debug.Log("[MapAutoBuilder] ===== Phase 14.10-O-13: Generated Harvest PlayMode Smoke Test Complete =====");
        }
    }
}
