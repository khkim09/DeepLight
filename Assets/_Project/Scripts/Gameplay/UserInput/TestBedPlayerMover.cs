using Project.Core.Events;
using Project.Gameplay.CameraSystem;
using UnityEngine;

namespace Project.Gameplay.UserInput
{
    /// <summary>테스트베드에서 탐사 모드 전용 잠수함 이동을 담당하는 클래스</summary>
    [RequireComponent(typeof(CharacterController))]
    public class TestBedPlayerMover : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ExplorationFollowCameraController explorationCameraController; // 탐사 카메라 컨트롤러

        [Header("Throttle")]
        [SerializeField] private float maxForwardSpeed = 7f; // 최대 전진 속도
        [SerializeField] private float maxReverseSpeed = 3.5f; // 최대 후진 속도
        [SerializeField] private float forwardAcceleration = 6f; // 전진 기본 가속도
        [SerializeField] private float reverseAcceleration = 4.5f; // 후진 기본 가속도
        [SerializeField] private float idleDrag = 1.5f; // 입력 없을 때 자연 감속도
        [SerializeField] private float brakeDeceleration = 10f; // 반대 방향 입력 시 감속도
        [SerializeField] private float boostMultiplier = 1.6f; // Shift 가속 배율

        [Header("Launch Resistance")]
        [SerializeField] private float minLaunchAccelerationMultiplier = 0.22f; // 정지 출발 시 최소 가속 배율
        [SerializeField] private float launchResistanceReleaseSpeed = 3f; // 전진 출발 저항이 풀리는 속도
        [SerializeField] private float reverseLaunchResistanceReleaseSpeed = 1.8f; // 후진 출발 저항이 풀리는 속도

        [Header("Rotation")]
        [SerializeField] private float yawSpeed = 85f; // 기본 좌우 조향 속도
        [SerializeField] private float cameraAlignSpeed = 120f; // 카메라 방향 정렬 속도
        [SerializeField] private float steeringAssistStrength = 0.65f; // 카메라 방향 보조 강도

        [Header("Vertical Movement")]
        [SerializeField] private float ascendSpeed = 3f; // 부상 속도
        [SerializeField] private float descendSpeed = 3f; // 하강 속도
        [SerializeField] private float verticalSmoothTime = 0.08f; // 수직 이동 보간 시간

        private CharacterController characterController; // 캐릭터 컨트롤러 참조
        private float currentForwardSpeed; // 현재 전후진 속도
        private float currentVerticalSpeed; // 현재 수직 속도
        private float verticalVelocityRef; // 수직 보간용 참조값
        private bool isHarvestMode; // 현재 채집 모드 여부

        /// <summary>필수 컴포넌트를 캐싱한다</summary>
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
        }

        /// <summary>이벤트 구독을 등록한다</summary>
        private void OnEnable()
        {
            EventBus.Subscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Subscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>이벤트 구독을 해제한다</summary>
        private void OnDisable()
        {
            EventBus.Unsubscribe<HarvestModeEnteredEvent>(OnHarvestModeEntered);
            EventBus.Unsubscribe<HarvestModeExitedEvent>(OnHarvestModeExited);
        }

        /// <summary>탐사 모드에서만 잠수함 이동을 처리한다</summary>
        private void Update()
        {
            // 채집 모드면 이동 금지
            if (isHarvestMode)
            {
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * Time.deltaTime);
                currentVerticalSpeed = Mathf.MoveTowards(currentVerticalSpeed, 0f, brakeDeceleration * Time.deltaTime);
                return;
            }

            float deltaTime = Time.deltaTime; // 프레임 시간 캐싱
            float throttleInput = GetThrottleInput(); // 엔진 출력 입력 계산
            float yawInput = GetYawInput(); // 좌우 회전 입력 계산
            float verticalInput = GetVerticalInput(); // 수직 이동 입력 계산
            bool isBoostPressed = IsBoostPressed(); // 가속 입력 계산

            // 회전 적용
            UpdateYaw(yawInput, throttleInput, deltaTime);

            // 엔진 출력 적용
            UpdateThrottle(throttleInput, isBoostPressed, deltaTime);

            // 수직 이동 적용
            UpdateVerticalMovement(verticalInput);

            // 실제 이동 적용
            Move(deltaTime);
        }

        /// <summary>채집 모드 진입 시 잠수함 이동을 잠근다</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            StopImmediately();
        }

        /// <summary>채집 모드 종료 시 잠수함 이동을 해제한다</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
        }

        /// <summary>전후진 엔진 출력 입력값을 반환한다</summary>
        private float GetThrottleInput()
        {
            if (Input.GetKey(KeyCode.W))
                return 1f;

            if (Input.GetKey(KeyCode.S))
                return -1f;

            return 0f;
        }

        /// <summary>좌우 조향 입력값을 반환한다</summary>
        private float GetYawInput()
        {
            if (Input.GetKey(KeyCode.A))
                return -1f;

            if (Input.GetKey(KeyCode.D))
                return 1f;

            return 0f;
        }

        /// <summary>수직 이동 입력값을 반환한다</summary>
        private float GetVerticalInput()
        {
            if (Input.GetKey(KeyCode.Space))
                return 1f;

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                return -1f;

            return 0f;
        }

        /// <summary>가속 입력 여부를 반환한다</summary>
        private bool IsBoostPressed()
        {
            return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        }

        /// <summary>카메라 방향 보조를 포함한 Yaw 회전을 갱신한다</summary>
        private void UpdateYaw(float yawInput, float throttleInput, float deltaTime)
        {
            // 수동 조향
            if (!Mathf.Approximately(yawInput, 0f))
                transform.Rotate(0f, yawInput * yawSpeed * deltaTime, 0f);

            // 자유시야 중이면 카메라 정렬 금지
            if (explorationCameraController == null || explorationCameraController.IsFreeLookActive)
                return;

            // 추진 중일 때만 카메라 방향 보조 적용
            if (Mathf.Approximately(throttleInput, 0f))
                return;

            Vector3 desiredForward = explorationCameraController.GetPlanarForward(); // 카메라 수평 전방
            if (desiredForward.sqrMagnitude <= 0.0001f)
                return;

            Quaternion desiredRotation = Quaternion.LookRotation(desiredForward, Vector3.up); // 목표 회전 계산
            float appliedAlignSpeed = cameraAlignSpeed * Mathf.Clamp01(Mathf.Abs(throttleInput) + Mathf.Abs(yawInput) * steeringAssistStrength); // 정렬 속도 계산

            // 카메라 방향 정렬
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                desiredRotation,
                appliedAlignSpeed * deltaTime);
        }

        /// <summary>엔진 출력과 가속을 반영해 전후진 속도를 갱신한다</summary>
        private void UpdateThrottle(float throttleInput, bool isBoostPressed, float deltaTime)
        {
            // 출력 없으면 자연 감속
            if (Mathf.Approximately(throttleInput, 0f))
            {
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, idleDrag * deltaTime);
                return;
            }

            float appliedBoostMultiplier = isBoostPressed ? boostMultiplier : 1f; // 부스트 배율 적용

            // 전진 처리
            if (throttleInput > 0f)
            {
                if (currentForwardSpeed < 0f)
                {
                    currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                    return;
                }

                float accelMultiplier = EvaluateForwardLaunchAccelerationMultiplier(); // 출발 저항 배율 계산
                float appliedAcceleration = forwardAcceleration * accelMultiplier * appliedBoostMultiplier; // 최종 전진 가속도 계산
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, maxForwardSpeed * appliedBoostMultiplier, appliedAcceleration * deltaTime);
                return;
            }

            // 후진 처리
            if (currentForwardSpeed > 0f)
            {
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                return;
            }

            float reverseAccelMultiplier = EvaluateReverseLaunchAccelerationMultiplier(); // 후진 출발 저항 배율 계산
            float appliedReverseAcceleration = reverseAcceleration * reverseAccelMultiplier; // 최종 후진 가속도 계산
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, -maxReverseSpeed, appliedReverseAcceleration * deltaTime);
        }

        /// <summary>수직 이동 속도를 갱신한다</summary>
        private void UpdateVerticalMovement(float verticalInput)
        {
            float targetVerticalSpeed = 0f; // 목표 수직 속도

            if (verticalInput > 0f)
                targetVerticalSpeed = ascendSpeed;

            if (verticalInput < 0f)
                targetVerticalSpeed = -descendSpeed;

            currentVerticalSpeed = Mathf.SmoothDamp(
                currentVerticalSpeed,
                targetVerticalSpeed,
                ref verticalVelocityRef,
                verticalSmoothTime);
        }

        /// <summary>현재 엔진 출력과 수직 속도를 기준으로 이동을 적용한다</summary>
        private void Move(float deltaTime)
        {
            Vector3 forwardVelocity = transform.forward * currentForwardSpeed; // 엔진 추진 이동 벡터
            Vector3 verticalVelocity = Vector3.up * currentVerticalSpeed; // 수직 이동 벡터
            Vector3 finalVelocity = forwardVelocity + verticalVelocity; // 최종 이동 벡터 조합

            characterController.Move(finalVelocity * deltaTime);
        }

        /// <summary>전진 시작 저항을 고려한 가속 배율을 반환한다</summary>
        private float EvaluateForwardLaunchAccelerationMultiplier()
        {
            float absSpeed = Mathf.Abs(currentForwardSpeed);
            if (launchResistanceReleaseSpeed <= 0f)
                return 1f;

            float normalized = Mathf.Clamp01(absSpeed / launchResistanceReleaseSpeed);
            return Mathf.Lerp(minLaunchAccelerationMultiplier, 1f, normalized);
        }

        /// <summary>후진 시작 저항을 고려한 가속 배율을 반환한다</summary>
        private float EvaluateReverseLaunchAccelerationMultiplier()
        {
            float absSpeed = Mathf.Abs(currentForwardSpeed);
            if (reverseLaunchResistanceReleaseSpeed <= 0f)
                return 1f;

            float normalized = Mathf.Clamp01(absSpeed / reverseLaunchResistanceReleaseSpeed);
            return Mathf.Lerp(minLaunchAccelerationMultiplier, 1f, normalized);
        }

        /// <summary>즉시 정지한다</summary>
        public void StopImmediately()
        {
            currentForwardSpeed = 0f;
            currentVerticalSpeed = 0f;
            verticalVelocityRef = 0f;
        }
    }
}
