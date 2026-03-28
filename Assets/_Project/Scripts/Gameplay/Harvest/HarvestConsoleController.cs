using System.Collections.Generic;
using System.Linq;
using Project.Core.Events;
using Project.Data.Enums;
using Project.Data.Harvest;
using Project.Data.Input;
using Project.Gameplay.GameModes;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>회수 콘솔 모드에서 센서 사용과 포인트 선택 입력을 담당하는 클래스이다.</summary>
    public class HarvestConsoleController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private HarvestScanRevealTuningSO scanRevealTuning; // 포인트 공개/선택 튜닝
        [SerializeField] private GameInputBindingsSO inputBindings;          // 공용 입력 바인딩 SO

        [Header("References")]
        [SerializeField] private Camera harvestConsoleCamera; // 조종실 카메라

        private HarvestModeSession harvestModeSession;
        private HarvestResolver harvestResolver;
        private HarvestModeCoordinator harvestModeCoordinator;
        private readonly List<HarvestScanPoint> currentPoints = new();

        private bool isHarvestMode;
        private HarvestScanPoint hoveredPoint;

        /// <summary>세션과 해석기, 조정기를 주입한다.</summary>
        public void Initialize(
            HarvestModeSession newHarvestModeSession,
            HarvestResolver newHarvestResolver,
            HarvestModeCoordinator newHarvestModeCoordinator)
        {
            harvestModeSession = newHarvestModeSession;
            harvestResolver = newHarvestResolver;
            harvestModeCoordinator = newHarvestModeCoordinator;
        }

        /// <summary>Harvest 모드 이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>Harvest 모드 이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>초기 탐사용 커서 상태를 적용한다.</summary>
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

            if (inputBindings == null)
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
            HandlePointClickInput();
            HandleCommitAndExitInput();
        }

        /// <summary>회수 콘솔 진입 시 포인트 목록과 상태를 초기화한다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            hoveredPoint = null;

            ApplyHarvestCursorState();
            CacheCurrentTargetPoints();
            ResetCurrentPoints();

            harvestModeSession.SetScanMode(HarvestScanMode.Sonar);
            EventBus.Publish(new HarvestScanModeChangedEvent((int)HarvestScanMode.Sonar));

            RecalculatePreview();
        }

        /// <summary>회수 콘솔 종료 시 런타임 상태를 정리한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
            ApplyExplorationCursorState();

            if (hoveredPoint != null)
            {
                hoveredPoint.SetHovered(false);
                hoveredPoint = null;
            }

            currentPoints.Clear();
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

            foreach (HarvestScanPoint point in revealedPoints)
            {
                harvestModeSession.AddRevealedPoint(point.PointId);
                EventBus.Publish(new HarvestPointRevealedEvent(point.PointId));
            }

            RecalculatePreview();
        }

        /// <summary>포인트 선택과 제거 입력을 처리한다.</summary>
        private void HandlePointClickInput()
        {
            if (Input.GetMouseButtonDown(0))
                TrySelectHoveredPoint();

            if (Input.GetMouseButtonDown(1))
                RemoveLastSelectedPoint();
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

        /// <summary>현재 마우스 위치 기준으로 hover 포인트를 갱신한다.</summary>
        private void UpdateHoveredPoint()
        {
            if (harvestConsoleCamera == null)
                return;

            Vector2 mousePosition = Input.mousePosition;
            HarvestScanPoint bestPoint = null;
            float bestDistance = float.MaxValue;

            foreach (HarvestScanPoint point in currentPoints)
            {
                if (point == null || !point.IsRevealed || point.IsSelected)
                    continue;

                Vector3 screenPosition = harvestConsoleCamera.WorldToScreenPoint(point.transform.position);
                if (screenPosition.z <= 0f)
                    continue;

                float distance = Vector2.Distance(mousePosition, screenPosition);

                if (scanRevealTuning == null || distance > scanRevealTuning.PointHoverRadiusPixels)
                    continue;

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestPoint = point;
            }

            if (hoveredPoint != null && hoveredPoint != bestPoint)
                hoveredPoint.SetHovered(false);

            hoveredPoint = bestPoint;

            if (hoveredPoint != null)
                hoveredPoint.SetHovered(true);
        }

        /// <summary>현재 hover 중인 포인트를 순서에 배치한다.</summary>
        private void TrySelectHoveredPoint()
        {
            if (hoveredPoint == null)
                return;

            Vector3 screenPosition = harvestConsoleCamera.WorldToScreenPoint(hoveredPoint.transform.position);
            float distance = Vector2.Distance(Input.mousePosition, screenPosition);

            if (scanRevealTuning == null || distance > scanRevealTuning.PointSelectRadiusPixels)
                return;

            hoveredPoint.Select();
            harvestModeSession.AddSelectedPoint(hoveredPoint.PointId);

            EventBus.Publish(new HarvestPointSelectedEvent(
                hoveredPoint.PointId,
                harvestModeSession.SelectedPointSequence.Count));

            hoveredPoint = null;
            RecalculatePreview();
        }

        /// <summary>현재 타깃의 회수 포인트를 캐싱한다.</summary>
        private void CacheCurrentTargetPoints()
        {
            currentPoints.Clear();

            if (harvestModeSession?.CurrentTarget is not HarvestTargetBehaviour targetBehaviour)
                return;

            IReadOnlyList<HarvestScanPoint> scanPoints = targetBehaviour.GetScanPoints();
            if (scanPoints == null)
                return;

            currentPoints.AddRange(scanPoints.Where(point => point != null));
        }

        /// <summary>현재 포인트 런타임 상태를 초기화한다.</summary>
        private void ResetCurrentPoints()
        {
            foreach (HarvestScanPoint point in currentPoints)
            {
                if (point == null)
                    continue;

                point.ResetRuntimeState();
            }

            harvestModeSession.ClearSelectedPoints();
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

            foreach (HarvestScanPoint point in unrevealed)
            {
                if (point == guaranteed)
                    continue;

                float signature = scanMode == HarvestScanMode.Sonar ? point.SonarSignature : point.LidarSignature;

                if (scanRevealTuning == null)
                    return revealed;

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

        /// <summary>마지막 선택 포인트를 회수 순서에서 제거한다.</summary>
        private void RemoveLastSelectedPoint()
        {
            if (harvestModeSession.SelectedPointSequence.Count <= 0)
                return;

            string lastPointId = harvestModeSession.SelectedPointSequence.Last();
            HarvestScanPoint point = currentPoints.FirstOrDefault(candidate => candidate.PointId == lastPointId);
            if (point != null)
                point.Deselect();

            List<string> rebuilt = harvestModeSession.SelectedPointSequence
                .Take(harvestModeSession.SelectedPointSequence.Count - 1)
                .ToList();

            harvestModeSession.ClearSelectedPoints();

            foreach (string pointId in rebuilt)
                harvestModeSession.AddSelectedPoint(pointId);

            RecalculatePreview();
        }

        /// <summary>현재 세션 상태를 기반으로 추정치를 다시 계산한다.</summary>
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
