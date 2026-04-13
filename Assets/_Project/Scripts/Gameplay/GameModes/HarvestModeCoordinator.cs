using Project.Core.Events;
using Project.Data.Harvest;
using Project.Data.Items;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.GameModes
{
    /// <summary>채집 대상 진입과 모드 전환 흐름을 조정하는 클래스이다.</summary>
    public class HarvestModeCoordinator
    {
        private readonly GameModeService gameModeService; // 게임 모드 서비스
        private readonly HarvestModeSession harvestModeSession; // 채집 세션 상태

        /// <summary>채집 세션을 반환한다.</summary>
        public HarvestModeSession HarvestModeSession => harvestModeSession;

        /// <summary>채집 모드 조정기를 생성한다.</summary>
        public HarvestModeCoordinator(GameModeService gameModeService, HarvestModeSession harvestModeSession)
        {
            this.gameModeService = gameModeService;
            this.harvestModeSession = harvestModeSession;
        }

        /// <summary>현재 타깃의 런타임 고유 키를 반환한다.</summary>
        private string ResolveRuntimeTargetKey(IHarvestTarget target)
        {
            if (target is HarvestTargetBehaviour targetBehaviour)
                return targetBehaviour.GetRuntimeTargetKey();

            if (target is Component component)
                return $"{component.gameObject.scene.name}_{component.GetInstanceID()}";

            return string.Empty;
        }

        /// <summary>채집 대상과 함께 채집 모드에 진입한다.</summary>
        public bool TryEnterHarvestMode(IHarvestTarget target)
        {
            if (target == null)
                return false;

            if (!target.IsAvailable)
                return false;

            HarvestTargetSO targetData = target.TargetData;
            if (targetData == null || !targetData.IsValid())
                return false;

            // 월드에는 존재하지만 현재 day 조건이 안 맞는 경우 진입만 막는다.
            if (target is HarvestTargetBehaviour targetBehaviour && !targetBehaviour.IsHarvestUnlocked)
            {
                Debug.LogWarning($"[HarvestModeCoordinator] Harvest blocked: {targetBehaviour.GetUnavailableReason()}");
                return false;
            }

            // 방어적 초기화:
            // 이전 세션의 휘발성 상태를 먼저 날린 뒤 새 타깃을 바인딩한다.
            harvestModeSession.ResetTransientState(clearTarget: true);
            harvestModeSession.BeginForTarget(target);

            string itemId = string.Empty;

            if (target is HarvestTargetBehaviour resolvedTargetBehaviour)
            {
                ItemSO resolvedItem = resolvedTargetBehaviour.GetResolvedItem();
                if (resolvedItem != null && resolvedItem.IsValid())
                    itemId = resolvedItem.ItemId;
            }

            string runtimeTargetKey = ResolveRuntimeTargetKey(target);
            EventBus.Publish(new HarvestSessionStartedEvent(runtimeTargetKey, itemId));

            gameModeService.EnterHarvestMode();

            Transform targetTransform = target is Component component ? component.transform : null;
            if (targetTransform != null)
                EventBus.Publish(new HarvestModeEnteredEvent(targetTransform));

            return true;
        }

        /// <summary>탐사 모드로 복귀한다.</summary>
        public void ExitHarvestMode()
        {
            string runtimeTargetKey = ResolveRuntimeTargetKey(harvestModeSession.CurrentTarget);

            if (!string.IsNullOrWhiteSpace(runtimeTargetKey))
                EventBus.Publish(new HarvestSessionEndedEvent(runtimeTargetKey));

            EventBus.Publish(new HarvestModeExitedEvent());

            // 종료 이벤트를 발행한 뒤 세션을 비워서, 리스너들이 현재 타깃 정보를 읽을 수 있게 한다.
            harvestModeSession.ResetTransientState(clearTarget: true);

            gameModeService.EnterExplorationMode();
        }
    }
}
