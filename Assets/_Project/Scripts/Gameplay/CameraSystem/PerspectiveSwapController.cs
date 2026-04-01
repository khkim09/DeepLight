using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Data.CameraSystem;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>탐사 카메라에서 조종석 카메라로 곡선 전환을 처리하는 클래스</summary>
    public class PerspectiveSwapController : MonoBehaviour
    {
        [Header("Camera References")]
        [SerializeField] private Camera explorationCamera; // 탐사 카메라
        [SerializeField] private Camera harvestConsoleCamera; // 조종석 카메라
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 카메라 제어기
        [SerializeField] private HarvestTransitionLetterboxPresenter transitionLetterboxPresenter; // 레터박스 연출기

        [Header("Transition Route (Local Anchor)")]
        [SerializeField] private Transform submarineTransform; // 잠수함 기준 축
        [SerializeField] private Transform transitionGuide; // 중간 가이드 포인트
        [SerializeField] private Transform cockpitTransitionTarget; // 최종 조종석 도착 포인트

        [Header("Tuning")]
        [SerializeField] private CameraTransitionTuningSO tuning; // 카메라 전환 튜닝

        private CancellationTokenSource transitionCts; // 진행 중 전환 취소용
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
            CancelCurrentTransition();
        }

        /// <summary>초기 카메라 상태를 탐사 모드로 맞춘다</summary>
        private void Start()
        {
            isHarvestMode = false;
            SetExplorationViewImmediate();
        }

        /// <summary>채집 모드 진입 전환을 시작한다</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            if (isHarvestMode)
                return;

            isHarvestMode = true;
            StartTransitionToHarvestAsync().Forget();
        }

        /// <summary>탐사 모드 복귀 전환을 시작한다</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            if (!isHarvestMode)
                return;

            isHarvestMode = false;
            StartTransitionToExplorationAsync().Forget();
        }

        /// <summary>탐사에서 조종석으로 전환한다</summary>
        private async UniTaskVoid StartTransitionToHarvestAsync()
        {
            if (tuning == null || submarineTransform == null || transitionGuide == null || cockpitTransitionTarget == null)
                return;

            CancelCurrentTransition();
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCts.Token;

            if (explorationFollowCameraController != null)
            {
                // 전환 중에는 카메라 입력과 자동 갱신을 잠근다.
                explorationFollowCameraController.IsInputLocked = true;
                explorationFollowCameraController.IsIndependentAlignment = true;
            }

            // 현재 탐사 카메라 위치 기준으로 가이드의 회전 방향을 유동적으로 계산한다.
            Vector3 localCamPos = submarineTransform.InverseTransformPoint(explorationCamera.transform.position);

            // Z축 대신 X축(측면)과 Y축(상하)을 사용하여 카메라의 진입 방향을 파악합니다.
            Vector2 xyDirection = new Vector2(localCamPos.x, localCamPos.y);
            float rollAngle = 0f;
            if (xyDirection.sqrMagnitude > 0.001f)
            {
                rollAngle = Vector2.SignedAngle(Vector2.up, xyDirection);
            }

            Quaternion zRoll = Quaternion.Euler(0f, 0f, rollAngle);
            Vector3 dynamicMidLocalPos = zRoll * transitionGuide.localPosition;

            Quaternion rolledRot = zRoll * transitionGuide.localRotation;
            Vector3 lookForward = rolledRot * Vector3.forward;

            // 방향(Forward)은 zRoll이 적용된 값을 그대로 사용하여 Pitch/Yaw 자연스러운 전환을 유지하되,
            // 정수리 방향(Up)을 잠수함의 로컬 Y축(Vector3.up)으로 강제 고정하여 Roll 현상을 방지합니다.
            Quaternion dynamicMidLocalRot = Quaternion.LookRotation(lookForward, Vector3.up);

            try
            {
                UniTask hideLetterboxTask = transitionLetterboxPresenter != null
                    ? transitionLetterboxPresenter.HideAsync()
                    : UniTask.CompletedTask;

                UniTask cameraTask = PlayDynamicBezierTransitionAsync(
                    explorationCamera.transform.position,
                    explorationCamera.transform.rotation,
                    dynamicMidLocalPos,
                    dynamicMidLocalRot,
                    cockpitTransitionTarget.position,
                    cockpitTransitionTarget.rotation,
                    tuning.GuideReachTime,
                    token);

                await UniTask.WhenAll(hideLetterboxTask, cameraTask);

                explorationCamera.gameObject.SetActive(false);
                harvestConsoleCamera.gameObject.SetActive(true);

                // Harvest 카메라 전환 완료 후 HUD 활성화 이벤트
                EventBus.Publish(new HarvestCameraTransitionCompletedEvent());
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>조종석에서 탐사로 복귀 전환한다</summary>
        private async UniTaskVoid StartTransitionToExplorationAsync()
        {
            if (tuning == null || submarineTransform == null || transitionGuide == null || cockpitTransitionTarget == null)
                return;

            CancelCurrentTransition();
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCts.Token;

            // 복귀 전 현재 카메라를 조종석 시작점으로 맞춘다.
            explorationCamera.transform.position = cockpitTransitionTarget.position;
            explorationCamera.transform.rotation = cockpitTransitionTarget.rotation;

            harvestConsoleCamera.gameObject.SetActive(false);
            explorationCamera.gameObject.SetActive(true);

            if (explorationFollowCameraController != null)
                explorationFollowCameraController.SnapToTarget();

            Vector3 finalExplorationPos = explorationCamera.transform.position;
            Quaternion finalExplorationRot = explorationCamera.transform.rotation;

            Vector3 localCamPos = submarineTransform.InverseTransformPoint(finalExplorationPos);

            // Z축 대신 X축(측면)과 Y축(상하)을 사용하여 카메라의 진입 방향을 파악합니다.
            Vector2 xyDirection = new Vector2(localCamPos.x, localCamPos.y);
            float rollAngle = 0f;
            if (xyDirection.sqrMagnitude > 0.001f)
            {
                rollAngle = Vector2.SignedAngle(Vector2.up, xyDirection);
            }

            Quaternion zRoll = Quaternion.Euler(0f, 0f, rollAngle);

            Vector3 dynamicMidLocalPos = zRoll * transitionGuide.localPosition;
            Quaternion rolledRot = zRoll * transitionGuide.localRotation;
            Vector3 lookForward = rolledRot * Vector3.forward;
            Quaternion dynamicMidLocalRot = Quaternion.LookRotation(lookForward, Vector3.up);

            // 역방향 전환을 위해 카메라를 다시 조종석 출발 위치로 되돌린다.
            explorationCamera.transform.position = cockpitTransitionTarget.position;
            explorationCamera.transform.rotation = cockpitTransitionTarget.rotation;

            try
            {
                UniTask showLetterboxTask = transitionLetterboxPresenter != null
                    ? transitionLetterboxPresenter.ShowAsync()
                    : UniTask.CompletedTask;

                float reverseMidTime = 1f - tuning.GuideReachTime;

                UniTask cameraTask = PlayDynamicBezierTransitionAsync(
                    cockpitTransitionTarget.position,
                    cockpitTransitionTarget.rotation,
                    dynamicMidLocalPos,
                    dynamicMidLocalRot,
                    finalExplorationPos,
                    finalExplorationRot,
                    reverseMidTime,
                    token);

                await UniTask.WhenAll(showLetterboxTask, cameraTask);

                if (explorationFollowCameraController != null)
                {
                    explorationFollowCameraController.IsIndependentAlignment = false;
                    explorationFollowCameraController.IsInputLocked = false;
                }

                UniTask hideLetterboxTask = transitionLetterboxPresenter != null
                    ? transitionLetterboxPresenter.HideAsync()
                    : UniTask.CompletedTask;

                await hideLetterboxTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        /// <summary>잠수함 로컬 기준 2차 베지어 곡선으로 카메라를 전환한다</summary>
        private async UniTask PlayDynamicBezierTransitionAsync(
            Vector3 startWorldPos,
            Quaternion startWorldRot,
            Vector3 dynamicMidLocalPos,
            Quaternion dynamicMidLocalRot,
            Vector3 endWorldPos,
            Quaternion endWorldRot,
            float midTimePoint,
            CancellationToken token)
        {
            Vector3 localStartPos = submarineTransform.InverseTransformPoint(startWorldPos);
            Vector3 localEndPos = submarineTransform.InverseTransformPoint(endWorldPos);

            Quaternion localStartRot = Quaternion.Inverse(submarineTransform.rotation) * startWorldRot;
            Quaternion localEndRot = Quaternion.Inverse(submarineTransform.rotation) * endWorldRot;

            // 지정한 중간 시간 비율에 맞게 control point를 역산한다.
            Vector3 localControlPos = CalculateQuadraticBezierControlPoint(
                localStartPos,
                dynamicMidLocalPos,
                localEndPos,
                midTimePoint);

            float elapsed = 0f;
            float duration = Mathf.Max(0.0001f, tuning.TransitionDuration);

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float curvedTime = tuning.TransitionCurve != null
                    ? tuning.TransitionCurve.Evaluate(normalizedTime)
                    : normalizedTime;

                Vector3 currentLocalPos = EvaluateQuadraticBezier(localStartPos, localControlPos, localEndPos, curvedTime);

                Quaternion currentLocalRot;
                if (curvedTime < midTimePoint)
                {
                    float t = curvedTime / midTimePoint;
                    currentLocalRot = Quaternion.Slerp(localStartRot, dynamicMidLocalRot, t);
                }
                else
                {
                    float t = (curvedTime - midTimePoint) / (1f - midTimePoint);
                    currentLocalRot = Quaternion.Slerp(dynamicMidLocalRot, localEndRot, t);
                }

                explorationCamera.transform.position = submarineTransform.TransformPoint(currentLocalPos);
                explorationCamera.transform.rotation = submarineTransform.rotation * currentLocalRot;

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            explorationCamera.transform.position = endWorldPos;
            explorationCamera.transform.rotation = endWorldRot;
        }

        /// <summary>중간 지점을 지나도록 2차 베지어 control point를 계산한다</summary>
        private Vector3 CalculateQuadraticBezierControlPoint(Vector3 start, Vector3 mid, Vector3 end, float tMid)
        {
            float oneMinusT = 1f - tMid;
            return (mid - (oneMinusT * oneMinusT * start) - (tMid * tMid * end)) / (2f * oneMinusT * tMid);
        }

        /// <summary>2차 베지어 곡선 위치를 계산한다</summary>
        private Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            float oneMinusT = 1f - t;
            return (oneMinusT * oneMinusT * start) + (2f * oneMinusT * t * control) + (t * t * end);
        }

        /// <summary>탐사 카메라를 즉시 기본 상태로 맞춘다</summary>
        private void SetExplorationViewImmediate()
        {
            if (explorationFollowCameraController != null)
                explorationFollowCameraController.SnapToTarget();

            if (explorationCamera != null)
                explorationCamera.gameObject.SetActive(true);

            if (harvestConsoleCamera != null)
                harvestConsoleCamera.gameObject.SetActive(false);
        }

        /// <summary>진행 중 전환 작업을 취소한다</summary>
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
