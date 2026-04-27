using UnityEngine;

namespace Project.Gameplay.World
{
    /// <summary>
    /// DeepLight Map Auto Builder의 Scene Context.
    /// Scene Hierarchy의 GameObject 참조를 안전하게 보관한다.
    /// Project Asset인 SettingsSO와 달리 Scene Object 참조를 직접 저장할 수 있다.
    /// </summary>
    public class DeepLightMapAutoBuilderSceneContext : MonoBehaviour
    {
        [Header("Scene Template References")]
        [SerializeField] private GameObject mapSettingsTemplateRoot; // 씬 내 MapSettings 오브젝트 (복제용 참조)
        [SerializeField] private GameObject globalWaterTemplate; // 글로벌 워터 템플릿 (복제용)
        [SerializeField] private GameObject underwaterAreaTemplate; // 수중 영역 템플릿 (Phase 4+)
        [SerializeField] private GameObject bubbleParticleTemplate; // 버블 파티클 템플릿 (Phase 4+)
        [SerializeField] private GameObject dynamicEffectTemplate; // 동적 이펙트 템플릿 (Phase 4+)

        [Header("Generated Root Override")]
        [SerializeField] private GameObject generatedRootOverride; // (옵션) 특정 GameObject를 Generated Root로 사용

        // ===== Public Getters =====

        /// <summary>씬 내 MapSettings 오브젝트 (복제용 참조)</summary>
        public GameObject MapSettingsTemplateRoot => mapSettingsTemplateRoot;

        /// <summary>글로벌 워터 템플릿 (복제용)</summary>
        public GameObject GlobalWaterTemplate => globalWaterTemplate;

        /// <summary>수중 영역 템플릿 (Phase 4+)</summary>
        public GameObject UnderwaterAreaTemplate => underwaterAreaTemplate;

        /// <summary>버블 파티클 템플릿 (Phase 4+)</summary>
        public GameObject BubbleParticleTemplate => bubbleParticleTemplate;

        /// <summary>동적 이펙트 템플릿 (Phase 4+)</summary>
        public GameObject DynamicEffectTemplate => dynamicEffectTemplate;

        /// <summary>(옵션) 특정 GameObject를 Generated Root로 사용</summary>
        public GameObject GeneratedRootOverride => generatedRootOverride;

        // ===== Public Setters (Editor Auto-Fill 용) =====

        /// <summary>MapSettings 템플릿 루트 설정 (Editor Auto-Fill 전용)</summary>
        public void SetMapSettingsTemplateRoot(GameObject value) { mapSettingsTemplateRoot = value; }

        /// <summary>글로벌 워터 템플릿 설정 (Editor Auto-Fill 전용)</summary>
        public void SetGlobalWaterTemplate(GameObject value) { globalWaterTemplate = value; }

        /// <summary>수중 영역 템플릿 설정 (Editor Auto-Fill 전용)</summary>
        public void SetUnderwaterAreaTemplate(GameObject value) { underwaterAreaTemplate = value; }

        /// <summary>버블 파티클 템플릿 설정 (Editor Auto-Fill 전용)</summary>
        public void SetBubbleParticleTemplate(GameObject value) { bubbleParticleTemplate = value; }

        /// <summary>동적 이펙트 템플릿 설정 (Editor Auto-Fill 전용)</summary>
        public void SetDynamicEffectTemplate(GameObject value) { dynamicEffectTemplate = value; }

        /// <summary>Generated Root Override 설정 (Editor Auto-Fill 전용)</summary>
        public void SetGeneratedRootOverride(GameObject value) { generatedRootOverride = value; }

        /// <summary>Generated Root Override 초기화</summary>
        public void ClearGeneratedRootOverride() { generatedRootOverride = null; }
    }
}
