using System;
using Ceres.Configs;
using UnityEngine;

namespace Ceres.DataDriven
{
    [Serializable]
    [ConfigPath("Ceres.DataDriven")]
    public class DataDrivenConfig : Config<DataDrivenConfig>
    {
        [SerializeField]
        internal bool initializeDataTableManagerOnLoad;
        
        [SerializeField]
        internal bool validateDataTableBeforeLoad = true;

        public static bool InitializeDataTableManagerOnLoad => Get().initializeDataTableManagerOnLoad;
        
        public static bool ValidateDataTableBeforeLoad => Get().validateDataTableBeforeLoad;
    }
}