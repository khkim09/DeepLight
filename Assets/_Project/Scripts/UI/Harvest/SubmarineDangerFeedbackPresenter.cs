using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Gameplay.CameraSystem;
using Project.Gameplay.Runtime;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Harvest
{
    /// <summary>위험 피해 발생 시 화면 가장자리 경고와 약한 흔들림을 재생한다.</summary>
    public class SubmarineDangerFeedbackPresenter : MonoBehaviour
    {
        [Header("Overlay")]
        [SerializeField] private GameObject dangerOverlayRoot; // 경고 오버레이 루트
        [SerializeField] private CanvasGroup dangerOverlayGroup; // 레드 경고 오버레이 그룹
        [SerializeField] private Image dangerOverlayImage; // 레드 경고 이미지

        [Header("Camera Controllers")]
        [SerializeField] private ExplorationFollowCameraController explorationFollowCameraController; // 탐사 카메라 제어기
        [SerializeField] private HarvestConsoleCameraController harvestConsoleCameraController; // 채집 카메라 제어기

        [Header("Flash Timing")]
        [SerializeField] private float fadeInDuration = 0.05f; // 빠른 진입 시간
        [SerializeField] private float holdDuration = 0.08f; // 잠깐 유지 시간
        [SerializeField] private float fadeOutDuration = 0.22f; // 부드러운 퇴장 시간
        [SerializeField] private float minAlpha = 0.08f; // 최소 경고 강도
        [SerializeField] private float maxAlpha = 0.42f; // 최대 경고 강도

        [Header("Shake")]
        [SerializeField] private float shakeDuration = 0.16f; // 흔들림 지속 시간
        [SerializeField] private float minShakeDistance = 0.015f; // 최소 흔들림 거리
        [SerializeField] private float maxShakeDistance = 0.08f; // 최대 흔들림 거리

        private CancellationTokenSource overlayCts; // 오버레이 연출 취소 토큰
        private CancellationTokenSource shakeCts; // 흔들림 연출 취소 토큰
        private GameModeType currentMode = GameModeType.Exploration3D; // 현재 게임 모드
        private Color overlayBaseColor = Color.red; // 오버레이 기본 색

        /// <summary>초기 상태를 정리한다.</summary>
        private void Awake()
        {
            if (dangerOverlayImage != null)
            {
                overlayBaseColor = dangerOverlayImage.color;
                overlayBaseColor.a = 1f;
                dangerOverlayImage.color = new Color(overlayBaseColor.r, overlayBaseColor.g, overlayBaseColor.b, 0f);
            }

            if (dangerOverlayGroup != null)
            {
                dangerOverlayGroup.alpha = 1f;
                dangerOverlayGroup.interactable = false;
                dangerOverlayGroup.blocksRaycasts = false;
            }

            if (dangerOverlayRoot != null)
                dangerOverlayRoot.SetActive(false);

            ApplyShakeOffset(Vector3.zero);
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<SubmarineDangerFeedbackEvent>(OnDangerFeedback);
            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<SubmarineDangerFeedbackEvent>(OnDangerFeedback);
            EventBus.Unsubscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);

            HideImmediate();
        }

        /// <summary>모드 변경 시 현재 모드를 갱신한다.</summary>
        private void OnGameModeChanged(GameModeChangedEvent publishedEvent)
        {
            currentMode = publishedEvent.CurrentMode;
        }

        /// <summary>위험 피해 이벤트를 받아 화면 경고를 재생한다.</summary>
        private void OnDangerFeedback(SubmarineDangerFeedbackEvent publishedEvent)
        {
            float normalizedHull = publishedEvent.MaxHull <= 0f
                ? 1f
                : Mathf.Clamp01(publishedEvent.CurrentHull / publishedEvent.MaxHull);

            float damageRatio = publishedEvent.MaxHull <= 0f
                ? 0f
                : Mathf.Clamp01(publishedEvent.DamageAmount / publishedEvent.MaxHull);

            float danger01 = Mathf.Clamp01(
                (1f - normalizedHull) * 0.65f
                + damageRatio * 0.35f);

            danger01 *= Mathf.Max(0f, publishedEvent.IntensityMultiplier);
            danger01 = Mathf.Clamp01(danger01);

            float targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, danger01);
            float shakeDistance = Mathf.Lerp(minShakeDistance, maxShakeDistance, danger01);

            PlayOverlayAsync(targetAlpha).Forget();
            PlayShakeAsync(shakeDistance).Forget();
        }

        /// <summary>Harvest 종료 시 현재 피드백을 즉시 정리한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            HideImmediate();
        }

        /// <summary>오버레이와 흔들림을 즉시 숨기고 원복한다.</summary>
        public void HideImmediate()
        {
            CancelOverlayTask();
            CancelShakeTask();

            if (dangerOverlayRoot != null)
                dangerOverlayRoot.SetActive(false);

            if (dangerOverlayImage != null)
                dangerOverlayImage.color = new Color(overlayBaseColor.r, overlayBaseColor.g, overlayBaseColor.b, 0f);

            ApplyShakeOffset(Vector3.zero);
        }

        /// <summary>현재 모드에 맞는 카메라 제어기에 shake offset을 적용한다.</summary>
        private void ApplyShakeOffset(Vector3 localOffset)
        {
            if (explorationFollowCameraController != null)
                explorationFollowCameraController.SetExternalShakeLocalOffset(Vector3.zero);

            if (harvestConsoleCameraController != null)
                harvestConsoleCameraController.SetExternalShakeLocalOffset(Vector3.zero);

            if (currentMode == GameModeType.HarvestConsole)
            {
                if (harvestConsoleCameraController != null)
                    harvestConsoleCameraController.SetExternalShakeLocalOffset(localOffset);
            }
            else
            {
                if (explorationFollowCameraController != null)
                    explorationFollowCameraController.SetExternalShakeLocalOffset(localOffset);
            }
        }

        /// <summary>오버레이 연출을 재생한다.</summary>
        private async UniTaskVoid PlayOverlayAsync(float targetAlpha)
        {
            if (dangerOverlayImage == null)
                return;

            CancelOverlayTask();
            overlayCts = new CancellationTokenSource();
            CancellationToken token = overlayCts.Token;

            if (dangerOverlayRoot != null && !dangerOverlayRoot.activeSelf)
                dangerOverlayRoot.SetActive(true);

            float startAlpha = dangerOverlayImage.color.a;
            float time = 0f;

            while (time < fadeInDuration)
            {
                time += Time.deltaTime;
                float t = fadeInDuration <= 0f ? 1f : Mathf.Clamp01(time / fadeInDuration);
                float alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
                dangerOverlayImage.color = new Color(overlayBaseColor.r, overlayBaseColor.g, overlayBaseColor.b, alpha);

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            dangerOverlayImage.color = new Color(overlayBaseColor.r, overlayBaseColor.g, overlayBaseColor.b, targetAlpha);

            float holdTime = 0f;
            while (holdTime < holdDuration)
            {
                holdTime += Time.deltaTime;

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            float fadeOutStart = dangerOverlayImage.color.a;
            float fadeOutTime = 0f;

            while (fadeOutTime < fadeOutDuration)
            {
                fadeOutTime += Time.deltaTime;
                float t = fadeOutDuration <= 0f ? 1f : Mathf.Clamp01(fadeOutTime / fadeOutDuration);
                float alpha = Mathf.Lerp(fadeOutStart, 0f, t);
                dangerOverlayImage.color = new Color(overlayBaseColor.r, overlayBaseColor.g, overlayBaseColor.b, alpha);

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            dangerOverlayImage.color = new Color(overlayBaseColor.r, overlayBaseColor.g, overlayBaseColor.b, 0f);

            if (dangerOverlayRoot != null)
                dangerOverlayRoot.SetActive(false);
        }

        /// <summary>약한 화면 흔들림을 재생한다.</summary>
        private async UniTaskVoid PlayShakeAsync(float shakeDistance)
        {
            CancelShakeTask();
            shakeCts = new CancellationTokenSource();
            CancellationToken token = shakeCts.Token;

            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                float normalized = shakeDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / shakeDuration);
                float damping = 1f - normalized;

                Vector2 randomOffset2D = Random.insideUnitCircle * shakeDistance * damping;
                ApplyShakeOffset(new Vector3(randomOffset2D.x, randomOffset2D.y, 0f));

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            ApplyShakeOffset(Vector3.zero);
        }

        /// <summary>현재 오버레이 연출을 취소한다.</summary>
        private void CancelOverlayTask()
        {
            if (overlayCts == null)
                return;

            overlayCts.Cancel();
            overlayCts.Dispose();
            overlayCts = null;
        }

        /// <summary>현재 흔들림 연출을 취소한다.</summary>
        private void CancelShakeTask()
        {
            if (shakeCts == null)
                return;

            shakeCts.Cancel();
            shakeCts.Dispose();
            shakeCts = null;
        }
    }
}
