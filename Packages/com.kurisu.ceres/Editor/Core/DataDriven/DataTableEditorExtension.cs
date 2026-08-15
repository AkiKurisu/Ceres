using System;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// Base class for extending the behavior of a DataTableEditor.
    /// Override this class to inject custom initialization, toolbar drawing,
    /// and cleanup logic for specific data tables.
    /// </summary>
    public abstract class DataTableEditorExtension: IDisposable
    {
        public virtual void Initialize(DataTableEditor editor)
        {
            
        }
        
        public virtual void Dispose()
        {
            
        }
    }
}