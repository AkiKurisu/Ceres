using System;
using Ceres.DataDriven;

namespace Ceres.Localization
{
    /// <summary>
    /// Row of a string table, keyed by its row id
    /// </summary>
    [Serializable]
    public class LocalizedTextRow : IDataTableRow
    {
        public string text = string.Empty;

        /// <summary>
        /// Context for translators
        /// </summary>
        [EditorOnly]
        public string note = string.Empty;
    }
}
