using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Gameplay.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.UI
{
    /// <summary>배터리와 내구도 HUD를 정상/위험 경로에 따라 서로 다른 애니메이션으로 표시한다.</summary>
    public class SubmarineStatusHUD : MonoBehaviour
    {
        [System.Serializable]
        private sealed class CircularGaugeView
        {
            [Header("Root")]
            public RectTransform gaugeRoot; // 바운스 적용 루트

            [Header("Main")]
            public Image fillImage; // 실제 현재값 링
            public RectTransform handlePivot; // 실제 현재값 핸들
            public TMP_Text percentText; // 퍼센트 텍스트

            [Header("Delayed Damage")]
            public Image delayedFillImage; // red lag 링

            [HideInInspector] public float displayedValue01 = 1f; // 실제 현재 게이지 표시값
            [HideInInspector] public float delayedValue01 = 1f; // 지연 손실 표시값
            [HideInInspector] public CancellationTokenSource animateCts; // 현재 애니메이션 취소 토큰
            [HideInInspector] public CancellationTokenSource bounceCts; // 현재 바운스 취소 토큰
        }

        [Header("Battery")]
        [SerializeField] private CircularGaugeView batteryGauge = new();

        [Header("Durability")]
        [SerializeField] private CircularGaugeView durabilityGauge = new();

        [Header("Normal Route Animation")]
        [SerializeField] private float normalDecreaseDuration = 0.3f; // 정상 소모 시 부드러운 감소 시간
        [SerializeField] private float normalIncreaseDuration = 0.2f; // 회복 시 부드러운 증가 시간

        [Header("Danger Route Animation")]
        [SerializeField] private float dangerImmediateDuration = 0.08f; // danger 경로 본 게이지 빠른 반영 시간
        [SerializeField] private float dangerDelayBeforeChase = 0.08f; // red 영역 유지 시간
        [SerializeField] private float dangerLagChaseDuration = 0.22f; // red 링 추격 시간

        [Header("Bounce")]
        [SerializeField] private float bounceScaleMultiplier = 1.14f; // 피격 시 확대 배율
        [SerializeField] private float bounceUpDuration = 0.06f; // 확대 시간
        [SerializeField] private float bounceDownDuration = 0.12f; // 복귀 시간

        private bool batteryInitialized; // 배터리 초기값 적용 여부
        private bool durabilityInitialized; // 내구도 초기값 적용 여부

        /// <summary>상태 변경 이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<BatteryChangedEvent>(OnBatteryChanged);
            EventBus.Subscribe<BatteryDangerFeedbackEvent>(OnBatteryDangerFeedback);
            EventBus.Subscribe<HullDurabilityChangedEvent>(OnHullDurabilityChanged);
            EventBus.Subscribe<SubmarineDangerFeedbackEvent>(OnSubmarineDangerFeedback);
        }

        /// <summary>상태 변경 이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<BatteryChangedEvent>(OnBatteryChanged);
            EventBus.Unsubscribe<BatteryDangerFeedbackEvent>(OnBatteryDangerFeedback);
            EventBus.Unsubscribe<HullDurabilityChangedEvent>(OnHullDurabilityChanged);
            EventBus.Unsubscribe<SubmarineDangerFeedbackEvent>(OnSubmarineDangerFeedback);

            CancelGaugeTasks(batteryGauge);
            CancelGaugeTasks(durabilityGauge);
        }

        /// <summary>배터리 UI를 갱신한다.</summary>
        private void OnBatteryChanged(BatteryChangedEvent publishedEvent)
        {
            float maxBattery = Mathf.Max(1f, publishedEvent.MaxBattery);
            float normalized = Mathf.Clamp01(publishedEvent.CurrentBattery / maxBattery);

            if (!batteryInitialized)
            {
                batteryInitialized = true;
                batteryGauge.displayedValue01 = normalized;
                batteryGauge.delayedValue01 = normalized;
                ApplyGaugeVisuals(batteryGauge, normalized, normalized);
                return;
            }

            PlayNormalGaugeAnimation(batteryGauge, normalized).Forget();
        }

        /// <summary>배터리 danger 피드백을 표시한다.</summary>
        private void OnBatteryDangerFeedback(BatteryDangerFeedbackEvent publishedEvent)
        {
            float maxBattery = Mathf.Max(1f, publishedEvent.MaxBattery);
            float normalized = Mathf.Clamp01(publishedEvent.CurrentBattery / maxBattery);

            PlayDangerGaugeAnimation(
                batteryGauge,
                normalized,
                Mathf.Max(0.8f, publishedEvent.IntensityMultiplier)).Forget();
        }

        /// <summary>내구도 UI를 갱신한다.</summary>
        private void OnHullDurabilityChanged(HullDurabilityChangedEvent publishedEvent)
        {
            float maxDurability = Mathf.Max(1f, publishedEvent.MaxDurability);
            float normalized = Mathf.Clamp01(publishedEvent.CurrentDurability / maxDurability);

            if (!durabilityInitialized)
            {
                durabilityInitialized = true;
                durabilityGauge.displayedValue01 = normalized;
                durabilityGauge.delayedValue01 = normalized;
                ApplyGaugeVisuals(durabilityGauge, normalized, normalized);
                return;
            }

            PlayNormalGaugeAnimation(durabilityGauge, normalized).Forget();
        }

        /// <summary>내구도 danger 피드백을 표시한다.</summary>
        private void OnSubmarineDangerFeedback(SubmarineDangerFeedbackEvent publishedEvent)
        {
            float maxHull = Mathf.Max(1f, publishedEvent.MaxHull);
            float normalized = Mathf.Clamp01(publishedEvent.CurrentHull / maxHull);

            PlayDangerGaugeAnimation(
                durabilityGauge,
                normalized,
                Mathf.Max(0.8f, publishedEvent.IntensityMultiplier)).Forget();
        }

        /// <summary>정상 경로 게이지 애니메이션을 재생한다.</summary>
        private async UniTaskVoid PlayNormalGaugeAnimation(CircularGaugeView gauge, float targetValue01)
        {
            CancelAnimationOnly(gauge);

            gauge.animateCts = new CancellationTokenSource();
            CancellationToken token = gauge.animateCts.Token;

            float startMain = gauge.displayedValue01;
            float startDelayed = gauge.delayedValue01;

            // 회복인지 감소인지에 따라 시간 분리
            bool isDecrease = targetValue01 < startMain;
            float duration = isDecrease ? normalDecreaseDuration : normalIncreaseDuration;
            duration = Mathf.Max(0.01f, duration);

            float elapsed = 0f;

            try
            {
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);

                    float currentMain = Mathf.Lerp(startMain, targetValue01, t);
                    float currentDelayed = Mathf.Lerp(startDelayed, targetValue01, t);

                    gauge.displayedValue01 = currentMain;
                    gauge.delayedValue01 = currentDelayed;

                    ApplyGaugeVisuals(gauge, currentMain, currentDelayed);
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            gauge.displayedValue01 = targetValue01;
            gauge.delayedValue01 = targetValue01;
            ApplyGaugeVisuals(gauge, targetValue01, targetValue01);
        }

        /// <summary>위험 경로 게이지 애니메이션을 재생한다.</summary>
        private async UniTaskVoid PlayDangerGaugeAnimation(
            CircularGaugeView gauge,
            float targetValue01,
            float intensityMultiplier)
        {
            CancelAnimationOnly(gauge);

            gauge.animateCts = new CancellationTokenSource();
            CancellationToken token = gauge.animateCts.Token;

            // danger 직전 값이 red lag 시작점이 된다.
            float previousDisplayed = gauge.displayedValue01;
            float previousDelayed = Mathf.Max(gauge.delayedValue01, previousDisplayed);

            // 1. 바운스 시작
            PlayBounce(gauge, intensityMultiplier).Forget();

            // 2. 메인 게이지는 빠르게 현재값으로 이동
            float mainElapsed = 0f;
            float mainDuration = Mathf.Max(0.01f, dangerImmediateDuration);

            try
            {
                while (mainElapsed < mainDuration)
                {
                    mainElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(mainElapsed / mainDuration);

                    float currentMain = Mathf.Lerp(previousDisplayed, targetValue01, t);
                    gauge.displayedValue01 = currentMain;

                    // red lag 값은 일단 이전 값 유지
                    gauge.delayedValue01 = previousDelayed;
                    ApplyGaugeVisuals(gauge, gauge.displayedValue01, gauge.delayedValue01);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                gauge.displayedValue01 = targetValue01;
                gauge.delayedValue01 = previousDelayed;
                ApplyGaugeVisuals(gauge, gauge.displayedValue01, gauge.delayedValue01);

                // 3. 잠깐 red 영역 유지
                await UniTask.Delay(
                    Mathf.RoundToInt(dangerDelayBeforeChase * 1000f),
                    DelayType.UnscaledDeltaTime,
                    PlayerLoopTiming.Update,
                    token);

                // 4. red lag 링이 현재값을 추격
                float lagStart = gauge.delayedValue01;
                float lagElapsed = 0f;
                float lagDuration = Mathf.Max(0.01f, dangerLagChaseDuration);

                while (lagElapsed < lagDuration)
                {
                    lagElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(lagElapsed / lagDuration);

                    gauge.delayedValue01 = Mathf.Lerp(lagStart, targetValue01, t);
                    ApplyGaugeVisuals(gauge, gauge.displayedValue01, gauge.delayedValue01);

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            gauge.displayedValue01 = targetValue01;
            gauge.delayedValue01 = targetValue01;
            ApplyGaugeVisuals(gauge, targetValue01, targetValue01);
        }

        /// <summary>게이지 루트에 1회 바운스를 재생한다.</summary>
        private async UniTaskVoid PlayBounce(CircularGaugeView gauge, float intensityMultiplier)
        {
            if (gauge.gaugeRoot == null)
                return;

            CancelBounceOnly(gauge);

            gauge.bounceCts = new CancellationTokenSource();
            CancellationToken token = gauge.bounceCts.Token;

            Vector3 baseScale = Vector3.one;
            Vector3 targetScale = Vector3.one * Mathf.Lerp(1f, bounceScaleMultiplier, Mathf.Clamp01(intensityMultiplier));

            try
            {
                float upElapsed = 0f;
                float upDuration = Mathf.Max(0.01f, bounceUpDuration);

                while (upElapsed < upDuration)
                {
                    upElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(upElapsed / upDuration);
                    gauge.gaugeRoot.localScale = Vector3.LerpUnclamped(baseScale, targetScale, EaseOutBack01(t));
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }

                float downElapsed = 0f;
                float downDuration = Mathf.Max(0.01f, bounceDownDuration);

                while (downElapsed < downDuration)
                {
                    downElapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(downElapsed / downDuration);
                    gauge.gaugeRoot.localScale = Vector3.LerpUnclamped(targetScale, baseScale, EaseOutCubic01(t));
                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            gauge.gaugeRoot.localScale = Vector3.one;
        }

        /// <summary>현재 게이지 시각 요소를 즉시 적용한다.</summary>
        private void ApplyGaugeVisuals(CircularGaugeView gauge, float mainValue01, float delayedValue01)
        {
            // 메인 링
            if (gauge.fillImage != null)
                gauge.fillImage.fillAmount = mainValue01;

            // 메인 핸들
            if (gauge.handlePivot != null)
                gauge.handlePivot.localRotation = Quaternion.Euler(0f, 0f, mainValue01 * 360f);

            // 퍼센트 텍스트는 실제 현재값 기준
            if (gauge.percentText != null)
                gauge.percentText.text = $"{mainValue01 * 100f:0}";

            // red lag 링
            if (gauge.delayedFillImage != null)
                gauge.delayedFillImage.fillAmount = delayedValue01;
        }

        /// <summary>게이지 애니메이션만 취소한다.</summary>
        private void CancelAnimationOnly(CircularGaugeView gauge)
        {
            if (gauge.animateCts == null)
                return;

            gauge.animateCts.Cancel();
            gauge.animateCts.Dispose();
            gauge.animateCts = null;
        }

        /// <summary>게이지 바운스만 취소한다.</summary>
        private void CancelBounceOnly(CircularGaugeView gauge)
        {
            if (gauge.bounceCts == null)
                return;

            gauge.bounceCts.Cancel();
            gauge.bounceCts.Dispose();
            gauge.bounceCts = null;

            if (gauge.gaugeRoot != null)
                gauge.gaugeRoot.localScale = Vector3.one;
        }

        /// <summary>게이지 관련 모든 태스크를 취소한다.</summary>
        private void CancelGaugeTasks(CircularGaugeView gauge)
        {
            CancelAnimationOnly(gauge);
            CancelBounceOnly(gauge);
        }

        /// <summary>OutBack easing 값을 반환한다.</summary>
        private float EaseOutBack01(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float x = Mathf.Clamp01(t) - 1f;
            return 1f + c3 * x * x * x + c1 * x * x;
        }

        /// <summary>Cubic easing 값을 반환한다.</summary>
        private float EaseOutCubic01(float t)
        {
            float x = 1f - Mathf.Clamp01(t);
            return 1f - x * x * x;
        }
    }
}
