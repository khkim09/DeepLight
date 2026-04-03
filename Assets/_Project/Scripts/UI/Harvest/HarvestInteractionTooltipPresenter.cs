using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Harvest
{
    /// <summary>HUD에 상호작용 가능 아이콘과 불가 툴팁을 표시하고 제어하는 프리젠터 클래스</summary>
    public class HarvestInteractionTooltipPresenter : MonoBehaviour
    {
        [Header("Icon Settings")]
        [SerializeField] private CanvasGroup iconGroup; // 느낌표 아이콘 영역
        [SerializeField] private Image iconImage; // 아이콘 Image
        [SerializeField] private Sprite availableIconSprite; // 상호작용 가능 아이콘
        [SerializeField] private Sprite unavailableIconSprite; // 상호작용 불가 아이콘

        [Header("Tooltip Settings")]
        [SerializeField] private CanvasGroup tooltipGroup; // 메시지 툴팁 영역
        [SerializeField] private TextMeshProUGUI messageText; // 툴팁 내 메시지 텍스트

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.15f; // 페이드 시간

        private bool isTargetFocused; // 현재 타깃 포커스 여부
        private bool isTooltipExpanded; // 현재 툴팁 펼침 여부

        private CancellationTokenSource iconFadeCts; // 아이콘 전용 페이드 토큰
        private CancellationTokenSource tooltipFadeCts; // 툴팁 전용 페이드 토큰

        /// <summary>초기 세팅 및 이벤트를 구독한다.</summary>
        private void Awake()
        {
            SetGroupAlphaInstant(iconGroup, 0f);
            SetGroupAlphaInstant(tooltipGroup, 0f);

            EventBus.Subscribe<HarvestTargetFocusedEvent>(OnTargetFocused);
            EventBus.Subscribe<HarvestTargetUnfocusedEvent>(OnTargetUnfocused);
            EventBus.Subscribe<HarvestTargetInteractMessageEvent>(OnInteractMessage);
        }

        /// <summary>이벤트 구독 해제 및 진행 중인 태스크를 취소한다.</summary>
        private void OnDestroy()
        {
            EventBus.Unsubscribe<HarvestTargetFocusedEvent>(OnTargetFocused);
            EventBus.Unsubscribe<HarvestTargetUnfocusedEvent>(OnTargetUnfocused);
            EventBus.Unsubscribe<HarvestTargetInteractMessageEvent>(OnInteractMessage);

            CancelIconFadeTask();
            CancelTooltipFadeTask();
        }

        /// <summary>툴팁이 펼쳐진 상태에서 좌클릭 시 툴팁을 닫는다.</summary>
        private void Update()
        {
            if (isTooltipExpanded && Input.GetMouseButtonDown(0))
                CloseTooltip();
        }

        /// <summary>타깃 포커스 시 아이콘을 표시한다.</summary>
        private void OnTargetFocused(HarvestTargetFocusedEvent publishedEvent)
        {
            isTargetFocused = true;

            if (iconImage != null)
                iconImage.sprite = publishedEvent.IsAvailable ? availableIconSprite : unavailableIconSprite;

            if (!isTooltipExpanded)
                PlayIconFadeTask(1f).Forget();
        }

        /// <summary>타깃 포커스 해제 시 아이콘과 툴팁을 모두 숨긴다.</summary>
        private void OnTargetUnfocused(HarvestTargetUnfocusedEvent publishedEvent)
        {
            isTargetFocused = false;
            isTooltipExpanded = false;

            PlayIconFadeTask(0f).Forget();
            PlayTooltipFadeTask(0f).Forget();
        }

        /// <summary>상호작용 불가 메시지 수신 시 툴팁을 펼친다.</summary>
        private void OnInteractMessage(HarvestTargetInteractMessageEvent publishedEvent)
        {
            isTooltipExpanded = true;

            if (messageText != null)
                messageText.text = publishedEvent.Message;

            PlayIconFadeTask(0f).Forget();
            PlayTooltipFadeTask(1f).Forget();
        }

        /// <summary>툴팁을 닫고, 아직 타깃 포커스 중이면 아이콘을 다시 띄운다.</summary>
        private void CloseTooltip()
        {
            isTooltipExpanded = false;
            PlayTooltipFadeTask(0f).Forget();

            if (isTargetFocused)
                PlayIconFadeTask(1f).Forget();
        }

        /// <summary>캔버스 그룹 알파를 즉시 적용한다.</summary>
        private void SetGroupAlphaInstant(CanvasGroup group, float alpha)
        {
            if (group == null)
                return;

            group.alpha = alpha;
            group.interactable = alpha > 0.99f;
            group.blocksRaycasts = alpha > 0.99f;
        }

        /// <summary>아이콘 페이드 태스크를 취소한다.</summary>
        private void CancelIconFadeTask()
        {
            if (iconFadeCts == null)
                return;

            iconFadeCts.Cancel();
            iconFadeCts.Dispose();
            iconFadeCts = null;
        }

        /// <summary>툴팁 페이드 태스크를 취소한다.</summary>
        private void CancelTooltipFadeTask()
        {
            if (tooltipFadeCts == null)
                return;

            tooltipFadeCts.Cancel();
            tooltipFadeCts.Dispose();
            tooltipFadeCts = null;
        }

        /// <summary>아이콘 CanvasGroup을 목표 알파까지 부드럽게 전환한다.</summary>
        private async UniTask PlayIconFadeTask(float targetAlpha)
        {
            if (iconGroup == null)
                return;

            CancelIconFadeTask();
            iconFadeCts = new CancellationTokenSource();
            CancellationToken token = iconFadeCts.Token;

            float startAlpha = iconGroup.alpha;
            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                float normalized = fadeDuration <= 0f ? 1f : Mathf.Clamp01(time / fadeDuration);

                iconGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, normalized);

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            iconGroup.alpha = targetAlpha;
            iconGroup.interactable = targetAlpha > 0.99f;
            iconGroup.blocksRaycasts = targetAlpha > 0.99f;
        }

        /// <summary>툴팁 CanvasGroup을 목표 알파까지 부드럽게 전환한다.</summary>
        private async UniTask PlayTooltipFadeTask(float targetAlpha)
        {
            if (tooltipGroup == null)
                return;

            CancelTooltipFadeTask();
            tooltipFadeCts = new CancellationTokenSource();
            CancellationToken token = tooltipFadeCts.Token;

            float startAlpha = tooltipGroup.alpha;
            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                float normalized = fadeDuration <= 0f ? 1f : Mathf.Clamp01(time / fadeDuration);

                tooltipGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, normalized);

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            tooltipGroup.alpha = targetAlpha;
            tooltipGroup.interactable = targetAlpha > 0.99f;
            tooltipGroup.blocksRaycasts = targetAlpha > 0.99f;
        }
    }
}
