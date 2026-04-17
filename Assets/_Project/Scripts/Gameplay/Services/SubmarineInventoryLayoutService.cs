using System.Collections.Generic;
using Project.Core.Events;
using Project.Data.Submarine;
using Project.Gameplay.Runtime;
using UnityEngine;

namespace Project.Gameplay.Services
{
    /// <summary>잠수함 인벤토리 레이아웃 단계와 교체를 관리하는 런타임 서비스이다.</summary>
    public class SubmarineInventoryLayoutRuntimeService
    {
        private readonly SubmarineRuntimeState submarineRuntimeState; // 잠수함 런타임 상태
        private readonly List<SubmarineInventoryLayoutSO> layoutStages = new(); // 업그레이드 단계별 레이아웃

        private int currentStageIndex; // 현재 단계 인덱스

        public int CurrentStageIndex => currentStageIndex;
        public int StageCount => layoutStages.Count;

        /// <summary>현재 적용 중인 인벤토리 레이아웃을 반환한다.</summary>
        public SubmarineInventoryLayoutSO CurrentLayout =>
            currentStageIndex >= 0 && currentStageIndex < layoutStages.Count
                ? layoutStages[currentStageIndex]
                : null;

        /// <summary>레벨 단계형 인벤토리 레이아웃 서비스를 생성한다.</summary>
        public SubmarineInventoryLayoutRuntimeService(
            SubmarineRuntimeState newSubmarineRuntimeState,
            SubmarineInventoryLayoutSO baseLayout,
            IReadOnlyList<SubmarineInventoryLayoutSO> upgradeLayouts)
        {
            submarineRuntimeState = newSubmarineRuntimeState;

            BuildLayoutStageList(baseLayout, upgradeLayouts);
            ResolveCurrentStageIndex();
        }

        /// <summary>다음 인벤토리 레이아웃 단계로 업그레이드를 시도한다.</summary>
        public bool TryAdvanceUpgrade()
        {
            return TrySetStage(currentStageIndex + 1);
        }

        /// <summary>특정 인벤토리 레이아웃 단계로 전환을 시도한다.</summary>
        public bool TrySetStage(int targetStageIndex)
        {
            if (submarineRuntimeState == null)
                return false;

            if (targetStageIndex < 0 || targetStageIndex >= layoutStages.Count)
                return false;

            if (targetStageIndex == currentStageIndex)
                return false;

            SubmarineInventoryLayoutSO nextLayout = layoutStages[targetStageIndex];
            if (nextLayout == null || !nextLayout.IsValid())
                return false;

            int previousStageIndex = currentStageIndex;

            // 기존 아이템이 새 레이아웃에 유지 가능할 때만 성공한다.
            bool isApplied = submarineRuntimeState.TryApplyInventoryLayout(nextLayout);
            if (!isApplied)
            {
                Debug.LogWarning(
                    $"[InventoryLayoutRuntimeService] Failed to apply inventory layout stage {targetStageIndex}. " +
                    $"Existing items do not fit in the target layout.");
                return false;
            }

            currentStageIndex = targetStageIndex;

            EventBus.Publish(new InventoryLayoutChangedEvent(
                previousStageIndex,
                currentStageIndex));

            return true;
        }

        /// <summary>현재 레이아웃 기준으로 UI 동기화 이벤트를 강제로 다시 발행한다.</summary>
        public void PublishCurrentLayoutChangedEvent()
        {
            EventBus.Publish(new InventoryLayoutChangedEvent(
                currentStageIndex,
                currentStageIndex));
        }

        /// <summary>기본 레이아웃과 업그레이드 레이아웃 배열로 단계 목록을 구성한다.</summary>
        private void BuildLayoutStageList(
            SubmarineInventoryLayoutSO baseLayout,
            IReadOnlyList<SubmarineInventoryLayoutSO> upgradeLayouts)
        {
            layoutStages.Clear();

            AddStageIfValid(baseLayout);

            if (upgradeLayouts == null)
                return;

            for (int i = 0; i < upgradeLayouts.Count; i++)
                AddStageIfValid(upgradeLayouts[i]);
        }

        /// <summary>현재 런타임 상태의 레이아웃과 가장 일치하는 단계 인덱스를 계산한다.</summary>
        private void ResolveCurrentStageIndex()
        {
            currentStageIndex = 0;

            if (submarineRuntimeState == null || submarineRuntimeState.CurrentInventoryLayout == null)
                return;

            for (int i = 0; i < layoutStages.Count; i++)
            {
                if (layoutStages[i] == submarineRuntimeState.CurrentInventoryLayout)
                {
                    currentStageIndex = i;
                    return;
                }
            }
        }

        /// <summary>유효하고 중복되지 않은 레이아웃만 단계 목록에 추가한다.</summary>
        private void AddStageIfValid(SubmarineInventoryLayoutSO layout)
        {
            if (layout == null || !layout.IsValid())
                return;

            if (layoutStages.Contains(layout))
                return;

            layoutStages.Add(layout);
        }
    }
}
