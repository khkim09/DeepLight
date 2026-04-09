using Project.Core.Events;
using Project.Data.Enums;
using Project.Data.Submarine;
using UnityEngine;

namespace Project.Gameplay.Runtime
{
    /// <summary>탐사/Harvest 상태에 따라 실제 조명과 단일 빔 비주얼을 제어한다.</summary>
    public class SubmarineLightController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private SubmarineLightTuningSO lightTuning; // 조명/빔 튜닝 데이터

        [Header("Top Exploration World Light")]
        [SerializeField] private Light topCenterWorldLight; // 탐사 상단 실제 조명
        [SerializeField] private Transform topFocusRayOrigin; // 탐사 상단 근접 감쇠 검사 원점

        [Header("Top Exploration Beam Visual")]
        [SerializeField] private Transform topBeamVisualOrigin; // 탐사 상단 빔 시작점
        [SerializeField] private Transform topBeamTransform; // 탐사 상단 빔 transform
        [SerializeField] private Renderer topBeamRenderer; // 탐사 상단 빔 renderer

        [Header("Bottom Left Harvest World Light")]
        [SerializeField] private Light bottomLeftHarvestWorldLight; // 좌측 하단 실제 조명
        [SerializeField] private Transform bottomLeftFocusRayOrigin; // 좌측 하단 감쇠 검사 원점

        [Header("Bottom Left Harvest Beam Visual")]
        [SerializeField] private Transform bottomLeftBeamVisualOrigin; // 좌측 하단 빔 시작점
        [SerializeField] private Transform bottomLeftBeamTransform; // 좌측 하단 빔 transform
        [SerializeField] private Renderer bottomLeftBeamRenderer; // 좌측 하단 빔 renderer

        [Header("Bottom Right Harvest World Light")]
        [SerializeField] private Light bottomRightHarvestWorldLight; // 우측 하단 실제 조명
        [SerializeField] private Transform bottomRightFocusRayOrigin; // 우측 하단 감쇠 검사 원점

        [Header("Bottom Right Harvest Beam Visual")]
        [SerializeField] private Transform bottomRightBeamVisualOrigin; // 우측 하단 빔 시작점
        [SerializeField] private Transform bottomRightBeamTransform; // 우측 하단 빔 transform
        [SerializeField] private Renderer bottomRightBeamRenderer; // 우측 하단 빔 renderer

        [Header("Colors")]
        [SerializeField] private Color explorationColor = new(0.97f, 0.985f, 1.00f, 1f); // 탐사 백색
        [SerializeField] private Color sonarColor = new(0.45f, 1.00f, 0.72f, 1f); // 소나 민트
        [SerializeField] private Color lidarColor = new(1.00f, 0.78f, 0.28f, 1f); // 라이다 앰버

        private static readonly int BeamColorId = Shader.PropertyToID("_BeamColor");
        private static readonly int BeamOpacityId = Shader.PropertyToID("_BeamOpacity");
        private static readonly int BeamOriginWsId = Shader.PropertyToID("_BeamOriginWS");
        private static readonly int BeamForwardWsId = Shader.PropertyToID("_BeamForwardWS");
        private static readonly int BeamLengthWsId = Shader.PropertyToID("_BeamLengthWS");
        private static readonly int BeamLengthFadePowerId = Shader.PropertyToID("_LengthFadePower");
        private static readonly int BeamViewRimPowerId = Shader.PropertyToID("_ViewRimPower");
        private static readonly int BeamDepthFadeDistanceId = Shader.PropertyToID("_DepthFadeDistance");

        private MaterialPropertyBlock sharedPropertyBlock; // 빔 셰이더 파라미터 갱신용 블록

        private HarvestScanMode currentScanMode = HarvestScanMode.Sonar; // 현재 Harvest 센서 모드
        private bool isHarvestVisualActive; // Harvest 카메라 전환 완료 후 하단 조명 활성 여부
        private float aperture01; // 0 = 넓게, 1 = 좁게

        /// <summary>초기 조리개 값을 설정한다.</summary>
        private void Awake()
        {
            sharedPropertyBlock ??= new MaterialPropertyBlock();

            if (lightTuning == null) return;

            // 시작 조리개를 SO 기준으로 고정한다.
            aperture01 = Mathf.Clamp(
                lightTuning.DefaultAperture01,
                lightTuning.MinAperture01,
                lightTuning.MaxAperture01);
        }

        /// <summary>초기 시각 상태를 탐사 모드 기준으로 반영한다.</summary>
        private void Start()
        {
            isHarvestVisualActive = false;
            ApplyCurrentVisualState();
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Subscribe<HarvestScanModeChangedEvent>(OnHarvestScanModeChanged);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestCameraTransitionCompletedEvent>(OnHarvestCameraTransitionCompleted);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Unsubscribe<HarvestScanModeChangedEvent>(OnHarvestScanModeChanged);
        }

        /// <summary>마우스 휠로 조리개를 조절한다.</summary>
        private void Update()
        {
            HandleMouseWheelApertureInput();
        }

        /// <summary>이동 후 프레임 기준으로 빔 셰이더의 월드 좌표 파라미터를 갱신한다.</summary>
        private void LateUpdate()
        {
            RefreshBeamRuntimeProperties();
        }

        /// <summary>Harvest 카메라 전환 완료 시 하단 세트만 활성화한다.</summary>
        private void OnHarvestCameraTransitionCompleted(HarvestCameraTransitionCompletedEvent publishedEvent)
        {
            isHarvestVisualActive = true;
            ApplyCurrentVisualState();
        }

        /// <summary>탐사 모드 복귀 시 상단 세트만 활성화한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestVisualActive = false;
            ApplyCurrentVisualState();
        }

        /// <summary>센서 모드 변경 시 하단 빔과 조명 색을 갱신한다.</summary>
        private void OnHarvestScanModeChanged(HarvestScanModeChangedEvent publishedEvent)
        {
            currentScanMode = (HarvestScanMode)publishedEvent.ScanMode;
            ApplyCurrentVisualState();
        }

        /// <summary>휠 업은 조리개를 조이고 휠 다운은 푼다.</summary>
        private void HandleMouseWheelApertureInput()
        {
            if (lightTuning == null)
                return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) <= 0.001f)
                return;

            // 휠 업일수록 더 좁고 긴 빔/조명을 만든다.
            aperture01 += scroll * lightTuning.ApertureWheelStep;
            aperture01 = Mathf.Clamp(
                aperture01,
                lightTuning.MinAperture01,
                lightTuning.MaxAperture01);

            ApplyCurrentVisualState();
        }

        /// <summary>현재 모드/센서/조리개 상태를 반영한다.</summary>
        private void ApplyCurrentVisualState()
        {
            if (lightTuning == null)
                return;

            if (!isHarvestVisualActive)
            {
                ApplyExplorationVisuals();
                DisableHarvestVisuals();
                return;
            }

            DisableExplorationVisuals();
            ApplyHarvestVisuals();
        }

        /// <summary>탐사 상단 실제 조명과 단일 빔 비주얼을 적용한다.</summary>
        private void ApplyExplorationVisuals()
        {
            float innerAngle = lightTuning.GetTopInnerAngle(aperture01);
            float outerAngle = lightTuning.GetTopOuterAngle(aperture01);
            float range = lightTuning.GetTopRange(aperture01);

            // 실제 조명은 근접 타깃 과노출을 막기 위해 감쇠한다.
            Transform rayOrigin = topFocusRayOrigin != null
                ? topFocusRayOrigin
                : topCenterWorldLight != null ? topCenterWorldLight.transform : null;

            float focusMultiplier = EvaluateFocusAssistMultiplier(rayOrigin);
            float worldIntensity = lightTuning.GetTopWorldIntensity(aperture01) * focusMultiplier;

            SetSpotLight(
                topCenterWorldLight,
                true,
                explorationColor,
                worldIntensity,
                innerAngle,
                outerAngle,
                range);

            // 빔 비주얼은 실제 광량과 분리된 순수 렌더링 오브젝트다.
            ApplyBeamVisual(
                topBeamVisualOrigin,
                topBeamTransform,
                topBeamRenderer,
                lightTuning.GetTopBeamScale(aperture01),
                lightTuning.GetTopBeamOpacity(aperture01),
                explorationColor);
        }

        /// <summary>Harvest 하단 실제 조명과 단일 빔 비주얼을 적용한다.</summary>
        private void ApplyHarvestVisuals()
        {
            Color sensorColor = currentScanMode == HarvestScanMode.Lidar
                ? lidarColor
                : sonarColor;

            float innerAngle = lightTuning.GetBottomInnerAngle(aperture01);
            float outerAngle = lightTuning.GetBottomOuterAngle(aperture01);
            float range = lightTuning.GetBottomRange(aperture01);

            // 좌측 실제 조명 적용
            Transform leftRayOrigin = bottomLeftFocusRayOrigin != null
                ? bottomLeftFocusRayOrigin
                : bottomLeftHarvestWorldLight != null ? bottomLeftHarvestWorldLight.transform : null;

            float leftFocusMultiplier = EvaluateFocusAssistMultiplier(leftRayOrigin);
            float leftWorldIntensity = lightTuning.GetBottomWorldIntensity(aperture01) * leftFocusMultiplier;

            SetSpotLight(
                bottomLeftHarvestWorldLight,
                true,
                sensorColor,
                leftWorldIntensity,
                innerAngle,
                outerAngle,
                range);

            // 우측 실제 조명 적용
            Transform rightRayOrigin = bottomRightFocusRayOrigin != null
                ? bottomRightFocusRayOrigin
                : bottomRightHarvestWorldLight != null ? bottomRightHarvestWorldLight.transform : null;

            float rightFocusMultiplier = EvaluateFocusAssistMultiplier(rightRayOrigin);
            float rightWorldIntensity = lightTuning.GetBottomWorldIntensity(aperture01) * rightFocusMultiplier;

            SetSpotLight(
                bottomRightHarvestWorldLight,
                true,
                sensorColor,
                rightWorldIntensity,
                innerAngle,
                outerAngle,
                range);

            // 좌우 빔 비주얼은 같은 색과 튜닝을 공유한다.
            Vector3 beamScale = lightTuning.GetBottomBeamScale(aperture01);
            float beamOpacity = lightTuning.GetBottomBeamOpacity(aperture01);

            ApplyBeamVisual(
                bottomLeftBeamVisualOrigin,
                bottomLeftBeamTransform,
                bottomLeftBeamRenderer,
                beamScale,
                beamOpacity,
                sensorColor);

            ApplyBeamVisual(
                bottomRightBeamVisualOrigin,
                bottomRightBeamTransform,
                bottomRightBeamRenderer,
                beamScale,
                beamOpacity,
                sensorColor);
        }

        /// <summary>탐사 상단 시각 요소를 비활성화한다.</summary>
        private void DisableExplorationVisuals()
        {
            SetSpotLight(topCenterWorldLight, false, explorationColor, 0f, 0f, 0f, 0f);
            DisableBeamVisual(topBeamRenderer);
        }

        /// <summary>Harvest 하단 시각 요소를 비활성화한다.</summary>
        private void DisableHarvestVisuals()
        {
            SetSpotLight(bottomLeftHarvestWorldLight, false, sonarColor, 0f, 0f, 0f, 0f);
            SetSpotLight(bottomRightHarvestWorldLight, false, sonarColor, 0f, 0f, 0f, 0f);

            DisableBeamVisual(bottomLeftBeamRenderer);
            DisableBeamVisual(bottomRightBeamRenderer);
        }

        /// <summary>단일 빔의 스케일과 셰이더 파라미터를 반영한다.</summary>
        private void ApplyBeamVisual(
            Transform origin,
            Transform beamTransform,
            Renderer beamRenderer,
            Vector3 beamScale,
            float beamOpacity,
            Color beamColor)
        {
            if (origin == null)
            {
                DisableBeamVisual(beamRenderer);
                return;
            }

            // cone의 스케일을 직접 갱신해 조리개 변화와 연동한다.
            if (beamTransform != null)
                beamTransform.localScale = beamScale;

            ApplyBeamRenderer(beamRenderer, origin, beamColor, beamOpacity);
        }

        /// <summary>단일 빔 renderer에 셰이더 파라미터를 적용한다.</summary>
        private void ApplyBeamRenderer(Renderer targetRenderer, Transform origin, Color beamColor, float beamOpacity)
        {
            if (targetRenderer == null || origin == null)
                return;

            sharedPropertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.enabled = true;

            // 현재 bounds 기준으로 apex -> far end 길이를 계산한다.
            float beamLengthWs = CalculateProjectedBeamLength(
                targetRenderer,
                origin.position,
                origin.forward);

            sharedPropertyBlock.Clear();
            sharedPropertyBlock.SetColor(BeamColorId, beamColor);
            sharedPropertyBlock.SetFloat(BeamOpacityId, beamOpacity);
            sharedPropertyBlock.SetVector(BeamOriginWsId, origin.position);
            sharedPropertyBlock.SetVector(BeamForwardWsId, origin.forward.normalized);
            sharedPropertyBlock.SetFloat(BeamLengthWsId, beamLengthWs);
            sharedPropertyBlock.SetFloat(BeamLengthFadePowerId, lightTuning.BeamLengthFadePower);
            sharedPropertyBlock.SetFloat(BeamViewRimPowerId, lightTuning.BeamViewRimPower);
            sharedPropertyBlock.SetFloat(BeamDepthFadeDistanceId, lightTuning.BeamDepthFadeDistance);
            targetRenderer.SetPropertyBlock(sharedPropertyBlock);
        }

        /// <summary>빔 renderer를 꺼서 렌더링을 중단한다.</summary>
        private void DisableBeamVisual(Renderer targetRenderer)
        {
            if (targetRenderer != null)
                targetRenderer.enabled = false;
        }

        /// <summary>renderer bounds로부터 빔의 실측 길이를 추정한다.</summary>
        private float CalculateProjectedBeamLength(Renderer targetRenderer, Vector3 originWs, Vector3 forwardWs)
        {
            if (targetRenderer == null)
                return 0.001f;

            Bounds bounds = targetRenderer.bounds;
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            Vector3 normalizedForward = forwardWs.normalized;

            float maxDistance = 0.001f;

            // bounds의 8개 코너를 조사해 가장 먼 전방 점까지의 길이를 찾는다.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(extents, new Vector3(x, y, z));
                        float projectedDistance = Vector3.Dot(corner - originWs, normalizedForward);

                        if (projectedDistance > maxDistance)
                            maxDistance = projectedDistance;
                    }
                }
            }

            return maxDistance;
        }

        /// <summary>근접 타깃 과노출 방지용 실제 광량 배율을 계산한다.</summary>
        private float EvaluateFocusAssistMultiplier(Transform origin)
        {
            if (lightTuning == null || origin == null)
                return 1f;

            Ray ray = new Ray(origin.position, origin.forward);

            if (!Physics.Raycast(
                    ray,
                    out RaycastHit hitInfo,
                    lightTuning.FocusAssistStartDistance,
                    lightTuning.FocusAssistMask))
            {
                return 1f;
            }

            if (hitInfo.distance >= lightTuning.FocusAssistStartDistance)
                return 1f;

            float t = Mathf.InverseLerp(
                lightTuning.FocusAssistStartDistance,
                lightTuning.FocusAssistEndDistance,
                hitInfo.distance);

            return Mathf.Lerp(1f, lightTuning.FocusAssistMinIntensityMultiplier, t);
        }

        /// <summary>실제 spotlight 활성과 조명 파라미터를 반영한다.</summary>
        private void SetSpotLight(
            Light targetLight,
            bool isEnabled,
            Color color,
            float intensity,
            float innerAngle,
            float outerAngle,
            float range)
        {
            if (targetLight == null)
                return;

            targetLight.enabled = isEnabled;
            if (!isEnabled)
                return;

            // 실제 월드 조명은 이 spotlight만 담당한다.
            targetLight.color = color;
            targetLight.intensity = intensity;
            targetLight.range = range;

            if (targetLight.type == LightType.Spot)
            {
                float safeInner = Mathf.Clamp(innerAngle, 0.1f, Mathf.Max(0.11f, outerAngle - 0.01f));
                float safeOuter = Mathf.Max(outerAngle, safeInner + 0.01f);

                targetLight.innerSpotAngle = safeInner;
                targetLight.spotAngle = safeOuter;
            }
        }

        /// <summary>현재 활성 모드 기준으로 빔 셰이더의 월드 좌표 파라미터만 다시 적용한다.</summary>
        private void RefreshBeamRuntimeProperties()
        {
            if (lightTuning == null)
                return;

            if (!isHarvestVisualActive)
            {
                RefreshSingleBeamRuntimeProperties(
                    topBeamVisualOrigin,
                    topBeamRenderer,
                    lightTuning.GetTopBeamOpacity(aperture01),
                    explorationColor);
                return;
            }

            Color sensorColor = currentScanMode == HarvestScanMode.Lidar
                ? lidarColor
                : sonarColor;

            float beamOpacity = lightTuning.GetBottomBeamOpacity(aperture01);

            RefreshSingleBeamRuntimeProperties(
                bottomLeftBeamVisualOrigin,
                bottomLeftBeamRenderer,
                beamOpacity,
                sensorColor);

            RefreshSingleBeamRuntimeProperties(
                bottomRightBeamVisualOrigin,
                bottomRightBeamRenderer,
                beamOpacity,
                sensorColor);
        }

        /// <summary>단일 빔의 셰이더 파라미터를 현재 월드 좌표 기준으로 다시 적용한다.</summary>
        private void RefreshSingleBeamRuntimeProperties(
            Transform origin,
            Renderer beamRenderer,
            float beamOpacity,
            Color beamColor)
        {
            if (origin == null || beamRenderer == null || !beamRenderer.enabled)
                return;

            ApplyBeamRenderer(beamRenderer, origin, beamColor, beamOpacity);
        }
    }
}
