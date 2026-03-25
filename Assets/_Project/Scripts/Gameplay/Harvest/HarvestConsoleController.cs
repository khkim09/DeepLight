using System.Collections.Generic;
using System.Linq;
using Project.Core.Events;
using Project.Data.Enums;
using Project.Gameplay.GameModes;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>회수 콘솔 모드에서 센서 사용과 포인트 선택 입력을 담당하는 클래스</summary>
    public class HarvestConsoleController : MonoBehaviour
    {
        [Header("Keys")]
        [SerializeField] private KeyCode scanKey = KeyCode.F; // 채집 모드 내 스캔 실행 키
        [SerializeField] private KeyCode switchSensorKey = KeyCode.R; // 센서 전환 키
        [SerializeField] private KeyCode commitKey = KeyCode.Space; // 회수 확정 키
        [SerializeField] private KeyCode exitKey = KeyCode.X; // 채집 취소 키

        [Header("Selection")]
        [SerializeField] private float pointSelectRadiusPixels = 90f; // 클릭 선택 허용 반경
        [SerializeField] private float pointHoverRadiusPixels = 120f; // hover 강조 허용 반경

        [Header("References")]
        [SerializeField] private Camera harvestConsoleCamera; // 조종실 카메라

        private HarvestModeSession harvestModeSession; // 현재 채집 세션
        private HarvestResolver harvestResolver; // 회수 해석기
        private HarvestModeCoordinator harvestModeCoordinator; // 모드 조정기
        private readonly List<HarvestScanPoint> currentPoints = new List<HarvestScanPoint>(); // 현재 타겟 포인트 목록

        private bool isHarvestMode; // 회수 콘솔 모드 여부
        private HarvestScanPoint hoveredPoint; // 현재 hover 중인 포인트

        /// <summary>의존성을 주입한다</summary>
        public void Initialize(
            HarvestModeSession newHarvestModeSession,
            HarvestResolver newHarvestResolver,
            HarvestModeCoordinator newHarvestModeCoordinator)
        {
            harvestModeSession = newHarvestModeSession; // 세션 저장
            harvestResolver = newHarvestResolver; // 해석기 저장
            harvestModeCoordinator = newHarvestModeCoordinator; // 조정기 저장
        }

        /// <summary>이벤트 구독을 등록한다</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독을 해제한다</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>초기 탐사용 커서 상태를 적용한다</summary>
        private void Start()
        {
            ApplyExplorationCursorState();
        }

        /// <summary>회수 콘솔 모드에서 입력을 처리한다</summary>
        private void Update()
        {
            // 채집 모드가 아니면 중단
            if (!isHarvestMode)
                return;

            // 핵심 참조 없으면 중단
            if (harvestModeSession == null || harvestResolver == null || harvestModeCoordinator == null)
                return;

            // 대상 없으면 중단
            if (!harvestModeSession.HasTarget)
                return;

            // 항상 hover는 갱신
            UpdateHoveredPoint();

            // 회수 결과 확정 이후에는 X만 허용
            if (harvestModeSession.IsResolved)
            {
                if (Input.GetKeyDown(exitKey))
                    harvestModeCoordinator.ExitHarvestMode();

                return;
            }

            // 센서 전환 처리
            HandleSensorModeInput();

            // 스캔 입력 처리
            HandleScanInput();

            // 포인트 선택/제거 입력 처리
            HandlePointClickInput();

            // 확정/종료 처리
            HandleCommitAndExitInput();
        }

        /// <summary>회수 콘솔 진입 시 대상 포인트를 캐싱한다</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true; // 모드 활성
            hoveredPoint = null; // hover 초기화

            ApplyHarvestCursorState(); // 회수 콘솔용 자유 커서 적용

            CacheCurrentTargetPoints(); // 현재 대상 포인트 캐싱
            ResetCurrentPoints(); // 포인트 상태 초기화

            // 시작 센서는 소나로 고정
            harvestModeSession.SetScanMode(HarvestScanMode.Sonar);
            EventBus.Publish(new HarvestScanModeChangedEvent((int)HarvestScanMode.Sonar));

            // 초기 추정치 계산
            RecalculatePreview();
        }

        /// <summary>회수 콘솔 종료 시 런타임 상태를 정리한다</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false; // 모드 비활성

            ApplyExplorationCursorState(); // 탐사용 고정 커서 복구

            // 호버 비주얼 정리
            if (hoveredPoint != null)
            {
                hoveredPoint.SetHovered(false);
                hoveredPoint = null;
            }

            currentPoints.Clear(); // 포인트 목록 정리
        }

        /// <summary>센서 전환 입력을 처리한다</summary>
        private void HandleSensorModeInput()
        {
            if (!Input.GetKeyDown(switchSensorKey))
                return;

            // 소나/라이다 토글
            HarvestScanMode nextMode = harvestModeSession.CurrentScanMode == HarvestScanMode.Sonar
                ? HarvestScanMode.Lidar
                : HarvestScanMode.Sonar;

            harvestModeSession.SetScanMode(nextMode); // 센서 변경
            EventBus.Publish(new HarvestScanModeChangedEvent((int)nextMode)); // HUD용 이벤트 발행

            // 센서 변경 즉시 preview 다시 그림
            RecalculatePreview();
        }

        /// <summary>스캔 입력을 처리한다</summary>
        private void HandleScanInput()
        {
            if (!Input.GetKeyDown(scanKey))
                return;

            HarvestScanMode currentMode = harvestModeSession.CurrentScanMode;
            if (currentMode == HarvestScanMode.None)
                return;

            // 센서 모드에 따라 포인트 공개
            List<HarvestScanPoint> revealedPoints = RevealPointsByScan(currentMode);
            if (revealedPoints.Count <= 0)
                return;

            // 스캔 비용만큼 배터리 소모
            float batteryCost = harvestResolver.GetScanPulseBatteryCost(currentMode);
            harvestResolver.SubmarineRuntimeState.ConsumeBattery(batteryCost);

            // 방전 시 종료
            if (harvestResolver.SubmarineRuntimeState.CurrentBattery <= 0f)
            {
                if (harvestModeSession.CurrentTarget?.TargetData != null)
                    EventBus.Publish(new HarvestSessionForcedEndedByBatteryEvent(
                        harvestModeSession.CurrentTarget.TargetData.TargetId));

                harvestModeCoordinator.ExitHarvestMode();
                return;
            }

            // 스캔 사용 횟수 기록
            harvestModeSession.AddScanPulse();
            EventBus.Publish(new HarvestScanPulseEvent((int)currentMode, harvestModeSession.ScanPulseCount));

            // 공개된 포인트 이벤트 발행
            foreach (HarvestScanPoint point in revealedPoints)
            {
                harvestModeSession.AddRevealedPoint(point.PointId);
                EventBus.Publish(new HarvestPointRevealedEvent(point.PointId));
            }

            RecalculatePreview(); // 추정치 갱신
        }

        /// <summary>포인트 클릭 입력을 처리한다</summary>
        private void HandlePointClickInput()
        {
            if (Input.GetMouseButtonDown(0))
                TrySelectHoveredPoint(); // hover된 포인트를 순서에 배치

            if (Input.GetMouseButtonDown(1))
                RemoveLastSelectedPoint(); // 마지막 순서 제거
        }

        /// <summary>회수 확정 및 종료 입력을 처리한다</summary>
        private void HandleCommitAndExitInput()
        {
            if (Input.GetKeyDown(commitKey))
            {
                CommitRecovery();
                return;
            }

            if (Input.GetKeyDown(exitKey))
                harvestModeCoordinator.ExitHarvestMode();
        }

        /// <summary>현재 마우스 위치 기준으로 hover 포인트를 갱신한다</summary>
        private void UpdateHoveredPoint()
        {
            if (harvestConsoleCamera == null)
                return;

            Vector2 mousePosition = Input.mousePosition;
            HarvestScanPoint bestPoint = null;
            float bestDistance = float.MaxValue;

            foreach (HarvestScanPoint point in currentPoints)
            {
                // 공개된 포인트만 hover 후보
                if (point == null || !point.IsRevealed || point.IsSelected)
                    continue;

                Vector3 screenPosition = harvestConsoleCamera.WorldToScreenPoint(point.transform.position);

                // 카메라 뒤쪽은 제외
                if (screenPosition.z <= 0f)
                    continue;

                float distance = Vector2.Distance(mousePosition, screenPosition);

                // hover 허용 반경 밖이면 제외
                if (distance > pointHoverRadiusPixels)
                    continue;

                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestPoint = point;
            }

            // 이전 hover 해제
            if (hoveredPoint != null && hoveredPoint != bestPoint)
                hoveredPoint.SetHovered(false);

            hoveredPoint = bestPoint;

            // 현재 hover 표시
            if (hoveredPoint != null)
                hoveredPoint.SetHovered(true);
        }

        /// <summary>현재 hover 중인 포인트를 순서에 배치한다</summary>
        private void TrySelectHoveredPoint()
        {
            if (hoveredPoint == null)
                return;

            Vector3 screenPosition = harvestConsoleCamera.WorldToScreenPoint(hoveredPoint.transform.position);
            float distance = Vector2.Distance(Input.mousePosition, screenPosition);

            if (distance > pointSelectRadiusPixels)
                return;

            hoveredPoint.Select(); // 포인트를 실제 선택 상태로 전환
            harvestModeSession.AddSelectedPoint(hoveredPoint.PointId); // 회수 순서에 추가

            EventBus.Publish(new HarvestPointSelectedEvent(
                hoveredPoint.PointId,
                harvestModeSession.SelectedPointSequence.Count));

            hoveredPoint = null; // hover 해제
            RecalculatePreview(); // 추정치 재계산
        }

        /// <summary>현재 타겟의 회수 포인트를 캐싱한다</summary>
        private void CacheCurrentTargetPoints()
        {
            currentPoints.Clear(); // 이전 목록 제거

            if (harvestModeSession?.CurrentTarget is not HarvestTargetBehaviour targetBehaviour)
                return;

            IReadOnlyList<HarvestScanPoint> scanPoints = targetBehaviour.GetScanPoints();
            if (scanPoints == null)
                return;

            currentPoints.AddRange(scanPoints.Where(point => point != null)); // 포인트 목록 저장
        }

        /// <summary>현재 포인트 상태를 초기화한다</summary>
        private void ResetCurrentPoints()
        {
            foreach (HarvestScanPoint point in currentPoints)
            {
                if (point == null)
                    continue;

                point.ResetRuntimeState(); // hidden 상태로 초기화
            }

            harvestModeSession.ClearSelectedPoints(); // 순서 초기화
        }

        /// <summary>현재 센서 모드에 따라 포인트를 공개한다</summary>
        private List<HarvestScanPoint> RevealPointsByScan(HarvestScanMode scanMode)
        {
            List<HarvestScanPoint> unrevealed = currentPoints
                .Where(point => point != null && !point.IsRevealed)
                .ToList();

            List<HarvestScanPoint> revealed = new List<HarvestScanPoint>();
            if (unrevealed.Count <= 0)
                return revealed;

            // 최소 1개는 공개 보장
            HarvestScanPoint guaranteed = scanMode == HarvestScanMode.Sonar
                ? unrevealed.OrderByDescending(point => point.SonarSignature).First()
                : unrevealed.OrderByDescending(point => point.LidarSignature).First();

            guaranteed.Reveal();
            revealed.Add(guaranteed);

            // 반응도에 따라 추가 공개
            foreach (HarvestScanPoint point in unrevealed)
            {
                if (point == guaranteed)
                    continue;

                float signature = scanMode == HarvestScanMode.Sonar ? point.SonarSignature : point.LidarSignature;
                float revealChance = Mathf.Lerp(0.15f, 0.75f, signature); // 반응도가 높을수록 더 잘 드러남

                if (Random.value > revealChance)
                    continue;

                point.Reveal();
                revealed.Add(point);
            }

            return revealed;
        }

        /// <summary>마지막으로 배치한 포인트를 회수 순서에서 제거한다</summary>
        private void RemoveLastSelectedPoint()
        {
            if (harvestModeSession.SelectedPointSequence.Count <= 0)
                return;

            string lastPointId = harvestModeSession.SelectedPointSequence.Last(); // 마지막으로 배치된 포인트
            HarvestScanPoint point = currentPoints.FirstOrDefault(candidate => candidate.PointId == lastPointId);
            if (point != null)
                point.Deselect(); // 선택 해제 후 revealed/hidden 상태로 복귀

            List<string> rebuilt = harvestModeSession.SelectedPointSequence
                .Take(harvestModeSession.SelectedPointSequence.Count - 1)
                .ToList();

            harvestModeSession.ClearSelectedPoints();

            foreach (string pointId in rebuilt)
                harvestModeSession.AddSelectedPoint(pointId);

            RecalculatePreview(); // 추정치 재계산
        }

        /// <summary>현재 세션 상태를 기반으로 추정치를 다시 계산한다</summary>
        private void RecalculatePreview()
        {
            harvestResolver.EvaluateRecoveryPlan(harvestModeSession, currentPoints); // preview 계산
        }

        /// <summary>현재 회수 계획을 확정하고 결과만 계산한다</summary>
        private void CommitRecovery()
        {
            // 이미 결과가 나온 뒤면 중단
            if (harvestModeSession.IsResolved)
                return;

            // 회수 계획 확정
            harvestModeSession.CommitRecoveryPlan();
            EventBus.Publish(new HarvestRecoveryCommittedEvent());

            // 실제 회수 결과 계산
            harvestResolver.ResolveCommittedRecovery(harvestModeSession, currentPoints);

            // 결과 확인은 같은 화면에서 유지하고,
            // 탐사 모드 복귀는 X 키로만 처리한다.
        }

        /// <summary>회수 콘솔용 마우스 커서 상태를 적용한다</summary>
        private void ApplyHarvestCursorState()
        {
            Cursor.lockState = CursorLockMode.None; // 자유 커서
            Cursor.visible = true; // 커서 표시
        }

        /// <summary>탐사용 마우스 커서 상태를 적용한다</summary>
        private void ApplyExplorationCursorState()
        {
            Cursor.lockState = CursorLockMode.Locked; // 화면 중앙 고정
            Cursor.visible = false; // 커서 숨김
        }
    }
}
