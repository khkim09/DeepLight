using Project.Core.Events;
using Project.Data.Input;
using Project.Data.Submarine;
using Project.Gameplay.CameraSystem;
using Project.Gameplay.Services;
using UnityEngine;

namespace Project.Gameplay.UserInput
{
    /// <summary>SO 기반 고정 이동값과 유저 감도를 사용해 잠수함 조종을 처리하는 클래스이다.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TestBedPlayerMover : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ExplorationFollowCameraController explorationCameraController; // 탐사 카메라 컨트롤러

        [Header("Data")]
        [SerializeField] private SubmarineMovementTuningSO movementTuning; // 잠수함 이동 기본값 SO
        [SerializeField] private GameInputBindingsSO inputBindings;        // 공용 입력 바인딩 SO

        [Header("User Settings")]
        [SerializeField] private SubmarineControlUserSettings userSettings = default; // 유저 감도 옵션

        [Header("Time Flow")]
        [SerializeField] private float minimumActiveSpeedForTimeFlow = 0.15f; // 이 속도 이상일 때만 탐사 시간이 흐른다

        private Rigidbody rb;
        private SubmarineMovementRuntimeSettings runtimeSettings;
        private GameTimeService gameTimeService; // 탐사 이동 기반 시간 진행 서비스

        private float currentForwardSpeed;
        private float currentVerticalSpeed;
        private float verticalVelocityRef;

        private bool isHarvestMode;
        private bool isExternallyLocked;
        private bool isInventoryOpen; // 인벤토리 열림 상태 플래그 추가

        // direct delta 회전 누적값
        private float targetYaw;
        private float targetSteeringPitch;
        private float currentYaw;
        private float currentSteeringPitch;
        private float basePitch;

        // 회전 스무딩 참조값
        private float yawVelocityRef;
        private float pitchVelocityRef;

        // Update -> FixedUpdate 전달용 입력 캐시
        private float currentThrottleInput;
        private float currentVerticalInput;
        private bool isCurrentBoostPressed;

        // 공개 property
        public bool IsMovementLocked => isHarvestMode || isExternallyLocked; // 이동 조작 잠금 여부
        public float CurrentWorldSpeed => rb != null ? rb.linearVelocity.magnitude : 0f; // 현재 실제 이동 속도

        /// <summary>시간 서비스를 초기 주입한다.</summary>
        public void Initialize(GameTimeService newGameTimeService)
        {
            gameTimeService = newGameTimeService;
        }

        /// <summary>강체와 런타임 세팅을 초기화한다.</summary>
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            RebuildRuntimeSettings();
        }

        /// <summary>시작 시 현재 회전을 내부 누적값에 동기화한다.</summary>
        private void Start()
        {
            SyncAccumulatedRotation();
        }

        /// <summary>에디터 값 변경 시 런타임 세팅을 다시 계산한다.</summary>
        private void OnValidate()
        {
            RebuildRuntimeSettings();
        }

        /// <summary>이벤트를 구독한다.</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Subscribe<InventoryUIToggledEvent>(OnInventoryToggled);
        }

        /// <summary>이벤트 구독을 해제한다.</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
            EventBus.Unsubscribe<InventoryUIToggledEvent>(OnInventoryToggled);
        }

        /// <summary>입력을 캐싱하고 렌더 프레임 기준으로 회전을 갱신한다.</summary>
        private void Update()
        {
            if (movementTuning == null || inputBindings == null)
                return;

            if (IsMovementLocked)
            {
                currentThrottleInput = 0f;
                currentVerticalInput = 0f;
                isCurrentBoostPressed = false;
                SyncAccumulatedRotation();
                return;
            }

            // 키 입력은 Update에서 읽고 이동은 FixedUpdate에서 적용한다.
            currentThrottleInput = GetThrottleInput();
            currentVerticalInput = GetVerticalInput();
            isCurrentBoostPressed = Input.GetKey(inputBindings.BoostKey);

            // 자유시점 중이 아니며, 인벤토리 패널이 닫혀있을 때만 마우스 회전을 허용한다.
            if (explorationCameraController != null && !explorationCameraController.IsFreeLookActive && !isInventoryOpen)
            {
                float rawMouseX = Input.GetAxisRaw("Mouse X");
                float rawMouseY = Input.GetAxisRaw("Mouse Y");

                targetYaw += rawMouseX * runtimeSettings.YawSensitivity;
                targetSteeringPitch -= rawMouseY * runtimeSettings.PitchSensitivity;
                targetSteeringPitch = Mathf.Clamp(
                    targetSteeringPitch,
                    -runtimeSettings.MaxPitchAngle,
                    runtimeSettings.MaxPitchAngle);
            }

            CalculateAndApplyRotation(Time.deltaTime);
        }

        /// <summary>물리 프레임 기준으로 선형 이동을 적용한다.</summary>
        private void FixedUpdate()
        {
            if (movementTuning == null || inputBindings == null)
                return;

            float fixedDeltaTime = Time.fixedDeltaTime;

            if (IsMovementLocked)
            {
                currentForwardSpeed = Mathf.MoveTowards(
                    currentForwardSpeed,
                    0f,
                    runtimeSettings.BrakeDeceleration * fixedDeltaTime);

                currentVerticalSpeed = Mathf.MoveTowards(
                    currentVerticalSpeed,
                    0f,
                    runtimeSettings.BrakeDeceleration * fixedDeltaTime);
            }
            else
            {
                UpdateThrottle(currentThrottleInput, isCurrentBoostPressed, fixedDeltaTime);
                UpdateVerticalMovement(currentVerticalInput, fixedDeltaTime);
            }

            ApplyMovement();
            AdvanceExplorationTime(fixedDeltaTime);
        }

        /// <summary>목표 회전을 향해 현재 회전을 부드럽게 적용한다.</summary>
        private void CalculateAndApplyRotation(float deltaTime)
        {
            currentYaw = Mathf.SmoothDampAngle(
                currentYaw,
                targetYaw,
                ref yawVelocityRef,
                runtimeSettings.RotationSmoothTime,
                Mathf.Infinity,
                deltaTime);

            currentSteeringPitch = Mathf.SmoothDampAngle(
                currentSteeringPitch,
                targetSteeringPitch,
                ref pitchVelocityRef,
                runtimeSettings.RotationSmoothTime,
                Mathf.Infinity,
                deltaTime);

            if (Mathf.Abs(currentForwardSpeed) > 0.01f && runtimeSettings.MaxForwardSpeed > 0.01f)
            {
                float speedRatio = currentForwardSpeed / runtimeSettings.MaxForwardSpeed;
                float loopRate = (currentSteeringPitch / runtimeSettings.MaxPitchAngle)
                                 * runtimeSettings.LoopRotationSpeed
                                 * speedRatio;

                basePitch += loopRate * deltaTime;
            }

            Quaternion targetLocalRotation = Quaternion.Euler(
                basePitch + currentSteeringPitch,
                currentYaw,
                0f);

            transform.rotation = transform.parent != null
                ? transform.parent.rotation * targetLocalRotation
                : targetLocalRotation;
        }

        /// <summary>현재 계산된 선형 속도를 강체에 적용한다.</summary>
        private void ApplyMovement()
        {
            Vector3 forwardVelocity = (transform.rotation * Vector3.forward) * currentForwardSpeed;
            Vector3 verticalVelocity = Vector3.up * currentVerticalSpeed;
            rb.linearVelocity = forwardVelocity + verticalVelocity;
        }

        /// <summary>실제 이동 중일 때만 탐사 시간을 전진시킨다.</summary>
        private void AdvanceExplorationTime(float deltaTime)
        {
            if (gameTimeService == null || IsMovementLocked)
                return;

            if (rb == null)
                return;

            // 실제 이동 속도를 기준으로 시간을 흐르게 한다.
            // 가만히 서 있거나, 입력만 넣고 실제 위치 변화가 거의 없으면 시간은 정지한다.
            float speed = rb.linearVelocity.magnitude;
            if (speed < minimumActiveSpeedForTimeFlow)
                return;

            gameTimeService.AdvanceByActiveRealSeconds(deltaTime);
        }

        /// <summary>현재 트랜스폼 회전을 내부 누적값에 맞춘다.</summary>
        private void SyncAccumulatedRotation()
        {
            Vector3 euler = transform.localEulerAngles;

            targetYaw = currentYaw = euler.y;
            basePitch = euler.x;

            if (basePitch > 180f)
                basePitch -= 360f;

            targetSteeringPitch = currentSteeringPitch = 0f;
        }

        /// <summary>전후진 입력값을 반환한다.</summary>
        private float GetThrottleInput()
        {
            if (Input.GetKey(inputBindings.MoveForwardKey))
                return 1f;

            if (Input.GetKey(inputBindings.MoveBackwardKey))
                return -1f;

            return 0f;
        }

        /// <summary>상승과 하강 입력값을 반환한다.</summary>
        private float GetVerticalInput()
        {
            if (Input.GetKey(inputBindings.AscendKey))
                return 1f;

            if (Input.GetKey(inputBindings.DescendKey))
                return -1f;

            return 0f;
        }

        /// <summary>전후진 입력에 따라 현재 선속을 계산한다.</summary>
        private void UpdateThrottle(float throttleInput, bool isBoostPressed, float deltaTime)
        {
            if (Mathf.Approximately(throttleInput, 0f))
            {
                currentForwardSpeed = Mathf.MoveTowards(
                    currentForwardSpeed,
                    0f,
                    runtimeSettings.IdleDrag * deltaTime);
                return;
            }

            float appliedBoost = isBoostPressed ? runtimeSettings.BoostMultiplier : 1f;

            if (throttleInput > 0f)
            {
                if (currentForwardSpeed < 0f)
                {
                    currentForwardSpeed = Mathf.MoveTowards(
                        currentForwardSpeed,
                        0f,
                        runtimeSettings.BrakeDeceleration * deltaTime);
                }
                else
                {
                    currentForwardSpeed = Mathf.MoveTowards(
                        currentForwardSpeed,
                        runtimeSettings.MaxForwardSpeed * appliedBoost,
                        runtimeSettings.ForwardAcceleration * appliedBoost * deltaTime);
                }
            }
            else
            {
                if (currentForwardSpeed > 0f)
                {
                    currentForwardSpeed = Mathf.MoveTowards(
                        currentForwardSpeed,
                        0f,
                        runtimeSettings.BrakeDeceleration * deltaTime);
                }
                else
                {
                    currentForwardSpeed = Mathf.MoveTowards(
                        currentForwardSpeed,
                        -runtimeSettings.MaxReverseSpeed,
                        runtimeSettings.ReverseAcceleration * deltaTime);
                }
            }
        }

        /// <summary>수직 입력에 따라 상승과 하강 속도를 계산한다.</summary>
        private void UpdateVerticalMovement(float verticalInput, float deltaTime)
        {
            float targetVerticalSpeed = 0f;

            if (verticalInput > 0f)
                targetVerticalSpeed = runtimeSettings.AscendSpeed;

            if (verticalInput < 0f)
                targetVerticalSpeed = -runtimeSettings.DescendSpeed;

            currentVerticalSpeed = Mathf.SmoothDamp(
                currentVerticalSpeed,
                targetVerticalSpeed,
                ref verticalVelocityRef,
                runtimeSettings.VerticalSmoothTime,
                Mathf.Infinity,
                deltaTime);
        }

        /// <summary>Harvest 진입 시 잠수함 이동을 정지시킨다.</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            StopImmediately();
        }

        /// <summary>Harvest 종료 시 잠금 상태를 해제한다.</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
        }

        /// <summary>인벤토리 토글 이벤트를 수신하여 상태를 갱신한다.</summary>
        private void OnInventoryToggled(InventoryUIToggledEvent publishedEvent)
        {
            isInventoryOpen = publishedEvent.IsOpen;
        }

        /// <summary>현재 이동 속도를 즉시 0으로 만든다.</summary>
        public void StopImmediately()
        {
            currentForwardSpeed = 0f;
            currentVerticalSpeed = 0f;
            verticalVelocityRef = 0f;

            if (rb != null)
                rb.linearVelocity = Vector3.zero;
        }

        /// <summary>외부 연출에 따라 조작 잠금을 설정한다.</summary>
        public void SetExternalControlLock(bool shouldLock)
        {
            isExternallyLocked = shouldLock;

            if (shouldLock)
                StopImmediately();
        }

        /// <summary>옵션 시스템이 감도 배율을 갱신할 때 호출한다.</summary>
        public void ApplyUserSettings(SubmarineControlUserSettings newUserSettings)
        {
            userSettings = newUserSettings;
            RebuildRuntimeSettings();
        }

        /// <summary>SO와 유저 감도를 합쳐 실제 사용 세팅을 다시 계산한다.</summary>
        private void RebuildRuntimeSettings()
        {
            if (movementTuning == null)
                return;

            runtimeSettings = movementTuning.BuildRuntimeSettings(userSettings);
        }
    }
}
