using System.Collections.Generic;
using System.Linq;
using Project.Data.Harvest;
using Project.Data.Items;
using UnityEngine;

namespace Project.Gameplay.Harvest
{
    /// <summary>채집 대상의 런타임 포인트와 실제 드랍 아이템을 관리한다.</summary>
    public class HarvestTargetBehaviour : MonoBehaviour, IHarvestTarget
    {
        [SerializeField] private HarvestTargetSO targetData; // 현재 채집 대상 데이터
        [SerializeField] private bool isAvailable = true; // 현재 채집 가능 여부
        [SerializeField] private Transform rotationPivot; // 회전 피벗 참조
        [SerializeField] private HarvestScanPoint[] scanPoints; // 런타임 포인트 캐시

        private readonly List<HarvestScanPoint> runtimePointCache = new(); // 런타임 생성 포인트 캐시
        private string runtimePreviewDisplayName = string.Empty; // 런타임 위장 이름
        private ItemSO runtimeResolvedItem; // 런타임 확정 드랍 아이템

        public HarvestTargetSO TargetData => targetData;
        public bool IsAvailable => isAvailable;
        public string RuntimePreviewDisplayName => runtimePreviewDisplayName;
        public ItemSO RuntimeResolvedItem => runtimeResolvedItem;

        /// <summary>초기 참조와 런타임 데이터를 구성한다.</summary>
        private void Awake()
        {
            ResolveRotationPivot();
            RebuildRuntimeData();
            ResetScanPoints();
        }

        /// <summary>비활성화 시 포인트 상태를 초기화한다.</summary>
        private void OnDisable()
        {
            ResetScanPoints();
        }

        /// <summary>현재 회전 피벗을 반환한다.</summary>
        public Transform GetRotationPivot()
        {
            ResolveRotationPivot();
            return rotationPivot != null ? rotationPivot : transform;
        }

        /// <summary>런타임 확정 아이템을 반환한다.</summary>
        public ItemSO GetResolvedItem()
        {
            return runtimeResolvedItem;
        }

        /// <summary>target data를 교체하고 런타임 구성을 다시 만든다.</summary>
        public void SetTargetData(HarvestTargetSO newTargetData)
        {
            targetData = newTargetData;
            RebuildRuntimeData();
            ResetScanPoints();
        }

        /// <summary>채집 성공 후 대상 상태를 갱신한다.</summary>
        public void Consume()
        {
            if (!isAvailable)
                return;

            isAvailable = false;
            ResetScanPoints();

            if (targetData != null && targetData.ConsumeOnSuccess)
                gameObject.SetActive(false);
        }

        /// <summary>레거시 충돌 반응을 처리한다.</summary>
        public void OnClawCollision()
        {
        }

        /// <summary>표시 순서 기준으로 회수 포인트를 반환한다.</summary>
        public IReadOnlyList<HarvestScanPoint> GetScanPoints()
        {
            if (scanPoints == null || scanPoints.Length == 0)
                CacheScanPoints();

            return scanPoints
                .Where(point => point != null)
                .OrderBy(point => point.DisplayOrder)
                .ToArray();
        }

        /// <summary>모든 회수 포인트 상태를 초기화한다.</summary>
        public void ResetScanPoints()
        {
            if (scanPoints == null)
                return;

            for (int i = 0; i < scanPoints.Length; i++)
            {
                if (scanPoints[i] == null)
                    continue;

                scanPoints[i].ResetRuntimeState();
            }
        }

        /// <summary>회전 피벗 참조를 보정한다.</summary>
        private void ResolveRotationPivot()
        {
            if (rotationPivot != null)
                return;

            HarvestTargetRotationPivot marker = GetComponentInChildren<HarvestTargetRotationPivot>(true);
            rotationPivot = marker != null ? marker.transform : transform;
        }

        /// <summary>현재 회전 피벗 하위의 포인트를 캐싱한다.</summary>
        private void CacheScanPoints()
        {
            Transform pivot = GetRotationPivot();
            scanPoints = pivot.GetComponentsInChildren<HarvestScanPoint>(true);
        }

        /// <summary>이름, 아이템, 포인트를 런타임 기준으로 다시 구성한다.</summary>
        private void RebuildRuntimeData()
        {
            ResolveRotationPivot();
            ClearRuntimePoints();

            if (targetData == null || !targetData.IsValid())
            {
                runtimePreviewDisplayName = string.Empty;
                runtimeResolvedItem = null;
                scanPoints = System.Array.Empty<HarvestScanPoint>();
                return;
            }

            // 회수 전 UI에서 보여줄 위장 이름을 1회 뽑아 고정한다.
            runtimePreviewDisplayName = targetData.BuildPreviewDisplayName();

            // 실제 획득 아이템도 target 생성 시 1회 뽑아 고정한다.
            runtimeResolvedItem = targetData.PickRandomItem();

            // 포인트를 런타임 생성한다.
            GenerateRuntimePoints();
            CacheScanPoints();
        }

        /// <summary>기존 런타임 포인트를 정리한다.</summary>
        private void ClearRuntimePoints()
        {
            for (int i = runtimePointCache.Count - 1; i >= 0; i--)
            {
                if (runtimePointCache[i] != null)
                    Destroy(runtimePointCache[i].gameObject);
            }

            runtimePointCache.Clear();
            scanPoints = System.Array.Empty<HarvestScanPoint>();
        }

        /// <summary>현재 target data 기준으로 포인트를 런타임 생성한다.</summary>
        private void GenerateRuntimePoints()
        {
            if (targetData == null || !targetData.IsValid())
                return;

            List<PlacedFacePoint> placedPoints = new();

            for (int i = 0; i < targetData.PointPresets.Count; i++)
            {
                HarvestScanPointPresetSO preset = targetData.PointPresets[i];
                if (preset == null)
                    continue;

                if (!TryBuildSpawnTransform(targetData, preset.AllowedFaces, placedPoints, out FacePlacement placement))
                    continue;

                GameObject pointObject = Instantiate(targetData.ScanPointPrefab, GetRotationPivot());
                pointObject.name = $"ScanPoint_{preset.PointId}";
                pointObject.transform.localPosition = placement.LocalPosition;
                pointObject.transform.localRotation = Quaternion.Euler(placement.LocalEulerAngles);
                pointObject.transform.localScale = Vector3.one;

                HarvestScanPoint point = pointObject.GetComponent<HarvestScanPoint>();
                if (point == null)
                    continue;

                // preset 기반으로 identity/stats/visual set을 주입한다.
                point.Initialize(preset, preset.BuildRuntimeStats());

                runtimePointCache.Add(point);
                placedPoints.Add(new PlacedFacePoint(placement.Face, placement.PlaneCoordA, placement.PlaneCoordB));
            }
        }

        /// <summary>허용된 면 규칙 안에서 겹치지 않는 포인트 생성 위치를 찾는다.</summary>
        private bool TryBuildSpawnTransform(
            HarvestTargetSO data,
            HarvestPointAllowedFaces allowedFaces,
            List<PlacedFacePoint> placedPoints,
            out FacePlacement placement)
        {
            const int maxAttempts = 48;

            List<CubeFace> candidateFaces = BuildCandidateFaces(allowedFaces);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                CubeFace face = candidateFaces[Random.Range(0, candidateFaces.Count)];

                // 면 평면 좌표를 범위 내 랜덤으로 뽑는다.
                float a = Random.Range(data.PlanarMin, data.PlanarMax);
                float b = Random.Range(data.PlanarMin, data.PlanarMax);

                if (IsOverlapping(data, face, a, b, placedPoints))
                    continue;

                placement = BuildPlacement(data, face, a, b);
                return true;
            }

            placement = default;
            return false;
        }

        /// <summary>허용 면 마스크를 실제 face 후보 목록으로 변환한다.</summary>
        private List<CubeFace> BuildCandidateFaces(HarvestPointAllowedFaces allowedFaces)
        {
            List<CubeFace> result = new();

            if (allowedFaces.HasFlag(HarvestPointAllowedFaces.PositiveX))
                result.Add(CubeFace.PositiveX);
            if (allowedFaces.HasFlag(HarvestPointAllowedFaces.NegativeX))
                result.Add(CubeFace.NegativeX);
            if (allowedFaces.HasFlag(HarvestPointAllowedFaces.PositiveY))
                result.Add(CubeFace.PositiveY);
            if (allowedFaces.HasFlag(HarvestPointAllowedFaces.NegativeY))
                result.Add(CubeFace.NegativeY);
            if (allowedFaces.HasFlag(HarvestPointAllowedFaces.PositiveZ))
                result.Add(CubeFace.PositiveZ);
            if (allowedFaces.HasFlag(HarvestPointAllowedFaces.NegativeZ))
                result.Add(CubeFace.NegativeZ);

            if (result.Count == 0)
                result.Add(CubeFace.PositiveY);

            return result;
        }

        /// <summary>같은 면에서 최소 간격 규칙을 위반하는지 검사한다.</summary>
        private bool IsOverlapping(
            HarvestTargetSO data,
            CubeFace face,
            float a,
            float b,
            List<PlacedFacePoint> placedPoints)
        {
            for (int i = 0; i < placedPoints.Count; i++)
            {
                PlacedFacePoint other = placedPoints[i];
                if (other.Face != face)
                    continue;

                float deltaA = Mathf.Abs(other.PlaneCoordA - a);
                float deltaB = Mathf.Abs(other.PlaneCoordB - b);

                // 두 축 모두 최소 간격보다 작으면 겹침으로 본다.
                if (deltaA < data.MinPointSpacing && deltaB < data.MinPointSpacing)
                    return true;
            }

            return false;
        }

        /// <summary>선택된 면과 평면 좌표를 실제 local transform으로 변환한다.</summary>
        private FacePlacement BuildPlacement(HarvestTargetSO data, CubeFace face, float a, float b)
        {
            float offset = data.FaceSurfaceOffset;

            return face switch
            {
                CubeFace.PositiveX => new FacePlacement(face, new Vector3(offset, a, b), new Vector3(0f, 90f, 0f), a, b),
                CubeFace.NegativeX => new FacePlacement(face, new Vector3(-offset, a, b), new Vector3(0f, -90f, 0f), a, b),
                CubeFace.PositiveY => new FacePlacement(face, new Vector3(a, offset, b), new Vector3(-90f, 0f, 0f), a, b),
                CubeFace.NegativeY => new FacePlacement(face, new Vector3(a, -offset, b), new Vector3(90f, 0f, 0f), a, b),
                CubeFace.PositiveZ => new FacePlacement(face, new Vector3(a, b, offset), new Vector3(0f, 0f, 0f), a, b),
                _ => new FacePlacement(face, new Vector3(a, b, -offset), new Vector3(0f, 180f, 0f), a, b)
            };
        }

        /// <summary>큐브의 생성 면 종류이다.</summary>
        private enum CubeFace
        {
            PositiveX,
            NegativeX,
            PositiveY,
            NegativeY,
            PositiveZ,
            NegativeZ
        }

        /// <summary>같은 면 내 이미 배치된 평면 좌표를 보관한다.</summary>
        private readonly struct PlacedFacePoint
        {
            public readonly CubeFace Face;
            public readonly float PlaneCoordA;
            public readonly float PlaneCoordB;

            public PlacedFacePoint(CubeFace face, float planeCoordA, float planeCoordB)
            {
                Face = face;
                PlaneCoordA = planeCoordA;
                PlaneCoordB = planeCoordB;
            }
        }

        /// <summary>생성에 성공한 면과 local transform 결과를 보관한다.</summary>
        private readonly struct FacePlacement
        {
            public readonly CubeFace Face;
            public readonly Vector3 LocalPosition;
            public readonly Vector3 LocalEulerAngles;
            public readonly float PlaneCoordA;
            public readonly float PlaneCoordB;

            public FacePlacement(CubeFace face, Vector3 localPosition, Vector3 localEulerAngles, float planeCoordA, float planeCoordB)
            {
                Face = face;
                LocalPosition = localPosition;
                LocalEulerAngles = localEulerAngles;
                PlaneCoordA = planeCoordA;
                PlaneCoordB = planeCoordB;
            }
        }
    }
}
