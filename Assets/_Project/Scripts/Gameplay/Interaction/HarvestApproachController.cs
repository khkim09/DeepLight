using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Gameplay.CameraSystem;
using Project.Gameplay.GameModes;
using Project.Gameplay.UserInput;
using UnityEngine;

namespace Project.Gameplay.Interaction
{
    /// <summary>채집 시작 전 잠수함을 타깃 근처로 이동시키고 카메라를 독립 정렬하는 연출 클래스</summary>
    public class HarvestApproachController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody controlledRigidbody; // 잠수함 물리 본체
        [SerializeField] private TestBedPlayerMover playerMover; // 조작 제어기
        [SerializeField] private ExplorationFollowCameraController explorationCameraController; // 탐사 카메라 컨트롤러
        [SerializeField] private CockpitTargetLookController cockpitTargetLookController; // 조종석 카메라 컨트롤러
        [SerializeField] private HarvestTransitionLetterboxPresenter transitionLetterboxPresenter; // 레터박스 연출기

        [Header("Approach Speed Settings")]
        [SerializeField] private float approachMoveSpeed = 8f; // 단위/초 (이동 속도)
        [SerializeField] private float approachRotationSpeed = 90f; // 도/초 (회전 속도)
        [SerializeField] private float minApproachDuration = 0.5f; // 최소 연출 보장 시간

        [Header("Approach Distance")]
        [SerializeField] private float stopDistancePadding = 0.35f; // 최종 접근 거리 보정
        [SerializeField] private bool preserveCurrentDepth = false; // 진입 시 현재 높이(Y) 유지 여부
        [SerializeField] private AnimationCurve approachCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 보간 곡선

        private bool isApproaching; // 연출 진행 상태

        /// <summary>현재 연출이 진행 중인지 여부를 반환한다</summary>
        public bool IsApproaching => isApproaching;

        private void Reset()
        {
            controlledRigidbody = GetComponent<Rigidbody>();
            playerMover = GetComponent<TestBedPlayerMover>();
        }

        /// <summary>잠수함을 타깃 존에 맞게 이동 및 정렬하고 채집 모드로 진입한다</summary>
        public async UniTask TryApproachAndEnterHarvestAsync(
            HarvestInteractionZone interactionZone,
            HarvestModeCoordinator harvestModeCoordinator)
        {
            if (isApproaching || interactionZone == null || harvestModeCoordinator == null) return;
            if (interactionZone.HarvestTarget == null || !interactionZone.HarvestTarget.IsAvailable) return;

            isApproaching = true;

            // 1. 유저 조작 즉시 잠금
            if (playerMover != null) playerMover.SetExternalControlLock(true);

            // 2. 목표 위치(Position) 및 회전(Rotation) 계산
            Vector3 targetCenter = interactionZone.GetTargetCenter();
            Vector3 currentPosition = controlledRigidbody != null ? controlledRigidbody.position : transform.position;

            Vector3 approachDirection = currentPosition - targetCenter;
            if (preserveCurrentDepth) approachDirection.y = 0f;
            if (approachDirection.sqrMagnitude <= 0.0001f) approachDirection = -transform.forward;

            approachDirection.Normalize();

            Camera refCam = explorationCameraController != null ? explorationCameraController.GetComponent<Camera>() : null;
            float framingDistance = interactionZone.EvaluateFramingDistance(refCam) + stopDistancePadding;

            // 최종 도착 위치
            Vector3 desiredPosition = targetCenter + approachDirection * framingDistance;
            if (preserveCurrentDepth) desiredPosition.y = currentPosition.y;

            // 선체가 바라볼 방향: 자신의 도착 위치에서 타겟을 바라보도록 정렬
            Vector3 lookDirection = targetCenter - desiredPosition;
            if (lookDirection.sqrMagnitude <= 0.0001f) lookDirection = transform.forward;
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

            // 가변 연출 시간 계산
            float distance = Vector3.Distance(currentPosition, desiredPosition);
            float angle = Quaternion.Angle(controlledRigidbody != null ? controlledRigidbody.rotation : transform.rotation, desiredRotation);
            float calculatedDuration = Mathf.Max(distance / approachMoveSpeed, angle / approachRotationSpeed);
            float finalDuration = Mathf.Max(calculatedDuration, minApproachDuration);

            // 조종석 앵커 카메라도 타겟을 바라볼 준비
            if (cockpitTargetLookController != null && interactionZone.TargetBehaviour != null)
                cockpitTargetLookController.BeginLookAt(interactionZone.TargetBehaviour.transform);

            CancellationToken token = this.GetCancellationTokenOnDestroy();

            // 3. 연출 병렬 실행 (레터박스 + 잠수함 정렬 + 카메라 독립 정렬)
            UniTask showLetterboxTask = transitionLetterboxPresenter != null
                ? transitionLetterboxPresenter.ShowAsync() : UniTask.CompletedTask;

            // 선체 본체 이동 및 회전 연출
            UniTask approachTask = PlayApproachAsync(desiredPosition, desiredRotation, finalDuration, token);

            // 카메라는 선체와 분리되어 타겟을 향해 독립적으로 회전
            UniTask alignCamTask = explorationCameraController != null
                ? explorationCameraController.AlignToTargetIndependentlyAsync(interactionZone.TargetBehaviour.transform, finalDuration, token)
                : UniTask.CompletedTask;

            await UniTask.WhenAll(showLetterboxTask, approachTask, alignCamTask);

            // 4. 연출 완료 후 채집 모드 진입 트리거 발동
            harvestModeCoordinator.TryEnterHarvestMode(interactionZone.HarvestTarget);
            isApproaching = false;
        }

        /// <summary>연출 종료 시 조종실 카메라 보정을 원래대로 되돌린다</summary>
        public void EndApproachLook()
        {
            if (cockpitTargetLookController != null) cockpitTargetLookController.EndLookAt();
            if (playerMover != null) playerMover.SetExternalControlLock(false);
            isApproaching = false;
        }

        /// <summary>렌더링 지연(Interpolation) 버그를 방지하고 선체를 화면에서 확실하게 회전시킨다</summary>
        private async UniTask PlayApproachAsync(Vector3 targetPosition, Quaternion targetRotation, float duration, CancellationToken token)
        {
            Vector3 startPosition = controlledRigidbody != null ? controlledRigidbody.position : transform.position;
            Quaternion startRotation = controlledRigidbody != null ? controlledRigidbody.rotation : transform.rotation;

            RigidbodyInterpolation originalInterpolation = RigidbodyInterpolation.None;

            if (controlledRigidbody != null)
            {
                // [핵심 버그 수정] 보간(Interpolation)이 켜져 있으면, 화면에서 메쉬 회전 업데이트를 무시해버리므로 연출 동안만 강제로 끕니다.
                originalInterpolation = controlledRigidbody.interpolation;
                controlledRigidbody.interpolation = RigidbodyInterpolation.None;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                elapsed += Time.fixedDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float curvedTime = approachCurve != null ? approachCurve.Evaluate(normalizedTime) : normalizedTime;

                if (controlledRigidbody != null)
                {
                    // 보간이 꺼져있으므로 MoveRotation이 즉각적으로 화면에 그려집니다.
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

            // 최종 위치/회전 오차 스냅 및 물리 원복
            if (controlledRigidbody != null)
            {
                controlledRigidbody.MovePosition(targetPosition);
                controlledRigidbody.MoveRotation(targetRotation);
                controlledRigidbody.linearVelocity = Vector3.zero;
                controlledRigidbody.angularVelocity = Vector3.zero;

                // 연출이 끝났으므로 떨림 방지용 보간(Interpolation)을 다시 켜줍니다.
                controlledRigidbody.interpolation = originalInterpolation;
            }
            else
            {
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
        }
    }
}
