using UnityEngine;

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
        public readonly Transform Target; // 상호작용(채집) 대상의 Transform

        /// <summary>채집 모드 진입 이벤트 정보 생성</summary>
        public HarvestModeEnteredEvent(Transform target)
        {
            Target = target;
        }
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

    /// <summary>회수 콘솔 센서 모드 변경 이벤트</summary>
    public readonly struct HarvestScanModeChangedEvent : IEvent
    {
        public readonly int ScanMode; // HarvestScanMode int 값

        /// <summary>센서 모드 변경 정보 생성</summary>
        public HarvestScanModeChangedEvent(int scanMode)
        {
            ScanMode = scanMode;
        }
    }

    /// <summary>스캔 펄스 사용 이벤트</summary>
    public readonly struct HarvestScanPulseEvent : IEvent
    {
        public readonly int ScanMode; // HarvestScanMode int 값
        public readonly int PulseCount; // 누적 스캔 횟수

        /// <summary>스캔 펄스 정보 생성</summary>
        public HarvestScanPulseEvent(int scanMode, int pulseCount)
        {
            ScanMode = scanMode;
            PulseCount = pulseCount;
        }
    }

    /// <summary>회수 포인트 공개 이벤트</summary>
    public readonly struct HarvestPointRevealedEvent : IEvent
    {
        public readonly string PointId; // 포인트 ID

        /// <summary>포인트 공개 정보 생성</summary>
        public HarvestPointRevealedEvent(string pointId)
        {
            PointId = pointId;
        }
    }

    /// <summary>회수 포인트 선택 이벤트</summary>
    public readonly struct HarvestPointSelectedEvent : IEvent
    {
        public readonly string PointId; // 포인트 ID
        public readonly int SequenceIndex; // 선택 순서

        /// <summary>포인트 선택 정보 생성</summary>
        public HarvestPointSelectedEvent(string pointId, int sequenceIndex)
        {
            PointId = pointId;
            SequenceIndex = sequenceIndex;
        }
    }

    /// <summary>회수 추정치 갱신 이벤트</summary>
    public readonly struct HarvestRecoveryPreviewUpdatedEvent : IEvent
    {
        public readonly float RecoveryChance; // 추정 성공률
        public readonly float BatteryCost; // 추정 배터리 소모량
        public readonly float DurabilityCost; // 추정 내구도 소모량

        // HUD 호환용 별칭
        public float EstimatedRecoveryChance => RecoveryChance; // 추정 성공률 별칭
        public float EstimatedBatteryCost => BatteryCost; // 추정 배터리 비용 별칭
        public float EstimatedDurabilityCost => DurabilityCost; // 추정 내구도 비용 별칭

        /// <summary>추정치 갱신 정보 생성</summary>
        public HarvestRecoveryPreviewUpdatedEvent(float recoveryChance, float batteryCost, float durabilityCost)
        {
            RecoveryChance = recoveryChance;
            BatteryCost = batteryCost;
            DurabilityCost = durabilityCost;
        }
    }

    /// <summary>회수 계획 확정 이벤트</summary>
    public readonly struct HarvestRecoveryCommittedEvent : IEvent
    {
    }

    /// <summary>회수 콘솔 결과 이벤트</summary>
    public readonly struct HarvestRecoveryResolvedEvent : IEvent
    {
        public readonly string ItemId; // 대상 아이템 ID
        public readonly bool IsSuccess; // 성공 여부
        public readonly float FinalChance; // 최종 성공률
        public readonly bool AddedToInventory; // 인벤토리 적재 여부

        /// <summary>회수 결과 정보 생성</summary>
        public HarvestRecoveryResolvedEvent(string itemId, bool isSuccess, float finalChance, bool addedToInventory)
        {
            ItemId = itemId;
            IsSuccess = isSuccess;
            FinalChance = finalChance;
            AddedToInventory = addedToInventory;
        }
    }

    /// <summary>현재 회수 콘솔 타깃 표시 정보 이벤트</summary>
    public readonly struct HarvestConsoleTargetPreparedEvent : IEvent
    {
        public readonly string DisplayName; // 현재 표시 이름
        public readonly int TotalPointCount; // 총 포인트 수

        /// <summary>회수 콘솔 타깃 표시 정보 생성</summary>
        public HarvestConsoleTargetPreparedEvent(string displayName, int totalPointCount)
        {
            DisplayName = displayName;
            TotalPointCount = totalPointCount;
        }
    }

    /// <summary>현재 선택 순서 개수 변경 이벤트</summary>
    public readonly struct HarvestSelectionSequenceChangedEvent : IEvent
    {
        public readonly int SelectedCount; // 현재 선택된 개수
        public readonly int TotalCount; // 총 개수

        /// <summary>선택 순서 개수 정보 생성</summary>
        public HarvestSelectionSequenceChangedEvent(int selectedCount, int totalCount)
        {
            SelectedCount = selectedCount;
            TotalCount = totalCount;
        }
    }

    /// <summary>현재 hover 중인 회수 포인트 정보 이벤트</summary>
    public readonly struct HarvestHoveredPointChangedEvent : IEvent
    {
        public readonly bool HasPoint; // 현재 hover 포인트 존재 여부
        public readonly string PointId; // 포인트 ID
        public readonly string DisplayLabel; // 표시 라벨
        public readonly int AssignedOrder; // 할당 순번
        public readonly float SonarSignature; // 소나 반응도
        public readonly float LidarSignature; // 라이다 반응도
        public readonly Vector2 ScreenPosition; // 툴팁 배치용 스크린 좌표

        /// <summary>hover 포인트 정보 생성</summary>
        public HarvestHoveredPointChangedEvent(
            bool hasPoint,
            string pointId,
            string displayLabel,
            int assignedOrder,
            float sonarSignature,
            float lidarSignature,
            Vector2 screenPosition)
        {
            HasPoint = hasPoint;
            PointId = pointId;
            DisplayLabel = displayLabel;
            AssignedOrder = assignedOrder;
            SonarSignature = sonarSignature;
            LidarSignature = lidarSignature;
            ScreenPosition = screenPosition;
        }
    }

    /// <summary>Harvest 카메라 전환 완료 이벤트</summary>
    public readonly struct HarvestCameraTransitionCompletedEvent : IEvent
    {
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

    #region Interaction UI

    /// <summary>채집 대상에 포커스가 맞춰져 HUD에 상호작용 아이콘(!)을 띄우는 이벤트</summary>
    public readonly struct HarvestTargetFocusedEvent : IEvent
    {
        public readonly string DisplayName; // 타깃 표시 이름
        public readonly KeyCode InteractKey; // 상호작용 키 (F)
        public readonly bool IsAvailable; // 현재 채집 가능한 대상인지 여부

        /// <summary>포커스 이벤트 정보 생성</summary>
        public HarvestTargetFocusedEvent(string displayName, KeyCode interactKey, bool isAvailable)
        {
            DisplayName = displayName;
            InteractKey = interactKey;
            IsAvailable = isAvailable;
        }
    }

    /// <summary>채집 대상에서 포커스가 해제되어 아이콘과 툴팁을 숨기는 이벤트</summary>
    public readonly struct HarvestTargetUnfocusedEvent : IEvent
    {
    }

    /// <summary>상호작용이 불가능한 대상에게 키를 입력했을 때 툴팁을 띄우는 이벤트</summary>
    public readonly struct HarvestTargetInteractMessageEvent : IEvent
    {
        public readonly string Message; // 띄울 안내 메시지

        /// <summary>메시지 이벤트 정보 생성</summary>
        public HarvestTargetInteractMessageEvent(string message)
        {
            Message = message;
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
