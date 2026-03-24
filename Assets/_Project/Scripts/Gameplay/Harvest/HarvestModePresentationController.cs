using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Gameplay.GameModes;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>채집 모드에서 플레이어와 대상의 표준 표시 배치를 담당하는 클래스</summary>
    public class HarvestModePresentationController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform playerTransform; // 플레이어 기준 Transform
        [SerializeField] private Camera harvestCamera; // 채집 카메라
        [SerializeField] private LayerMask targetLayerMask; // 일반 타겟 레이어 마스크
        [SerializeField] private LayerMask harvestingLayerMask; // 현재 채집 타겟 레이어 마스크

        [Header("Presentation Offsets")]
        [SerializeField] private Vector3 playerPresentationOffset = Vector3.zero; // 플레이어 표시 위치 보정
        [SerializeField] private Vector3 targetPresentationOffset = new Vector3(0f, 0f, 5.5f); // 플레이어 기준 대상 표시 위치
        [SerializeField] private Vector3 targetPresentationEulerAngles = Vector3.zero; // 대상 표시 회전
        [SerializeField] private bool restoreTargetOnExit = true; // 종료 시 원위치 복귀 여부

        private HarvestModeSession harvestModeSession; // 현재 채집 세션
        private Transform currentPresentedTarget; // 현재 표시 중 대상
        private Vector3 originalPlayerPosition; // 원래 플레이어 위치
        private Quaternion originalPlayerRotation; // 원래 플레이어 회전
        private Vector3 originalTargetPosition; // 원래 대상 위치
        private Quaternion originalTargetRotation; // 원래 대상 회전
        private bool hasCachedPose; // 포즈 캐시 여부
        private int targetLayer = -1; // 일반 타겟 레이어 인덱스
        private int harvestingLayer = -1; // 현재 채집 타겟 레이어 인덱스
        private int originalHarvestCameraMask; // 채집 카메라 원래 컬링 마스크

        /// <summary>채집 세션과 플레이어 기준 대상을 주입한다</summary>
        public void Initialize(HarvestModeSession newHarvestModeSession, Transform newPlayerTransform)
        {
            // 세션 저장
            harvestModeSession = newHarvestModeSession;

            // 플레이어 저장
            playerTransform = newPlayerTransform;
        }

        /// <summary>레이어 인덱스와 기본 카메라 마스크를 캐싱한다</summary>
        private void Awake()
        {
            // 일반 타겟 레이어 캐싱
            targetLayer = GetSingleLayerIndex(targetLayerMask);

            // 채집 중 레이어 캐싱
            harvestingLayer = GetSingleLayerIndex(harvestingLayerMask);

            // 채집 카메라 마스크 캐싱
            if (harvestCamera != null)
                originalHarvestCameraMask = harvestCamera.cullingMask;
        }

        /// <summary>채집 진입용 표시 전환을 비동기로 실행한다</summary>
        public async UniTask EnterPresentationAsync(float duration, CancellationToken token)
        {
            // 세션 없으면 중단
            if (harvestModeSession == null)
                return;

            // 플레이어 없으면 중단
            if (playerTransform == null)
                return;

            // 현재 대상 없으면 중단
            if (harvestModeSession.CurrentTarget == null)
                return;

            // 대상이 Component가 아니면 중단
            if (harvestModeSession.CurrentTarget is not Component targetComponent)
                return;

            Transform targetTransform = targetComponent.transform; // 대상 Transform 참조
            if (targetTransform == null)
                return;

            // 원래 포즈 캐시
            currentPresentedTarget = targetTransform;
            originalPlayerPosition = playerTransform.position;
            originalPlayerRotation = playerTransform.rotation;
            originalTargetPosition = targetTransform.position;
            originalTargetRotation = targetTransform.rotation;
            hasCachedPose = true;

            // 다른 타겟들은 일반 타겟 레이어 유지
            SetAllTargetsToDefaultLayer();

            // 현재 타겟만 채집 중 레이어 적용
            HarvestTargetBehaviour currentTargetBehaviour = targetTransform.GetComponent<HarvestTargetBehaviour>();
            if (currentTargetBehaviour != null)
                currentTargetBehaviour.ApplyHarvestingLayer();

            // 채집 카메라 컬링 마스크 갱신
            ApplyHarvestCameraCullingMask();

            // 목표 포즈 계산
            Vector3 targetPlayerPosition = originalPlayerPosition + playerPresentationOffset; // 플레이어 목표 위치
            Quaternion targetPlayerRotation = originalPlayerRotation; // 플레이어 목표 회전 유지
            Vector3 targetTargetPosition = targetPlayerPosition + originalPlayerRotation * targetPresentationOffset; // 대상 목표 위치
            Quaternion targetTargetRotation = Quaternion.Euler(targetPresentationEulerAngles); // 대상 목표 회전

            // 시간 0 이하면 즉시 반영
            if (duration <= 0f)
            {
                playerTransform.position = targetPlayerPosition;
                playerTransform.rotation = targetPlayerRotation;
                targetTransform.position = targetTargetPosition;
                targetTransform.rotation = targetTargetRotation;
                return;
            }

            float elapsed = 0f; // 누적 시간

            // 동시 보간
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = Mathf.SmoothStep(0f, 1f, t);

                // 플레이어 보간
                playerTransform.position = Vector3.Lerp(originalPlayerPosition, targetPlayerPosition, easedT);
                playerTransform.rotation = Quaternion.Slerp(originalPlayerRotation, targetPlayerRotation, easedT);

                // 대상 보간
                targetTransform.position = Vector3.Lerp(originalTargetPosition, targetTargetPosition, easedT);
                targetTransform.rotation = Quaternion.Slerp(originalTargetRotation, targetTargetRotation, easedT);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 최종 보정
            playerTransform.position = targetPlayerPosition;
            playerTransform.rotation = targetPlayerRotation;
            targetTransform.position = targetTargetPosition;
            targetTransform.rotation = targetTargetRotation;
        }

        /// <summary>채집 종료용 표시 복귀를 비동기로 실행한다</summary>
        public async UniTask ExitPresentationAsync(float duration, CancellationToken token)
        {
            // 카메라 컬링 마스크 복구
            RestoreHarvestCameraCullingMask();

            // 복귀 옵션 꺼져 있으면 레이어만 복구
            if (!restoreTargetOnExit)
            {
                RestoreCurrentTargetLayer();
                ClearCachedPose();
                return;
            }

            // 캐시 없으면 현재 타겟 레이어만 복구
            if (!hasCachedPose)
            {
                RestoreCurrentTargetLayer();
                return;
            }

            // 대상 없으면 정리 후 중단
            if (currentPresentedTarget == null || playerTransform == null)
            {
                ClearCachedPose();
                return;
            }

            Vector3 startPlayerPosition = playerTransform.position; // 현재 플레이어 위치
            Quaternion startPlayerRotation = playerTransform.rotation; // 현재 플레이어 회전
            Vector3 startTargetPosition = currentPresentedTarget.position; // 현재 대상 위치
            Quaternion startTargetRotation = currentPresentedTarget.rotation; // 현재 대상 회전

            // 시간 0 이하면 즉시 복귀
            if (duration <= 0f)
            {
                playerTransform.position = originalPlayerPosition;
                playerTransform.rotation = originalPlayerRotation;
                currentPresentedTarget.position = originalTargetPosition;
                currentPresentedTarget.rotation = originalTargetRotation;
                RestoreCurrentTargetLayer();
                ClearCachedPose();
                return;
            }

            float elapsed = 0f; // 누적 시간

            // 동시 복귀 보간
            while (elapsed < duration)
            {
                token.ThrowIfCancellationRequested();

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float easedT = Mathf.SmoothStep(0f, 1f, t);

                // 플레이어 보간
                playerTransform.position = Vector3.Lerp(startPlayerPosition, originalPlayerPosition, easedT);
                playerTransform.rotation = Quaternion.Slerp(startPlayerRotation, originalPlayerRotation, easedT);

                // 대상 보간
                currentPresentedTarget.position = Vector3.Lerp(startTargetPosition, originalTargetPosition, easedT);
                currentPresentedTarget.rotation = Quaternion.Slerp(startTargetRotation, originalTargetRotation, easedT);

                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            // 최종 복귀
            playerTransform.position = originalPlayerPosition;
            playerTransform.rotation = originalPlayerRotation;
            currentPresentedTarget.position = originalTargetPosition;
            currentPresentedTarget.rotation = originalTargetRotation;

            // 현재 타겟 레이어 복구
            RestoreCurrentTargetLayer();

            // 캐시 정리
            ClearCachedPose();
        }

        /// <summary>씬의 모든 타겟을 기본 레이어로 복구한다</summary>
        private void SetAllTargetsToDefaultLayer()
        {
            HarvestTargetBehaviour[] targets = FindObjectsByType<HarvestTargetBehaviour>(FindObjectsSortMode.None); // 활성 타겟 전체 검색
            for (int i = 0; i < targets.Length; i++)
                targets[i].RestoreDefaultLayer();
        }

        /// <summary>현재 타겟 레이어를 기본값으로 복구한다</summary>
        private void RestoreCurrentTargetLayer()
        {
            // 현재 대상 없으면 중단
            if (currentPresentedTarget == null)
                return;

            HarvestTargetBehaviour targetBehaviour = currentPresentedTarget.GetComponent<HarvestTargetBehaviour>();
            if (targetBehaviour == null)
                return;

            targetBehaviour.RestoreDefaultLayer();
        }

        /// <summary>채집 카메라 컬링 마스크를 채집 전용으로 갱신한다</summary>
        private void ApplyHarvestCameraCullingMask()
        {
            // 카메라 없으면 중단
            if (harvestCamera == null)
                return;

            originalHarvestCameraMask = harvestCamera.cullingMask; // 원래 마스크 백업

            if (targetLayer >= 0)
                harvestCamera.cullingMask &= ~(1 << targetLayer);

            if (harvestingLayer >= 0)
                harvestCamera.cullingMask |= 1 << harvestingLayer;
        }

        /// <summary>채집 카메라 컬링 마스크를 원래 상태로 복구한다</summary>
        private void RestoreHarvestCameraCullingMask()
        {
            // 카메라 없으면 중단
            if (harvestCamera == null)
                return;

            harvestCamera.cullingMask = originalHarvestCameraMask;
        }

        /// <summary>레이어 마스크에서 단일 레이어 인덱스를 추출한다</summary>
        private int GetSingleLayerIndex(LayerMask layerMask)
        {
            // 비어 있으면 실패
            if (layerMask.value == 0)
                return -1;

            int value = layerMask.value; // 원본 값 캐싱
            int index = 0; // 레이어 인덱스 계산용

            // 첫 번째 활성 비트 탐색
            while (value > 1)
            {
                value >>= 1;
                index++;
            }

            return index;
        }

        /// <summary>캐시된 표시 포즈 정보를 초기화한다</summary>
        private void ClearCachedPose()
        {
            currentPresentedTarget = null;
            hasCachedPose = false;
            originalPlayerPosition = Vector3.zero;
            originalPlayerRotation = Quaternion.identity;
            originalTargetPosition = Vector3.zero;
            originalTargetRotation = Quaternion.identity;
        }
    }
}
