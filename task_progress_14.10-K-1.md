# Phase 14.10-K-1 Task Progress

- [x] Read and analyze current implementation files
- [x] Read related tag/binding component files
- [ ] Modify WorldMapRuntimeSpawnInstanceRegistry.cs:
  - [ ] Remove WorldMapRuntimeSpawnPlaceholderTag exclusion from RebuildCacheFromRoot
  - [ ] Add IsUnderNamedAncestor helper
  - [ ] Add IsUnderRuntimeSpawnPreviewInstances helper
  - [ ] Keep IsUnderRuntimeSpawnedInstances (already exists)
  - [ ] Update RebuildCacheFromRoot filter logic per requirements
- [ ] Modify DeepLightMapRuntimeSpawnInstanceRegistryUtility.cs:
  - [ ] Fix RuntimeSpawnedInstances root search (recursive, not direct child)
  - [ ] Remove WorldMapRuntimeSpawnPlaceholderTag exclusion from Validate
  - [ ] Add IsUnderRuntimeSpawnPreviewInstances helper
  - [ ] Update Validate scene count to use same filter as RebuildCache
  - [ ] Strengthen Validate policy (FAIL when scene has instances but registry is 0)
  - [ ] Add auto-rebuild when cache is stale (count mismatch)
- [ ] Verify no UnityEditor API in gameplay file
- [ ] Verify public API preserved
