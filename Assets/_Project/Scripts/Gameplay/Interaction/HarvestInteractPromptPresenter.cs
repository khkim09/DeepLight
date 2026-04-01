using Project.Core.Events;
using TMPro;
using UnityEngine;

namespace Project.Gameplay.Interaction
{
    /// <summary>채집 상호작용 프롬프트 디버깅</summary>
    public class HarvestInteractPromptPresenter : MonoBehaviour
    {
        [SerializeField] private HarvestPointInteractor harvestPointInteractor; // 상호작용 감지기
        [SerializeField] private TMP_Text promptText; // 프롬프트 텍스트

        private bool isHarvestMode; // 현재 채집 모드 여부

        /// <summary>이벤트 구독 등록</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독 해제</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>프롬프트 텍스트를 갱신</summary>
        private void Update()
        {
            // 텍스트 없으면 중단
            if (promptText == null) return;

            // 감지기 없으면 숨김
            if (harvestPointInteractor == null)
            {
                promptText.gameObject.SetActive(false);
                return;
            }

            // 채집 모드 중이면 숨김
            if (isHarvestMode)
            {
                promptText.gameObject.SetActive(false);
                return;
            }

            // // 대상 없으면 숨김
            // if (!harvestPointInteractor.HasAvailableTarget())
            // {
            //     promptText.gameObject.SetActive(false);
            //     return;
            // }

            // 표시 이름 가져오기
            string displayName = harvestPointInteractor.GetCurrentTargetDisplayName();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                promptText.gameObject.SetActive(false);
                return;
            }

            // 프롬프트 노출
            promptText.gameObject.SetActive(true);
            promptText.text = $"[{harvestPointInteractor.InteractKey}] 채집 시작 : {displayName}";
        }

        /// <summary>채집 모드 진입</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
        }

        /// <summary>채집 모드 종료</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
        }
    }
}
