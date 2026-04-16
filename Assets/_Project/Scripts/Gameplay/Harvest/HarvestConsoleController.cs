using System.Collections.Generic;
using System.Linq;
using Project.Core.Events;
using Project.Data.Enums;
using Project.Data.Harvest;
using Project.Data.Input;
using Project.Gameplay.GameModes;
using Project.Gameplay.Runtime;
using Project.Gameplay.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Gameplay.Harvest
{
    /// <summary>회수 콘솔 입력, 포인트 선택, 타깃 회전을 처리한다.</summary>
    public class HarvestConsoleController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private HarvestScanRevealTuningSO scanRevealTuning; // 공개/선택 반경 튜닝
        [SerializeField] private GameInputBindingsSO inputBindings; // 입력 바인딩 SO

        [Header("References")]
        [SerializeField] private Camera harvestConsoleCamera; // 회수 콘솔 카메라
        [SerializeField] private HarvestTargetRotationController targetRotationController; // 타깃 회전 제어기

        [Header("Rotation Input Guard")]
        [SerializeField] private bool blockInputWhenPointerOverUi = true; // UI 위 포인터일 때 입력 차단
        [SerializeField] private float validRotationHalfWidthPixels = 450f; // 화면 중심 기준 허용 x 반폭

        private HarvestModeSession harvestModeSession; // 현재 회수 세션
        private HarvestResolver harvestResolver; // 회수 계산기
        private HarvestModeCoordinator harvestModeCoordinator; // 모드 전환 조정기
        private readonly List<HarvestScanPoint> currentPoints = new(); // 현재 타깃 포인트 캐시

        private bool isHarvestMode; // 현재 Harvest 모드 여부
        private bool isDraggingTarget; // 현재 타깃 드래그 회전 여부
        private HarvestScanPoint hoveredPoint; // 현재 hover 중인 포인트
        private HarvestTargetBehaviour currentTargetBehaviour; // 현재 타깃 Behaviour
        private HarvestTargetScanStateService harvestTargetScanStateService; // 타깃별 공개 스캔 포인트 상태 서비스

        /// <summary>세션과 계산기, 조정기를 주입한다.</summary>
        public void Initialize(
            HarvestModeSession newHarvestModeSession,
            HarvestResolver newHarvestResolver,
            HarvestModeCoordinator newHarvestModeCoordinator,
            HarvestTargetScanStateService newHarvestTargetScanStateService)
        {
            harvestModeSession = newHarvestModeSession;
            harvestResolver = newHarvestResolver;
            harvestModeCoordinator = newHarvestModeCoordinator;
            harvestTargetScanStateService = newHarvestTargetScanStateService;
        }

        /// <summary>Harvest 이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Subscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);
        }

        /// <summary>Harvest 이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Unsubscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);
        }

        /// <summary>초기 탐사 커서 상태를 적용한다.</summary>
        private void Start()
        {
            ApplyExplorationCursorState();
        }

        /// <summary>회수 콘솔 모드 입력을 처리한다.</summary>
        private void Update()
        {
            if (!isHarvestMode)
                return;

            if (harvestModeSession == null || harvestResolver == null || harvestModeCoordinator == null)
                return;

            if (inputBindings == null || harvestConsoleCamera == null)
                return;

            if (!harvestModeSession.HasTarget)
                return;

            UpdateHoveredPoint();

            if (harvestModeSession.IsResolved)
            {
                if (Input.GetKeyDown(inputBindings.HarvestExitKey))
                    harvestModeCoordinator.ExitHarvestMode();

                return;
            }

            HandleSensorModeInput();
            HandleScanInput();
            HandleTargetRotationInput();
            HandlePointClickInput();
            HandleCommitAndExitInput();
            HandleResetInput();
        }

        /// <summary>회수 콘솔 진입 시 현재 타깃과 포인트 상태를 초기화한다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            if (harvestModeSession == null || harvestResolver == null || harvestModeCoordinator == null)
                return;

            // 현재 세션의 휘발성 상태만 초기화한다.
            // 단, 타깃 자체는 이미 coordinator에서 바인딩한 상태를 유지한다.
            harvestModeSession.ResetTransientState(clearTarget: false);
            harvestModeSession.SetScanMode(HarvestScanMode.Sonar);

            isHarvestMode = true;
            isDraggingTarget = false;
            hoveredPoint = null;
            currentTargetBehaviour = harvestModeSession.CurrentTarget as HarvestTargetBehaviour;

            ApplyHarvestCursorState();
            CacheCurrentTargetPoints();

            // 화면상 포인트는 일단 전부 초기화한 뒤,
            // 저장된 reveal 상태를 다시 복원한다.
            ResetCurrentPoints();

            // 현재 타깃 포인트 개수 기준으로 순서 슬롯 개수를 맞춘다.
            harvestModeSession.EnsureSequenceCapacity(currentPoints.Count);

            if (currentTargetBehaviour != null && targetRotationController != null)
            {
                targetRotationController.BindTarget(currentTargetBehaviour.GetRotationPivot());
                targetRotationController.ResetRotation();
            }

            harvestModeSession.SetScanMode(HarvestScanMode.Sonar);
            EventBus.Publish(new HarvestScanModeChangedEvent((int)HarvestScanMode.Sonar));

            // 타깃별로 이전에 공개했던 포인트를 복원한다.
            RestorePersistedRevealState();

            PublishTargetPrepared();
            PublishRuntimeState();
            PublishSelectionChanged();
            PublishHoveredPoint(null);

            // 0짜리 강제 초기 preview를 보내지 않고,
            // 현재 타깃 기준 실제 계산 결과만 바로 반영한다.
            RecalculatePreview();
        }

        /// <summary>Harvest 카메라 전환 완료 후 현재 타깃 기준 HUD를 다시 맞춘다.</summary>
        private void OnHarvestCameraTransitionCompleted(HarvestCameraTransitionCompletedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            if (harvestModeSession == null || harvestResolver == null)
                return;

            if (!harvestModeSession.HasTarget)
                return;

            // 카메라 전환 완료 시점에도 0 상태를 다시 밀어 넣지 않고,
            // 현재 타깃 기준 실제 회수 계획 값을 다시 계산해서 반영한다.
            PublishTargetPrepared();
            PublishSelectionChanged();
            PublishHoveredPoint(hoveredPoint);

            RecalculatePreview();
        }

        /// <summary>회수 콘솔 종료 시 현재 타깃 상태를 정리한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
            isDraggingTarget = false;

            ApplyExplorationCursorState();

            if (hoveredPoint != null)
            {
                hoveredPoint.SetHovered(false);
                hoveredPoint = null;
            }

            if (currentTargetBehaviour != null)
                currentTargetBehaviour.ResetScanPoints();

            if (harvestModeSession != null)
                harvestModeSession.ResetTransientState(clearTarget: true);

            currentPoints.Clear();

            if (targetRotationController != null)
            {
                targetRotationController.EndDrag();
                targetRotationController.UnbindTarget();
            }

            PublishHoveredPoint(null);
            currentTargetBehaviour = null;
        }

        /// <summary>센서 전환 입력을 처리한다.</summary>
        private void HandleSensorModeInput()
        {
            if (!Input.GetKeyDown(inputBindings.HarvestSwitchSensorKey))
                return;

            HarvestScanMode nextMode = harvestModeSession.CurrentScanMode == HarvestScanMode.Sonar
                ? HarvestScanMode.Lidar
                : HarvestScanMode.Sonar;

            harvestModeSession.SetScanMode(nextMode);
            EventBus.Publish(new HarvestScanModeChangedEvent((int)nextMode));

            RecalculatePreview();
        }

        /// <summary>스캔 입력을 처리한다.</summary>
        private void HandleScanInput()
        {
            if (!Input.GetKeyDown(inputBindings.HarvestScanKey))
                return;

            HarvestScanMode currentMode = harvestModeSession.CurrentScanMode;
            if (currentMode == HarvestScanMode.None)
                return;

            List<HarvestScanPoint> revealedPoints = RevealPointsByScan(currentMode);
            if (revealedPoints.Count <= 0)
                return;

            float batteryCost = harvestResolver.GetScanPulseBatteryCost(currentMode);
            harvestResolver.SubmarineRuntimeState.ConsumeBatteryOperational(batteryCost);

            if (harvestResolver.SubmarineRuntimeState.CurrentBattery <= 0f)
            {
                if (harvestModeSession.CurrentTarget?.TargetData != null)
                {
                    string targetKey = currentTargetBehaviour != null
                        ? currentTargetBehaviour.GetRuntimeTargetKey()
                        : string.Empty;

                    EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(targetKey));
                }

                harvestModeCoordinator.ExitHarvestMode();
                return;
            }

            harvestModeSession.AddScanPulse();
            EventBus.Publish(new HarvestScanPulseEvent((int)currentMode, harvestModeSession.ScanPulseCount));

            for (int i = 0; i < revealedPoints.Count; i++)
            {
                HarvestScanPoint point = revealedPoints[i];
                if (point == null)
                    continue;

                // 현재 세션에도 반영
                harvestModeSession.AddRevealedPoint(point.PointId);

                // 타깃 영속 상태에도 반영
                if (currentTargetBehaviour != null)
                    harvestTargetScanStateService?.MarkPointRevealed(currentTargetBehaviour, point.PointId);

                EventBus.Publish(new HarvestPointRevealedEvent(point.PointId));
            }

            PublishRuntimeState();
            RecalculatePreview();
        }

        /// <summary>타깃 회전 입력을 처리한다.</summary>
        private void HandleTargetRotationInput()
        {
            if (targetRotationController == null)
                return;

            if (Input.GetMouseButtonDown(0))
            {
                if (CanStartTargetRotationDrag(Input.mousePosition))
                {
                    isDraggingTarget = true;
                    targetRotationController.BeginDrag(Input.mousePosition);
                }
            }

            if (Input.GetMouseButton(0) && isDraggingTarget)
            {
                if (CanContinueTargetRotationDrag(Input.mousePosition))
                    targetRotationController.RotateByDrag(Input.mousePosition);
            }

            if (Input.GetMouseButtonUp(0) && isDraggingTarget)
            {
                isDraggingTarget = false;
                targetRotationController.EndDrag();
            }
        }

        /// <summary>포인트 선택과 제거 입력을 처리한다.</summary>
        private void HandlePointClickInput()
        {
            if (IsPointerBlockedByUi())
                return;

            if (Input.GetMouseButtonDown(0))
                TrySelectHoveredPoint();

            if (Input.GetMouseButtonDown(1))
                TryRemoveHoveredPoint();
        }

        /// <summary>회수 확정과 종료 입력을 처리한다.</summary>
        private void HandleCommitAndExitInput()
        {
            if (Input.GetKeyDown(inputBindings.HarvestCommitKey))
            {
                CommitRecovery();
                return;
            }

            if (Input.GetKeyDown(inputBindings.HarvestExitKey))
                harvestModeCoordinator.ExitHarvestMode();
        }

        /// <summary>순서 리셋 입력을 처리한다.</summary>
        private void HandleResetInput()
        {
            if (!Input.GetKeyDown(inputBindings.HarvestResetSequenceKey))
                return;

            ResetSelectionSequence();
        }

        /// <summary>현재 마우스 위치 기준으로 hover 포인트를 갱신한다.</summary>
        private void UpdateHoveredPoint()
        {
            if (IsPointerBlockedByUi())
            {
                ClearHoveredPoint();
                return;
            }

            HarvestScanPoint bestPoint = null;
            float bestDistance = float.MaxValue;
            Vector2 mousePosition = Input.mousePosition;

            for (int i = 0; i < currentPoints.Count; i++)
            {
                HarvestScanPoint point = currentPoints[i];
                if (point == null || !point.IsRevealed)
                    continue;

                Vector3 screenPosition = harvestConsoleCamera.WorldToScreenPoint(point.transform.position);
                if (screenPosition.z <= 0f)
                    continue;

                float distance = Vector2.Distance(mousePosition, screenPosition);
                if (scanRevealTuning != null && distance > scanRevealTuning.PointHoverRadiusPixels)
                    continue;

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestPoint = point;
            }

            // 핵심:
            // 같은 포인트를 계속 hover 중이면 이벤트를 다시 보내지 않는다.
            // static radar rebuild, 값 텍스트 갱신, 레이아웃 재계산을 매 프레임 막기 위함이다.
            if (hoveredPoint == bestPoint)
                return;

            if (hoveredPoint != null)
                hoveredPoint.SetHovered(false);

            hoveredPoint = bestPoint;

            if (hoveredPoint != null)
                hoveredPoint.SetHovered(true);

            PublishHoveredPoint(hoveredPoint);
        }

        /// <summary>현재 hover 포인트를 가장 앞의 빈 순번에 배치한다.</summary>
        private void TrySelectHoveredPoint()
        {
            if (hoveredPoint == null)
                return;

            if (!hoveredPoint.IsRevealed)
                return;

            if (harvestModeSession.ContainsSelectedPoint(hoveredPoint.PointId))
                return;

            Vector3 screenPosition = harvestConsoleCamera.WorldToScreenPoint(hoveredPoint.transform.position);
            float distance = Vector2.Distance(Input.mousePosition, screenPosition);

            if (scanRevealTuning != null && distance > scanRevealTuning.PointSelectRadiusPixels)
                return;

            if (!harvestModeSession.TryAssignSelectedPoint(hoveredPoint.PointId))
                return;

            RefreshAssignedOrders();
            PublishSelectionChanged();
            PublishHoveredPoint(hoveredPoint);

            EventBus.Publish(new HarvestPointSelectedEvent(
                hoveredPoint.PointId,
                harvestModeSession.GetAssignedOrder(hoveredPoint.PointId)));

            RecalculatePreview();
        }

        /// <summary>현재 hover 중인 선택 포인트를 순서에서 제거한다.</summary>
        private void TryRemoveHoveredPoint()
        {
            if (hoveredPoint == null)
                return;

            if (!harvestModeSession.ContainsSelectedPoint(hoveredPoint.PointId))
                return;

            if (!harvestModeSession.RemoveSelectedPoint(hoveredPoint.PointId))
                return;

            RefreshAssignedOrders();
            PublishSelectionChanged();
            PublishHoveredPoint(hoveredPoint);
            RecalculatePreview();
        }

        /// <summary>현재 순서 슬롯을 전부 비운다.</summary>
        public void ResetSelectionSequence()
        {
            harvestModeSession.ClearSequence();
            RefreshAssignedOrders();
            PublishSelectionChanged();
            PublishHoveredPoint(hoveredPoint);
            RecalculatePreview();
        }

        /// <summary>현재 타깃의 회수 포인트를 캐싱한다.</summary>
        private void CacheCurrentTargetPoints()
        {
            currentPoints.Clear();

            if (currentTargetBehaviour == null)
                return;

            IReadOnlyList<HarvestScanPoint> scanPoints = currentTargetBehaviour.GetScanPoints();
            if (scanPoints == null)
                return;

            currentPoints.AddRange(scanPoints.Where(point => point != null));
        }

        /// <summary>현재 포인트 상태를 초기화한다.</summary>
        private void ResetCurrentPoints()
        {
            for (int i = 0; i < currentPoints.Count; i++)
            {
                if (currentPoints[i] == null)
                    continue;

                currentPoints[i].ResetRuntimeState();
            }

            harvestModeSession.ClearSequence();
        }

        /// <summary>저장된 공개 포인트 상태를 현재 타깃 포인트에 복원한다.</summary>
        private void RestorePersistedRevealState()
        {
            if (currentTargetBehaviour == null || harvestTargetScanStateService == null)
                return;

            string targetKey = currentTargetBehaviour.GetRuntimeTargetKey();
            IReadOnlyCollection<string> revealedPointIds = harvestTargetScanStateService.GetRevealedPointIds(targetKey);
            if (revealedPointIds == null || revealedPointIds.Count <= 0)
                return;

            for (int i = 0; i < currentPoints.Count; i++)
            {
                HarvestScanPoint point = currentPoints[i];
                if (point == null)
                    continue;

                if (!revealedPointIds.Contains(point.PointId))
                    continue;

                point.Reveal();
                harvestModeSession.AddRevealedPoint(point.PointId);

                // 카운트/다른 HUD가 기존 이벤트 흐름을 그대로 재사용할 수 있게 한다.
                EventBus.Publish(new HarvestPointRevealedEvent(point.PointId));
            }
        }

        /// <summary>현재 센서 모드 기준으로 포인트를 공개한다.</summary>
        private List<HarvestScanPoint> RevealPointsByScan(HarvestScanMode scanMode)
        {
            List<HarvestScanPoint> unrevealed = currentPoints
                .Where(point => point != null && !point.IsRevealed)
                .ToList();

            List<HarvestScanPoint> revealed = new();
            if (unrevealed.Count <= 0)
                return revealed;

            HarvestScanPoint guaranteed = scanMode == HarvestScanMode.Sonar
                ? unrevealed.OrderByDescending(point => point.SonarSignature).First()
                : unrevealed.OrderByDescending(point => point.LidarSignature).First();

            guaranteed.Reveal();
            revealed.Add(guaranteed);

            for (int i = 0; i < unrevealed.Count; i++)
            {
                HarvestScanPoint point = unrevealed[i];
                if (point == guaranteed)
                    continue;

                if (scanRevealTuning == null)
                    continue;

                float signature = scanMode == HarvestScanMode.Sonar
                    ? point.SonarSignature
                    : point.LidarSignature;

                float revealChance = Mathf.Lerp(
                    scanRevealTuning.MinRevealChance,
                    scanRevealTuning.MaxRevealChance,
                    signature);

                if (Random.value > revealChance)
                    continue;

                point.Reveal();
                revealed.Add(point);
            }

            return revealed;
        }

        /// <summary>세션 슬롯 정보에 맞춰 각 포인트의 순번 표시를 갱신한다.</summary>
        private void RefreshAssignedOrders()
        {
            for (int i = 0; i < currentPoints.Count; i++)
            {
                HarvestScanPoint point = currentPoints[i];
                if (point == null)
                    continue;

                int assignedOrder = harvestModeSession.GetAssignedOrder(point.PointId);
                if (assignedOrder > 0)
                    point.AssignOrder(assignedOrder);
                else
                    point.ClearAssignedOrder();
            }
        }

        /// <summary>현재 세션 상태를 기준으로 추정치를 다시 계산한다.</summary>
        private void RecalculatePreview()
        {
            harvestResolver.EvaluateRecoveryPlan(harvestModeSession, currentPoints);
        }

        /// <summary>현재 회수 계획을 확정하고 결과를 계산한다.</summary>
        private void CommitRecovery()
        {
            if (harvestModeSession.IsResolved)
                return;

            EventBus.Publish(new HarvestRecoveryCommittedEvent());
            harvestResolver.ResolveCommittedRecovery(harvestModeSession, currentPoints);
        }

        /// <summary>현재 타깃 기본 정보를 HUD 이벤트로 발행한다.</summary>
        private void PublishTargetPrepared()
        {
            if (currentTargetBehaviour == null || currentTargetBehaviour.TargetData == null)
                return;

            string displayName = currentTargetBehaviour.RuntimePreviewDisplayName;
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = currentTargetBehaviour.TargetData.TargetId;

            EventBus.Publish(new HarvestConsoleTargetPreparedEvent(displayName, currentPoints.Count));
        }

        /// <summary>현재 실제 선택 개수를 HUD 이벤트로 발행한다.</summary>
        private void PublishSelectionChanged()
        {
            EventBus.Publish(new HarvestSelectionSequenceChangedEvent(
                harvestModeSession.GetAssignedPointCount(),
                currentPoints.Count));
        }

        /// <summary>현재 hover 포인트 정보를 HUD 이벤트로 발행한다.</summary>
        private void PublishHoveredPoint(HarvestScanPoint point)
        {
            if (point == null)
            {
                EventBus.Publish(new HarvestHoveredPointChangedEvent(
                    false,
                    string.Empty,
                    string.Empty,
                    0,
                    0f,
                    0f,
                    0f,
                    0f,
                    Vector2.zero));
                return;
            }

            Vector3 screenPosition = harvestConsoleCamera.WorldToScreenPoint(point.TooltipAnchor.position);

            EventBus.Publish(new HarvestHoveredPointChangedEvent(
                true,
                point.PointId,
                point.DisplayLabel,
                point.AssignedOrder,
                point.BaseStability,
                point.FirstAnchorBias,
                point.SequenceBias,
                point.RiskWeight,
                screenPosition));
        }

        /// <summary>현재 배터리/내구도 상태를 HUD 초기화용으로 재발행한다.</summary>
        private void PublishRuntimeState()
        {
            SubmarineRuntimeState runtime = harvestResolver.SubmarineRuntimeState;
            if (runtime == null || runtime.BaseStats == null)
                return;

            EventBus.Publish(new BatteryChangedEvent(runtime.CurrentBattery, runtime.BaseStats.MaxBattery));
            EventBus.Publish(new HullDurabilityChangedEvent(runtime.CurrentHullDurability, runtime.BaseStats.MaxHullDurability));
        }

        /// <summary>현재 hover 포인트를 즉시 해제한다.</summary>
        private void ClearHoveredPoint()
        {
            if (hoveredPoint != null)
            {
                hoveredPoint.SetHovered(false);
                hoveredPoint = null;
            }

            PublishHoveredPoint(null);
        }

        /// <summary>현재 포인터가 UI 위에 있어 입력을 막아야 하는지 반환한다.</summary>
        private bool IsPointerBlockedByUi()
        {
            if (!blockInputWhenPointerOverUi)
                return false;

            if (EventSystem.current == null)
                return false;

            return EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>화면 중심 기준 회전 허용 x 대역 안에 있는지 검사한다.</summary>
        private bool IsInsideRotationBand(Vector3 screenPosition)
        {
            float screenCenterX = Screen.width * 0.5f;
            float offsetXFromCenter = screenPosition.x - screenCenterX;
            return Mathf.Abs(offsetXFromCenter) <= Mathf.Max(0f, validRotationHalfWidthPixels);
        }

        /// <summary>현재 위치에서 타깃 회전 드래그를 시작할 수 있는지 검사한다.</summary>
        private bool CanStartTargetRotationDrag(Vector3 screenPosition)
        {
            if (hoveredPoint != null)
                return false;

            if (IsPointerBlockedByUi())
                return false;

            if (!IsInsideRotationBand(screenPosition))
                return false;

            return true;
        }

        /// <summary>현재 위치에서 타깃 회전 드래그를 계속 진행할 수 있는지 검사한다.</summary>
        private bool CanContinueTargetRotationDrag(Vector3 screenPosition)
        {
            if (IsPointerBlockedByUi())
                return false;

            if (!IsInsideRotationBand(screenPosition))
                return false;

            return true;
        }

        /// <summary>회수 콘솔용 자유 커서 상태를 적용한다.</summary>
        private void ApplyHarvestCursorState()
        {
            Cursor.lockState = CursorLockMode.Confined;
            Cursor.visible = true;
        }

        /// <summary>탐사용 잠금 커서 상태를 적용한다.</summary>
        private void ApplyExplorationCursorState()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
