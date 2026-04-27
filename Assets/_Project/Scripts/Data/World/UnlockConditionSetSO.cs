using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.World
{
    /// <summary>해금 조건 세트 (Traversal, Knowledge, Narrative, Risk 4축)</summary>
    [CreateAssetMenu(
        fileName = "UnlockConditionSet_",
        menuName = "Project/World/Unlock Condition Set")]
    public class UnlockConditionSetSO : ScriptableObject
    {
        [Serializable]
        public class ConditionEntry
        {
            [SerializeField] private UnlockConditionType conditionType;
            [SerializeField] private string conditionKey; // 업그레이드 ID, 로그 ID, 플래그 키 등
            [SerializeField] private int requiredValue; // 필요한 수량/값 (로그 수, 선체 티어 등)
            [SerializeField] private string displayHint; // 디버그/UI용 표시 힌트

            public UnlockConditionType ConditionType => conditionType;
            public string ConditionKey => conditionKey;
            public int RequiredValue => requiredValue;
            public string DisplayHint => displayHint;
        }

        [Header("Traversal Conditions (탐사)")]
        [SerializeField] private List<ConditionEntry> traversalConditions = new();

        [Header("Knowledge Conditions (지식)")]
        [SerializeField] private List<ConditionEntry> knowledgeConditions = new();

        [Header("Narrative Conditions (서사)")]
        [SerializeField] private List<ConditionEntry> narrativeConditions = new();

        [Header("Risk Conditions (위험도)")]
        [SerializeField] private List<ConditionEntry> riskConditions = new();

        /// <summary>Traversal 조건 목록</summary>
        public IReadOnlyList<ConditionEntry> TraversalConditions => traversalConditions;

        /// <summary>Knowledge 조건 목록</summary>
        public IReadOnlyList<ConditionEntry> KnowledgeConditions => knowledgeConditions;

        /// <summary>Narrative 조건 목록</summary>
        public IReadOnlyList<ConditionEntry> NarrativeConditions => narrativeConditions;

        /// <summary>Risk 조건 목록</summary>
        public IReadOnlyList<ConditionEntry> RiskConditions => riskConditions;

        /// <summary>모든 조건을 하나의 리스트로 반환</summary>
        public IEnumerable<ConditionEntry> GetAllConditions()
        {
            foreach (ConditionEntry condition in traversalConditions) yield return condition;
            foreach (ConditionEntry condition in knowledgeConditions) yield return condition;
            foreach (ConditionEntry condition in narrativeConditions) yield return condition;
            foreach (ConditionEntry condition in riskConditions) yield return condition;
        }

        /// <summary>조건 세트가 비어있는지 확인</summary>
        public bool IsEmpty()
        {
            return traversalConditions.Count == 0 &&
                    knowledgeConditions.Count == 0 &&
                    narrativeConditions.Count == 0 &&
                    riskConditions.Count == 0;
        }

        /// <summary>특정 타입의 조건만 필터링하여 반환</summary>
        public List<ConditionEntry> GetConditionsByType(UnlockConditionType type)
        {
            List<ConditionEntry> result = new();

            foreach (ConditionEntry condition in traversalConditions)
                if (condition.ConditionType == type) result.Add(condition);

            foreach (ConditionEntry condition in knowledgeConditions)
                if (condition.ConditionType == type) result.Add(condition);

            foreach (ConditionEntry condition in narrativeConditions)
                if (condition.ConditionType == type) result.Add(condition);

            foreach (ConditionEntry condition in riskConditions)
                if (condition.ConditionType == type) result.Add(condition);

            return result;
        }
    }
}
