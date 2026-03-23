namespace Project.Core.Events
{
    #region Mode

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

    #endregion

    #region HarvestSession

    /// <summary>채집 세션 시작 이벤트</summary>
    public readonly struct HarvestSessionStartedEvent : IEvent
    {
        public readonly string TargetId; // 채집 대상 ID
        public readonly string ItemId; // 대상 아이템 ID

        /// <summary>채집 세션 시작 정보 생성</summary>
        public HarvestSessionStartedEvent(string targetId, string itemId)
        {
            TargetId = targetId;
            ItemId = itemId;
        }
    }

    /// <summary>채집 세션 종료 이벤트</summary>
    public readonly struct HarvestSessionEndedEvent : IEvent
    {
        public readonly string TargetId; // 채집 대상 ID

        /// <summary>채집 세션 종료 정보 생성</summary>
        public HarvestSessionEndedEvent(string targetId)
        {
            TargetId = targetId;
        }
    }

    /// <summary>배터리 방전으로 인한 채집 세션 강제 종료 이벤트</summary>
    public readonly struct HarvestSessionForcedEndedByBatteryEvent : IEvent
    {
        public readonly string TargetId; // 채집 대상 ID

        /// <summary>채집 세션 강제 종료 정보 생성</summary>
        public HarvestSessionForcedEndedByBatteryEvent(string targetId)
        {
            TargetId = targetId;
        }
    }

    #endregion

    #region HarvestAttempt

    /// <summary>채집 시도 시작 이벤트</summary>
    public readonly struct HarvestAttemptStartedEvent : IEvent
    {
        public readonly string ItemId; // 대상 아이템 ID
        public readonly float PreviewChance; // 사전 표시 확률

        /// <summary>채집 시도 시작 정보 생성</summary>
        public HarvestAttemptStartedEvent(string itemId, float previewChance)
        {
            ItemId = itemId;
            PreviewChance = previewChance;
        }
    }

    /// <summary>채집 시도 완료 이벤트</summary>
    public readonly struct HarvestAttemptResolvedEvent : IEvent
    {
        public readonly string ItemId; // 대상 아이템 ID
        public readonly bool IsSuccess; // 성공 여부
        public readonly float FinalChance; // 최종 성공 확률
        public readonly bool AddedToInventory; // 인벤토리 적재 성공 여부

        /// <summary>채집 시도 완료 정보 생성</summary>
        public HarvestAttemptResolvedEvent(string itemId, bool isSuccess, float finalChance, bool addedToInventory)
        {
            ItemId = itemId;
            IsSuccess = isSuccess;
            FinalChance = finalChance;
            AddedToInventory = addedToInventory;
        }
    }

    #endregion

    #region Claw

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

    #endregion

    #region Inventory

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

    #endregion

    #region Runtime

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

    /// <summary>선체 내구도 상태 변경 이벤트</summary>
    public readonly struct HullDurabilityChangedEvent : IEvent
    {
        public readonly float CurrentDurability; // 현재 선체 내구도
        public readonly float MaxDurability; // 최대 선체 내구도

        /// <summary>선체 내구도 변경 정보 생성</summary>
        public HullDurabilityChangedEvent(float currentDurability, float maxDurability)
        {
            CurrentDurability = currentDurability;
            MaxDurability = maxDurability;
        }
    }

    /// <summary>로봇 팔 내구도 상태 변경 이벤트</summary>
    public readonly struct ClawDurabilityChangedEvent : IEvent
    {
        public readonly float CurrentDurability; // 현재 팔 내구도
        public readonly float MaxDurability; // 최대 팔 내구도

        /// <summary>로봇 팔 내구도 변경 정보 생성</summary>
        public ClawDurabilityChangedEvent(float currentDurability, float maxDurability)
        {
            CurrentDurability = currentDurability;
            MaxDurability = maxDurability;
        }
    }

    #endregion

    #region Progression

    /// <summary>업그레이드 구매 이벤트</summary>
    public readonly struct UpgradePurchasedEvent : IEvent
    {
        public readonly string UpgradeId; // 업그레이드 ID

        /// <summary>업그레이드 구매 정보 생성</summary>
        public UpgradePurchasedEvent(string upgradeId)
        {
            UpgradeId = upgradeId;
        }
    }

    #endregion
}
