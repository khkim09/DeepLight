using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Gameplay.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.UI.Harvest
{
    /// <summary>현재 포커스된 채집 대상 위에 아이콘 프롬프트와 별도 메시지 툴팁을 표시한다.</summary>
    public class HarvestWorldTargetPromptPresenter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HarvestPointInteractor harvestPointInteractor; // 현재 타깃 감지기
        [SerializeField] private Camera explorationCamera; // 탐사 카메라

        [Header("Prompt Root")]
        [SerializeField] private RectTransform promptRoot; // 아이콘 프롬프트 루트
        [SerializeField] private CanvasGroup promptGroup; // 프롬프트 그룹
        [SerializeField] private Image iconImage; // 아이콘 이미지

        [Header("Message Root")]
        [SerializeField] private RectTransform messageRoot; // 불가 메시지 루트
        [SerializeField] private CanvasGroup messageGroup; // 메시지 그룹
        [SerializeField] private TextMeshProUGUI messageText; // 불가 메시지 텍스트

        [Header("Sprites")]
        [SerializeField] private Sprite availableIconSprite; // 진입 가능 아이콘
        [SerializeField] private Sprite blockedIconSprite; // 블락 아이콘

        [Header("Prompt Color")]
        [SerializeField] private Color availableColor = Color.white; // 가능 상태 색
        [SerializeField] private Color blockedColor = new(1f, 0.85f, 0.2f, 1f); // 불가 상태 색

        [Header("Position")]
        [SerializeField] private Vector2 promptScreenOffset = new(0f, 18f); // 아이콘 화면 오프셋
        [SerializeField] private float screenClampPadding = 36f; // 화면 가장자리 여유
        [SerializeField] private bool hideWhenBehindCamera = true; // 카메라 뒤면 숨길지 여부

        [Header("Message Timing")]
        [SerializeField] private float messageVisibleDuration = 1.8f; // 메시지 노출 시간

        private bool isFocused; // 현재 타깃 포커스 여부
        private bool isInteractable; // 현재 상호작용 가능 여부
        private CancellationTokenSource messageCts; // 메시지 타이머 취소 토큰

        /// <summary>초기 상태를 숨김으로 맞춘다.</summary>
        private void Awake()
        {
            HidePromptImmediate();
            HideMessageImmediate();
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestTargetFocusedEvent>(OnTargetFocused);
            EventBus.Subscribe<HarvestTargetUnfocusedEvent>(OnTargetUnfocused);
            EventBus.Subscribe<HarvestTargetInteractMessageEvent>(OnInteractMessage);
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestTargetFocusedEvent>(OnTargetFocused);
            EventBus.Unsubscribe<HarvestTargetUnfocusedEvent>(OnTargetUnfocused);
            EventBus.Unsubscribe<HarvestTargetInteractMessageEvent>(OnInteractMessage);
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);

            CancelMessageTask();
        }

        /// <summary>아이콘 위치를 현재 타깃 월드 좌표에 맞춰 갱신한다.</summary>
        private void LateUpdate()
        {
            if (!ShouldShowWorldPrompt())
            {
                SetPromptVisible(false);
                return;
            }

            Camera referenceCamera = ResolveReferenceCamera();
            if (referenceCamera == null)
                return;

            Vector3 worldPosition = harvestPointInteractor.GetCurrentPromptWorldPosition();
            Vector3 screenPosition = referenceCamera.WorldToScreenPoint(worldPosition);

            if (hideWhenBehindCamera && screenPosition.z <= 0f)
            {
                SetPromptVisible(false);
                return;
            }

            SetPromptVisible(true);

            Vector2 promptPosition = ClampToScreen((Vector2)screenPosition + promptScreenOffset);
            promptRoot.position = promptPosition;
        }

        /// <summary>타깃 포커스 시 현재 상태에 맞는 아이콘을 표시한다.</summary>
        private void OnTargetFocused(HarvestTargetFocusedEvent publishedEvent)
        {
            isFocused = true;
            isInteractable = publishedEvent.IsAvailable;

            RefreshPromptIcon();

            // 정책:
            // - 이미 소모된 대상만 완전 숨김
            // - 아직 존재하지만 진입 불가인 대상은 blocked icon 유지
            SetPromptVisible(ShouldShowWorldPrompt());
        }

        /// <summary>타깃 포커스 해제 시 아이콘과 메시지를 숨긴다.</summary>
        private void OnTargetUnfocused(HarvestTargetUnfocusedEvent publishedEvent)
        {
            isFocused = false;
            HidePromptImmediate();
            HideMessageImmediate();
        }

        /// <summary>불가 메시지 이벤트를 받아 메시지를 일정 시간 표시한다.</summary>
        private void OnInteractMessage(HarvestTargetInteractMessageEvent publishedEvent)
        {
            if (messageText != null)
                messageText.text = publishedEvent.Message;

            ShowMessageTimedAsync().Forget();
        }

        /// <summary>Harvest 진입 시 탐사 프롬프트를 숨긴다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            HidePromptImmediate();
            HideMessageImmediate();
        }

        /// <summary>Harvest 종료 시 포커스 이벤트가 다시 올 때까지 숨긴다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            HidePromptImmediate();
            HideMessageImmediate();
        }

        /// <summary>현재 대상 상태에 맞는 아이콘과 색을 반영한다.</summary>
        private void RefreshPromptIcon()
        {
            if (iconImage == null)
                return;

            if (isInteractable)
            {
                iconImage.sprite = availableIconSprite;
                iconImage.color = availableColor;
                return;
            }

            iconImage.sprite = blockedIconSprite;
            iconImage.color = blockedColor;
        }

        /// <summary>현재 월드 프롬프트를 표시해도 되는지 판단한다.</summary>
        private bool ShouldShowWorldPrompt()
        {
            if (!isFocused || harvestPointInteractor == null || promptRoot == null)
                return false;

            if (harvestPointInteractor.CurrentTarget == null)
                return false;

            // 이미 소모되어 타깃 자체가 끝난 경우만 숨긴다.
            // retry penalty / day lock 상태는 blocked icon을 계속 보여준다.
            return harvestPointInteractor.CurrentTarget.IsAvailable;
        }

        /// <summary>메시지를 일정 시간 동안 표시했다가 숨긴다.</summary>
        private async UniTaskVoid ShowMessageTimedAsync()
        {
            if (messageRoot == null || messageGroup == null)
                return;

            CancelMessageTask();
            messageCts = new CancellationTokenSource();
            CancellationToken token = messageCts.Token;

            SetMessageVisible(true);

            float elapsed = 0f;
            while (elapsed < messageVisibleDuration)
            {
                elapsed += Time.deltaTime;

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled)
                    return;
            }

            HideMessageImmediate();
        }

        /// <summary>프롬프트를 즉시 숨긴다.</summary>
        private void HidePromptImmediate()
        {
            SetPromptVisible(false);
        }

        /// <summary>메시지를 즉시 숨긴다.</summary>
        private void HideMessageImmediate()
        {
            CancelMessageTask();
            SetMessageVisible(false);
        }

        /// <summary>프롬프트 표시 여부를 즉시 반영한다.</summary>
        private void SetPromptVisible(bool visible)
        {
            if (promptGroup != null)
            {
                promptGroup.alpha = visible ? 1f : 0f;
                promptGroup.interactable = false;
                promptGroup.blocksRaycasts = false;
            }

            if (promptRoot != null)
                promptRoot.gameObject.SetActive(visible);
        }

        /// <summary>메시지 표시 여부를 즉시 반영한다.</summary>
        private void SetMessageVisible(bool visible)
        {
            if (messageGroup != null)
            {
                messageGroup.alpha = visible ? 1f : 0f;
                messageGroup.interactable = false;
                messageGroup.blocksRaycasts = false;
            }

            if (messageRoot != null)
                messageRoot.gameObject.SetActive(visible);
        }

        /// <summary>스크린 좌표를 화면 안쪽으로 보정한다.</summary>
        private Vector2 ClampToScreen(Vector2 screenPosition)
        {
            float x = Mathf.Clamp(screenPosition.x, screenClampPadding, Screen.width - screenClampPadding);
            float y = Mathf.Clamp(screenPosition.y, screenClampPadding, Screen.height - screenClampPadding);
            return new Vector2(x, y);
        }

        /// <summary>탐사 카메라 참조를 반환한다.</summary>
        private Camera ResolveReferenceCamera()
        {
            if (explorationCamera != null && explorationCamera.gameObject.activeInHierarchy)
                return explorationCamera;

            if (Camera.main != null)
                return Camera.main;

            return explorationCamera;
        }

        /// <summary>진행 중인 메시지 타이머를 취소한다.</summary>
        private void CancelMessageTask()
        {
            if (messageCts == null)
                return;

            messageCts.Cancel();
            messageCts.Dispose();
            messageCts = null;
        }
    }
}
