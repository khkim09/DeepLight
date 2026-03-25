using Project.Core.Events;
using Project.Data.Harvest;
using Project.Data.Items;
using Project.Gameplay.GameModes;
using Project.Gameplay.Inventory;
using Project.Gameplay.Runtime;
using Project.Gameplay.Services;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Gameplay.Harvest
{
    /// <summary>채집 확률 계산과 결과 반영을 담당하는 클래스</summary>
    public class HarvestResolver
    {
        private readonly SubmarineRuntimeState submarineRuntimeState; // 잠수함 런타임 상태
        private readonly ClawRuntimeState clawRuntimeState; // 로봇 팔 런타임 상태
        private readonly InventoryService inventoryService; // 인벤토리 서비스

        public SubmarineRuntimeState SubmarineRuntimeState => submarineRuntimeState;
        public ClawRuntimeState ClawRuntimeState => clawRuntimeState;

        /// <summary>채집 해석기 생성</summary>
        public HarvestResolver(
            SubmarineRuntimeState submarineRuntimeState,
            ClawRuntimeState clawRuntimeState,
            InventoryService inventoryService)
        {
            this.submarineRuntimeState = submarineRuntimeState; // 잠수함 상태 보관
            this.clawRuntimeState = clawRuntimeState; // 팔 상태 보관
            this.inventoryService = inventoryService; // 인벤토리 서비스 보관
        }

        /// <summary>현재 상태 기준 채집 예상 확률을 반환한다</summary>
        public float EvaluatePreviewChance(IHarvestTarget harvestTarget)
        {
            if (harvestTarget == null) return 0f;
            if (!harvestTarget.IsAvailable) return 0f;

            HarvestTargetSO targetData = harvestTarget.TargetData; // 대상 데이터
            if (targetData == null || !targetData.IsValid())
                return 0f;

            ItemSO itemData = targetData.ItemData; // 아이템 데이터
            if (itemData == null || !itemData.IsValid())
                return 0f;

            float totalDifficulty = Mathf.Clamp01(itemData.BaseCatchDifficulty + targetData.AdditionalDifficulty); // 총 난이도

            return clawRuntimeState.BaseStats.EvaluateCatchChance(
                itemData.Weight,
                totalDifficulty,
                submarineRuntimeState.CurrentBattery,
                submarineRuntimeState.BaseStats.MaxBattery,
                clawRuntimeState.CurrentDurability);
        }

        /// <summary>드롭 시도를 시작할 수 있으면 세션 상태와 배터리를 갱신한다</summary>
        public bool TryBeginAttempt(HarvestModeSession harvestModeSession)
        {
            if (harvestModeSession == null)
                return false;

            if (harvestModeSession.IsAttemptInProgress)
                return false;

            IHarvestTarget currentTarget = harvestModeSession.CurrentTarget; // 현재 대상
            if (currentTarget == null || !currentTarget.IsAvailable)
                return false;

            HarvestTargetSO targetData = currentTarget.TargetData; // 대상 데이터
            if (targetData == null || !targetData.IsValid())
                return false;

            ItemSO itemData = targetData.ItemData; // 아이템 데이터
            if (itemData == null || !itemData.IsValid())
                return false;

            float previewChance = EvaluatePreviewChance(currentTarget); // 현재 기준 사전 확률

            // 시도 시작 이벤트 발행
            EventBus.Publish(new HarvestAttemptStartedEvent(itemData.ItemId, previewChance));

            // 세션 시도 시작
            harvestModeSession.BeginAttempt(previewChance);

            // 배터리 소모
            submarineRuntimeState.ConsumeBattery(clawRuntimeState.BaseStats.BatteryCostPerDrop);

            // 방전 시 즉시 실패 처리
            if (submarineRuntimeState.CurrentBattery <= 0f)
            {
                EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(targetData.TargetId));
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, 0f, false));

                harvestModeSession.CompleteAttempt();
                return false;
            }

            return true;
        }

        /// <summary>복귀 완료 후 실제 잡기 결과를 해석한다</summary>
        public HarvestResolveResult ResolveReturnedCatch(HarvestModeSession harvestModeSession)
        {
            if (harvestModeSession == null)
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            IHarvestTarget currentTarget = harvestModeSession.CurrentTarget; // 세션 대상
            if (currentTarget == null || !currentTarget.IsAvailable)
            {
                harvestModeSession.CompleteAttempt();
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);
            }

            HarvestTargetSO currentTargetData = currentTarget.TargetData; // 대상 데이터
            if (currentTargetData == null || !currentTargetData.IsValid())
            {
                harvestModeSession.CompleteAttempt();
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);
            }

            ItemSO currentItemData = currentTargetData.ItemData; // 아이템 데이터
            string itemId = currentItemData != null ? currentItemData.ItemId : string.Empty; // 기본 아이템 ID

            // 실제로 아무것도 못 걸었으면 즉시 실패
            if (!harvestModeSession.HasLatchedTarget)
            {
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemId, false, 0f, false));

                harvestModeSession.CompleteAttempt();
                return new HarvestResolveResult(itemId, false, 0f, false, false);
            }

            IHarvestTarget latchedTarget = harvestModeSession.LatchedTarget; // 실제 포획 대상
            HarvestTargetSO latchedTargetData = latchedTarget.TargetData; // 실제 포획 대상 데이터
            if (latchedTargetData == null || !latchedTargetData.IsValid())
            {
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemId, false, 0f, false));

                harvestModeSession.CompleteAttempt();
                return new HarvestResolveResult(itemId, false, 0f, false, false);
            }

            ItemSO latchedItemData = latchedTargetData.ItemData; // 실제 포획 아이템 데이터
            if (latchedItemData == null || !latchedItemData.IsValid())
            {
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemId, false, 0f, false));

                harvestModeSession.CompleteAttempt();
                return new HarvestResolveResult(itemId, false, 0f, false, false);
            }

            itemId = latchedItemData.ItemId; // 실제 포획한 아이템 ID 사용

            // 조작 정확도에 따라 최종 확률 보정
            float finalChance = CalculateFinalChance(
                harvestModeSession.CachedPreviewChance,
                harvestModeSession.LatchQuality01);

            float roll = Random.value; // 0~1 랜덤값
            bool chanceSuccess = roll <= finalChance; // 최종 성공 여부

            if (!chanceSuccess)
            {
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemId, false, finalChance, false));

                harvestModeSession.CompleteAttempt();
                return new HarvestResolveResult(itemId, false, finalChance, false, false);
            }

            bool addedToInventory = inventoryService.TryAddItem(latchedItemData, out InventoryItemInstance itemInstance); // 인벤토리 적재 시도
            bool finalSuccess = addedToInventory; // 최종 성공은 적재 완료 기준

            if (finalSuccess && latchedTargetData.ConsumeOnSuccess)
                latchedTarget.Consume(); // 성공 시 대상 소비

            EventBus.Publish(new HarvestAttemptResolvedEvent(itemId, finalSuccess, finalChance, addedToInventory));

            harvestModeSession.CompleteAttempt();
            return new HarvestResolveResult(itemId, finalSuccess, finalChance, addedToInventory, false);
        }

        /// <summary>조작 정확도를 반영해 최종 성공 확률을 계산한다</summary>
        private float CalculateFinalChance(float previewChance, float latchQuality01)
        {
            // 조작 정확도에 따라 최대 20% 감점 / 25% 가점
            float qualityMultiplier = Mathf.Lerp(0.8f, 1.25f, Mathf.Clamp01(latchQuality01));

            // 최종 확률 보정
            return Mathf.Clamp01(previewChance * qualityMultiplier);
        }

        /// <summary>장애물 충돌 시 팔 내구도 감소를 처리한다</summary>
        public void ApplyObstacleCollision(IHarvestTarget harvestTarget)
        {
            if (harvestTarget == null) return;

            HarvestTargetSO targetData = harvestTarget.TargetData; // 대상 데이터
            if (targetData == null) return;

            clawRuntimeState.Damage(targetData.CollisionDamage); // 내구도 감소
            harvestTarget.OnClawCollision(); // 반응 호출
        }
    }
}
