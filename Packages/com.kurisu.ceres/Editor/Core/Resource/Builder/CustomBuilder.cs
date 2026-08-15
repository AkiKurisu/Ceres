namespace Ceres.Resource.Editor
{
    public abstract class CustomBuilder : IResourceBuilder
    {
        public abstract string Description { get; }
        
        public virtual void Build(ResourceExportContext context)
        {

        }

        public virtual void Cleanup(ResourceExportContext context)
        {

        }
    }
}