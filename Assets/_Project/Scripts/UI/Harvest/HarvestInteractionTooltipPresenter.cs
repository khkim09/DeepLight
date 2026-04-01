using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using TMPro;
using UnityEngine;
using UnityEngine.UI; // Image 컴포넌트용

namespace Project.UI.Harvest
{
    /// <summary>HUD에 상호작용 가능 아이콘(!)과 불가 툴팁을 표시하고 제어하는 프리젠터 클래스</summary>
    public class HarvestInteractionTooltipPresenter : MonoBehaviour
    {
        [Header("Icon Settings")]
        [SerializeField] private CanvasGroup iconGroup; // 느낌표 아이콘 영역
        [SerializeField] private Image iconImage; // 변경할 아이콘의 Image 컴포넌트
        [SerializeField] private Sprite availableIconSprite; // 초록 배경 하얀 느낌표
        [SerializeField] private Sprite unavailableIconSprite; // 노랑 배경 하얀 느낌표

        [Header("Tooltip Settings")]
        [SerializeField] private CanvasGroup tooltipGroup; // 메시지 확장 툴팁 영역
        [SerializeField] private TextMeshProUGUI messageText; // 툴팁 내 메시지 텍스트

        [Header("Animation")]
        [SerializeField] private float fadeDuration = 0.15f; // 페이드 속도

        private bool isTargetFocused; // 현재 타깃이 포커스 중인지 여부
        private bool isTooltipExpanded; // 툴팁이 열려있는지 여부
        private CancellationTokenSource fadeCts; // 애니메이션 취소 토큰

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
            CancelFadeTask();
        }

        /// <summary>툴팁이 확장된 상태에서 마우스 좌클릭 시 툴팁을 닫는다.</summary>
        private void Update()
        {
            // 커서가 잠겨있어도 클릭 입력은 감지됨
            if (isTooltipExpanded && Input.GetMouseButtonDown(0))
            {
                CloseTooltip();
            }
        }

        /// <summary>존 진입 시 타깃 상태에 맞춰 아이콘을 띄운다.</summary>
        private void OnTargetFocused(HarvestTargetFocusedEvent evt)
        {
            isTargetFocused = true;

            // 상태에 따라 스프라이트 교체
            if (iconImage != null)
                iconImage.sprite = evt.IsAvailable ? availableIconSprite : unavailableIconSprite;

            if (!isTooltipExpanded)
                PlayFadeTask(iconGroup, 1f).Forget();
        }

        /// <summary>존 이탈 시 모든 UI를 숨긴다.</summary>
        private void OnTargetUnfocused(HarvestTargetUnfocusedEvent evt)
        {
            isTargetFocused = false;
            isTooltipExpanded = false;

            PlayFadeTask(iconGroup, 0f).Forget();
            PlayFadeTask(tooltipGroup, 0f).Forget();
        }

        /// <summary>불가 대상 상호작용 시 느낌표를 숨기고 툴팁을 띄운다.</summary>
        private void OnInteractMessage(HarvestTargetInteractMessageEvent evt)
        {
            isTooltipExpanded = true;
            messageText.text = evt.Message;

            PlayFadeTask(iconGroup, 0f).Forget();
            PlayFadeTask(tooltipGroup, 1f).Forget();
        }

        /// <summary>좌클릭으로 툴팁을 닫고, 아직 존 안이라면 느낌표를 다시 띄운다.</summary>
        private void CloseTooltip()
        {
            isTooltipExpanded = false;
            PlayFadeTask(tooltipGroup, 0f).Forget();

            if (isTargetFocused)
                PlayFadeTask(iconGroup, 1f).Forget();
        }

        /// <summary>캔버스 그룹의 알파값을 즉시 적용한다.</summary>
        private void SetGroupAlphaInstant(CanvasGroup group, float alpha)
        {
            if (group != null)
            {
                group.alpha = alpha;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
        }

        /// <summary>기존 진행 중인 페이드 태스크가 있다면 취소한다.</summary>
        private void CancelFadeTask()
        {
            if (fadeCts != null)
            {
                fadeCts.Cancel();
                fadeCts.Dispose();
                fadeCts = null;
            }
        }

        /// <summary>지정된 캔버스 그룹을 목표 알파값까지 부드럽게 페이드 처리한다.</summary>
        private async UniTask PlayFadeTask(CanvasGroup targetGroup, float targetAlpha)
        {
            if (targetGroup == null) return;

            CancelFadeTask();
            fadeCts = new CancellationTokenSource();
            CancellationToken token = fadeCts.Token;

            float startAlpha = targetGroup.alpha;
            float time = 0f;

            while (time < fadeDuration)
            {
                time += Time.deltaTime;
                targetGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / fadeDuration);

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled) return;
            }

            targetGroup.alpha = targetAlpha;
        }
    }
}
