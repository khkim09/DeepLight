using System.Threading;
using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Data.Input;
using UnityEngine;

namespace Project.UI.Inventory
{
    /// <summary>Tab 키로 토글되는 글로벌 인벤토리 패널의 슬라이드 연출과 상태를 관리한다.</summary>
    public class InventoryOverlayPresenter : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameInputBindingsSO inputBindings; // 공용 입력 바인딩

        [Header("References")]
        [SerializeField] private RectTransform panelRect; // InventoryOverlay
        [SerializeField] private RectTransform tabRect; // TabButton

        [Header("Animation Settings")]
        [SerializeField] private float slideDuration = 0.25f; // 슬라이드 애니메이션 시간

        private bool isOpen; // 현재 인벤토리가 열려있는지 여부
        private GameModeType currentMode; // 현재 게임 모드 캐싱
        private CancellationTokenSource slideCts; // 슬라이드 취소 토큰

        private float openPositionX = 0f;
        private float closedPositionX;

        /// <summary>초기 상태 설정 및 해상도에 따른 닫힘 위치를 계산한다.</summary>
        private void Awake()
        {
            isOpen = false;

            // 패널 넓이에서 탭 넓이를 뺀 만큼 우측으로 밀어내면 탭만 보이게 된다.
            float panelWidth = panelRect.rect.width;
            float tabWidth = tabRect != null ? tabRect.rect.width : 50f;
            closedPositionX = panelWidth;

            // Tab이 부모 영역 밖에 있으므로 부모 사이즈만큼만 우측으로 밀면 됨.
            panelRect.anchoredPosition = new Vector2(closedPositionX, panelRect.anchoredPosition.y);

            EventBus.Subscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Subscribe<HarvestRecoveryResolvedEvent>(OnHarvestRecoveryResolved);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독 해제 및 태스크를 정리한다.</summary>
        private void OnDestroy()
        {
            EventBus.Unsubscribe<GameModeChangedEvent>(OnGameModeChanged);
            EventBus.Unsubscribe<HarvestRecoveryResolvedEvent>(OnHarvestRecoveryResolved);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);

            CancelSlideTask();
        }

        /// <summary>매 프레임 Tab 키 입력을 감지하여 인벤토리를 토글한다.</summary>
        private void Update()
        {
            if (inputBindings == null) return;

            if (Input.GetKeyDown(inputBindings.HarvestInventoryKey))
                ToggleInventory();
        }

        /// <summary>인벤토리의 열림/닫힘 상태를 전환하고 애니메이션을 시작한다.</summary>
        public void ToggleInventory()
        {
            isOpen = !isOpen;

            // 다른 시스템(플레이어 회전 잠금 등)에 상태 알림
            EventBus.Publish(new InventoryUIToggledEvent(isOpen));

            // 모드에 맞춰 커서 잠금 해제/설정
            UpdateCursorState();

            // 슬라이드 애니메이션 실행
            PlaySlideTask(isOpen ? openPositionX : closedPositionX).Forget();
        }

        /// <summary>게임 모드 변경 시 현재 모드를 캐싱한다.</summary>
        private void OnGameModeChanged(GameModeChangedEvent evt)
        {
            currentMode = evt.CurrentMode;

            // 채집 모드에서 빠져나올 때 인벤토리가 열려있다면 커서를 닫지 않고 유지하기 위함
            UpdateCursorState();
        }

        /// <summary>채집 결과 이벤트를 수신하여 성공 시 인벤토리를 자동으로 연다.</summary>
        private void OnHarvestRecoveryResolved(HarvestRecoveryResolvedEvent evt)
        {
            // 성공적으로 아이템이 인벤토리에 들어갔고, 인벤토리가 닫혀있을 때만 연다.
            if (evt.IsSuccess && evt.AddedToInventory && !isOpen)
                ToggleInventory();
        }

        // 채집 모드가 종료될 때 인벤토리가 열려있다면 자동으로 닫는다.
        private void OnHarvestModeExited(HarvestModeExitedEvent evt)
        {
            if (isOpen) ToggleInventory();
        }

        /// <summary>현재 모드와 인벤토리 상태에 따라 마우스 커서를 제어한다.</summary>
        private void UpdateCursorState()
        {
            // Harvest 모드일 때는 HarvestConsoleController가 이미 커서를 자유롭게 관리하므로 간섭하지 않는다.
            if (currentMode == GameModeType.HarvestConsole)
                return;

            // 탐사 모드일 경우 인벤토리가 열리면 커서를 표시하고, 닫히면 다시 잠근다.
            if (currentMode == GameModeType.Exploration3D)
            {
                if (isOpen)
                {
                    Cursor.lockState = CursorLockMode.Confined;
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
            }
        }

        /// <summary>기존 진행 중인 슬라이드 태스크가 있다면 취소한다.</summary>
        private void CancelSlideTask()
        {
            if (slideCts != null)
            {
                slideCts.Cancel();
                slideCts.Dispose();
                slideCts = null;
            }
        }

        /// <summary>지정된 목표 X 좌표로 패널을 부드럽게 이동시킨다.</summary>
        private async UniTask PlaySlideTask(float targetX)
        {
            CancelSlideTask();
            slideCts = new CancellationTokenSource();
            CancellationToken token = slideCts.Token;

            float startX = panelRect.anchoredPosition.x;
            float time = 0f;

            while (time < slideDuration)
            {
                time += Time.deltaTime;
                float normalizedTime = time / slideDuration;

                // 자연스러운 감속(Ease-Out) 연출을 위해 Sine 곡선 적용
                float curvedTime = Mathf.Sin(normalizedTime * Mathf.PI * 0.5f);

                panelRect.anchoredPosition = new Vector2(
                    Mathf.Lerp(startX, targetX, curvedTime),
                    panelRect.anchoredPosition.y);

                bool isCanceled = await UniTask.Yield(PlayerLoopTiming.Update, token).SuppressCancellationThrow();
                if (isCanceled) return;
            }

            panelRect.anchoredPosition = new Vector2(targetX, panelRect.anchoredPosition.y);
        }
    }
}
