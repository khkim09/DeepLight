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
        public readonly string TargetKey; // 채집 대상 런타임 고유 키
        public readonly string ItemId; // 대상 아이템 ID

        /// <summary>채집 세션 시작 정보 생성</summary>
        public HarvestSessionStartedEvent(string targetKey, string itemId)
        {
            TargetKey = targetKey;
            ItemId = itemId;
        }
    }

    /// <summary>채집 세션 종료 이벤트</summary>
    public readonly struct HarvestSessionEndedEvent : IEvent
    {
        public readonly string TargetKey; // 채집 대상 런타임 고유 키

        /// <summary>채집 세션 종료 정보 생성</summary>
        public HarvestSessionEndedEvent(string targetKey)
        {
            TargetKey = targetKey;
        }
    }

    /// <summary>배터리 방전으로 인한 채집 세션 강제 종료 이벤트</summary>
    public readonly struct HarvestSessionForcedEndedByBatteryEvent : IEvent
    {
        public readonly string TargetKey; // 채집 대상 런타임 고유 키

        /// <summary>채집 세션 강제 종료 정보 생성</summary>
        public HarvestSessionForcedEndedByBatteryEvent(string targetKey)
        {
            TargetKey = targetKey;
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

        public float EstimatedRecoveryChance => RecoveryChance; // HUD 호환용 별칭
        public float EstimatedBatteryCost => BatteryCost; // HUD 호환용 별칭
        public float EstimatedDurabilityCost => DurabilityCost; // HUD 호환용 별칭

        /// <summary>추정치 갱신 정보 생성</summary>
        public HarvestRecoveryPreviewUpdatedEvent(float recoveryChance, float batteryCost, float durabilityCost)
        {
            RecoveryChance = recoveryChance;
            BatteryCost = batteryCost;
            DurabilityCost = durabilityCost;
        }
    }

    /// <summary>고정 패널용 회수 계획 상태 갱신 이벤트</summary>
    public readonly struct HarvestRecoveryPlanMetricsUpdatedEvent : IEvent
    {
        public readonly string TargetKey; // 현재 타깃 런타임 고유 키
        public readonly int RevealedPointCount; // 공개 개수
        public readonly int TotalPointCount; // 총 개수
        public readonly int SelectedPointCount; // 현재 선택 개수
        public readonly int RecommendedPointCount; // 권장 개수
        public readonly float FirstAnchorScore01; // 현재 1번 포인트 앵커 점수
        public readonly float SequenceScore01; // 현재 순서 균형 점수
        public readonly float FinalChance01; // 예상 성공률

        /// <summary>회수 계획 상태 정보 생성</summary>
        public HarvestRecoveryPlanMetricsUpdatedEvent(
            string targetKey,
            int revealedPointCount,
            int totalPointCount,
            int selectedPointCount,
            int recommendedPointCount,
            float firstAnchorScore01,
            float sequenceScore01,
            float finalChance01)
        {
            TargetKey = targetKey;
            RevealedPointCount = revealedPointCount;
            TotalPointCount = totalPointCount;
            SelectedPointCount = selectedPointCount;
            RecommendedPointCount = recommendedPointCount;
            FirstAnchorScore01 = Mathf.Clamp01(firstAnchorScore01);
            SequenceScore01 = Mathf.Clamp01(sequenceScore01);
            FinalChance01 = Mathf.Clamp01(finalChance01);
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

        public readonly float BaseStability; // 구조 안정성
        public readonly float FirstAnchorBias; // 첫 앵커 적합도
        public readonly float SequenceBias; // 후속 순서 적합도
        public readonly float RiskWeight; // 위험도

        public readonly Vector2 ScreenPosition; // 향후 확장용 스크린 좌표

        /// <summary>hover 포인트 정보 생성</summary>
        public HarvestHoveredPointChangedEvent(
            bool hasPoint,
            string pointId,
            string displayLabel,
            int assignedOrder,
            float baseStability,
            float firstAnchorBias,
            float sequenceBias,
            float riskWeight,
            Vector2 screenPosition)
        {
            HasPoint = hasPoint;
            PointId = pointId;
            DisplayLabel = displayLabel;
            AssignedOrder = assignedOrder;
            BaseStability = Mathf.Clamp01(baseStability);
            FirstAnchorBias = Mathf.Clamp01(firstAnchorBias);
            SequenceBias = Mathf.Clamp01(sequenceBias);
            RiskWeight = Mathf.Clamp01(riskWeight);
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

    /// <summary>채집 대상에 포커스가 맞춰져 HUD에 상호작용 아이콘을 띄우는 이벤트</summary>
    public readonly struct HarvestTargetFocusedEvent : IEvent
    {
        public readonly string DisplayName; // 타깃 표시 이름
        public readonly KeyCode InteractKey; // 상호작용 키
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

    /// <summary>상호작용 컨테이너에 표시할 프롬프트 타입 변경 이벤트</summary>
    public readonly struct InteractionPromptChangedEvent : IEvent
    {
        public readonly int PromptType; // 표시할 프롬프트 타입 값
        public readonly KeyCode InteractKey; // 표시할 키

        /// <summary>프롬프트 변경 정보 생성</summary>
        public InteractionPromptChangedEvent(int promptType, KeyCode interactKey)
        {
            PromptType = promptType;
            InteractKey = interactKey;
        }
    }

    /// <summary>상호작용 컨테이너를 숨겨야 할 때 발생하는 이벤트</summary>
    public readonly struct InteractionPromptClearedEvent : IEvent
    {
    }

    #endregion

    #region Inventory

    /// <summary>인벤토리 UI 열림/닫힘 상태 변경 이벤트</summary>
    public readonly struct InventoryUIToggledEvent : IEvent
    {
        public readonly bool IsOpen; // 열림 여부

        /// <summary>인벤토리 토글 정보 생성</summary>
        public InventoryUIToggledEvent(bool isOpen)
        {
            IsOpen = isOpen;
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

    /// <summary>아이템 배치 최종 확정 이벤트</summary>
    public readonly struct InventoryItemPlacementConfirmedEvent : IEvent
    {
        public readonly string ItemId; // 배치 완료된 아이템 ID
        public readonly bool WasFreshRecovery; // 채집 직후 fresh recovery grab 여부

        /// <summary>아이템 배치 확정 정보 생성</summary>
        public InventoryItemPlacementConfirmedEvent(string itemId, bool wasFreshRecovery)
        {
            ItemId = itemId;
            WasFreshRecovery = wasFreshRecovery;
        }
    }

    /// <summary>그랩 중 아이템 폐기 이벤트</summary>
    public readonly struct InventoryItemDiscardedEvent : IEvent
    {
        public readonly string ItemId; // 폐기된 아이템 ID
        public readonly bool WasFreshRecovery; // 채집 직후 fresh recovery grab 여부

        /// <summary>아이템 폐기 정보 생성</summary>
        public InventoryItemDiscardedEvent(string itemId, bool wasFreshRecovery)
        {
            ItemId = itemId;
            WasFreshRecovery = wasFreshRecovery;
        }
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

    /// <summary>배터리 위험 피드백 이벤트</summary>
    public readonly struct BatteryDangerFeedbackEvent : IEvent
    {
        public readonly float DamageAmount; // 이번에 잃은 배터리량
        public readonly float CurrentBattery; // 현재 배터리
        public readonly float MaxBattery; // 최대 배터리
        public readonly float IntensityMultiplier; // 추가 강도 배율

        /// <summary>배터리 위험 피드백 정보를 생성한다.</summary>
        public BatteryDangerFeedbackEvent(
            float damageAmount,
            float currentBattery,
            float maxBattery,
            float intensityMultiplier)
        {
            DamageAmount = damageAmount;
            CurrentBattery = currentBattery;
            MaxBattery = maxBattery;
            IntensityMultiplier = intensityMultiplier;
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

    #region Time

    /// <summary>현재 하루 표시 방식이다.</summary>
    public enum GameDayLengthMode
    {
        TwelveHour = 0,      // 초반 가짜 하루: 12시간
        TwentyFourHour = 1   // 진실 이후 실제 하루: 24시간
    }

    /// <summary>게임 시간 상태 변경 이벤트</summary>
    public readonly struct GameTimeChangedEvent : IEvent
    {
        public readonly int Day; // 현재 Day
        public readonly float HourOfDay; // 현재 하루 내부 시각
        public readonly float DayLengthHours; // 현재 하루 길이
        public readonly bool IsDaylight; // 현재 낮 구간 여부
        public readonly bool HasPendingDayLengthSwitch; // 다음 경계에서 24시간제로 전환 예약 여부

        /// <summary>게임 시간 상태 정보를 생성한다.</summary>
        public GameTimeChangedEvent(
            int day,
            float hourOfDay,
            float dayLengthHours,
            bool isDaylight,
            bool hasPendingDayLengthSwitch)
        {
            Day = day;
            HourOfDay = hourOfDay;
            DayLengthHours = dayLengthHours;
            IsDaylight = isDaylight;
            HasPendingDayLengthSwitch = hasPendingDayLengthSwitch;
        }
    }

    /// <summary>하루 길이 표시 방식 전환 이벤트</summary>
    public readonly struct GameDayLengthModeChangedEvent : IEvent
    {
        public readonly GameDayLengthMode PreviousMode;
        public readonly GameDayLengthMode CurrentMode;

        /// <summary>하루 길이 표시 방식 전환 정보를 생성한다.</summary>
        public GameDayLengthModeChangedEvent(GameDayLengthMode previousMode, GameDayLengthMode currentMode)
        {
            PreviousMode = previousMode;
            CurrentMode = currentMode;
        }
    }

    #endregion

    #region Navigation

    /// <summary>탐사 상단 방향 HUD용 현재 헤딩 정보 이벤트</summary>
    public readonly struct ExplorationHeadingChangedEvent : IEvent
    {
        public readonly float HeadingDegrees; // 북(0) 기준 시계방향 각도
        public readonly float HeadingNormalized01; // 0~1 정규화 값
        public readonly string MajorCardinal; // 가장 가까운 주방위 문자열

        /// <summary>헤딩 변경 정보를 생성한다.</summary>
        public ExplorationHeadingChangedEvent(float headingDegrees, float headingNormalized01, string majorCardinal)
        {
            HeadingDegrees = headingDegrees;
            HeadingNormalized01 = headingNormalized01;
            MajorCardinal = majorCardinal;
        }
    }

    #endregion
}
