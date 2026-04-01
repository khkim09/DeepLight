using System.Collections.Generic;
using Project.Core.Events;
using Project.Data.Harvest;
using Project.Data.Input;
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
        private bool isHarvestMode; // Harvest 모드 여부

        /// <summary>현재 채집 진입 키를 반환한다.</summary>
        public KeyCode InteractKey => inputBindings != null ? inputBindings.InteractHarvestKey : KeyCode.None;

        /// <summary>현재 대상 정보를 반환한다.</summary>
        public IHarvestTarget CurrentTarget => currentTarget;

        /// <summary>채집 모드 조정기를 주입한다.</summary>
        public void Initialize(HarvestModeCoordinator newHarvestModeCoordinator)
        {
            harvestModeCoordinator = newHarvestModeCoordinator;
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
        }

        /// <summary>탐사 모드에서 상호작용 대상을 갱신하고 입력을 처리한다.</summary>
        private void Update()
        {
            if (isHarvestMode) return;

            // 현재 겹친 존 중 가장 적절한 대상을 재선정한다.
            UpdateCurrentZone();

            if (harvestModeCoordinator == null || inputBindings == null) return;

            // 타깃이 없거나 데이터가 유효하지 않으면 입력 무시
            if (currentTarget == null || currentTarget.TargetData == null || !currentTarget.TargetData.IsValid()) return;

            if (Input.GetKeyDown(inputBindings.InteractHarvestKey))
            {
                // 타깃이 감지되었으나 채집 불가능(이미 채집함 등) 상태일 때 툴팁 이벤트 발행
                if (!currentTarget.IsAvailable)
                {
                    EventBus.Publish(new HarvestTargetInteractMessageEvent("현재는 채집할 수 없는 대상입니다."));
                    return;
                }

                // 접근 연출이 없으면 즉시 진입한다.
                if (approachController == null)
                {
                    harvestModeCoordinator.TryEnterHarvestMode(currentTarget);
                    return;
                }

                _ = approachController.TryApproachAndEnterHarvestAsync(currentZone, harvestModeCoordinator);
            }
        }

        /// <summary>Harvest 진입 시 탐사 상호작용을 잠근다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            ClearCurrentHighlight();
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
            if (currentTarget == null || currentTarget.TargetData == null) return string.Empty;

            if (currentTarget is HarvestTargetBehaviour targetBehaviour)
            {
                if (!string.IsNullOrWhiteSpace(targetBehaviour.RuntimePreviewDisplayName))
                    return targetBehaviour.RuntimePreviewDisplayName;
            }

            return !string.IsNullOrWhiteSpace(currentTarget.TargetData.TargetId)
                ? currentTarget.TargetData.TargetId
                : string.Empty;
        }

        /// <summary>겹친 존 중 현재 상호작용할 대상을 선정한다.</summary>
        private void UpdateCurrentZone()
        {
            ClearCurrentHighlight();
            currentTarget = null;
            currentZone = null;

            float bestScore = float.MinValue;

            for (int i = overlappedZones.Count - 1; i >= 0; i--)
            {
                HarvestInteractionZone zone = overlappedZones[i];
                if (zone == null || zone.HarvestTarget == null)
                {
                    overlappedZones.RemoveAt(i);
                    continue;
                }

                Vector3 toTarget = zone.GetTargetCenter() - transform.position;
                float distance = toTarget.magnitude;
                if (distance <= 0.0001f) continue;

                Vector3 direction = toTarget.normalized;
                float alignment = Vector3.Dot(transform.forward, direction);
                float score = alignment * 2f - distance * 0.15f;

                if (score > bestScore)
                {
                    bestScore = score;
                    currentZone = zone;
                    currentTarget = zone.HarvestTarget;
                }
            }

            if (currentZone != null)
                ApplyCurrentHighlight();
        }

        /// <summary>현재 타깃에 하이라이트를 적용하고 UI 이벤트를 발행한다.</summary>
        private void ApplyCurrentHighlight()
        {
            HarvestTargetBehaviour targetBehaviour = currentZone != null ? currentZone.TargetBehaviour : null;
            if (targetBehaviour == null) return;

            currentHighlight = targetBehaviour.GetComponentInParent<HarvestTargetHighlightController>();
            if (currentHighlight == null)
                currentHighlight = targetBehaviour.GetComponent<HarvestTargetHighlightController>();

            if (currentHighlight != null)
            {
                currentHighlight.SetHighlight(true);

                // 타깃 이름, 키, 그리고 '채집 가능 여부'를 UI에 전달
                EventBus.Publish(new HarvestTargetFocusedEvent(
                    GetCurrentTargetDisplayName(),
                    InteractKey,
                    currentTarget.IsAvailable));
            }
        }

        /// <summary>현재 하이라이트를 해제하고 UI 숨김 이벤트를 발행한다.</summary>
        private void ClearCurrentHighlight()
        {
            if (currentHighlight != null)
            {
                currentHighlight.SetHighlight(false);
                currentHighlight = null;
            }

            EventBus.Publish(new HarvestTargetUnfocusedEvent());
        }

        /// <summary>상호작용 존 진입 시 후보 목록에 추가한다.</summary>
        private void OnTriggerEnter(Collider other)
        {
            HarvestInteractionZone zone = other.GetComponent<HarvestInteractionZone>();
            if (zone == null) return;

            if (!overlappedZones.Contains(zone))
                overlappedZones.Add(zone);
        }

        /// <summary>상호작용 존 이탈 시 후보 목록에서 제거한다.</summary>
        private void OnTriggerExit(Collider other)
        {
            HarvestInteractionZone zone = other.GetComponent<HarvestInteractionZone>();
            if (zone == null) return;

            overlappedZones.Remove(zone);

            if (currentZone == zone)
            {
                ClearCurrentHighlight();
                currentZone = null;
                currentTarget = null;
            }
        }
    }
}
