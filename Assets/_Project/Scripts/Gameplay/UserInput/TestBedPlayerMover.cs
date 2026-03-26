using Cysharp.Threading.Tasks;
using Project.Core.Events;
using Project.Gameplay.CameraSystem;
using UnityEngine;

namespace Project.Gameplay.UserInput
{
    /// <summary>탐사 모드에서 전투기 및 잠수함 방식의 하이브리드 이동을 처리하는 클래스</summary>
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

        [Header("Mouse Flight Rotation")]
        [SerializeField] private float yawSpeed = 120f; // 마우스 X축 기반 좌우 회전(Yaw) 속도
        [SerializeField] private float pitchSpeed = 100f; // 마우스 Y축 기반 상하 회전(Pitch) 속도
        [SerializeField] private float maxPitchAngle = 70f; // 상하 최대 꺾임 제한 각도

        [Header("Vertical Movement (Space/C)")]
        [SerializeField] private float ascendSpeed = 4f; // Space 키 입력 시 부상 속도
        [SerializeField] private float descendSpeed = 4f; // C 키 입력 시 하강 속도
        [SerializeField] private float verticalSmoothTime = 0.1f; // 수직 이동 보간 시간

        private Rigidbody rb; // 물리 이동 처리를 위한 리지드바디 캐싱
        private float currentForwardSpeed; // 현재 전후진 속도
        private float currentVerticalSpeed; // 현재 수직 이동 속도
        private float verticalVelocityRef; // SmoothDamp 참조용 변수
        private bool isHarvestMode; // 채집 모드 진입 여부
        private bool isExternallyLocked; // 외부 연출 등에 의한 조작 잠금 여부

        // 마우스 델타값 유실 방지를 위한 회전 누적 변수
        private float accumulatedYaw;
        private float accumulatedPitch;

        // Update/FixedUpdate 주기 차이 보완용 입력 캐싱
        private float currentThrottleInput;
        private float currentVerticalInput;
        private bool isCurrentBoostPressed;

        /// <summary>현재 이동 조작이 잠겨있는지 여부를 반환한다</summary>
        public bool IsMovementLocked => isHarvestMode || isExternallyLocked;

        /// <summary>컴포넌트 초기화 및 물리 엔진 필수 설정을 적용한다</summary>
        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false; // 심해 환경이므로 중력 무시
            rb.interpolation = RigidbodyInterpolation.Interpolate; // 카메라 떨림 방지

            // 전투기식 Pitch(상하) 회전을 위해 X축 잠금을 해제하고 Z축(롤링)만 고정함
            rb.constraints = RigidbodyConstraints.FreezeRotationZ;
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

        /// <summary>매 프레임 유저 입력을 캐싱하고 회전 목표값을 계산한다</summary>
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

            // 자유 시야(Alt) 모드가 아닐 때만 마우스 조향 적용
            if (explorationCameraController != null && !explorationCameraController.IsFreeLookActive)
            {
                float mouseX = Input.GetAxis("Mouse X");
                float mouseY = Input.GetAxis("Mouse Y");

                accumulatedYaw += mouseX * yawSpeed * Time.deltaTime;
                accumulatedPitch -= mouseY * pitchSpeed * Time.deltaTime; // 위로 올리면 고개를 들도록 반전 적용
                accumulatedPitch = Mathf.Clamp(accumulatedPitch, -maxPitchAngle, maxPitchAngle);
            }
        }

        /// <summary>물리 갱신 주기마다 실제 이동 및 회전을 리지드바디에 적용한다</summary>
        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;

            // 잠금 상태 시 물리 제동만 적용하고 물리 갱신 종료 (연출 스크립트와 충돌 방지)
            if (IsMovementLocked)
            {
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                currentVerticalSpeed = Mathf.MoveTowards(currentVerticalSpeed, 0f, brakeDeceleration * deltaTime);
                return;
            }

            UpdateThrottle(currentThrottleInput, isCurrentBoostPressed, deltaTime);
            UpdateVerticalMovement(currentVerticalInput, deltaTime);

            // 누적된 마우스 조향 각도를 물리 회전에 적용
            Quaternion targetRotation = Quaternion.Euler(accumulatedPitch, accumulatedYaw, 0f);
            rb.MoveRotation(targetRotation);

            Move();
        }

        /// <summary>외부 스크립트에 의해 변경된 물리 회전값을 누적 변수에 동기화한다</summary>
        private void SyncAccumulatedRotation()
        {
            Vector3 euler = rb.rotation.eulerAngles;
            accumulatedYaw = euler.y;
            accumulatedPitch = euler.x;
            if (accumulatedPitch > 180f) accumulatedPitch -= 360f; // -180 ~ 180 체계로 정규화
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

        /// <summary>최종 속도를 조합하여 리지드바디 선형 속도에 반영한다</summary>
        private void Move()
        {
            // 전후진은 선체의 로컬 정면 방향을 따르고, 승강은 월드 상하 방향을 따름
            Vector3 forwardVelocity = (rb.rotation * Vector3.forward) * currentForwardSpeed;
            Vector3 verticalVelocity = Vector3.up * currentVerticalSpeed;

            rb.linearVelocity = forwardVelocity + verticalVelocity; // Unity 6 신규 API 적용
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
