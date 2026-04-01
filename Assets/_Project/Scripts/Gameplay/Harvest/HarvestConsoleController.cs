using System.Collections.Generic;
using System.Linq;
using Project.Core.Events;
using Project.Data.Enums;
using Project.Data.Harvest;
using Project.Data.Input;
using Project.Gameplay.GameModes;
using Project.Gameplay.Runtime;
using UnityEngine;

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

        private HarvestModeSession harvestModeSession; // 현재 회수 세션
        private HarvestResolver harvestResolver; // 회수 계산기
        private HarvestModeCoordinator harvestModeCoordinator; // 모드 전환 조정기
        private readonly List<HarvestScanPoint> currentPoints = new(); // 현재 타깃 포인트 캐시

        private bool isHarvestMode; // 현재 Harvest 모드 여부
        private bool isDraggingTarget; // 현재 타깃 드래그 회전 여부
        private HarvestScanPoint hoveredPoint; // 현재 hover 중인 포인트
        private HarvestTargetBehaviour currentTargetBehaviour; // 현재 타깃 Behaviour

        /// <summary>세션과 계산기, 조정기를 주입한다.</summary>
        public void Initialize(
            HarvestModeSession newHarvestModeSession,
            HarvestResolver newHarvestResolver,
            HarvestModeCoordinator newHarvestModeCoordinator)
        {
            harvestModeSession = newHarvestModeSession;
            harvestResolver = newHarvestResolver;
            harvestModeCoordinator = newHarvestModeCoordinator;
        }

        /// <summary>Harvest 이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>Harvest 이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
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
            // 이벤트는 먼저 왔지만 런타임 주입이 아직 끝나지 않은 경우를 방어한다.
            if (harvestModeSession == null || harvestResolver == null || harvestModeCoordinator == null)
                return;

            isHarvestMode = true;
            isDraggingTarget = false;
            hoveredPoint = null;
            currentTargetBehaviour = harvestModeSession.CurrentTarget as HarvestTargetBehaviour;

            ApplyHarvestCursorState();
            CacheCurrentTargetPoints();
            ResetCurrentPoints();

            harvestModeSession.EnsureSequenceCapacity(currentPoints.Count);

            if (currentTargetBehaviour != null && targetRotationController != null)
            {
                targetRotationController.BindTarget(currentTargetBehaviour.GetRotationPivot());
                targetRotationController.ResetRotation();
            }

            harvestModeSession.SetScanMode(HarvestScanMode.Sonar);
            EventBus.Publish(new HarvestScanModeChangedEvent((int)HarvestScanMode.Sonar));

            PublishTargetPrepared();
            PublishRuntimeState();
            PublishSelectionChanged();
            PublishHoveredPoint(null);

            RecalculatePreview();
        }

        /// <summary>회수 콘솔 종료 시 현재 타깃 상태를 정리한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
            isDraggingTarget = false;
            currentTargetBehaviour = null;

            ApplyExplorationCursorState();

            if (hoveredPoint != null)
            {
                hoveredPoint.SetHovered(false);
                hoveredPoint = null;
            }

            currentPoints.Clear();

            if (targetRotationController != null)
            {
                targetRotationController.EndDrag();
                targetRotationController.UnbindTarget();
            }

            PublishHoveredPoint(null);
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
            harvestResolver.SubmarineRuntimeState.ConsumeBattery(batteryCost);

            if (harvestResolver.SubmarineRuntimeState.CurrentBattery <= 0f)
            {
                if (harvestModeSession.CurrentTarget?.TargetData != null)
                {
                    EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(
                        harvestModeSession.CurrentTarget.TargetData.TargetId));
                }

                harvestModeCoordinator.ExitHarvestMode();
                return;
            }

            harvestModeSession.AddScanPulse();
            EventBus.Publish(new HarvestScanPulseEvent((int)currentMode, harvestModeSession.ScanPulseCount));

            for (int i = 0; i < revealedPoints.Count; i++)
            {
                HarvestScanPoint point = revealedPoints[i];
                harvestModeSession.AddRevealedPoint(point.PointId);
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
                if (hoveredPoint == null)
                {
                    isDraggingTarget = true;
                    targetRotationController.BeginDrag(Input.mousePosition);
                }
            }

            if (Input.GetMouseButton(0) && isDraggingTarget)
                targetRotationController.RotateByDrag(Input.mousePosition);

            if (Input.GetMouseButtonUp(0) && isDraggingTarget)
            {
                isDraggingTarget = false;
                targetRotationController.EndDrag();
            }
        }

        /// <summary>포인트 선택과 제거 입력을 처리한다.</summary>
        private void HandlePointClickInput()
        {
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

            if (hoveredPoint == bestPoint)
            {
                PublishHoveredPoint(hoveredPoint);
                return;
            }

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
                    Vector2.zero));
                return;
            }

            Vector3 screenPosition = harvestConsoleCamera.WorldToScreenPoint(point.TooltipAnchor.position);
            EventBus.Publish(new HarvestHoveredPointChangedEvent(
                true,
                point.PointId,
                point.DisplayLabel,
                point.AssignedOrder,
                point.SonarSignature,
                point.LidarSignature,
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

        /// <summary>회수 콘솔용 자유 커서 상태를 적용한다.</summary>
        private void ApplyHarvestCursorState()
        {
            Cursor.lockState = CursorLockMode.None;
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
