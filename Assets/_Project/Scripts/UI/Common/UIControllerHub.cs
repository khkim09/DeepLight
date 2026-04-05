using Project.Core.Events;
using Project.UI.Harvest;
using UnityEngine;

namespace Project.Gameplay.UI
{
    /// <summary>상위 UI 흐름과 HUD/팝업 표시 전환을 관리하는 클래스이다.</summary>
    public class UIControllerHub : MonoBehaviour
    {
        [Header("HUD Controllers")]
        [SerializeField] private HarvestConsoleHUD harvestConsoleHud; // Harvest HUD 컨트롤러

        [Header("Overlay Presenters")]
        [SerializeField] private HarvestFailureReasonPopupPresenter failureReasonPopupPresenter; // 실패 이유 팝업
        [SerializeField] private SubmarineDangerFeedbackPresenter dangerFeedbackPresenter; // 위험 피해 피드백

        /// <summary>UI 관련 이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>UI 관련 이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>시작 시 Harvest HUD와 오버레이를 숨긴다.</summary>
        private void Start()
        {
            if (harvestConsoleHud != null)
                harvestConsoleHud.SetHudVisible(false);

            if (failureReasonPopupPresenter != null)
                failureReasonPopupPresenter.HideImmediate();

            if (dangerFeedbackPresenter != null)
                dangerFeedbackPresenter.HideImmediate();
        }

        /// <summary>Harvest 카메라 전환 완료 시 Harvest HUD를 표시한다.</summary>
        private void OnHarvestCameraTransitionCompleted(HarvestCameraTransitionCompletedEvent publishedEvent)
        {
            if (harvestConsoleHud != null)
                harvestConsoleHud.SetHudVisible(true);

            if (failureReasonPopupPresenter != null)
                failureReasonPopupPresenter.HideImmediate();
        }

        /// <summary>Harvest 종료 시 HUD와 오버레이를 정리한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            if (harvestConsoleHud != null)
                harvestConsoleHud.SetHudVisible(false);

            if (failureReasonPopupPresenter != null)
                failureReasonPopupPresenter.HideImmediate();

            if (dangerFeedbackPresenter != null)
                dangerFeedbackPresenter.HideImmediate();
        }
    }
}
