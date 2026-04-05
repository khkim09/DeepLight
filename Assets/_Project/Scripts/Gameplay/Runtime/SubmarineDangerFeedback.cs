using Project.Core.Events;

namespace Project.Gameplay.Runtime
{
    /// <summary>잠수함 위험 피드백 원인 종류이다.</summary>
    public enum SubmarineDangerFeedbackType
    {
        None = 0,
        HarvestFailureBacklash = 1,
        CollisionWithHarvestTarget = 2,
        CollisionWithEnvironment = 3
    }

    /// <summary>위험 피해 발생 시 화면 연출용으로 전달하는 이벤트이다.</summary>
    public readonly struct SubmarineDangerFeedbackEvent : IEvent
    {
        public readonly SubmarineDangerFeedbackType FeedbackType; // 위험 원인
        public readonly float DamageAmount; // 이번에 입은 피해량
        public readonly float CurrentHull; // 현재 선체 내구도
        public readonly float MaxHull; // 최대 선체 내구도
        public readonly float IntensityMultiplier; // 추가 강도 배율

        /// <summary>위험 피드백 이벤트를 생성한다.</summary>
        public SubmarineDangerFeedbackEvent(
            SubmarineDangerFeedbackType feedbackType,
            float damageAmount,
            float currentHull,
            float maxHull,
            float intensityMultiplier)
        {
            FeedbackType = feedbackType;
            DamageAmount = damageAmount;
            CurrentHull = currentHull;
            MaxHull = maxHull;
            IntensityMultiplier = intensityMultiplier;
        }
    }
}
