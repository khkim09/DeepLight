using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.Harvest
{
    /// <summary>회수 전 위장 이름 조합 풀을 제공한다.</summary>
    [CreateAssetMenu(
        fileName = "HarvestPreviewNamePoolSO",
        menuName = "Project/Harvest/Definitions/Harvest Preview Name Pool")]
    public class HarvestPreviewNamePoolSO : ScriptableObject
    {
        [SerializeField] private List<string> adjectives = new(); // 형용사 목록
        [SerializeField] private List<string> nouns = new(); // 명사 목록

        /// <summary>랜덤 위장 표시 이름을 생성한다.</summary>
        public string GetRandomDisplayName()
        {
            string adjective = adjectives != null && adjectives.Count > 0
                ? adjectives[Random.Range(0, adjectives.Count)]
                : "수상한";

            string noun = nouns != null && nouns.Count > 0
                ? nouns[Random.Range(0, nouns.Count)]
                : "상자";

            return $"{adjective} {noun}";
        }
    }
}
