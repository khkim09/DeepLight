using Project.Core.Events;
using Project.Data.Enums;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Gameplay.UI
{
    /// <summary>Harvest HUD의 타깃, 포인트 개수, 센서 상태, 회수 미리보기를 갱신하는 클래스이다.</summary>
    public class HarvestConsoleHUD : MonoBehaviour
    {
        [Header("Visual Root")]
        [SerializeField] private CanvasGroup canvasGroup; // Harvest HUD 표시/숨김용 CanvasGroup

        [Header("Dynamic Visibility")]
        [Tooltip("채집 성공 시 화면에서 숨길 UI 오브젝트 배열입니다.")]
        [SerializeField] private GameObject[] hideOnSuccessObjects; // 성공 시 숨길 대상들

        [Header("Target")]
        [SerializeField] private TMP_Text targetNameText; // 현재 타깃 이름

        [Header("Point Count Visual")]
        [SerializeField] private Image pointCountImage; // 포인트 개수 표시 이미지
        [SerializeField] private Sprite[] pointCountSprites; // 포인트 개수별 스프라이트 배열 (index 0 = 1개, index 1 = 2개 ...)

        [Header("Sensor")]
        [SerializeField] private TMP_Text sensorModeText; // 현재 센서 모드 텍스트
        [SerializeField] private Image sensorModeImage; // 현재 센서 모드 아이콘
        [SerializeField] private Sprite sonarSprite; // 소나 아이콘
        [SerializeField] private Sprite lidarSprite; // 라이다 아이콘
        [SerializeField] private Color sonarColor = new Color(0.45f, 0.9f, 1f, 1f); // 소나 컬러
        [SerializeField] private Color lidarColor = new Color(1f, 0.8f, 0.35f, 1f); // 라이다 컬러
        [SerializeField] private Color noneColor = Color.white; // 기본 컬러

        [Header("Recovery Preview")]
        [SerializeField] private Slider recoveryChanceSlider; // 예상 성공률 슬라이더

        private int currentTotalPointCount; // 현재 총 포인트 개수 캐시

        /// <summary>Harvest HUD 관련 데이터 이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestRecoveryPreviewUpdatedEvent>(OnRecoveryPreviewUpdated);
            EventBus.Subscribe<HarvestConsoleTargetPreparedEvent>(OnTargetPrepared);
            EventBus.Subscribe<HarvestScanModeChangedEvent>(OnScanModeChanged);
            EventBus.Subscribe<HarvestRecoveryResolvedEvent>(OnHarvestRecoveryResolved); // 채집 결과 이벤트 구독
        }

        /// <summary>Harvest HUD 관련 데이터 이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestRecoveryPreviewUpdatedEvent>(OnRecoveryPreviewUpdated);
            EventBus.Unsubscribe<HarvestConsoleTargetPreparedEvent>(OnTargetPrepared);
            EventBus.Unsubscribe<HarvestScanModeChangedEvent>(OnScanModeChanged);
            EventBus.Unsubscribe<HarvestRecoveryResolvedEvent>(OnHarvestRecoveryResolved); // 채집 결과 이벤트 해제
        }

        /// <summary>시작 시 HUD를 숨기고 기본값을 넣는다.</summary>
        private void Start()
        {
            SetHudVisible(false);
            ResetPreviewUi();
        }

        /// <summary>외부 허브에서 Harvest HUD 표시 여부를 제어한다.</summary>
        public void SetHudVisible(bool isVisible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = isVisible ? 1f : 0f;
            canvasGroup.interactable = isVisible;
            canvasGroup.blocksRaycasts = isVisible;

            if (!isVisible)
            {
                ResetPreviewUi();
            }
            else
            {
                // HUD가 켜질 때(탐사 -> 채집 재진입 시) 숨겼던 오브젝트들을 전부 다시 켜준다.
                SetDynamicObjectsActive(true);
            }
        }

        /// <summary>채집 결과를 수신하여 성공 시 지정된 UI들을 숨긴다.</summary>
        private void OnHarvestRecoveryResolved(HarvestRecoveryResolvedEvent publishedEvent)
        {
            if (publishedEvent.IsSuccess)
            {
                SetDynamicObjectsActive(false);
            }
        }

        /// <summary>동적 시각 요소(배열)의 켜고 끄기를 일괄 처리한다.</summary>
        private void SetDynamicObjectsActive(bool isActive)
        {
            if (hideOnSuccessObjects == null || hideOnSuccessObjects.Length == 0)
                return;

            for (int i = 0; i < hideOnSuccessObjects.Length; i++)
            {
                if (hideOnSuccessObjects[i] != null)
                    hideOnSuccessObjects[i].SetActive(isActive);
            }
        }

        /// <summary>예상 성공률과 예상 비용 UI를 갱신한다.</summary>
        private void OnRecoveryPreviewUpdated(HarvestRecoveryPreviewUpdatedEvent publishedEvent)
        {
            if (recoveryChanceSlider != null)
                recoveryChanceSlider.value = Mathf.Clamp01(publishedEvent.EstimatedRecoveryChance);
        }

        /// <summary>현재 대상 이름과 총 포인트 개수를 갱신한다.</summary>
        private void OnTargetPrepared(HarvestConsoleTargetPreparedEvent publishedEvent)
        {
            if (targetNameText != null)
                targetNameText.text = publishedEvent.DisplayName;

            currentTotalPointCount = publishedEvent.TotalPointCount;
            RefreshPointCountVisual(currentTotalPointCount);
        }

        /// <summary>현재 센서 모드 텍스트와 아이콘을 갱신한다.</summary>
        private void OnScanModeChanged(HarvestScanModeChangedEvent publishedEvent)
        {
            HarvestScanMode scanMode = (HarvestScanMode)publishedEvent.ScanMode;

            if (sensorModeText != null)
            {
                sensorModeText.text = scanMode switch
                {
                    HarvestScanMode.Sonar => "Sonar",
                    HarvestScanMode.Lidar => "Lidar",
                    _ => "None"
                };

                sensorModeText.color = scanMode switch
                {
                    HarvestScanMode.Sonar => sonarColor,
                    HarvestScanMode.Lidar => lidarColor,
                    _ => noneColor
                };
            }

            if (sensorModeImage != null)
            {
                switch (scanMode)
                {
                    case HarvestScanMode.Sonar:
                        sensorModeImage.sprite = sonarSprite;
                        sensorModeImage.color = sonarColor;
                        sensorModeImage.enabled = sonarSprite != null;
                        break;

                    case HarvestScanMode.Lidar:
                        sensorModeImage.sprite = lidarSprite;
                        sensorModeImage.color = lidarColor;
                        sensorModeImage.enabled = lidarSprite != null;
                        break;

                    default:
                        sensorModeImage.sprite = null;
                        sensorModeImage.color = noneColor;
                        sensorModeImage.enabled = false;
                        break;
                }
            }
        }

        /// <summary>포인트 개수에 맞는 스프라이트를 단일 이미지에 반영한다.</summary>
        private void RefreshPointCountVisual(int totalCount)
        {
            if (pointCountImage == null)
                return;

            if (pointCountSprites == null || pointCountSprites.Length == 0)
            {
                pointCountImage.enabled = false;
                return;
            }

            int spriteIndex = totalCount - 1;
            if (spriteIndex < 0 || spriteIndex >= pointCountSprites.Length || pointCountSprites[spriteIndex] == null)
            {
                pointCountImage.enabled = false;
                return;
            }

            pointCountImage.enabled = true;
            pointCountImage.sprite = pointCountSprites[spriteIndex];
        }

        /// <summary>미리보기 UI를 초기값으로 되돌린다.</summary>
        private void ResetPreviewUi()
        {
            currentTotalPointCount = 0;

            if (recoveryChanceSlider != null)
                recoveryChanceSlider.value = 0f;

            if (sensorModeText != null)
            {
                sensorModeText.text = "Sonar";
                sensorModeText.color = sonarColor;
            }

            if (sensorModeImage != null)
            {
                sensorModeImage.sprite = sonarSprite;
                sensorModeImage.color = sonarColor;
                sensorModeImage.enabled = sonarSprite != null;
            }

            if (targetNameText != null)
                targetNameText.text = string.Empty;

            RefreshPointCountVisual(0);
        }
    }
}
