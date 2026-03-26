using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>Harvest 전환 시 짧은 레터박스 연출을 재생하는 클래스</summary>
    public class HarvestTransitionLetterboxPresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform topBar; // 상단 검은 바
        [SerializeField] private RectTransform bottomBar; // 하단 검은 바
        [SerializeField] private float shownHeight = 90f; // 펼쳐질 높이
        [SerializeField] private float animateDuration = 0.18f; // 한 번의 펼침/복귀 시간

        /// <summary>레터박스를 화면에 표시한다</summary>
        public async UniTask ShowAsync() => await AnimateAsync(0f, shownHeight);

        /// <summary>레터박스를 화면에서 숨긴다</summary>
        public async UniTask HideAsync() => await AnimateAsync(shownHeight, 0f);

        /// <summary>지정된 높이 구간으로 막대 크기를 보간한다</summary>
        private async UniTask AnimateAsync(float from, float to)
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, animateDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float value = Mathf.Lerp(from, to, t);

                SetBarHeight(value);
                await UniTask.Yield();
            }

            SetBarHeight(to);
        }

        /// <summary>상하단 막대 높이를 동시에 갱신한다</summary>
        private void SetBarHeight(float height)
        {
            if (topBar != null)
                topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, height);

            if (bottomBar != null)
                bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, height);
        }
    }
}
