// using System;
// using System.Threading;
// using Cysharp.Threading.Tasks;
// using Project.Core.Events;
// using UnityEngine;

// namespace Project.Gameplay.CameraSystem
// {
//     /// <summary>탐사 카메라와 회수 콘솔 카메라 사이 동적 궤도 전환 연출을 담당하는 클래스</summary>
//     public class PerspectiveSwapController : MonoBehaviour
//     {
//         [Header("Camera References")]
//         [SerializeField] private Camera explorationCamera;
//         [SerializeField] private Camera harvestConsoleCamera;
//         [SerializeField] private Camera transitionCamera;
//         [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController;
//         [SerializeField] private HarvestTransitionLetterboxPresenter transitionLetterboxPresenter;

//         [Header("Harvest Transition Route (Guides)")]
//         [SerializeField] private Transform harvestRoofGuide; // [가이드 3] 지붕
//         [SerializeField] private Transform harvestCanopyGuide; // [가이드 2] 캐노피

//         [Header("Transition Settings")]
//         [SerializeField] private float transitionDuration = 1.2f; // [Phase 2] 동적 궤도 이동 시간
//         [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

//         [Tooltip("곡선의 몇 % 지점(예: 0.7 = 캐노피)에서 회전이 조종석 정면으로 완전히 고정될지 설정합니다.")]
//         [Range(0.5f, 0.95f)]
//         [SerializeField] private float rotationLockPoint = 0.7f; // 2-1 구간 진입 시점 (회전 고정 시작점)

//         private CancellationTokenSource transitionCts;
//         private bool isHarvestMode;

//         private void OnEnable()
//         {
//             EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
//             EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
//         }

//         private void OnDisable()
//         {
//             EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
//             EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
//             CancelCurrentTransition();
//         }

//         private void Start()
//         {
//             isHarvestMode = false;
//             SetExplorationViewImmediate();
//         }

//         private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
//         {
//             if (isHarvestMode) return;
//             isHarvestMode = true;
//             StartTransitionToHarvestAsync().Forget();
//         }

//         private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
//         {
//             if (!isHarvestMode) return;
//             isHarvestMode = false;
//             StartTransitionToExplorationAsync().Forget();
//         }

//         /// <summary>[Phase 2] 레터박스가 걷히며 1인칭 조종석으로 동적 트랜지션을 수행한다</summary>
//         private async UniTaskVoid StartTransitionToHarvestAsync()
//         {
//             CancelCurrentTransition();
//             transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
//             CancellationToken token = transitionCts.Token;

//             // Phase 1에서 이미 정렬이 끝난 카메라 상태를 시작점으로 캐싱
//             Camera sourceCamera = GetCurrentlyActiveCamera(explorationCamera) ?? explorationCamera;
//             Vector3 fixedStartPos = sourceCamera.transform.position;
//             Quaternion fixedStartRot = sourceCamera.transform.rotation;

//             try
//             {
//                 var hideLetterboxTask = transitionLetterboxPresenter != null
//                     ? transitionLetterboxPresenter.HideAsync() : UniTask.CompletedTask;

//                 var cameraTask = PlayDynamicTransitionAsync(
//                     fixedStartPos, fixedStartRot,
//                     harvestRoofGuide, harvestCanopyGuide, harvestConsoleCamera.transform,
//                     true, token);

//                 await UniTask.WhenAll(hideLetterboxTask, cameraTask);
//             }
//             catch (OperationCanceledException) { }
//         }

//         /// <summary>탐사 뷰로 역순 전환을 수행한다</summary>
//         private async UniTaskVoid StartTransitionToExplorationAsync()
//         {
//             CancelCurrentTransition();
//             transitionCts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
//             CancellationToken token = transitionCts.Token;

//             Camera sourceCamera = GetCurrentlyActiveCamera(harvestConsoleCamera) ?? harvestConsoleCamera;
//             Vector3 fixedStartPos = sourceCamera.transform.position;
//             Quaternion fixedStartRot = sourceCamera.transform.rotation;

//             if (explorationFollowCameraController != null)
//                 explorationFollowCameraController.SnapToTarget();

//             try
//             {
//                 var showLetterboxTask = transitionLetterboxPresenter != null
//                     ? transitionLetterboxPresenter.ShowAsync() : UniTask.CompletedTask;

//                 var cameraTask = PlayDynamicTransitionAsync(
//                     fixedStartPos, fixedStartRot,
//                     harvestCanopyGuide, harvestRoofGuide, explorationCamera.transform,
//                     false, token);

//                 await UniTask.WhenAll(showLetterboxTask, cameraTask);

//                 if (explorationFollowCameraController != null)
//                     explorationFollowCameraController.IsInputLocked = false;

//                 var hideLetterboxTask = transitionLetterboxPresenter != null
//                     ? transitionLetterboxPresenter.HideAsync() : UniTask.CompletedTask;

//                 await hideLetterboxTask;
//             }
//             catch (OperationCanceledException) { }
//         }

//         /// <summary>베지어 이동 + 구간별 회전 고정 롤러코스터 연출</summary>
//         private async UniTask PlayDynamicTransitionAsync(Vector3 fixedStartPos, Quaternion fixedStartRot, Transform guide1, Transform guide2, Transform finalAnchor, bool intoHarvest, CancellationToken token)
//         {
//             if (transitionCamera == null || guide1 == null || guide2 == null || finalAnchor == null) return;

//             transitionCamera.transform.position = fixedStartPos;
//             transitionCamera.transform.rotation = fixedStartRot;
//             transitionCamera.gameObject.SetActive(true);
//             explorationCamera.gameObject.SetActive(false);
//             harvestConsoleCamera.gameObject.SetActive(false);

//             float elapsed = 0f;
//             float duration = Mathf.Max(0.0001f, transitionDuration);

//             while (elapsed < duration)
//             {
//                 token.ThrowIfCancellationRequested();

//                 elapsed += Time.deltaTime;
//                 float normalized = Mathf.Clamp01(elapsed / duration);
//                 float curved = transitionCurve != null ? transitionCurve.Evaluate(normalized) : normalized;

//                 Vector3 currentP1 = guide1.position;
//                 Vector3 currentP2 = guide2.position;
//                 Vector3 currentP3 = finalAnchor.position;
//                 Quaternion finalTargetRot = finalAnchor.rotation;

//                 // 1. 곡선 위치 계산 (4 Points Bezier)
//                 Vector3 currentPos = EvaluateCubicBezier(fixedStartPos, currentP1, currentP2, currentP3, curved);

//                 // 2. 하강 구간 회전 고정 계산 (핵심)
//                 float rotT;
//                 if (intoHarvest)
//                 {
//                     // 진입 시: 시작부터 rotationLockPoint(예: 0.7)까지만 회전 진행, 이후(조종석 하강)는 회전 멈춤
//                     rotT = Mathf.Clamp01(curved / rotationLockPoint);
//                 }
//                 else
//                 {
//                     // 복귀 시: 조종석 상승(1.0 - 0.7) 구간 동안은 회전 멈춤, 이후 지붕으로 갈 때 회전 복구
//                     float unlockPoint = 1f - rotationLockPoint;
//                     rotT = Mathf.Clamp01((curved - unlockPoint) / rotationLockPoint);
//                 }

//                 // 목표 회전값으로 부드럽게 Slerp 보간
//                 Quaternion currentRot = Quaternion.Slerp(fixedStartRot, finalTargetRot, rotT);

//                 transitionCamera.transform.position = currentPos;
//                 transitionCamera.transform.rotation = currentRot;

//                 await UniTask.Yield(PlayerLoopTiming.Update, token);
//             }

//             transitionCamera.gameObject.SetActive(false);

//             if (intoHarvest) harvestConsoleCamera.gameObject.SetActive(true);
//             else explorationCamera.gameObject.SetActive(true);
//         }

//         /// <summary>Cubic Bezier 곡선 위치 계산</summary>
//         private static Vector3 EvaluateCubicBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
//         {
//             float omt = 1f - t;
//             return omt * omt * omt * p0 + 3f * omt * omt * t * p1 + 3f * omt * t * t * p2 + t * t * t * p3;
//         }

//         private void SetExplorationViewImmediate()
//         {
//             if (explorationFollowCameraController != null) explorationFollowCameraController.SnapToTarget();
//             if (explorationCamera != null) explorationCamera.gameObject.SetActive(true);
//             if (harvestConsoleCamera != null) harvestConsoleCamera.gameObject.SetActive(false);
//             if (transitionCamera != null) transitionCamera.gameObject.SetActive(false);
//         }

//         private Camera GetCurrentlyActiveCamera(Camera fallbackCamera)
//         {
//             if (explorationCamera != null && explorationCamera.gameObject.activeSelf) return explorationCamera;
//             if (harvestConsoleCamera != null && harvestConsoleCamera.gameObject.activeSelf) return harvestConsoleCamera;
//             if (transitionCamera != null && transitionCamera.gameObject.activeSelf) return transitionCamera;
//             return fallbackCamera;
//         }

//         private void CancelCurrentTransition()
//         {
//             if (transitionCts != null)
//             {
//                 transitionCts.Cancel();
//                 transitionCts.Dispose();
//                 transitionCts = null;
//             }
//         }
//     }
// }


using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    public class PerspectiveSwapController : MonoBehaviour
    {
        [Header("Camera References")]
        [SerializeField] private Camera explorationCamera;
        [SerializeField] private Camera harvestConsoleCamera;
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController;
        [SerializeField] private HarvestTransitionLetterboxPresenter transitionLetterboxPresenter;

        private bool isHarvestMode;

        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        private void Start()
        {
            isHarvestMode = false;
            SetExplorationViewImmediate();
        }

        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            if (isHarvestMode) return;
            isHarvestMode = true;

            // 롤러코스터 생략, 레터박스 숨기고 즉시 조종석 카메라 켜기
            if (transitionLetterboxPresenter != null) transitionLetterboxPresenter.HideAsync().Forget();

            explorationCamera.gameObject.SetActive(false);
            harvestConsoleCamera.gameObject.SetActive(true);
        }

        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            if (!isHarvestMode) return;
            isHarvestMode = false;

            // 탐사 모드 복귀 시 즉각 처리
            harvestConsoleCamera.gameObject.SetActive(false);
            explorationCamera.gameObject.SetActive(true);

            if (explorationFollowCameraController != null)
            {
                explorationFollowCameraController.IsInputLocked = false;
                explorationFollowCameraController.IsIndependentAlignment = false; // 카메라 독립 연출 해제
                explorationFollowCameraController.SnapToTarget(); // 잠수함 뒤로 복귀
            }
        }

        private void SetExplorationViewImmediate()
        {
            if (explorationFollowCameraController != null) explorationFollowCameraController.SnapToTarget();
            if (explorationCamera != null) explorationCamera.gameObject.SetActive(true);
            if (harvestConsoleCamera != null) harvestConsoleCamera.gameObject.SetActive(false);
        }
    }
}
