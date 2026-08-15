using Ceres.Gameplay.Mod;
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Ceres.Gameplay.Tests
{
    public class ModLoaderTest : MonoBehaviour
    {
        public string modTestObjectAddress = "ModTestObject";
        
        private void Awake()
        {
            ModAPI.Initialized.Where(x => x).Subscribe(_ =>
            {
                Debug.Log("ModAPI Initialized succeed");
                InstantiateModObjectTest().Forget();
            }).AddTo(this);
            ModAPI.Initialize(ModConfig.Get()).Forget();
        }

        private async UniTask InstantiateModObjectTest()
        {
            await UniTask.Delay(1);
            await ResourceSystem.InstantiateAsync(modTestObjectAddress, transform);
        }
    }
}
