using System.Collections.Generic;
using Project.Core.Events;
using Project.Data.Harvest;
using Project.Data.Input;
using Project.Data.UI;
using Project.Gameplay.GameModes;
using Project.Gameplay.Harvest;
using UnityEngine;

namespace Project.Gameplay.Interaction
{
    /// <summary>트리거 존 기반으로 채집 대상을 감지하고 Harvest 진입 입력을 처리하는 클래스이다.</summary>
    public class HarvestPointInteractor : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GameInputBindingsSO inputBindings; // 공용 입력 바인딩 SO

        [Header("References")]
        [SerializeField] private HarvestApproachController approachController; // 자연스러운 접근 연출 제어기

        private HarvestModeCoordinator harvestModeCoordinator; // 채집 모드 조정기
        private readonly List<HarvestInteractionZone> overlappedZones = new(); // 현재 겹친 상호작용 존 목록

        private IHarvestTarget currentTarget; // 현재 상호작용 대상
        private HarvestInteractionZone currentZone; // 현재 선택된 상호작용 존
        private HarvestTargetHighlightController currentHighlight; // 현재 하이라이트 컨트롤러
        private HarvestRetryPenaltyService harvestRetryPenaltyService; // 타깃별 재시도 패널티 서비스

        private bool isHarvestMode; // Harvest 모드 여부
        private bool currentTargetInteractable; // 현재 대상 실제 상호작용 가능 여부

        /// <summary>현재 채집 진입 키를 반환한다.</summary>
        public KeyCode InteractKey => inputBindings != null ? inputBindings.InteractHarvestKey : KeyCode.None;

        /// <summary>현재 대상 정보를 반환한다.</summary>
        public IHarvestTarget CurrentTarget => currentTarget;

        /// <summary>현재 대상이 실제 상호작용 가능한지 반환한다.</summary>
        public bool IsCurrentTargetInteractable => currentTargetInteractable;

        /// <summary>채집 모드 조정기와 재시도 패널티 서비스를 주입한다.</summary>
        public void Initialize(
            HarvestModeCoordinator newHarvestModeCoordinator,
            HarvestRetryPenaltyService newHarvestRetryPenaltyService)
        {
            harvestModeCoordinator = newHarvestModeCoordinator;
            harvestRetryPenaltyService = newHarvestRetryPenaltyService;
        }

        /// <summary>Harvest 모드 이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>Harvest 모드 이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            SetCurrentFocus(null, null);
        }

        /// <summary>탐사 모드에서 상호작용 대상을 갱신하고 입력을 처리한다.</summary>
        private void Update()
        {
            if (isHarvestMode)
                return;

            // 매 프레임 현재 겹친 zone 기준으로 실제 포커스 대상을 다시 계산
            UpdateCurrentZone();

            if (harvestModeCoordinator == null || inputBindings == null)
                return;

            if (!Input.GetKeyDown(inputBindings.InteractHarvestKey))
                return;

            // 핵심:
            // zone 안에 실제로 들어가 있지 않다면 F를 눌러도 아무 반응도 하지 않는다.
            if (!HasValidInteractionContext())
                return;

            if (currentTarget == null || currentTarget.TargetData == null || !currentTarget.TargetData.IsValid())
                return;

            // zone 안에 들어와 있고, 그 대상이 블락 상태일 때만 불가 메시지를 띄운다.
            if (!currentTargetInteractable)
            {
                EventBus.Publish(new HarvestTargetInteractMessageEvent(GetCurrentTargetUnavailableMessage()));
                return;
            }

            if (approachController == null)
            {
                harvestModeCoordinator.TryEnterHarvestMode(currentTarget);
                return;
            }

            _ = approachController.TryApproachAndEnterHarvestAsync(currentZone, harvestModeCoordinator);
        }

        /// <summary>Harvest 진입 시 탐사 상호작용을 잠근다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            SetCurrentFocus(null, null);
            ClearInteractionPrompt();
        }

        /// <summary>Harvest 종료 시 탐사 상호작용을 복구한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;

            if (approachController != null)
                approachController.EndApproachLook();
        }

        /// <summary>현재 대상의 표시 이름을 반환한다.</summary>
        public string GetCurrentTargetDisplayName()
        {
            if (currentTarget == null || currentTarget.TargetData == null)
                return string.Empty;

            if (currentTarget is HarvestTargetBehaviour targetBehaviour)
            {
                if (!string.IsNullOrWhiteSpace(targetBehaviour.RuntimePreviewDisplayName))
                    return targetBehaviour.RuntimePreviewDisplayName;
            }

            return !string.IsNullOrWhiteSpace(currentTarget.TargetData.TargetId)
                ? currentTarget.TargetData.TargetId
                : string.Empty;
        }

        /// <summary>현재 프롬프트를 배치할 월드 좌표를 반환한다.</summary>
        public Vector3 GetCurrentPromptWorldPosition()
        {
            if (currentZone == null)
                return transform.position;

            Bounds bounds = currentZone.GetTargetBounds();
            return bounds.center + Vector3.up * (bounds.extents.y + 0.55f);
        }

        /// <summary>현재 대상의 상호작용 불가 메시지를 반환한다.</summary>
        public string GetCurrentTargetUnavailableMessage()
        {
            // 재시도 패널티가 최우선
            if (harvestRetryPenaltyService != null && currentTarget != null)
            {
                string penaltyText = harvestRetryPenaltyService.GetRemainingPenaltyDisplayText(currentTarget);
                if (!string.IsNullOrWhiteSpace(penaltyText))
                    return $"Retry blocked. Remaining time: {penaltyText}";
            }

            if (currentTarget is HarvestTargetBehaviour targetBehaviour)
            {
                string reason = targetBehaviour.GetUnavailableReason();
                if (!string.IsNullOrWhiteSpace(reason))
                    return reason;
            }

            if (currentTarget != null && !currentTarget.IsAvailable)
                return "This target is currently unavailable for harvesting.";

            return "This target cannot be harvested at the moment.";
        }

        /// <summary>현재 입력 시점에 실제 상호작용 가능한 컨텍스트가 있는지 검사한다.</summary>
        private bool HasValidInteractionContext()
        {
            // 현재 zone/target이 없으면 상호작용 컨텍스트가 없다.
            if (currentZone == null || currentTarget == null)
                return false;

            // 비활성화된 zone은 이미 상호작용 컨텍스트가 끝난 상태다.
            if (!currentZone.gameObject.activeInHierarchy)
                return false;

            // overlapped 목록에 현재 zone이 실제로 남아 있어야 한다.
            if (!overlappedZones.Contains(currentZone))
                return false;

            // 현재 zone이 파괴되었거나 target이 비어 있으면 무효
            if (currentZone.HarvestTarget == null)
                return false;

            // 현재 zone이 currentTarget과 동일한 대상을 바라보는지도 다시 확인
            if (currentZone.HarvestTarget != currentTarget)
                return false;

            // 이미 소모된 target은 zone 안에 있었더라도 더 이상 현재 상호작용 컨텍스트가 아니다.
            if (!currentTarget.IsAvailable)
                return false;

            return true;
        }

        /// <summary>겹친 존 중 현재 상호작용할 대상을 선정한다.</summary>
        private void UpdateCurrentZone()
        {
            HarvestInteractionZone bestZone = null;
            IHarvestTarget bestTarget = null;
            float bestScore = float.MinValue;

            for (int i = overlappedZones.Count - 1; i >= 0; i--)
            {
                HarvestInteractionZone zone = overlappedZones[i];

                // 파괴되었거나 비활성화된 zone은 stale entry이므로 즉시 정리한다.
                if (zone == null || !zone.gameObject.activeInHierarchy)
                {
                    overlappedZones.RemoveAt(i);
                    continue;
                }

                IHarvestTarget target = zone.HarvestTarget;

                // 타깃 참조가 없거나 이미 소모된 대상도 더 이상 후보가 아니다.
                if (target == null || !target.IsAvailable)
                {
                    overlappedZones.RemoveAt(i);
                    continue;
                }

                Vector3 toTarget = zone.GetTargetCenter() - transform.position;
                float distance = toTarget.magnitude;
                if (distance <= 0.0001f)
                    continue;

                Vector3 direction = toTarget.normalized;
                float alignment = Vector3.Dot(transform.forward, direction);
                float score = alignment * 2f - distance * 0.15f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestZone = zone;
                    bestTarget = target;
                }
            }

            SetCurrentFocus(bestZone, bestTarget);
        }

        /// <summary>현재 포커스 대상과 하이라이트 및 이벤트 상태를 갱신한다.</summary>
        private void SetCurrentFocus(HarvestInteractionZone nextZone, IHarvestTarget nextTarget)
        {
            bool nextInteractable = EvaluateTargetInteractable(nextZone, nextTarget);

            bool isSameZone = currentZone == nextZone;
            bool isSameTarget = currentTarget == nextTarget;
            bool isSameInteractable = currentTargetInteractable == nextInteractable;

            if (isSameZone && isSameTarget && isSameInteractable)
                return;

            ClearCurrentHighlightWithoutEvent();

            currentZone = nextZone;
            currentTarget = nextTarget;
            currentTargetInteractable = nextInteractable;

            if (currentZone == null || currentTarget == null)
            {
                EventBus.Publish(new HarvestTargetUnfocusedEvent());
                ClearInteractionPrompt();
                return;
            }

            ApplyCurrentHighlight();

            EventBus.Publish(new HarvestTargetFocusedEvent(
                GetCurrentTargetDisplayName(),
                InteractKey,
                currentTargetInteractable));

            PublishInteractionPrompt();
        }

        /// <summary>현재 대상의 실제 상호작용 가능 여부를 평가한다.</summary>
        private bool EvaluateTargetInteractable(HarvestInteractionZone zone, IHarvestTarget target)
        {
            if (zone == null || target == null)
                return false;

            // 이미 성공 처리되어 사라졌거나 사용 불가인 대상 차단
            if (!target.IsAvailable)
                return false;

            // 스토리 day 조건 차단
            HarvestTargetBehaviour targetBehaviour = zone.TargetBehaviour;
            if (targetBehaviour != null && !targetBehaviour.IsHarvestUnlocked)
                return false;

            // 재시도 패널티 차단
            if (harvestRetryPenaltyService != null && harvestRetryPenaltyService.IsRetryBlocked(target))
                return false;

            return true;
        }

        /// <summary>현재 타깃에 하이라이트를 적용한다. 불가 대상은 하이라이트하지 않는다.</summary>
        private void ApplyCurrentHighlight()
        {
            HarvestTargetBehaviour targetBehaviour = currentZone != null ? currentZone.TargetBehaviour : null;
            if (targetBehaviour == null)
                return;

            currentHighlight = targetBehaviour.GetComponentInParent<HarvestTargetHighlightController>();
            if (currentHighlight == null)
                currentHighlight = targetBehaviour.GetComponent<HarvestTargetHighlightController>();

            if (currentHighlight == null)
                return;

            if (currentTargetInteractable)
                currentHighlight.SetHighlight(true);
            else
                currentHighlight.SetHighlight(false);
        }

        /// <summary>현재 하이라이트를 이벤트 없이 해제한다.</summary>
        private void ClearCurrentHighlightWithoutEvent()
        {
            if (currentHighlight == null)
                return;

            currentHighlight.SetHighlight(false);
            currentHighlight = null;
        }

        /// <summary>현재 상태에 맞는 상호작용 프롬프트를 발행한다.</summary>
        private void PublishInteractionPrompt()
        {
            InteractionPromptType promptType = EvaluateCurrentPromptType();
            if (promptType == InteractionPromptType.None)
            {
                ClearInteractionPrompt();
                return;
            }

            EventBus.Publish(new InteractionPromptChangedEvent((int)promptType, InteractKey));
        }

        /// <summary>현재 상태를 평가해 표시할 프롬프트 타입을 결정한다.</summary>
        private InteractionPromptType EvaluateCurrentPromptType()
        {
            if (isHarvestMode)
                return InteractionPromptType.None;

            if (currentZone == null || currentTarget == null)
                return InteractionPromptType.None;

            if (currentTargetInteractable)
                return InteractionPromptType.HarvestAvailable;

            return InteractionPromptType.None;
        }

        /// <summary>상호작용 프롬프트를 숨기도록 이벤트를 발행한다.</summary>
        private void ClearInteractionPrompt()
        {
            EventBus.Publish(new InteractionPromptClearedEvent());
        }

        /// <summary>상호작용 존 진입 시 후보 목록에 추가한다.</summary>
        private void OnTriggerEnter(Collider other)
        {
            HarvestInteractionZone zone = other.GetComponent<HarvestInteractionZone>();
            if (zone == null)
                return;

            if (!zone.gameObject.activeInHierarchy)
                return;

            if (zone.HarvestTarget == null || !zone.HarvestTarget.IsAvailable)
                return;

            if (!overlappedZones.Contains(zone))
                overlappedZones.Add(zone);
        }

        /// <summary>상호작용 존 이탈 시 후보 목록에서 제거한다.</summary>
        private void OnTriggerExit(Collider other)
        {
            HarvestInteractionZone zone = other.GetComponent<HarvestInteractionZone>();
            if (zone == null)
                return;

            overlappedZones.Remove(zone);

            if (currentZone == zone)
                SetCurrentFocus(null, null);
        }
    }
}
