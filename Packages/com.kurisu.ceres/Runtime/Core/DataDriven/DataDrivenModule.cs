using Ceres.Modules;
using UnityEngine.Scripting;

namespace Ceres.DataDriven
{
    [Preserve]
    internal class DataDrivenModule: RuntimeModule
    {
        public override void Initialize()
        {
            if (DataDrivenConfig.InitializeDataTableManagerOnLoad)
            {
                DataTableManager.Initialize();
            }
        }
    }
}