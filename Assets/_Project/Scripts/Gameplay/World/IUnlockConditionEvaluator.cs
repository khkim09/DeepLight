using Project.Data.World;

namespace Project.Gameplay.World
{
    /// <summary>개별 해금 조건 평가를 담당하는 인터페이스</summary>
    public interface IUnlockConditionEvaluator
    {
        /// <summary>조건 평가</summary>
        /// <param name="conditionType">조건 타입</param>
        /// <param name="conditionKey">조건 키 (업그레이드 ID, 로그 ID 등)</param>
        /// <param name="requiredValue">필요 값 (수량, 티어 등)</param>
        /// <param name="progressQuery">진행도 질의 인터페이스</param>
        /// <returns>조건 충족 여부</returns>
        bool EvaluateCondition(
            UnlockConditionType conditionType,
            string conditionKey,
            int requiredValue,
            IWorldProgressQuery progressQuery);

        /// <summary>조건 실패 이유 설명 (디버그/UI용)</summary>
        string GetFailureReason(
            UnlockConditionType conditionType,
            string conditionKey,
            int requiredValue,
            IWorldProgressQuery progressQuery);
    }
}
