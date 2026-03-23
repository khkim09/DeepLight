using Project.Core.Events;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>탐사 카메라와 채집 카메라 전환</summary>
    public class PerspectiveSwapController : MonoBehaviour
    {
        [SerializeField] private Camera explorationCamera; // 3D 탐사 카메라
        [SerializeField] private Camera harvestCamera; // 2D 채집 카메라

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

        /// <summary>초기 카메라 상태 설정</summary>
        private void Start()
        {
            SetExplorationView(); // 탐사 카메라 활성
        }

        /// <summary>채집 모드 진입 시 카메라 전환</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            SetHarvestView(); // 채집 화면 활성
        }

        /// <summary>채집 모드 종료 시 카메라 복귀</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            SetExplorationView(); // 탐사 화면 복귀
        }

        /// <summary>탐사 카메라 상태 적용</summary>
        private void SetExplorationView()
        {
            if (explorationCamera != null)
                explorationCamera.gameObject.SetActive(true);

            if (harvestCamera != null)
                harvestCamera.gameObject.SetActive(false);
        }

        /// <summary>채집 카메라 상태 적용</summary>
        private void SetHarvestView()
        {
            if (explorationCamera != null)
                explorationCamera.gameObject.SetActive(false);

            if (harvestCamera != null)
                harvestCamera.gameObject.SetActive(true);
        }
    }
}
