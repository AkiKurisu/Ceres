using Ceres.Modules;
using Cysharp.Threading.Tasks;
using UnityEngine.Scripting;

namespace Ceres.Localization
{
    [Preserve]
    internal sealed class LocalizationModule : RuntimeModule
    {
        public override int Order => 10;

        public override void Initialize()
        {
            LocalizationSystem.InitializeAsync().Forget();
        }
    }
}
