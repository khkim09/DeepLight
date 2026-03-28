using UnityEngine;

namespace Project.Data.CameraSystem
{
    /// <summary>조종실 타깃 바라보기 회전 속도를 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "CockpitLookTuningSO",
        menuName = "Project/Camera/Cockpit Look Tuning")]
    public class CockpitLookTuningSO : ScriptableObject
    {
        [SerializeField] private float rotationSpeed = 8f; // 회전 보간 속도

        /// <summary>유효한 회전 보간 속도를 반환한다.</summary>
        public float RotationSpeed => Mathf.Max(0.01f, rotationSpeed);
    }
}
