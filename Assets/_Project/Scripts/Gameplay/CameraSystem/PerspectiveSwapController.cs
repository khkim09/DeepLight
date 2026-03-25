using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>탐사 카메라와 회수 콘솔 카메라 사이 전환을 담당하는 클래스</summary>
    public class PerspectiveSwapController : MonoBehaviour
    {
        [Header("Camera References")]
        [SerializeField] private Camera explorationCamera; // 탐사 카메라
        [SerializeField] private Camera harvestConsoleCamera; // 회수 콘솔 카메라
        [SerializeField] private Camera transitionCamera; // 전환 연출 카메라
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 추적 카메라 컨트롤러
        [SerializeField] private HarvestConsoleCameraController harvestConsoleCameraController; // 회수 콘솔 카메라 컨트롤러

        [Header("Transition")]
        [SerializeField] private float transitionDuration = 0.9f; // 전환 시간
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 전환 곡선

        private CancellationTokenSource transitionCts; // 전환 취소 토큰
        private bool isHarvestMode; // 현재 회수 콘솔 모드 여부

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
            CancelCurrentTransition();
        }

        /// <summary>초기 카메라 상태를 설정한다</summary>
        private void Start()
        {
            isHarvestMode = false;
            SetExplorationViewImmediate();
        }

        /// <summary>회수 콘솔 모드 진입 시 전환을 시작한다</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            if (isHarvestMode)
                return;

            isHarvestMode = true;
            StartTransitionToHarvestAsync().Forget();
        }

        /// <summary>회수 콘솔 모드 종료 시 전환을 시작한다</summary>
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

            if (harvestConsoleCamera != null)
                harvestConsoleCamera.gameObject.SetActive(false);

            if (transitionCamera != null)
                transitionCamera.gameObject.SetActive(false);
        }

        /// <summary>회수 콘솔 카메라 상태를 즉시 적용한다</summary>
        private void SetHarvestViewImmediate()
        {
            if (harvestConsoleCameraController != null)
                harvestConsoleCameraController.SnapToDesiredPose();

            if (explorationCamera != null)
                explorationCamera.gameObject.SetActive(false);

            if (harvestConsoleCamera != null)
                harvestConsoleCamera.gameObject.SetActive(true);

            if (transitionCamera != null)
                transitionCamera.gameObject.SetActive(false);
        }

        /// <summary>회수 콘솔 카메라로 전환을 시작한다</summary>
        private async UniTaskVoid StartTransitionToHarvestAsync()
        {
            if (transitionCamera == null || harvestConsoleCamera == null)
            {
                SetHarvestViewImmediate();
                return;
            }

            CancelCurrentTransition();

            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCts.Token;

            Camera sourceCamera = GetCurrentlyActiveCamera(explorationCamera);
            if (sourceCamera == null)
                sourceCamera = explorationCamera;

            Vector3 targetPosition = harvestConsoleCamera.transform.position;
            Quaternion targetRotation = harvestConsoleCamera.transform.rotation;

            if (harvestConsoleCameraController != null)
                harvestConsoleCameraController.GetDesiredPose(out targetPosition, out targetRotation);

            try
            {
                await PlayTransitionAsync(sourceCamera, targetPosition, targetRotation, harvestConsoleCamera, token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>탐사 카메라로 전환을 시작한다</summary>
        private async UniTaskVoid StartTransitionToExplorationAsync()
        {
            if (transitionCamera == null || explorationCamera == null)
            {
                SetExplorationViewImmediate();
                return;
            }

            CancelCurrentTransition();

            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCts.Token;

            Camera sourceCamera = GetCurrentlyActiveCamera(harvestConsoleCamera);
            if (sourceCamera == null)
                sourceCamera = harvestConsoleCamera;

            if (explorationFollowCameraController != null)
                explorationFollowCameraController.SnapToTarget();

            Vector3 targetPosition = explorationCamera.transform.position;
            Quaternion targetRotation = explorationCamera.transform.rotation;

            try
            {
                await PlayTransitionAsync(sourceCamera, targetPosition, targetRotation, explorationCamera, token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>소스 카메라에서 목표 포즈로 전환 연출을 재생한다</summary>
        private async UniTask PlayTransitionAsync(
            Camera sourceCamera,
            Vector3 targetPosition,
            Quaternion targetRotation,
            Camera finalCamera,
            CancellationToken token)
        {
            if (sourceCamera == null || transitionCamera == null || finalCamera == null)
                return;

            transitionCamera.gameObject.SetActive(true);
            transitionCamera.transform.position = sourceCamera.transform.position;
            transitionCamera.transform.rotation = sourceCamera.transform.rotation;

            if (explorationCamera != null)
                explorationCamera.gameObject.SetActive(false);

            if (harvestConsoleCamera != null)
                harvestConsoleCamera.gameObject.SetActive(false);

            float elapsed = 0f;

            while (elapsed < transitionDuration)
            {
                token.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, transitionDuration));
                float curved = transitionCurve != null ? transitionCurve.Evaluate(normalized) : normalized;

                transitionCamera.transform.position = Vector3.Lerp(sourceCamera.transform.position, targetPosition, curved);
                transitionCamera.transform.rotation = Quaternion.Slerp(sourceCamera.transform.rotation, targetRotation, curved);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            transitionCamera.transform.position = targetPosition;
            transitionCamera.transform.rotation = targetRotation;

            finalCamera.gameObject.SetActive(true);
            transitionCamera.gameObject.SetActive(false);
        }

        /// <summary>현재 활성화된 카메라를 반환한다</summary>
        private Camera GetCurrentlyActiveCamera(Camera fallbackCamera)
        {
            if (explorationCamera != null && explorationCamera.gameObject.activeSelf)
                return explorationCamera;

            if (harvestConsoleCamera != null && harvestConsoleCamera.gameObject.activeSelf)
                return harvestConsoleCamera;

            if (transitionCamera != null && transitionCamera.gameObject.activeSelf)
                return transitionCamera;

            return fallbackCamera;
        }

        /// <summary>진행 중인 전환을 취소한다</summary>
        private void CancelCurrentTransition()
        {
            if (transitionCts == null)
                return;

            transitionCts.Cancel();
            transitionCts.Dispose();
            transitionCts = null;
        }
    }
}
