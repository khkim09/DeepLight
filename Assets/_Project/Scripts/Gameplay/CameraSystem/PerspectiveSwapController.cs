using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>탐사 카메라의 시작 위치에 따라 가이드 포인트를 유동적으로 회전시켜 조종석에 착지하는 연출 클래스</summary>
    public class PerspectiveSwapController : MonoBehaviour
    {
        [Header("Camera References")]
        [SerializeField] private Camera explorationCamera; // [시작점] 3인칭 탐사 카메라
        [SerializeField] private Camera harvestConsoleCamera; // [도착점] 1인칭 조종석 카메라
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 카메라 컨트롤러
        [SerializeField] private HarvestTransitionLetterboxPresenter transitionLetterboxPresenter; // 레터박스 UI

        [Header("Transition Route (Local Anchor)")]
        [Tooltip("모든 동적 좌표 계산의 기준이 될 잠수함 본체 Transform")]
        [SerializeField] private Transform submarineTransform;

        [Tooltip("[중간 가이드] 잠수함 정수리(위쪽)에 고정된 기본 가이드 Transform. 카메라 위치에 따라 동적으로 측면으로 회전합니다.")]
        [SerializeField] private Transform transitionGuide;

        [Tooltip("[도착점] 1인칭 조종석 시점과 완벽히 동일한 위치/회전을 가진 목표 Transform (CockpitViewAnchor 할당)")]
        [SerializeField] private Transform cockpitTransitionTarget;

        [Header("Transition Timing & Speed")]
        [SerializeField] private float transitionDuration = 1.2f; // 전체 궤도 이동 시간
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 전체 속도 보간 곡선 (S자 권장)

        [Tooltip("0.4 = 전체 시간의 40%만에 가이드에 도달. 수치가 낮을수록 조종석으로 내려가는 착지(Landing) 구간이 길고 여유로워집니다.")]
        [Range(0.1f, 0.9f)]
        [SerializeField] private float guideReachTime = 0.4f;

        private CancellationTokenSource transitionCts; // 연출 취소 토큰
        private bool isHarvestMode; // 채집 모드 상태

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

        private void Start()
        {
            isHarvestMode = false;
            SetExplorationViewImmediate();
        }

        /// <summary>채집 모드 진입 시 카메라 위치 기반의 동적 트랜지션을 시작한다</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            if (isHarvestMode) return;
            isHarvestMode = true;

            StartTransitionToHarvestAsync().Forget();
        }

        /// <summary>채집 모드 종료 시 역순 복귀 트랜지션을 시작한다</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            if (!isHarvestMode) return;
            isHarvestMode = false;

            StartTransitionToExplorationAsync().Forget();
        }

        /// <summary>탐사 카메라가 유동적 중간 가이드를 통과해 콕핏 앵커로 부드럽게 착지한다</summary>
        private async UniTaskVoid StartTransitionToHarvestAsync()
        {
            CancelCurrentTransition();
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCts.Token;

            if (explorationFollowCameraController != null)
            {
                explorationFollowCameraController.IsInputLocked = true;
                explorationFollowCameraController.IsIndependentAlignment = true;
            }

            // 1. 카메라의 현재 로컬 위치를 분석하여 어느 쪽으로 치우쳐 있는지 각도(Orbit Angle)를 계산합니다.
            Vector3 localCamPos = submarineTransform.InverseTransformPoint(explorationCamera.transform.position);
            float orbitAngle = Mathf.Atan2(localCamPos.x, -localCamPos.z) * Mathf.Rad2Deg;

            // 2. 에디터에 세팅된 '정수리 가이드'를 카메라가 있는 측면으로 완전히 굴려버립니다.
            Quaternion zRoll = Quaternion.Euler(0f, 0f, -orbitAngle);
            Vector3 dynMidLocalPos = zRoll * transitionGuide.localPosition;
            Quaternion dynMidLocalRot = zRoll * transitionGuide.localRotation;

            try
            {
                UniTask hideLetterboxTask = transitionLetterboxPresenter != null
                    ? transitionLetterboxPresenter.HideAsync() : UniTask.CompletedTask;

                UniTask cameraTask = PlayDynamicBezierTransitionAsync(
                    explorationCamera.transform.position, explorationCamera.transform.rotation,
                    dynMidLocalPos, dynMidLocalRot,
                    cockpitTransitionTarget.position, cockpitTransitionTarget.rotation,
                    guideReachTime, true, token);

                await UniTask.WhenAll(hideLetterboxTask, cameraTask);

                explorationCamera.gameObject.SetActive(false);
                harvestConsoleCamera.gameObject.SetActive(true);
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>조종석에서 유동적 중간 가이드를 통과해 탐사 카메라의 원위치로 복귀한다</summary>
        private async UniTaskVoid StartTransitionToExplorationAsync()
        {
            CancelCurrentTransition();
            transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            CancellationToken token = transitionCts.Token;

            // 카메라 스왑 준비
            explorationCamera.transform.position = cockpitTransitionTarget.position;
            explorationCamera.transform.rotation = cockpitTransitionTarget.rotation;

            harvestConsoleCamera.gameObject.SetActive(false);
            explorationCamera.gameObject.SetActive(true);

            if (explorationFollowCameraController != null)
                explorationFollowCameraController.SnapToTarget();

            Vector3 finalExplorationPos = explorationCamera.transform.position;
            Quaternion finalExplorationRot = explorationCamera.transform.rotation;

            // 복귀할 타겟 위치를 기준으로 동적 가이드 각도 재계산
            Vector3 localCamPos = submarineTransform.InverseTransformPoint(finalExplorationPos);
            float orbitAngle = Mathf.Atan2(localCamPos.x, -localCamPos.z) * Mathf.Rad2Deg;
            Quaternion zRoll = Quaternion.Euler(0f, 0f, -orbitAngle);

            Vector3 dynMidLocalPos = zRoll * transitionGuide.localPosition;
            Quaternion dynMidLocalRot = zRoll * transitionGuide.localRotation;

            explorationCamera.transform.position = cockpitTransitionTarget.position;
            explorationCamera.transform.rotation = cockpitTransitionTarget.rotation;

            try
            {
                UniTask showLetterboxTask = transitionLetterboxPresenter != null
                    ? transitionLetterboxPresenter.ShowAsync() : UniTask.CompletedTask;

                float reverseMidTime = 1f - guideReachTime;

                UniTask cameraTask = PlayDynamicBezierTransitionAsync(
                    cockpitTransitionTarget.position, cockpitTransitionTarget.rotation,
                    dynMidLocalPos, dynMidLocalRot,
                    finalExplorationPos, finalExplorationRot,
                    reverseMidTime, false, token);

                await UniTask.WhenAll(showLetterboxTask, cameraTask);

                if (explorationFollowCameraController != null)
                {
                    explorationFollowCameraController.IsIndependentAlignment = false;
                    explorationFollowCameraController.IsInputLocked = false;
                }

                UniTask hideLetterboxTask = transitionLetterboxPresenter != null
                    ? transitionLetterboxPresenter.HideAsync() : UniTask.CompletedTask;

                await hideLetterboxTask;
            }
            catch (OperationCanceledException) { }
        }

        /// <summary>동적으로 계산된 로컬 가이드를 바탕으로 3D 베지어 궤도 연출을 수행한다</summary>
        private async UniTask PlayDynamicBezierTransitionAsync(
            Vector3 startWorldPos, Quaternion startWorldRot,
            Vector3 dynMidLocalPos, Quaternion dynMidLocalRot,
            Vector3 endWorldPos, Quaternion endWorldRot,
            float midTimePoint, bool intoHarvest, CancellationToken token)
        {
            if (submarineTransform == null) return;

            // 1. 월드 좌표를 잠수함 로컬 좌표로 변환
            Vector3 lStartPos = submarineTransform.InverseTransformPoint(startWorldPos);
            Vector3 lEndPos = submarineTransform.InverseTransformPoint(endWorldPos);

            Quaternion lStartRot = Quaternion.Inverse(submarineTransform.rotation) * startWorldRot;
            Quaternion lEndRot = Quaternion.Inverse(submarineTransform.rotation) * endWorldRot;

            // 2. 가이드 포인트를 정확히 관통하게 만드는 2차 베지어 제어점(Control Point) 계산
            Vector3 lControlPos = CalculateQuadraticBezierControlPoint(lStartPos, dynMidLocalPos, lEndPos, midTimePoint);

            float elapsed = 0f;
            float duration = Mathf.Max(0.0001f, transitionDuration);

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                float curvedT = transitionCurve != null ? transitionCurve.Evaluate(normalized) : normalized;

                // 3. 로컬 2차 베지어 곡선 위치 도출
                // 🌟 문제의 X=0 강제 고정 코드를 삭제하여 측면(X축) 이동을 완벽하게 허용합니다!
                Vector3 currentLocalPos = EvaluateQuadraticBezier(lStartPos, lControlPos, lEndPos, curvedT);

                // 4. 로컬 회전 보간
                // 🌟 동적으로 Z축이 굴러간(Roll) 가이드를 사용하므로, 오일러 분해 없이 Slerp를 써야 꼬이지 않고 완벽한 3D 비행 궤도가 완성됩니다.
                Quaternion currentLocalRot;
                if (curvedT < midTimePoint)
                {
                    float t = curvedT / midTimePoint;
                    currentLocalRot = Quaternion.Slerp(lStartRot, dynMidLocalRot, t);
                }
                else
                {
                    float t = (curvedT - midTimePoint) / (1f - midTimePoint);
                    currentLocalRot = Quaternion.Slerp(dynMidLocalRot, lEndRot, t);
                }

                // 5. 실제 탐사 카메라의 트랜스폼 조작 (잠수함이 흔들려도 완벽 동기화)
                explorationCamera.transform.position = submarineTransform.TransformPoint(currentLocalPos);
                explorationCamera.transform.rotation = submarineTransform.rotation * currentLocalRot;

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 최종 위치 및 회전 스냅
            explorationCamera.transform.position = endWorldPos;
            explorationCamera.transform.rotation = endWorldRot;
        }

        /// <summary>2차 베지어 곡선이 특정 시점에 중간 지점을 통과하도록 제어점을 계산한다</summary>
        private Vector3 CalculateQuadraticBezierControlPoint(Vector3 start, Vector3 mid, Vector3 end, float t_mid)
        {
            float omt = 1f - t_mid;
            return (mid - (omt * omt * start) - (t_mid * t_mid * end)) / (2f * omt * t_mid);
        }

        /// <summary>시작, 제어점, 도착점을 기반으로 특정 시점의 2차 베지어 좌표를 반환한다</summary>
        private Vector3 EvaluateQuadraticBezier(Vector3 start, Vector3 c, Vector3 end, float t)
        {
            float omt = 1f - t;
            return (omt * omt * start) + (2f * omt * t * c) + (t * t * end);
        }

        /// <summary>시작 시 탐사 모드로 즉시 초기화한다</summary>
        private void SetExplorationViewImmediate()
        {
            if (explorationFollowCameraController != null) explorationFollowCameraController.SnapToTarget();
            if (explorationCamera != null) explorationCamera.gameObject.SetActive(true);
            if (harvestConsoleCamera != null) harvestConsoleCamera.gameObject.SetActive(false);
        }

        /// <summary>현재 씬에서 활성화된 메인 카메라를 반환한다</summary>
        private Camera GetCurrentlyActiveCamera(Camera fallbackCamera)
        {
            if (explorationCamera != null && explorationCamera.gameObject.activeSelf) return explorationCamera;
            if (harvestConsoleCamera != null && harvestConsoleCamera.gameObject.activeSelf) return harvestConsoleCamera;
            return fallbackCamera;
        }

        /// <summary>진행 중인 카메라 트랜지션을 안전하게 취소한다</summary>
        private void CancelCurrentTransition()
        {
            if (transitionCts != null)
            {
                transitionCts.Cancel();
                transitionCts.Dispose();
                transitionCts = null;
            }
        }
    }
}
