using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Gameplay.CameraSystem;
using UnityEngine;

namespace Project.Gameplay.UserInput
{
    /// <summary>물리 엔진의 회전을 끄고 Update에서 직접 회전을 처리하여 모든 주사율에서 부드러운 다이렉트 조작감을 제공하는 잠수함 조종 클래스이다</summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TestBedPlayerMover : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ExplorationFollowCameraController explorationCameraController; // 3인칭 탐사 카메라 컨트롤러

        [Header("Throttle (W/S)")]
        [SerializeField] private float maxForwardSpeed = 12f; // 최대 전진 속도
        [SerializeField] private float maxReverseSpeed = 4f; // 최대 후진 속도
        [SerializeField] private float forwardAcceleration = 8f; // 전진 가속도
        [SerializeField] private float reverseAcceleration = 5f; // 후진 가속도
        [SerializeField] private float idleDrag = 2f; // 무입력 시 자연 감속도
        [SerializeField] private float brakeDeceleration = 12f; // 역방향 입력 시 제동 감속도
        [SerializeField] private float boostMultiplier = 1.6f; // Shift 키 입력 시 가속 배율

        [Header("Mouse Flight Rotation (Direct Delta)")]
        [SerializeField] private float yawSpeed = 3f; // 마우스 X축 기반 좌우 회전(Yaw) 민감도
        [SerializeField] private float pitchSpeed = 2f; // 마우스 Y축 기반 상하 회전(Pitch) 민감도
        [SerializeField] private float maxPitchAngle = 50f; // 상하 최대 꺾임 제한 각도
        [SerializeField] private float rotationSmoothTime = 0.15f; // 목표 회전각 도달 보간 시간
        [SerializeField] private float loopRotationSpeed = 50f; // 전진 중 상하 조향 시 선체 루프(Loop) 보조 회전 속도

        [Header("Vertical Movement (Space/C)")]
        [SerializeField] private float ascendSpeed = 4f; // Space 키 입력 시 부상 속도
        [SerializeField] private float descendSpeed = 4f; // C 키 입력 시 하강 속도
        [SerializeField] private float verticalSmoothTime = 0.1f; // 수직 이동 보간 시간

        private Rigidbody rb; // 물리 이동 처리를 위한 리지드바디 캐싱
        private float currentForwardSpeed; // 현재 적용 중인 전후진 선형 속도
        private float currentVerticalSpeed; // 현재 적용 중인 수직 선형 속도
        private float verticalVelocityRef; // 수직 이동 SmoothDamp 참조용 변수
        private bool isHarvestMode; // 채집 모드 진입 여부
        private bool isExternallyLocked; // 외부 연출 등에 의한 조작 잠금 여부

        // 마우스 델타 누적 및 스무딩 연산용 변수
        private float targetYaw; // 누적된 목표 요우 각도
        private float targetSteeringPitch; // 누적된 목표 피치 각도
        private float currentYaw; // 현재 보간 중인 요우 각도
        private float currentSteeringPitch; // 현재 보간 중인 피치 각도
        private float basePitch; // 루프 회전에 의해 누적된 기본 베이스 피치 각도

        // 회전 스무딩 SmoothDampAngle 참조용 변수
        private float yawVelocityRef;
        private float pitchVelocityRef;

        // Update/FixedUpdate 주기 차이 보완용 입력 캐싱
        private float currentThrottleInput;
        private float currentVerticalInput;
        private bool isCurrentBoostPressed;

        /// <summary>현재 이동 조작이 잠겨있는지 여부를 반환한다</summary>
        public bool IsMovementLocked => isHarvestMode || isExternallyLocked;

        /// <summary>컴포넌트 초기화 및 렌더링 동기화 회전을 위한 물리 엔진 설정을 적용한다</summary>
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false; // 심해 환경이므로 중력 무시

            // 위치 이동에 대해서만 물리 보간을 활성화하여 카메라 떨림을 방지함
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // 회전에 대한 물리 엔진의 개입을 완벽히 차단(Freeze)하여 Transform 직접 제어 시 엇박자 충돌을 방지함
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }

        /// <summary>시작 시 현재 회전값을 동기화한다</summary>
        private void Start()
        {
            SyncAccumulatedRotation();
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

        /// <summary>매 프레임 유저 입력을 캐싱하고 주사율에 맞춰 회전을 즉시 갱신한다</summary>
        private void Update()
        {
            // 조작 잠금 상태일 경우 입력 무시 및 현재 회전값 지속 동기화
            if (IsMovementLocked)
            {
                currentThrottleInput = 0f;
                currentVerticalInput = 0f;
                isCurrentBoostPressed = false;
                SyncAccumulatedRotation();
                return;
            }

            // 키보드 입력 캐싱
            currentThrottleInput = GetThrottleInput();
            currentVerticalInput = GetVerticalInput();
            isCurrentBoostPressed = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // 자유 시야(Alt) 모드가 아닐 때만 마우스 다이렉트 입력 누적
            if (explorationCameraController != null && !explorationCameraController.IsFreeLookActive)
            {
                // 프레임 간섭을 막기 위해 deltaTime을 곱하지 않은 순수 델타값(Raw) 사용
                float rawMouseX = Input.GetAxisRaw("Mouse X");
                float rawMouseY = Input.GetAxisRaw("Mouse Y");

                targetYaw += rawMouseX * yawSpeed;
                targetSteeringPitch -= rawMouseY * pitchSpeed;
                targetSteeringPitch = Mathf.Clamp(targetSteeringPitch, -maxPitchAngle, maxPitchAngle);
            }

            // 프레임 주사율(Update)에 완벽히 동기화하여 회전을 스무딩하고 Transform에 즉시 반영
            CalculateAndApplyRotation(Time.deltaTime);
        }

        /// <summary>물리 갱신 주기마다 실제 선형 이동을 리지드바디에 적용한다</summary>
        private void FixedUpdate()
        {
            float fixedDeltaTime = Time.fixedDeltaTime;

            // 잠금 상태 시 물리 제동만 적용하고 속도 갱신 종료
            if (IsMovementLocked)
            {
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * fixedDeltaTime);
                currentVerticalSpeed = Mathf.MoveTowards(currentVerticalSpeed, 0f, brakeDeceleration * fixedDeltaTime);
            }
            else
            {
                UpdateThrottle(currentThrottleInput, isCurrentBoostPressed, fixedDeltaTime);
                UpdateVerticalMovement(currentVerticalInput, fixedDeltaTime);
            }

            // 물리 주기에서는 오직 선형 이동(Velocity)만 처리 (회전은 이미 Update에서 적용됨)
            ApplyMovement();
        }

        /// <summary>렌더링 주기에 맞춰 부드러운 회전값을 계산하고 Transform에 직접 반영한다</summary>
        private void CalculateAndApplyRotation(float deltaTime)
        {
            // 누적된 목표 각도를 향해 현재 각도를 부드럽게 보간
            currentYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref yawVelocityRef, rotationSmoothTime, Mathf.Infinity, deltaTime);
            currentSteeringPitch = Mathf.SmoothDampAngle(currentSteeringPitch, targetSteeringPitch, ref pitchVelocityRef, rotationSmoothTime, Mathf.Infinity, deltaTime);

            // 전진 속도가 있을 때 상하 조향 시 자연스럽게 루프 회전이 더해지도록 처리
            if (Mathf.Abs(currentForwardSpeed) > 0.01f)
            {
                float speedRatio = currentForwardSpeed / maxForwardSpeed;
                float loopRate = (currentSteeringPitch / maxPitchAngle) * loopRotationSpeed * speedRatio;
                basePitch += loopRate * deltaTime;
            }

            // 최종 로컬 회전값 계산
            Quaternion targetLocalRotation = Quaternion.Euler(basePitch + currentSteeringPitch, currentYaw, 0f);

            // Rigidbody.MoveRotation을 쓰지 않고 직접 돌려 물리-렌더링 엇박자를 원천 차단
            transform.rotation = transform.parent != null
                ? transform.parent.rotation * targetLocalRotation
                : targetLocalRotation;
        }

        /// <summary>계산된 선형 속도를 리지드바디에 적용한다</summary>
        private void ApplyMovement()
        {
            // 회전은 Update에서 처리되었으므로, 갱신된 현재 정면(transform.forward) 방향으로 속도를 가함
            Vector3 forwardVelocity = (transform.rotation * Vector3.forward) * currentForwardSpeed;
            Vector3 verticalVelocity = Vector3.up * currentVerticalSpeed;

            // Unity 6 신규 API 적용
            rb.linearVelocity = forwardVelocity + verticalVelocity;
        }

        /// <summary>현재 적용 중인 회전값을 누적 변수에 동기화한다</summary>
        private void SyncAccumulatedRotation()
        {
            Vector3 euler = transform.localEulerAngles;
            targetYaw = currentYaw = euler.y;
            basePitch = euler.x;
            if (basePitch > 180f) basePitch -= 360f; // -180 ~ 180 체계로 정규화
            targetSteeringPitch = currentSteeringPitch = 0f;
        }

        /// <summary>전후진 입력값을 반환한다</summary>
        private float GetThrottleInput()
        {
            if (Input.GetKey(KeyCode.W)) return 1f;
            if (Input.GetKey(KeyCode.S)) return -1f;
            return 0f;
        }

        /// <summary>수직 상승/하강 입력값을 반환한다</summary>
        private float GetVerticalInput()
        {
            if (Input.GetKey(KeyCode.Space)) return 1f;
            if (Input.GetKey(KeyCode.C)) return -1f;
            return 0f;
        }

        /// <summary>입력에 따른 전후진 속도를 계산한다</summary>
        private void UpdateThrottle(float throttleInput, bool isBoostPressed, float deltaTime)
        {
            // 무입력 시 마찰력을 적용하여 자연 감속
            if (Mathf.Approximately(throttleInput, 0f))
            {
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, idleDrag * deltaTime);
                return;
            }

            float appliedBoost = isBoostPressed ? boostMultiplier : 1f;

            if (throttleInput > 0f) // 전진
            {
                if (currentForwardSpeed < 0f) currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                else currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, maxForwardSpeed * appliedBoost, forwardAcceleration * appliedBoost * deltaTime);
            }
            else // 후진
            {
                if (currentForwardSpeed > 0f) currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                else currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, -maxReverseSpeed, reverseAcceleration * deltaTime);
            }
        }

        /// <summary>입력에 따른 수직 속도를 계산한다</summary>
        private void UpdateVerticalMovement(float verticalInput, float deltaTime)
        {
            float targetVerticalSpeed = 0f;
            if (verticalInput > 0f) targetVerticalSpeed = ascendSpeed;
            if (verticalInput < 0f) targetVerticalSpeed = -descendSpeed;

            currentVerticalSpeed = Mathf.SmoothDamp(currentVerticalSpeed, targetVerticalSpeed, ref verticalVelocityRef, verticalSmoothTime, Mathf.Infinity, deltaTime);
        }

        /// <summary>채집 모드 진입 이벤트를 처리한다</summary>
        private void OnHarvestModeEntered(HarvestModeEnteredEvent publishedEvent)
        {
            isHarvestMode = true;
            StopImmediately();
        }

        /// <summary>채집 모드 종료 이벤트를 처리한다</summary>
        private void OnHarvestModeExited(HarvestModeExitedEvent publishedEvent)
        {
            isHarvestMode = false;
        }

        /// <summary>잠수함의 물리적 이동을 즉시 정지시킨다</summary>
        public void StopImmediately()
        {
            currentForwardSpeed = 0f;
            currentVerticalSpeed = 0f;
            verticalVelocityRef = 0f;
            if (rb != null) rb.linearVelocity = Vector3.zero;
        }

        /// <summary>연출 등 외부 요인에 의해 조작을 잠근다</summary>
        public void SetExternalControlLock(bool shouldLock)
        {
            isExternallyLocked = shouldLock;
            if (shouldLock) StopImmediately();
        }
    }
}
