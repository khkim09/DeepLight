namespace Project.Core.Events
{
    /// <summary>게임 모드 변경 이벤트</summary>
    public readonly struct GameModeChangedEvent : IEvent
    {
        public readonly GameModeType PreviousMode; // 이전 모드
        public readonly GameModeType CurrentMode; // 현재 모드

        /// <summary>게임 모드 변경 정보 생성</summary>
        public GameModeChangedEvent(GameModeType previousMode, GameModeType currentMode)
        {
            PreviousMode = previousMode;
            CurrentMode = currentMode;
        }
    }

    /// <summary>채집 모드 진입 이벤트</summary>
    public readonly struct HarvestModeEnteredEvent : IEvent
    {
    }

    /// <summary>채집 모드 종료 이벤트</summary>
    public readonly struct HarvestModeExitedEvent : IEvent
    {
    }

    /// <summary>로봇 팔 투하 시작 이벤트</summary>
    public readonly struct ClawDropStartedEvent : IEvent
    {
    }

    /// <summary>로봇 팔 집기 결과 이벤트</summary>
    public readonly struct ClawCatchResolvedEvent : IEvent
    {
        public readonly bool IsSuccess; // 채집 성공 여부
        public readonly string ItemId; // 대상 아이템 ID
        public readonly float SuccessChance; // 최종 성공 확률

        /// <summary>로봇 팔 집기 결과 생성</summary>
        public ClawCatchResolvedEvent(bool isSuccess, string itemId, float successChance)
        {
            IsSuccess = isSuccess;
            ItemId = itemId;
            SuccessChance = successChance;
        }
    }

    /// <summary>인벤토리 아이템 추가 이벤트</summary>
    public readonly struct InventoryItemAddedEvent : IEvent
    {
        public readonly string ItemId; // 추가된 아이템 ID
        public readonly int Amount; // 추가 수량

        /// <summary>인벤토리 아이템 추가 정보 생성</summary>
        public InventoryItemAddedEvent(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }

    /// <summary>인벤토리 아이템 제거 이벤트</summary>
    public readonly struct InventoryItemRemovedEvent : IEvent
    {
        public readonly string ItemId; // 제거된 아이템 ID
        public readonly int Amount; // 제거 수량

        /// <summary>인벤토리 아이템 제거 정보 생성</summary>
        public InventoryItemRemovedEvent(string itemId, int amount)
        {
            ItemId = itemId;
            Amount = amount;
        }
    }

    /// <summary>인벤토리 전체 변경 이벤트</summary>
    public readonly struct InventoryChangedEvent : IEvent
    {
    }

    /// <summary>배터리 상태 변경 이벤트</summary>
    public readonly struct BatteryChangedEvent : IEvent
    {
        public readonly float CurrentBattery; // 현재 배터리
        public readonly float MaxBattery; // 최대 배터리

        /// <summary>배터리 상태 변경 정보 생성</summary>
        public BatteryChangedEvent(float currentBattery, float maxBattery)
        {
            CurrentBattery = currentBattery;
            MaxBattery = maxBattery;
        }
    }

    /// <summary>내구도 상태 변경 이벤트</summary>
    public readonly struct DurabilityChangedEvent : IEvent
    {
        public readonly float CurrentDurability; // 현재 내구도
        public readonly float MaxDurability; // 최대 내구도

        /// <summary>내구도 상태 변경 정보 생성</summary>
        public DurabilityChangedEvent(float currentDurability, float maxDurability)
        {
            CurrentDurability = currentDurability;
            MaxDurability = maxDurability;
        }
    }

    /// <summary>업그레이드 구매 이벤트</summary>
    public readonly struct UpgradePurchasedEvent : IEvent
    {
        public readonly string UpgradeId; // 구매한 업그레이드 ID

        /// <summary>업그레이드 구매 정보 생성</summary>
        public UpgradePurchasedEvent(string upgradeId)
        {
            UpgradeId = upgradeId;
        }
    }
}
