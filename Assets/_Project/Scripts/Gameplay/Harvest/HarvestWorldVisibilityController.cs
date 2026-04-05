using System.Collections.Generic;
using System.Linq;
using Project.Core.Events;
using Project.Gameplay.GameModes;
using Project.Gameplay.Interaction;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>Harvest 중 현재 대상만 남기고 나머지 월드 타깃과 스캔 포인트까지 숨기는 클래스</summary>
    public class HarvestWorldVisibilityController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HarvestModeSession harvestModeSession; // 현재 Harvest 세션 참조

        [Header("Options")]
        [SerializeField] private bool autoFindTargetsOnAwake = true; // 시작 시 씬 타깃 자동 수집 여부
        [SerializeField] private bool includeInactiveTargets = true; // 비활성 오브젝트까지 캐싱할지 여부

        private readonly List<TargetVisibilityEntry> cachedEntries = new();

        /// <summary>초기 캐시를 구성한다.</summary>
        private void Awake()
        {
            if (autoFindTargetsOnAwake)
                RebuildCache();
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            RestoreAllTargets();
        }

        /// <summary>외부에서 세션 참조를 주입한다.</summary>
        public void Initialize(HarvestModeSession newHarvestModeSession)
        {
            harvestModeSession = newHarvestModeSession;
        }

        /// <summary>씬 내 전체 타깃 캐시를 다시 만든다.</summary>
        [ContextMenu("Rebuild Target Cache")]
        public void RebuildCache()
        {
            cachedEntries.Clear();

            HarvestTargetBehaviour[] targets = includeInactiveTargets
                ? Resources.FindObjectsOfTypeAll<HarvestTargetBehaviour>()
                    .Where(IsSceneObject)
                    .ToArray()
                : FindObjectsByType<HarvestTargetBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            foreach (HarvestTargetBehaviour target in targets)
            {
                if (target == null)
                    continue;

                cachedEntries.Add(new TargetVisibilityEntry(target));
            }
        }

        /// <summary>Harvest 카메라 전환 완료 후 현재 대상만 남기고 가린다.</summary>
        private void OnHarvestCameraTransitionCompleted(HarvestCameraTransitionCompletedEvent publishedEvent)
        {
            if (harvestModeSession == null || !harvestModeSession.HasTarget)
                return;

            if (cachedEntries.Count <= 0)
                RebuildCache();

            HarvestTargetBehaviour activeTarget = harvestModeSession.CurrentTarget as HarvestTargetBehaviour;
            ApplyVisibilityForActiveTarget(activeTarget);
        }

        /// <summary>Harvest 종료 시 전부 복구한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            RestoreAllTargets();
        }

        /// <summary>현재 대상만 남기고 나머지를 숨긴다.</summary>
        private void ApplyVisibilityForActiveTarget(HarvestTargetBehaviour activeTarget)
        {
            for (int i = 0; i < cachedEntries.Count; i++)
            {
                TargetVisibilityEntry entry = cachedEntries[i];
                if (entry == null || entry.Target == null)
                    continue;

                bool shouldRemainVisible = entry.Target == activeTarget;
                entry.SetVisible(shouldRemainVisible);
            }
        }

        /// <summary>전체 타깃을 원래 상태로 복원한다.</summary>
        private void RestoreAllTargets()
        {
            for (int i = 0; i < cachedEntries.Count; i++)
            {
                TargetVisibilityEntry entry = cachedEntries[i];
                if (entry == null)
                    continue;

                entry.Restore();
            }
        }

        /// <summary>씬 오브젝트인지 여부를 판정한다.</summary>
        private static bool IsSceneObject(HarvestTargetBehaviour target)
        {
            if (target == null)
                return false;

            return !string.IsNullOrEmpty(target.gameObject.scene.name);
        }

        /// <summary>타깃 1개에 대한 원래 가시성 상태를 저장하고 복원한다.</summary>
        private sealed class TargetVisibilityEntry
        {
            private readonly HarvestTargetBehaviour target;
            private readonly Renderer[] renderers;
            private readonly Collider[] colliders;
            private readonly Behaviour[] behaviours;
            private readonly HarvestScanPoint[] scanPoints;
            private readonly GameObject[] ringVisualRoots;

            private readonly bool[] rendererStates;
            private readonly bool[] colliderStates;
            private readonly bool[] behaviourStates;
            private readonly bool[] scanPointStates;
            private readonly bool[] ringRootStates;

            public HarvestTargetBehaviour Target => target;

            public TargetVisibilityEntry(HarvestTargetBehaviour target)
            {
                this.target = target;

                renderers = target.GetComponentsInChildren<Renderer>(true);
                colliders = target.GetComponentsInChildren<Collider>(true);

                List<Behaviour> collectedBehaviours = new();
                collectedBehaviours.AddRange(target.GetComponentsInChildren<HarvestInteractionZone>(true));
                collectedBehaviours.AddRange(target.GetComponentsInChildren<HarvestTargetHighlightController>(true));
                behaviours = collectedBehaviours.Where(component => component != null).Distinct().ToArray();

                scanPoints = target.GetComponentsInChildren<HarvestScanPoint>(true);

                ringVisualRoots = target.GetComponentsInChildren<Transform>(true)
                    .Where(child => child != null && child.gameObject.layer == LayerMask.NameToLayer("HarvestPointVisual"))
                    .Select(child => child.gameObject)
                    .Distinct()
                    .ToArray();

                rendererStates = renderers.Select(component => component.enabled).ToArray();
                colliderStates = colliders.Select(component => component.enabled).ToArray();
                behaviourStates = behaviours.Select(component => component.enabled).ToArray();
                scanPointStates = scanPoints.Select(component => component.enabled).ToArray();
                ringRootStates = ringVisualRoots.Select(component => component.activeSelf).ToArray();
            }

            public void SetVisible(bool shouldBeVisible)
            {
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null)
                        renderers[i].enabled = shouldBeVisible && rendererStates[i];

                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null)
                        colliders[i].enabled = shouldBeVisible && colliderStates[i];

                for (int i = 0; i < behaviours.Length; i++)
                    if (behaviours[i] != null)
                        behaviours[i].enabled = shouldBeVisible && behaviourStates[i];

                for (int i = 0; i < scanPoints.Length; i++)
                    if (scanPoints[i] != null)
                        scanPoints[i].enabled = shouldBeVisible && scanPointStates[i];

                for (int i = 0; i < ringVisualRoots.Length; i++)
                    if (ringVisualRoots[i] != null)
                        ringVisualRoots[i].SetActive(shouldBeVisible && ringRootStates[i]);
            }

            public void Restore()
            {
                for (int i = 0; i < renderers.Length; i++)
                    if (renderers[i] != null)
                        renderers[i].enabled = rendererStates[i];

                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null)
                        colliders[i].enabled = colliderStates[i];

                for (int i = 0; i < behaviours.Length; i++)
                    if (behaviours[i] != null)
                        behaviours[i].enabled = behaviourStates[i];

                for (int i = 0; i < scanPoints.Length; i++)
                    if (scanPoints[i] != null)
                        scanPoints[i].enabled = scanPointStates[i];

                for (int i = 0; i < ringVisualRoots.Length; i++)
                    if (ringVisualRoots[i] != null)
                        ringVisualRoots[i].SetActive(ringRootStates[i]);
            }
        }
    }
}
