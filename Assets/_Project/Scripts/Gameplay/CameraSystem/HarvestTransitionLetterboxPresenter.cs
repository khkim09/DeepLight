using Cysharp.Threading.Tasks;
using Project.Data.CameraSystem;
using UnityEngine;

namespace Project.Gameplay.CameraSystem
{
    /// <summary>레터박스 진입과 해제 연출을 처리하는 클래스</summary>
    public class HarvestTransitionLetterboxPresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform topBar; // 상단 바
        [SerializeField] private RectTransform bottomBar; // 하단 바
        [SerializeField] private CameraTransitionTuningSO tuning; // 전환/레터박스 튜닝

        /// <summary>레터박스를 화면에 펼친다</summary>
        public async UniTask ShowAsync()
        {
            if (tuning == null)
                return;

            await AnimateAsync(0f, tuning.LetterboxShownHeight);
        }

        /// <summary>레터박스를 화면에서 접는다</summary>
        public async UniTask HideAsync()
        {
            if (tuning == null)
                return;

            await AnimateAsync(tuning.LetterboxShownHeight, 0f);
        }

        /// <summary>두 바의 높이를 보간 변경한다</summary>
        private async UniTask AnimateAsync(float from, float to)
        {
            if (tuning == null)
                return;

            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, tuning.LetterboxAnimateDuration);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float currentHeight = Mathf.Lerp(from, to, normalizedTime);

                SetBarHeight(currentHeight);
                await UniTask.Yield();
            }

            SetBarHeight(to);
        }

        /// <summary>상하단 바 높이를 동시에 적용한다</summary>
        private void SetBarHeight(float height)
        {
            if (topBar != null)
                topBar.sizeDelta = new Vector2(topBar.sizeDelta.x, height);

            if (bottomBar != null)
                bottomBar.sizeDelta = new Vector2(bottomBar.sizeDelta.x, height);
        }
    }
}
