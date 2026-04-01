using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Data.Harvest;
using Project.Gameplay.CameraSystem;
using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using Project.Gameplay.UserInput;
using UnityEngine;

namespace Project.Gameplay.Interaction
{
    /// <summary>SO 기반 자동 접근 세팅과 런타임 보정값을 사용해 채집 진입 연출을 수행하는 클래스이다.</summary>
    public class HarvestApproachController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody controlledRigidbody; // 잠수함 강체
        [SerializeField] private TestBedPlayerMover playerMover; // 플레이어 조작기
        [SerializeField] private ExplorationFollowCameraController explorationCameraController; // 탐사 카메라
        [SerializeField] private CockpitTargetLookController cockpitTargetLookController; // 조종실 바라보기 보정기
        [SerializeField] private HarvestTransitionLetterboxPresenter transitionLetterboxPresenter; // 레터박스 연출기

        [Header("Tuning")]
        [SerializeField] private HarvestApproachTuningSO approachTuning; // 자동 접근 기본값 SO
        [SerializeField] private HarvestApproachUpgradeOverrides runtimeUpgradeOverrides = default; // 업그레이드 런타임 보정값

        private HarvestApproachRuntimeSettings runtimeSettings; // 실제 적용 수치
        private bool isApproaching; // 연출 진행 여부

        /// <summary>현재 자동 접근 연출 진행 여부를 반환한다.</summary>
        public bool IsApproaching => isApproaching;

        /// <summary>필수 참조를 자동 연결한다.</summary>
        private void Reset()
        {
            controlledRigidbody = GetComponent<Rigidbody>();
            playerMover = GetComponent<TestBedPlayerMover>();
        }

        /// <summary>런타임 세팅을 초기화한다.</summary>
        private void Awake()
        {
            RebuildRuntimeSettings();
        }

        /// <summary>에디터 값 변경 시 런타임 세팅을 다시 계산한다.</summary>
        private void OnValidate()
        {
            RebuildRuntimeSettings();
        }

        /// <summary>타깃 존까지 접근 연출을 수행한 뒤 Harvest 진입을 시도한다.</summary>
        public async UniTask TryApproachAndEnterHarvestAsync(
            HarvestInteractionZone interactionZone,
            HarvestModeCoordinator harvestModeCoordinator)
        {
            if (approachTuning == null)
                return;

            if (isApproaching || interactionZone == null || harvestModeCoordinator == null)
                return;

            if (interactionZone.HarvestTarget == null || !interactionZone.HarvestTarget.IsAvailable)
                return;

            // 잠긴 대상은 approach 연출 자체를 시작하지 않는다.
            if (interactionZone.TargetBehaviour != null && !interactionZone.TargetBehaviour.IsHarvestUnlocked)
            {
                Debug.LogWarning(
                    $"[HarvestApproachController] Harvest blocked before approach: {interactionZone.TargetBehaviour.GetUnavailableReason()}");
                return;
            }

            isApproaching = true;

            // 접근 연출 중에는 플레이어 입력을 잠근다.
            if (playerMover != null)
                playerMover.SetExternalControlLock(true);

            try
            {
                Vector3 targetCenter = interactionZone.GetTargetCenter();
                Vector3 currentPosition = controlledRigidbody != null ? controlledRigidbody.position : transform.position;

                // 타깃 반대 방향에서 접근할 최종 위치를 계산한다.
                Vector3 approachDirection = currentPosition - targetCenter;
                if (runtimeSettings.PreserveCurrentDepth)
                    approachDirection.y = 0f;

                if (approachDirection.sqrMagnitude <= 0.0001f)
                    approachDirection = -transform.forward;

                approachDirection.Normalize();

                Camera refCam = explorationCameraController != null ? explorationCameraController.GetComponent<Camera>() : null;
                float framingDistance = interactionZone.EvaluateFramingDistance(refCam) + runtimeSettings.StopDistancePadding;

                Vector3 desiredPosition = targetCenter + approachDirection * framingDistance;
                if (runtimeSettings.PreserveCurrentDepth)
                    desiredPosition.y = currentPosition.y;

                // 잠수정이 최종 위치에서 타깃을 바라보게 회전을 계산한다.
                Vector3 lookDirection = targetCenter - desiredPosition;
                if (lookDirection.sqrMagnitude <= 0.0001f)
                    lookDirection = transform.forward;

                Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

                float distance = Vector3.Distance(currentPosition, desiredPosition);
                float angle = Quaternion.Angle(
                    controlledRigidbody != null ? controlledRigidbody.rotation : transform.rotation,
                    desiredRotation);

                // 이동 거리와 회전 각도 중 더 오래 걸리는 쪽을 연출 시간 기준으로 사용한다.
                float calculatedDuration = Mathf.Max(
                    distance / runtimeSettings.ApproachMoveSpeed,
                    angle / runtimeSettings.ApproachRotationSpeed);

                float finalDuration = Mathf.Max(calculatedDuration, runtimeSettings.MinApproachDuration);

                // 조종실 타깃 보정도 미리 시작한다.
                if (cockpitTargetLookController != null && interactionZone.TargetBehaviour != null)
                    cockpitTargetLookController.BeginLookAt(interactionZone.TargetBehaviour.transform);

                CancellationToken token = this.GetCancellationTokenOnDestroy();

                UniTask showLetterboxTask = transitionLetterboxPresenter != null
                    ? transitionLetterboxPresenter.ShowAsync()
                    : UniTask.CompletedTask;

                UniTask approachTask = PlayApproachAsync(desiredPosition, desiredRotation, finalDuration, token);

                UniTask alignCamTask = explorationCameraController != null
                    ? explorationCameraController.AlignToTargetIndependentlyAsync(
                        interactionZone.TargetBehaviour.transform,
                        finalDuration,
                        token)
                    : UniTask.CompletedTask;

                // 레터박스, 선체 접근, 카메라 독립 정렬을 병렬 실행한다.
                await UniTask.WhenAll(showLetterboxTask, approachTask, alignCamTask);

                bool entered = harvestModeCoordinator.TryEnterHarvestMode(interactionZone.HarvestTarget);

                // 진입이 실패하면 잠금과 보정을 즉시 해제한다.
                if (!entered)
                    EndApproachLook();
            }
            finally
            {
                // 아직 approach 상태가 살아 있고, 실제 Harvest 세션 진입이 안 된 경우만 잠금 해제한다.
                if (isApproaching)
                {
                    bool enteredHarvest =
                        harvestModeCoordinator != null &&
                        harvestModeCoordinator.HarvestModeSession != null &&
                        harvestModeCoordinator.HarvestModeSession.HasTarget;

                    if (!enteredHarvest)
                        EndApproachLook();
                }

                isApproaching = false;
            }
        }

        /// <summary>접근 연출 종료 시 시점 보정과 입력 잠금을 해제한다.</summary>
        public void EndApproachLook()
        {
            if (cockpitTargetLookController != null)
                cockpitTargetLookController.EndLookAt();

            if (playerMover != null)
                playerMover.SetExternalControlLock(false);

            isApproaching = false;
        }

        /// <summary>업그레이드 시스템이 자동 접근 보정값을 갱신할 때 호출한다.</summary>
        public void ApplyUpgradeOverrides(HarvestApproachUpgradeOverrides newOverrides)
        {
            runtimeUpgradeOverrides = newOverrides;
            RebuildRuntimeSettings();
        }

        /// <summary>선체를 목표 위치와 회전으로 보간 이동시킨다.</summary>
        private async UniTask PlayApproachAsync(
            Vector3 targetPosition,
            Quaternion targetRotation,
            float duration,
            CancellationToken token)
        {
            Vector3 startPosition = controlledRigidbody != null ? controlledRigidbody.position : transform.position;
            Quaternion startRotation = controlledRigidbody != null ? controlledRigidbody.rotation : transform.rotation;

            RigidbodyInterpolation originalInterpolation = RigidbodyInterpolation.None;

            if (controlledRigidbody != null)
            {
                // 접근 연출 중에는 보간을 잠시 끄고 즉각 반영되게 만든다.
                originalInterpolation = controlledRigidbody.interpolation;
                controlledRigidbody.interpolation = RigidbodyInterpolation.None;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                elapsed += Time.fixedDeltaTime;

                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float curvedTime = runtimeSettings.ApproachCurve != null
                    ? runtimeSettings.ApproachCurve.Evaluate(normalizedTime)
                    : normalizedTime;

                if (controlledRigidbody != null)
                {
                    controlledRigidbody.MovePosition(Vector3.Lerp(startPosition, targetPosition, curvedTime));
                    controlledRigidbody.MoveRotation(Quaternion.Slerp(startRotation, targetRotation, curvedTime));
                }
                else
                {
                    transform.position = Vector3.Lerp(startPosition, targetPosition, curvedTime);
                    transform.rotation = Quaternion.Slerp(startRotation, targetRotation, curvedTime);
                }

                await UniTask.Yield(PlayerLoopTiming.FixedUpdate, token);
            }

            if (controlledRigidbody != null)
            {
                controlledRigidbody.MovePosition(targetPosition);
                controlledRigidbody.MoveRotation(targetRotation);
                controlledRigidbody.linearVelocity = Vector3.zero;
                controlledRigidbody.angularVelocity = Vector3.zero;
                controlledRigidbody.interpolation = originalInterpolation;
            }
            else
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
        }

        /// <summary>SO와 런타임 보정값을 합쳐 실제 접근 세팅을 다시 계산한다.</summary>
        private void RebuildRuntimeSettings()
        {
            if (approachTuning == null)
                return;

            runtimeSettings = approachTuning.BuildRuntimeSettings(runtimeUpgradeOverrides);
        }
    }
}
