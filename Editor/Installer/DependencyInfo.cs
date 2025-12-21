namespace Ceres.Editor.Installer
{
    /// <summary>
    /// Represents the installation status of a dependency
    /// </summary>
    public enum DependencyStatus
    {
        NotInstalled,
        Installing,
        Installed,
        Error
    }

    /// <summary>
    /// Information about a package dependency
    /// </summary>
    public class DependencyInfo
    {
        public string PackageId { get; set; }
        
        public string DisplayName { get; set; }
        
        public string GitUrl { get; set; }
        
        public string Description { get; set; }
        
        public DependencyStatus Status { get; set; }
        
        public string ErrorMessage { get; set; }

        public DependencyInfo(string packageId, string displayName, string gitUrl, string description = "")
        {
            PackageId = packageId;
            DisplayName = displayName;
            GitUrl = gitUrl;
            Description = description;
            Status = DependencyStatus.NotInstalled;
            ErrorMessage = string.Empty;
        }
    }
}

