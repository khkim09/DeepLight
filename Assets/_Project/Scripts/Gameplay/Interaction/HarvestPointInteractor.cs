using System.Collections.Generic;
using Project.Core.Events;
using Project.Data.Harvest;
using Project.Data.Input;
using Project.Data.Items;
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

        private HarvestModeCoordinator harvestModeCoordinator;
        private readonly List<HarvestInteractionZone> overlappedZones = new();
        private IHarvestTarget currentTarget;
        private HarvestInteractionZone currentZone;
        private HarvestTargetHighlightController currentHighlight;
        private bool isHarvestMode;

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

        /// <summary>탐사 모드에서 현재 상호작용 대상을 갱신하고 입력을 처리한다.</summary>
        private void Update()
        {
            if (isHarvestMode)
                return;

            UpdateCurrentZone();

            if (harvestModeCoordinator == null || inputBindings == null)
                return;

            if (!HasAvailableTarget())
                return;

            if (!Input.GetKeyDown(inputBindings.InteractHarvestKey))
                return;

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
            ClearCurrentHighlight();
        }

        /// <summary>Harvest 종료 시 탐사 상호작용을 복구한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;

            if (approachController != null)
                approachController.EndApproachLook();
        }

        /// <summary>현재 유효한 채집 대상 존재 여부를 반환한다.</summary>
        public bool HasAvailableTarget()
        {
            if (currentTarget == null)
                return false;

            if (!currentTarget.IsAvailable)
                return false;

            if (currentTarget.TargetData == null)
                return false;

            if (!currentTarget.TargetData.IsValid())
                return false;

            return true;
        }

        /// <summary>현재 대상의 표시 이름을 반환한다.</summary>
        public string GetCurrentTargetDisplayName()
        {
            if (!HasAvailableTarget())
                return string.Empty;

            HarvestTargetSO targetData = currentTarget.TargetData;
            ItemSO itemData = targetData.ItemData;
            if (itemData == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(itemData.DisplayName))
                return itemData.DisplayName;

            return itemData.ItemId;
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
                if (zone == null || zone.HarvestTarget == null || !zone.HarvestTarget.IsAvailable)
                {
                    overlappedZones.RemoveAt(i);
                    continue;
                }

                Vector3 toTarget = zone.GetTargetCenter() - transform.position;
                float distance = toTarget.magnitude;
                if (distance <= 0.0001f)
                    continue;

                Vector3 direction = toTarget.normalized;

                // 정면 우선 + 가까운 대상 우선
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

        /// <summary>현재 타깃에 하이라이트를 적용한다.</summary>
        private void ApplyCurrentHighlight()
        {
            HarvestTargetBehaviour targetBehaviour = currentZone != null ? currentZone.TargetBehaviour : null;
            if (targetBehaviour == null)
                return;

            currentHighlight = targetBehaviour.GetComponentInParent<HarvestTargetHighlightController>();
            if (currentHighlight == null)
                currentHighlight = targetBehaviour.GetComponent<HarvestTargetHighlightController>();

            if (currentHighlight != null)
                currentHighlight.SetHighlight(true);
        }

        /// <summary>현재 하이라이트를 해제한다.</summary>
        private void ClearCurrentHighlight()
        {
            if (currentHighlight == null)
                return;

            currentHighlight.SetHighlight(false);
            currentHighlight = null;
        }

        /// <summary>상호작용 존 진입 시 후보 목록에 추가한다.</summary>
        private void OnTriggerEnter(Collider other)
        {
            HarvestInteractionZone zone = other.GetComponent<HarvestInteractionZone>();
            if (zone == null)
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
            {
                ClearCurrentHighlight();
                currentZone = null;
                currentTarget = null;
            }
        }
    }
}
