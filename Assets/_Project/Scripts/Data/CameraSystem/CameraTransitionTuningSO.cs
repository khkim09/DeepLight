using UnityEngine;

namespace Project.Data.CameraSystem
{
    /// <summary>탐사 카메라와 조종석 카메라 전환 연출 수치를 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "CameraTransitionTuningSO",
        menuName = "Project/Camera/Camera Transition Tuning")]
    public class CameraTransitionTuningSO : ScriptableObject
    {
        [Header("Letterbox")]
        [SerializeField] private float letterboxShownHeight = 140f;      // 레터박스 펼침 높이
        [SerializeField] private float letterboxAnimateDuration = 0.35f; // 레터박스 애니메이션 시간

        [Header("Camera Transition")]
        [SerializeField] private float transitionDuration = 1.1f; // 탐사/조종석 카메라 전환 총 시간
        [SerializeField] private float guideReachTime = 0.45f;    // 베지어 중간 가이드를 지나는 시간 비율
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f); // 전환 보간 곡선

        /// <summary>레터박스가 완전히 펼쳐졌을 때의 높이를 반환한다.</summary>
        public float LetterboxShownHeight => Mathf.Max(0f, letterboxShownHeight);

        /// <summary>레터박스 애니메이션 시간을 반환한다.</summary>
        public float LetterboxAnimateDuration => Mathf.Max(0.01f, letterboxAnimateDuration);

        /// <summary>카메라 전환 총 시간을 반환한다.</summary>
        public float TransitionDuration => Mathf.Max(0.01f, transitionDuration);

        /// <summary>중간 가이드를 지나는 시간 비율을 반환한다.</summary>
        public float GuideReachTime => Mathf.Clamp(guideReachTime, 0.05f, 0.95f);

        /// <summary>카메라 전환 곡선을 반환한다.</summary>
        public AnimationCurve TransitionCurve => transitionCurve;
    }
}
