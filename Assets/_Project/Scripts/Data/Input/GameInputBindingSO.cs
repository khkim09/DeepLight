using UnityEngine;

namespace Project.Data.Input
{
    /// <summary>게임 전역 입력 키 바인딩을 정의하는 SO이다.</summary>
    [CreateAssetMenu(
        fileName = "GameInputBindingsSO",
        menuName = "Project/Input/Input Bindings")]
    public class GameInputBindingsSO : ScriptableObject
    {
        [Header("Exploration - Movement")]
        [SerializeField] private KeyCode moveForwardKey = KeyCode.W;      // 전진
        [SerializeField] private KeyCode moveBackwardKey = KeyCode.S;     // 후진
        [SerializeField] private KeyCode ascendKey = KeyCode.Space;       // 상승
        [SerializeField] private KeyCode descendKey = KeyCode.C;          // 하강
        [SerializeField] private KeyCode boostKey = KeyCode.LeftShift;    // 부스트
        [SerializeField] private KeyCode freeLookKey = KeyCode.LeftAlt;   // 자유 시점

        [Header("Interaction")]
        [SerializeField] private KeyCode interactHarvestKey = KeyCode.F;  // 채집 진입

        [Header("Harvest Console")]
        [SerializeField] private KeyCode harvestScanKey = KeyCode.F;               // 스캔
        [SerializeField] private KeyCode harvestSwitchSensorKey = KeyCode.R;       // 센서 전환
        [SerializeField] private KeyCode harvestCommitKey = KeyCode.Space;         // 회수 확정
        [SerializeField] private KeyCode harvestExitKey = KeyCode.X;               // 회수 종료
        [SerializeField] private KeyCode harvestResetSequenceKey = KeyCode.G;      // 순서 리셋

        [Header("Always")]
        [SerializeField] private KeyCode harvestInventoryKey = KeyCode.Tab;        // 인벤토리

        [Header("Inventory")]
        [SerializeField] private KeyCode inventoryDiscardKey = KeyCode.Z;          // 아이템 버리기

        /// <summary>전진 키를 반환한다.</summary>
        public KeyCode MoveForwardKey => moveForwardKey;

        /// <summary>후진 키를 반환한다.</summary>
        public KeyCode MoveBackwardKey => moveBackwardKey;

        /// <summary>상승 키를 반환한다.</summary>
        public KeyCode AscendKey => ascendKey;

        /// <summary>하강 키를 반환한다.</summary>
        public KeyCode DescendKey => descendKey;

        /// <summary>부스트 키를 반환한다.</summary>
        public KeyCode BoostKey => boostKey;

        /// <summary>자유 시점 키를 반환한다.</summary>
        public KeyCode FreeLookKey => freeLookKey;

        /// <summary>채집 진입 키를 반환한다.</summary>
        public KeyCode InteractHarvestKey => interactHarvestKey;

        /// <summary>Harvest 스캔 키를 반환한다.</summary>
        public KeyCode HarvestScanKey => harvestScanKey;

        /// <summary>Harvest 센서 전환 키를 반환한다.</summary>
        public KeyCode HarvestSwitchSensorKey => harvestSwitchSensorKey;

        /// <summary>Harvest 회수 확정 키를 반환한다.</summary>
        public KeyCode HarvestCommitKey => harvestCommitKey;

        /// <summary>Harvest 종료 키를 반환한다.</summary>
        public KeyCode HarvestExitKey => harvestExitKey;

        /// <summary>Harvest 순서 리셋 키를 반환한다.</summary>
        public KeyCode HarvestResetSequenceKey => harvestResetSequenceKey;

        /// <summary>Harvest 인벤토리 키를 반환한다.</summary>
        public KeyCode HarvestInventoryKey => harvestInventoryKey;

        /// <summary>Inventory 폐기 키를 반환한다.</summary>
        public KeyCode InventoryDiscardKey => inventoryDiscardKey;
    }
}
