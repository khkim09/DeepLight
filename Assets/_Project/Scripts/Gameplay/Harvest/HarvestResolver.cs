using Project.Core.Events;
using Project.Data.Harvest;
using Project.Data.Items;
using Project.Gameplay.Inventory;
using Project.Gameplay.Runtime;
using Project.Gameplay.Services;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Project.Gameplay.Harvest
{
    /// <summary>채집 확률 계산과 결과 반영</summary>
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

        /// <summary>현재 상태 기준 채집 예상 확률 반환</summary>
        public float EvaluatePreviewChance(IHarvestTarget harvestTarget)
        {
            // 대상 유효성 검사
            if (harvestTarget == null) return 0f;
            // 대상 사용 가능 여부 검사
            if (!harvestTarget.IsAvailable) return 0f;

            HarvestTargetSO targetData = harvestTarget.TargetData; // 대상 데이터 참조
            if (targetData == null || !targetData.IsValid())
                return 0f;

            ItemSO itemData = targetData.ItemData; // 아이템 데이터 참조
            float totalDifficulty = Mathf.Clamp01(itemData.BaseCatchDifficulty + targetData.AdditionalDifficulty); // 총 난이도 계산

            // 현재 상태 기준 확률 계산
            return clawRuntimeState.BaseStats.EvaluateCatchChance(
                itemData.Weight,
                totalDifficulty,
                submarineRuntimeState.CurrentBattery,
                submarineRuntimeState.BaseStats.MaxBattery,
                clawRuntimeState.CurrentDurability);
        }

        /// <summary>채집 시도를 수행하고 결과를 반환</summary>
        public HarvestResolveResult Resolve(IHarvestTarget harvestTarget)
        {
            // 대상 유효성 검사
            if (harvestTarget == null)
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            // 대상 사용 가능 여부 검사
            if (!harvestTarget.IsAvailable)
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            HarvestTargetSO targetData = harvestTarget.TargetData; // 대상 데이터 참조
            if (targetData == null || !targetData.IsValid())
                return new HarvestResolveResult(string.Empty, false, 0f, false, false);

            ItemSO itemData = targetData.ItemData; // 아이템 데이터 참조
            float previewChance = EvaluatePreviewChance(harvestTarget); // 사전 확률 계산

            // 채집 시작 이벤트 발행
            EventBus.Publish(new HarvestAttemptStartedEvent(itemData.ItemId, previewChance));

            // 배터리 소모 적용
            submarineRuntimeState.ConsumeBattery(clawRuntimeState.BaseStats.BatteryCostPerDrop);

            // 방전 시 강제 종료 처리
            if (submarineRuntimeState.CurrentBattery <= 0f)
            {
                string targetId = targetData.TargetId; // 현재 대상
                EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(targetId)); // 강제 종료
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, previewChance, false));

                return new HarvestResolveResult(itemData.ItemId, false, previewChance, false, true);
            }

            // 확률 판정 수행
            float roll = Random.value; // 0~1 랜덤값
            bool isSuccess = roll <= previewChance; // 성공 여부 판정

            // 실패면 바로 결과 반환
            if (!isSuccess)
            {
                EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, false, previewChance, false));
                return new HarvestResolveResult(itemData.ItemId, false, previewChance, false, false);
            }

            // 성공 시 인벤토리 자동 적재 시도
            bool addedToInventory = inventoryService.TryAddItem(itemData, out InventoryItemInstance itemInstance);

            // 적재 성공 시 대상 소비
            if (addedToInventory)
                harvestTarget.Consume();

            // 결과 이벤트 발행
            EventBus.Publish(new HarvestAttemptResolvedEvent(itemData.ItemId, true, previewChance, addedToInventory));

            return new HarvestResolveResult(itemData.ItemId, true, previewChance, addedToInventory, false);
        }

        /// <summary>장애물 충돌 시 팔 내구도 감소 처리</summary>
        public void ApplyObstacleCollision(IHarvestTarget harvestTarget)
        {
            // 대상 유효성 검사
            if (harvestTarget == null) return;

            HarvestTargetSO targetData = harvestTarget.TargetData; // 대상 데이터 참조
            if (targetData == null) return;

            // 내구도 피해 적용
            clawRuntimeState.Damage(targetData.CollisionDamage);

            // 대상 충돌 반응 호출
            harvestTarget.OnClawCollision();
        }
    }
}
