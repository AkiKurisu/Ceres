namespace Ceres.Resource.Editor
{
    public interface IResourceBuilder
    {
        /// <summary>
        /// Preprocess for mod assets
        /// </summary>
        /// <param name="context"></param>
        void Build(ResourceExportContext context);
        
        /// <summary>
        /// Clean after build
        /// </summary>
        /// <param name="context"></param>
        void Cleanup(ResourceExportContext context);
    }
}
