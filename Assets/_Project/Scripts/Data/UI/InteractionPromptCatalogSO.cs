using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Data.UI
{
    /// <summary>상호작용 상황별 아이콘과 타이틀을 매핑하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "InteractionPromptCatalogSO",
        menuName = "Project/UI/Interaction Prompt Catalog")]
    public class InteractionPromptCatalogSO : ScriptableObject
    {
        [SerializeField] private List<InteractionPromptEntry> entries = new(); // 상황별 프롬프트 정의 목록

        /// <summary>지정 타입 값에 대응하는 프롬프트 정의를 반환한다.</summary>
        public bool TryGetPrompt(int promptType, out InteractionPromptEntry entry)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if ((int)entries[i].PromptType != promptType)
                    continue;

                entry = entries[i];
                return true;
            }

            entry = default;
            return false;
        }
    }

    /// <summary>상호작용 프롬프트 상황 종류이다.</summary>
    public enum InteractionPromptType
    {
        // TODO : 상호작용 프롬프트 상황 추가
        // SO - {상황 : 키 이미지 : 타이틀} 매핑
        None = 0,
        HarvestAvailable = 1,
    }

    /// <summary>상황 하나에 대응하는 프롬프트 데이터이다.</summary>
    [Serializable]
    public struct InteractionPromptEntry
    {
        [SerializeField] private InteractionPromptType promptType; // 상황 키
        [SerializeField] private Sprite iconSprite; // 표시할 아이콘
        [SerializeField] private string title; // 표시할 타이틀

        public InteractionPromptType PromptType => promptType;
        public Sprite IconSprite => iconSprite;
        public string Title => title;
    }
}
