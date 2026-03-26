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

        private readonly List<TargetVisibilityEntry> cachedEntries = new List<TargetVisibilityEntry>();

        private void Awake()
        {
            if (autoFindTargetsOnAwake)
                RebuildCache();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            RestoreAllTargets();
        }

        public void Initialize(HarvestModeSession newHarvestModeSession)
        {
            harvestModeSession = newHarvestModeSession;
        }

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

        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            if (harvestModeSession == null)
                return;

            if (cachedEntries.Count <= 0)
                RebuildCache();

            HarvestTargetBehaviour activeTarget = harvestModeSession.CurrentTarget as HarvestTargetBehaviour;
            ApplyVisibilityForActiveTarget(activeTarget);
        }

        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            RestoreAllTargets();
        }

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

        private static bool IsSceneObject(HarvestTargetBehaviour target)
        {
            if (target == null)
                return false;

            return !string.IsNullOrEmpty(target.gameObject.scene.name);
        }

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

                List<Behaviour> collectedBehaviours = new List<Behaviour>();
                collectedBehaviours.AddRange(target.GetComponentsInChildren<HarvestInteractionZone>(true));
                collectedBehaviours.AddRange(target.GetComponentsInChildren<HarvestTargetHighlightController>(true));
                collectedBehaviours.AddRange(target.GetComponentsInChildren<HarvestPointBillboard>(true));
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
