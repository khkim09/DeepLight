using Project.Core.Events;
using Project.Data.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Common
{
    /// <summary>상호작용 컨테이너 UI를 상황별 프롬프트 정의에 따라 표시하는 클래스이다.</summary>
    public class InteractionContainerPresenter : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private InteractionPromptCatalogSO promptCatalog; // 상황별 프롬프트 정의 SO

        [Header("Root")]
        [SerializeField] private GameObject interactionContainer; // AlwaysHUD의 상호작용 컨테이너 루트
        [SerializeField] private CanvasGroup interactionGroup; // 표시/숨김용 CanvasGroup

        [Header("Visual")]
        [SerializeField] private Image interactionImage; // 상황 아이콘
        [SerializeField] private TMP_Text interactionTitle; // 상황 타이틀

        /// <summary>초기 상태를 숨김으로 맞춘다.</summary>
        private void Awake()
        {
            HideImmediate();
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<InteractionPromptChangedEvent>(OnInteractionPromptChanged);
            EventBus.Subscribe<InteractionPromptClearedEvent>(OnInteractionPromptCleared);
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<InteractionPromptChangedEvent>(OnInteractionPromptChanged);
            EventBus.Unsubscribe<InteractionPromptClearedEvent>(OnInteractionPromptCleared);
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
        }

        /// <summary>상호작용 프롬프트 변경 이벤트를 받아 UI를 갱신한다.</summary>
        private void OnInteractionPromptChanged(InteractionPromptChangedEvent publishedEvent)
        {
            if (promptCatalog == null)
            {
                HideImmediate();
                return;
            }

            if (!promptCatalog.TryGetPrompt(publishedEvent.PromptType, out InteractionPromptEntry entry))
            {
                HideImmediate();
                return;
            }

            if (interactionImage != null)
                interactionImage.sprite = entry.IconSprite;

            if (interactionTitle != null)
                interactionTitle.text = entry.Title;

            ShowImmediate();
        }

        /// <summary>상호작용 프롬프트 해제 이벤트를 받아 UI를 숨긴다.</summary>
        private void OnInteractionPromptCleared(InteractionPromptClearedEvent publishedEvent)
        {
            HideImmediate();
        }

        /// <summary>Harvest 진입 시 탐사 상호작용 UI를 숨긴다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            HideImmediate();
        }

        /// <summary>컨테이너를 즉시 표시한다.</summary>
        private void ShowImmediate()
        {
            if (interactionContainer != null)
                interactionContainer.SetActive(true);

            if (interactionGroup != null)
            {
                interactionGroup.alpha = 1f;
                interactionGroup.interactable = false;
                interactionGroup.blocksRaycasts = false;
            }
        }

        /// <summary>컨테이너를 즉시 숨긴다.</summary>
        private void HideImmediate()
        {
            if (interactionGroup != null)
            {
                interactionGroup.alpha = 0f;
                interactionGroup.interactable = false;
                interactionGroup.blocksRaycasts = false;
            }

            if (interactionContainer != null)
                interactionContainer.SetActive(false);
        }
    }
}
