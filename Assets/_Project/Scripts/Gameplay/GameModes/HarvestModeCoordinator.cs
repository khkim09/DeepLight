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

            // 세션 대상 저장
            harvestModeSession.SetTarget(target);

            // 세션 시작 이벤트는 현재 target이 런타임에서 확정한 아이템 ID를 사용한다.
            string itemId = string.Empty;

            if (target is HarvestTargetBehaviour targetBehaviour)
            {
                ItemSO resolvedItem = targetBehaviour.GetResolvedItem();
                if (resolvedItem != null && resolvedItem.IsValid())
                    itemId = resolvedItem.ItemId;
            }

            EventBus.Publish(new HarvestSessionStartedEvent(targetData.TargetId, itemId));

            gameModeService.EnterHarvestMode();

            Transform targetTransform = target is Component component ? component.transform : null;
            if (targetTransform != null)
                EventBus.Publish(new HarvestModeEnteredEvent(targetTransform));

            return true;
        }

        /// <summary>탐사 모드로 복귀한다.</summary>
        public void ExitHarvestMode()
        {
            string targetId = string.Empty;
            if (harvestModeSession.CurrentTarget != null && harvestModeSession.CurrentTarget.TargetData != null)
                targetId = harvestModeSession.CurrentTarget.TargetData.TargetId;

            if (!string.IsNullOrWhiteSpace(targetId))
                EventBus.Publish(new HarvestSessionEndedEvent(targetId));

            EventBus.Publish(new HarvestModeExitedEvent());

            harvestModeSession.ClearTarget();
            gameModeService.EnterExplorationMode();
        }
    }
}
