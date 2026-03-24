using UnityEngine;

namespace Project.Gameplay.UserInput
{
    /// <summary>테스트베드에서 배그식 스로틀 기반 평면 이동을 담당하는 클래스</summary>
    [RequireComponent(typeof(CharacterController))]
    public class TestBedPlayerMover : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float maxForwardSpeed = 6f; // 최대 전진 속도
        [SerializeField] private float maxReverseSpeed = 3f; // 최대 후진 속도
        [SerializeField] private float forwardAcceleration = 8f; // 전진 기본 가속도
        [SerializeField] private float reverseAcceleration = 6f; // 후진 기본 가속도
        [SerializeField] private float idleDeceleration = 5f; // 입력 없을 때 감속도
        [SerializeField] private float brakeDeceleration = 10f; // 반대 방향 입력 시 제동 감속도

        [Header("Launch Resistance")]
        [SerializeField] private float minLaunchAccelerationMultiplier = 0.28f; // 정지 출발 시 최소 가속 배율
        [SerializeField] private float launchResistanceReleaseSpeed = 2.25f; // 무거운 출발감이 풀리는 속도
        [SerializeField] private float reverseLaunchResistanceReleaseSpeed = 1.4f; // 후진 출발감이 풀리는 속도

        [Header("Steering")]
        [SerializeField] private float maxSteerAnglePerSecond = 120f; // 최대 초당 선회량
        [SerializeField] private float minSteerSpeedThreshold = 0.2f; // 조향이 먹기 시작하는 최소 속도
        [SerializeField] private bool invertSteeringWhenReversing = true; // 후진 시 조향 반전 여부

        [Header("Planar Movement")]
        [SerializeField] private float lateralDamping = 8f; // 측면 미끄러짐 보정값
        [SerializeField] private bool lockYPosition = true; // Y축 고정 여부
        [SerializeField] private float lockedY = 0f; // 고정할 Y값

        private CharacterController characterController; // 캐릭터 컨트롤러 참조
        private float currentForwardSpeed; // 현재 전후진 속도

        /// <summary>필수 컴포넌트를 캐싱한다</summary>
        private void Awake()
        {
            // 캐릭터 컨트롤러 캐싱
            characterController = GetComponent<CharacterController>();

            // 시작 Y값 캐싱
            if (lockYPosition)
                lockedY = transform.position.y;
        }

        /// <summary>입력 기반 이동과 조향을 처리한다</summary>
        private void Update()
        {
            float deltaTime = Time.deltaTime; // 프레임 시간 캐싱
            float throttleInput = GetThrottleInput(); // 스로틀 입력 계산
            float steerInput = GetSteerInput(); // 조향 입력 계산

            // 전후진 속도 갱신
            UpdateForwardSpeed(throttleInput, deltaTime);

            // 조향 적용
            UpdateSteering(steerInput, deltaTime);

            // 실제 이동 적용
            Move(deltaTime);

            // Y축 강제 고정
            if (lockYPosition)
                ForceLockY();
        }

        /// <summary>전후진 스로틀 입력을 반환한다</summary>
        private float GetThrottleInput()
        {
            // 전진 입력 검사
            if (UnityEngine.Input.GetKey(KeyCode.W))
                return 1f;

            // 후진 입력 검사
            if (UnityEngine.Input.GetKey(KeyCode.S))
                return -1f;

            return 0f;
        }

        /// <summary>좌우 조향 입력을 반환한다</summary>
        private float GetSteerInput()
        {
            // 좌조향 입력 검사
            if (UnityEngine.Input.GetKey(KeyCode.A))
                return -1f;

            // 우조향 입력 검사
            if (UnityEngine.Input.GetKey(KeyCode.D))
                return 1f;

            return 0f;
        }

        /// <summary>스로틀 입력 기준 전후진 속도를 갱신한다</summary>
        private void UpdateForwardSpeed(float throttleInput, float deltaTime)
        {
            // 입력 없으면 자연 감속
            if (Mathf.Approximately(throttleInput, 0f))
            {
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, idleDeceleration * deltaTime);
                return;
            }

            // 전진 입력 처리
            if (throttleInput > 0f)
            {
                // 후진 중이면 먼저 강한 제동
                if (currentForwardSpeed < 0f)
                {
                    currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                    return;
                }

                float accelMultiplier = EvaluateForwardLaunchAccelerationMultiplier(); // 출발 저항 배율 계산
                float appliedAcceleration = forwardAcceleration * accelMultiplier; // 최종 가속도 계산

                // 전진 가속
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, maxForwardSpeed, appliedAcceleration * deltaTime);
                return;
            }

            // 후진 입력 처리
            if (currentForwardSpeed > 0f)
            {
                // 전진 중이면 먼저 강한 제동
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                return;
            }

            float reverseAccelMultiplier = EvaluateReverseLaunchAccelerationMultiplier(); // 후진 출발 저항 배율 계산
            float appliedReverseAcceleration = reverseAcceleration * reverseAccelMultiplier; // 최종 후진 가속도 계산

            // 후진 가속
            currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, -maxReverseSpeed, appliedReverseAcceleration * deltaTime);
        }

        /// <summary>전진 시작 저항을 고려한 가속 배율을 반환한다</summary>
        private float EvaluateForwardLaunchAccelerationMultiplier()
        {
            float absSpeed = Mathf.Abs(currentForwardSpeed); // 현재 절대 속도
            if (launchResistanceReleaseSpeed <= 0f)
                return 1f;

            float normalized = Mathf.Clamp01(absSpeed / launchResistanceReleaseSpeed); // 저항 해제 비율 계산
            return Mathf.Lerp(minLaunchAccelerationMultiplier, 1f, normalized);
        }

        /// <summary>후진 시작 저항을 고려한 가속 배율을 반환한다</summary>
        private float EvaluateReverseLaunchAccelerationMultiplier()
        {
            float absSpeed = Mathf.Abs(currentForwardSpeed); // 현재 절대 속도
            if (reverseLaunchResistanceReleaseSpeed <= 0f)
                return 1f;

            float normalized = Mathf.Clamp01(absSpeed / reverseLaunchResistanceReleaseSpeed); // 저항 해제 비율 계산
            return Mathf.Lerp(minLaunchAccelerationMultiplier, 1f, normalized);
        }

        /// <summary>현재 속도 기준 조향을 적용한다</summary>
        private void UpdateSteering(float steerInput, float deltaTime)
        {
            // 조향 입력 없으면 중단
            if (Mathf.Approximately(steerInput, 0f))
                return;

            float absSpeed = Mathf.Abs(currentForwardSpeed); // 절대 속도 계산
            if (absSpeed < minSteerSpeedThreshold)
                return;

            float speedRatio; // 속도 비율 계산
            if (currentForwardSpeed >= 0f)
                speedRatio = Mathf.Clamp01(absSpeed / maxForwardSpeed);
            else
                speedRatio = Mathf.Clamp01(absSpeed / maxReverseSpeed);

            float appliedSteerInput = steerInput; // 실제 조향 입력값
            if (invertSteeringWhenReversing && currentForwardSpeed < 0f)
                appliedSteerInput *= -1f;

            float yawDelta = appliedSteerInput * maxSteerAnglePerSecond * speedRatio * deltaTime; // 최종 Yaw 계산

            // 회전 적용
            transform.Rotate(0f, yawDelta, 0f);
        }

        /// <summary>현재 속도 기준 평면 이동을 적용한다</summary>
        private void Move(float deltaTime)
        {
            Vector3 forwardVelocity = transform.forward * currentForwardSpeed; // 전방 속도 벡터 계산
            Vector3 localVelocity = transform.InverseTransformDirection(forwardVelocity); // 로컬 속도 계산

            // 측면 미끄러짐 보정
            localVelocity.x = Mathf.MoveTowards(localVelocity.x, 0f, lateralDamping * deltaTime);

            Vector3 planarVelocity = transform.TransformDirection(localVelocity); // 월드 이동 벡터 복원
            planarVelocity.y = 0f; // Y축 제거

            // 실제 이동 적용
            characterController.Move(planarVelocity * deltaTime);
        }

        /// <summary>Y축 위치를 강제로 고정한다</summary>
        private void ForceLockY()
        {
            Vector3 currentPosition = transform.position; // 현재 위치 가져오기
            currentPosition.y = lockedY; // Y값 고정
            transform.position = currentPosition; // 위치 반영
        }

        /// <summary>현재 전후진 속도를 반환한다</summary>
        public float GetCurrentForwardSpeed()
        {
            return currentForwardSpeed;
        }

        /// <summary>현재 전후진 속도를 강제로 설정한다</summary>
        public void SetCurrentForwardSpeed(float newSpeed)
        {
            // 전진 상한 제한
            if (newSpeed > maxForwardSpeed)
                newSpeed = maxForwardSpeed;

            // 후진 하한 제한
            if (newSpeed < -maxReverseSpeed)
                newSpeed = -maxReverseSpeed;

            currentForwardSpeed = newSpeed;
        }

        /// <summary>즉시 정지한다</summary>
        public void StopImmediately()
        {
            currentForwardSpeed = 0f; // 속도 초기화
        }
    }
}
