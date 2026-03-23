using UnityEngine;

namespace Project.Gameplay.UserInput
{
    /// <summary>테스트베드에서 배그식 스로틀 기반 이동을 담당하는 클래스</summary>
    [RequireComponent(typeof(CharacterController))]
    public class TestBedPlayerMover : MonoBehaviour
    {
        [Header("Speed")]
        [SerializeField] private float maxForwardSpeed = 6f; // 최대 전진 속도
        [SerializeField] private float maxReverseSpeed = 3f; // 최대 후진 속도
        [SerializeField] private float forwardAcceleration = 8f; // 전진 가속도
        [SerializeField] private float reverseAcceleration = 6f; // 후진 가속도
        [SerializeField] private float idleDeceleration = 5f; // 입력 없을 때 감속도
        [SerializeField] private float brakeDeceleration = 10f; // 반대 방향 입력 시 제동 감속도

        [Header("Steering")]
        [SerializeField] private float maxSteerAnglePerSecond = 120f; // 최대 초당 선회량
        [SerializeField] private float minSteerSpeedThreshold = 0.2f; // 조향이 먹기 시작하는 최소 속도
        [SerializeField] private bool invertSteeringWhenReversing = true; // 후진 시 조향 반전 여부

        [Header("Optional Feel")]
        [SerializeField] private float lateralDamping = 8f; // 측면 미끄러짐 보정값
        [SerializeField] private float gravity = -20f; // 중력값

        private CharacterController characterController; // 캐릭터 컨트롤러 참조
        private float currentForwardSpeed; // 현재 전후진 속도
        private float verticalVelocity; // 수직 속도

        /// <summary>필수 컴포넌트를 캐싱한다</summary>
        private void Awake()
        {
            // 캐릭터 컨트롤러 캐싱
            characterController = GetComponent<CharacterController>();
        }

        /// <summary>입력 기반 이동과 조향을 처리한다</summary>
        private void Update()
        {
            // 프레임 시간 캐싱
            float deltaTime = Time.deltaTime;

            // 스로틀 입력 계산
            float throttleInput = GetThrottleInput();

            // 조향 입력 계산
            float steerInput = GetSteerInput();

            // 전후진 속도 갱신
            UpdateForwardSpeed(throttleInput, deltaTime);

            // 조향 적용
            UpdateSteering(steerInput, deltaTime);

            // 실제 이동 적용
            Move(deltaTime);
        }

        /// <summary>전후진 스로틀 입력을 반환한다</summary>
        private float GetThrottleInput()
        {
            // 전진 입력 우선 검사
            if (UnityEngine.Input.GetKey(KeyCode.W))
                return 1f;

            // 후진 입력 검사
            if (UnityEngine.Input.GetKey(KeyCode.S))
                return -1f;

            // 입력 없음
            return 0f;
        }

        /// <summary>좌우 조향 입력을 반환한다</summary>
        private float GetSteerInput()
        {
            // 좌회전 입력 검사
            if (UnityEngine.Input.GetKey(KeyCode.A))
                return -1f;

            // 우회전 입력 검사
            if (UnityEngine.Input.GetKey(KeyCode.D))
                return 1f;

            // 입력 없음
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
                // 현재 후진 중이면 먼저 강한 제동
                if (currentForwardSpeed < 0f)
                {
                    currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                    return;
                }

                // 전진 가속
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, maxForwardSpeed, forwardAcceleration * deltaTime);
                return;
            }

            // 후진 입력 처리
            if (throttleInput < 0f)
            {
                // 현재 전진 중이면 먼저 강한 제동
                if (currentForwardSpeed > 0f)
                {
                    currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, 0f, brakeDeceleration * deltaTime);
                    return;
                }

                // 후진 가속
                currentForwardSpeed = Mathf.MoveTowards(currentForwardSpeed, -maxReverseSpeed, reverseAcceleration * deltaTime);
            }
        }

        /// <summary>현재 속도 기준 조향을 적용한다</summary>
        private void UpdateSteering(float steerInput, float deltaTime)
        {
            // 조향 입력 없으면 중단
            if (Mathf.Approximately(steerInput, 0f))
                return;

            // 절대 속도 계산
            float absSpeed = Mathf.Abs(currentForwardSpeed);

            // 속도가 너무 낮으면 조향 중단
            if (absSpeed < minSteerSpeedThreshold)
                return;

            // 속도 비율 계산
            float speedRatio = 0f;
            if (currentForwardSpeed >= 0f)
                speedRatio = Mathf.Clamp01(absSpeed / maxForwardSpeed);
            else
                speedRatio = Mathf.Clamp01(absSpeed / maxReverseSpeed);

            // 후진 시 조향 반전 적용
            float appliedSteerInput = steerInput;
            if (invertSteeringWhenReversing && currentForwardSpeed < 0f)
                appliedSteerInput *= -1f;

            // 최종 회전량 계산
            float yawDelta = appliedSteerInput * maxSteerAnglePerSecond * speedRatio * deltaTime;

            // Yaw 회전 적용
            transform.Rotate(0f, yawDelta, 0f);
        }

        /// <summary>현재 속도 기준 실제 이동을 적용한다</summary>
        private void Move(float deltaTime)
        {
            // 수평 이동 벡터 계산
            Vector3 forwardVelocity = transform.forward * currentForwardSpeed;

            // 측면 미끄러짐 보정
            Vector3 localVelocity = transform.InverseTransformDirection(forwardVelocity);
            localVelocity.x = Mathf.MoveTowards(localVelocity.x, 0f, lateralDamping * deltaTime);

            // 월드 기준 이동 벡터 복원
            Vector3 planarVelocity = transform.TransformDirection(localVelocity);

            // 바닥에 붙어 있으면 약한 하강 유지
            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;

            // 중력 적용
            verticalVelocity += gravity * deltaTime;

            // 최종 이동 벡터 조합
            Vector3 finalVelocity = planarVelocity;
            finalVelocity.y = verticalVelocity;

            // 컨트롤러 이동 적용
            characterController.Move(finalVelocity * deltaTime);
        }

        /// <summary>현재 전후진 속도를 반환한다</summary>
        public float GetCurrentForwardSpeed()
        {
            return currentForwardSpeed;
        }

        /// <summary>현재 전후진 속도를 강제로 설정한다</summary>
        public void SetCurrentForwardSpeed(float newSpeed)
        {
            // 전진 최대 속도 상한 적용
            if (newSpeed > maxForwardSpeed)
                newSpeed = maxForwardSpeed;

            // 후진 최대 속도 하한 적용
            if (newSpeed < -maxReverseSpeed)
                newSpeed = -maxReverseSpeed;

            // 속도 반영
            currentForwardSpeed = newSpeed;
        }

        /// <summary>즉시 정지한다</summary>
        public void StopImmediately()
        {
            // 전진 속도 초기화
            currentForwardSpeed = 0f;

            // 수직 속도 초기화
            verticalVelocity = 0f;
        }
    }
}
