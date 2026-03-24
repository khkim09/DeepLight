using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>탐사 카메라와 채집 카메라 사이의 시네마틱 전환을 담당하는 클래스</summary>
    public class PerspectiveSwapController : MonoBehaviour
    {
        [Header("Camera References")]
        [SerializeField] private Camera explorationCamera; // 탐사 카메라
        [SerializeField] private Camera harvestCamera; // 채집 카메라
        [SerializeField] private Camera transitionCamera; // 전환용 카메라
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 카메라 컨트롤러
        [SerializeField] private HarvestCinematicCameraController harvestCinematicCameraController; // 채집 카메라 컨트롤러
        [SerializeField] private HarvestModePresentationController harvestModePresentationController; // 채집 표시 보정 컨트롤러

        [Header("Transition")]
        [SerializeField] private float transitionDuration = 0.9f; // 전환 시간
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 전환 곡선

        private CancellationTokenSource transitionCts; // 현재 전환 취소 토큰
        private bool isHarvestMode; // 현재 채집 모드 여부

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

            // 진행 중 전환 취소
            CancelCurrentTransition();
        }

        /// <summary>초기 카메라 상태를 설정한다</summary>
        private void Start()
        {
            isHarvestMode = false;
            SetExplorationViewImmediate();
        }

        /// <summary>채집 모드 진입 시 시네마틱 전환을 시작한다</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            if (isHarvestMode)
                return;

            isHarvestMode = true;
            StartTransitionToHarvestAsync().Forget();
        }

        /// <summary>채집 모드 종료 시 시네마틱 전환을 시작한다</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            isHarvestMode = false;
            StartTransitionToExplorationAsync().Forget();
        }

        /// <summary>탐사 카메라 상태를 즉시 적용한다</summary>
        private void SetExplorationViewImmediate()
        {
            if (explorationFollowCameraController != null)
                explorationFollowCameraController.SnapToTarget();

            if (explorationCamera != null)
                explorationCamera.gameObject.SetActive(true);

            if (harvestCamera != null)
                harvestCamera.gameObject.SetActive(false);

            if (transitionCamera != null)
                transitionCamera.gameObject.SetActive(false);
        }

        /// <summary>채집 카메라 상태를 즉시 적용한다</summary>
        private void SetHarvestViewImmediate()
        {
            if (harvestCinematicCameraController != null)
                harvestCinematicCameraController.SnapToDesiredPose();

            if (explorationCamera != null)
                explorationCamera.gameObject.SetActive(false);

            if (harvestCamera != null)
                harvestCamera.gameObject.SetActive(true);

            if (transitionCamera != null)
                transitionCamera.gameObject.SetActive(false);
        }

        /// <summary>채집 카메라로 전환을 시작한다</summary>
        private async UniTaskVoid StartTransitionToHarvestAsync()
        {
            // 참조 누락 시 중단
            if (transitionCamera == null || harvestCamera == null)
                return;

            CancelCurrentTransition();

            // 새 전환 토큰 생성
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCts.Token;

            // 현재 활성 카메라 찾기
            Camera sourceCamera = GetCurrentlyActiveCamera(explorationCamera);
            if (sourceCamera == null)
                sourceCamera = explorationCamera;

            // 채집 목표 포즈 계산
            Vector3 targetPosition = harvestCamera.transform.position;
            Quaternion targetRotation = harvestCamera.transform.rotation;
            if (harvestCinematicCameraController != null)
                harvestCinematicCameraController.GetDesiredPose(out targetPosition, out targetRotation);

            // 표시 보정과 카메라 전환을 동시에 실행
            UniTask presentationTask = harvestModePresentationController != null
                ? harvestModePresentationController.EnterPresentationAsync(transitionDuration, token)
                : UniTask.CompletedTask;

            UniTask cameraTask = PlayTransitionAsync(sourceCamera, targetPosition, targetRotation, harvestCamera, token);

            try
            {
                await UniTask.WhenAll(cameraTask, presentationTask);
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>탐사 카메라로 전환을 시작한다</summary>
        private async UniTaskVoid StartTransitionToExplorationAsync()
        {
            if (transitionCamera == null || explorationCamera == null)
                return;

            CancelCurrentTransition();

            // 새 전환 토큰 생성
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCts.Token;

            Camera sourceCamera = GetCurrentlyActiveCamera(harvestCamera);
            if (sourceCamera == null)
                sourceCamera = harvestCamera;

            // 탐사 카메라 목표 포즈를 현재 탐사 로직 기준으로 먼저 갱신
            if (explorationFollowCameraController != null)
                explorationFollowCameraController.SnapToTarget();

            // 탐사 목표 포즈 계산
            Vector3 targetPosition = explorationCamera.transform.position;
            Quaternion targetRotation = explorationCamera.transform.rotation;

            // 표시 복귀와 카메라 전환을 동시에 실행
            UniTask presentationTask = harvestModePresentationController != null
                ? harvestModePresentationController.ExitPresentationAsync(transitionDuration, token)
                : UniTask.CompletedTask;

            UniTask cameraTask = PlayTransitionAsync(sourceCamera, targetPosition, targetRotation, explorationCamera, token);

            try
            {
                await UniTask.WhenAll(cameraTask, presentationTask);
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>현재 활성 카메라를 반환한다</summary>
        private Camera GetCurrentlyActiveCamera(Camera fallbackCamera)
        {
            if (explorationCamera != null && explorationCamera.gameObject.activeInHierarchy)
                return explorationCamera;

            if (harvestCamera != null && harvestCamera.gameObject.activeInHierarchy)
                return harvestCamera;

            if (transitionCamera != null && transitionCamera.gameObject.activeInHierarchy)
                return transitionCamera;

            return fallbackCamera;
        }

        /// <summary>카메라 전환을 비동기로 실행한다</summary>
        private async UniTask PlayTransitionAsync(
            Camera sourceCamera,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Camera targetCamera,
            CancellationToken token)
        {
            if (sourceCamera == null || targetCamera == null)
                return;

            // 전환 카메라 시작 상태 복사
            transitionCamera.transform.position = sourceCamera.transform.position;
            transitionCamera.transform.rotation = sourceCamera.transform.rotation;
            transitionCamera.orthographic = sourceCamera.orthographic;
            transitionCamera.fieldOfView = sourceCamera.fieldOfView;
            transitionCamera.orthographicSize = sourceCamera.orthographicSize;

            // 목표 카메라의 렌더링 설정 캐시
            bool endOrthographic = targetCamera.orthographic; // 목표 투영 모드
            float endFov = targetCamera.fieldOfView; // 목표 FOV
            float endOrthoSize = targetCamera.orthographicSize; // 목표 Ortho Size

            if (explorationCamera != null)
                explorationCamera.gameObject.SetActive(false);

            if (harvestCamera != null)
                harvestCamera.gameObject.SetActive(false);

            transitionCamera.gameObject.SetActive(true);

            Vector3 startPosition = sourceCamera.transform.position; // 시작 위치
            Quaternion startRotation = sourceCamera.transform.rotation; // 시작 회전
            float startFov = sourceCamera.fieldOfView; // 시작 FOV
            float startOrthoSize = sourceCamera.orthographicSize; // 시작 Ortho Size

            float elapsed = 0f; // 누적 시간

            // 전환 진행
            while (elapsed < transitionDuration)
            {
                token.ThrowIfCancellationRequested();

                // 시간 누적
                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / transitionDuration);
                float curvedT = transitionCurve.Evaluate(normalized);

                // 위치 보간
                transitionCamera.transform.position = Vector3.Lerp(startPosition, targetPosition, curvedT);

                // 회전 보간
                transitionCamera.transform.rotation = Quaternion.Slerp(startRotation, targetRotation, curvedT);

                // 투영 모드 동일 여부에 따라 처리
                if (!sourceCamera.orthographic && !endOrthographic)
                {
                    transitionCamera.orthographic = false;
                    transitionCamera.fieldOfView = Mathf.Lerp(startFov, endFov, curvedT);
                }
                else if (sourceCamera.orthographic && endOrthographic)
                {
                    transitionCamera.orthographic = true;
                    transitionCamera.orthographicSize = Mathf.Lerp(startOrthoSize, endOrthoSize, curvedT);
                }
                else
                {
                    // 다른 투영 모드는 마지막 구간에서 전환
                    if (normalized < 0.85f)
                    {
                        transitionCamera.orthographic = sourceCamera.orthographic;

                        if (sourceCamera.orthographic)
                            transitionCamera.orthographicSize = startOrthoSize;
                        else
                            transitionCamera.fieldOfView = startFov;
                    }
                    else
                    {
                        transitionCamera.orthographic = endOrthographic;

                        if (endOrthographic)
                            transitionCamera.orthographicSize = endOrthoSize;
                        else
                            transitionCamera.fieldOfView = endFov;
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 목표 포즈 정확히 반영
            transitionCamera.transform.position = targetPosition;
            transitionCamera.transform.rotation = targetRotation;
            transitionCamera.orthographic = endOrthographic;
            transitionCamera.fieldOfView = endFov;
            transitionCamera.orthographicSize = endOrthoSize;

            // 채집 카메라면 고정 포즈 재적용
            if (targetCamera == harvestCamera && harvestCinematicCameraController != null)
                harvestCinematicCameraController.SnapToDesiredPose();

            // 탐사 카메라면 최신 추적 포즈 반영
            if (targetCamera == explorationCamera && explorationFollowCameraController != null)
                explorationFollowCameraController.SnapToTarget();

            targetCamera.gameObject.SetActive(true);
            transitionCamera.gameObject.SetActive(false);

            DisposeCurrentTransitionToken();
        }

        /// <summary>현재 전환을 취소한다</summary>
        private void CancelCurrentTransition()
        {
            // 토큰 없으면 중단
            if (transitionCts == null)
                return;

            // 취소 요청
            if (!transitionCts.IsCancellationRequested)
                transitionCts.Cancel();

            // 자원 해제
            transitionCts.Dispose();
            transitionCts = null;
        }

        /// <summary>현재 전환 토큰을 정리한다</summary>
        private void DisposeCurrentTransitionToken()
        {
            // 토큰 없으면 중단
            if (transitionCts == null)
                return;

            transitionCts.Dispose();
            transitionCts = null;
        }
    }
}
