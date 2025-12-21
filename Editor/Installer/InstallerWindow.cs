using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ceres.Editor.Installer
{
    /// <summary>
    /// Editor window for managing Ceres package dependencies
    /// </summary>
    public class InstallerWindow : EditorWindow
    {
        private const string BannerPath = "Documentation~/resources/Images/ceres_banner.png";

        private const float BannerHeight = 302f;

        private VisualElement _root;

        private VisualElement _dependencyContainer;

        private Button _installAllButton;

        private Button _refreshButton;

        private Label _statusLabel;

        private List<DependencyInfo> _dependencies;

        private readonly Dictionary<string, VisualElement> _dependencyCards = new();

        private ListRequest _listRequest;

        private AddRequest _addRequest;

        private bool _isRefreshing;

        private readonly Queue<DependencyInfo> _installQueue = new();

        [MenuItem("Tools/Ceres/Installer", false, 0)]
        public static void ShowWindow()
        {
            var window = GetWindow<InstallerWindow>();
            window.titleContent = new GUIContent("Ceres Installer", EditorGUIUtility.IconContent("Package Manager").image);
            window.maxSize = window.minSize = new Vector2(600, 800);
            window.Show();
        }

        private void CreateGUI()
        {
            _root = rootVisualElement;
            _root.style.flexGrow = 1;

            // Initialize dependencies
            _dependencies = new List<DependencyInfo>(DependencyConfig.Dependencies);

            // Apply styles
            ApplyStyles();

            // Create UI
            CreateBanner();
            CreateHeader();
            CreateDependencyList();
            CreateFooter();

            // Start checking installed packages
            RefreshDependencies();
        }

        private void ApplyStyles()
        {
            // Set background color
            _root.style.backgroundColor = new Color(0.22f, 0.22f, 0.22f);
        }

        private void CreateBanner()
        {
            var banner = new VisualElement
            {
                name = "banner",
                style =
                {
                    height = BannerHeight,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f),
                    alignItems = Align.Center,
                    justifyContent = Justify.Center,
                    marginBottom = 20,
                    borderBottomWidth = 2,
                    borderBottomColor = new Color(0.12f, 0.12f, 0.12f)
                }
            };

            var bannerTexture = LoadTextureFromFile(BannerPath);
            var bannerImage = new Image
            {
                image = bannerTexture,
                scaleMode = ScaleMode.ScaleToFit,
                style =
                {
                    width = Length.Percent(100),
                    height = Length.Percent(100)
                }
            };
            banner.Add(bannerImage);

            _root.Add(banner);
        }

        private void CreateHeader()
        {
            var header = new VisualElement
            {
                style =
                {
                    paddingLeft = 20,
                    paddingRight = 20,
                    paddingBottom = 10,
                    marginBottom = 10
                }
            };

            var titleLabel = new Label("Required Dependencies")
            {
                style =
                {
                    fontSize = 18,
                    color = Color.white,
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 5
                }
            };
            header.Add(titleLabel);

            var descriptionLabel = new Label("Ceres requires the following packages to function properly. Please install any missing dependencies.")
            {
                style =
                {
                    fontSize = 12,
                    color = new Color(0.8f, 0.8f, 0.8f),
                    whiteSpace = WhiteSpace.Normal
                }
            };
            header.Add(descriptionLabel);

            _root.Add(header);
        }

        private void CreateDependencyList()
        {
            var scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    flexGrow = 1,
                    paddingLeft = 20,
                    paddingRight = 20
                }
            };

            _dependencyContainer = new VisualElement
            {
                style =
                {
                    flexGrow = 1
                }
            };
            scrollView.Add(_dependencyContainer);

            foreach (var dependency in _dependencies)
            {
                var card = CreateDependencyCard(dependency);
                _dependencyCards[dependency.PackageId] = card;
                _dependencyContainer.Add(card);
            }

            _root.Add(scrollView);
        }

        private VisualElement CreateDependencyCard(DependencyInfo dependency)
        {
            var card = new VisualElement
            {
                name = $"dependency-card-{dependency.PackageId}",
                style =
                {
                    backgroundColor = new Color(0.25f, 0.25f, 0.25f),
                    borderTopLeftRadius = 6,
                    borderTopRightRadius = 6,
                    borderBottomLeftRadius = 6,
                    borderBottomRightRadius = 6,
                    marginBottom = 10,
                    paddingTop = 15,
                    paddingBottom = 15,
                    paddingLeft = 15,
                    paddingRight = 15,
                    borderLeftWidth = 3,
                    borderLeftColor = new Color(0.3f, 0.3f, 0.3f)
                }
            };

            // Header row (name + status)
            var headerRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    alignItems = Align.Center,
                    marginBottom = 8
                }
            };

            var nameLabel = new Label(dependency.DisplayName)
            {
                name = "name-label",
                style =
                {
                    fontSize = 14,
                    color = Color.white,
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            };
            headerRow.Add(nameLabel);

            var statusLabel = new Label
            {
                name = "status-label",
                style =
                {
                    fontSize = 11,
                    paddingTop = 4,
                    paddingBottom = 4,
                    paddingLeft = 10,
                    paddingRight = 10,
                    borderTopLeftRadius = 10,
                    borderTopRightRadius = 10,
                    borderBottomLeftRadius = 10,
                    borderBottomRightRadius = 10
                }
            };
            UpdateStatusLabel(statusLabel, dependency.Status);
            headerRow.Add(statusLabel);

            card.Add(headerRow);

            // Package ID
            var packageIdLabel = new Label($"Package: {dependency.PackageId}")
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.7f, 0.7f, 0.7f),
                    marginBottom = 5
                }
            };
            card.Add(packageIdLabel);

            // Description
            if (!string.IsNullOrEmpty(dependency.Description))
            {
                var descLabel = new Label(dependency.Description)
                {
                    style =
                    {
                        fontSize = 11,
                        color = new Color(0.8f, 0.8f, 0.8f),
                        whiteSpace = WhiteSpace.Normal,
                        marginBottom = 10
                    }
                };
                card.Add(descLabel);
            }

            // Git URL
            var urlLabel = new Label($"Source: {dependency.GitUrl}")
            {
                style =
                {
                    fontSize = 10,
                    color = new Color(0.6f, 0.6f, 0.6f),
                    marginBottom = 10,
                    whiteSpace = WhiteSpace.Normal
                }
            };
            card.Add(urlLabel);

            // Action row
            var actionRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.FlexEnd
                }
            };

            var installButton = new Button(() => InstallDependency(dependency))
            {
                name = "install-button",
                text = "Install",
                style =
                {
                    paddingTop = 6,
                    paddingBottom = 6,
                    paddingLeft = 20,
                    paddingRight = 20,
                    backgroundColor = new Color(0.24f, 0.48f, 0.24f),
                    color = Color.white,
                    borderTopLeftRadius = 3,
                    borderTopRightRadius = 3,
                    borderBottomLeftRadius = 3,
                    borderBottomRightRadius = 3,
                    borderLeftWidth = 0,
                    borderRightWidth = 0,
                    borderTopWidth = 0,
                    borderBottomWidth = 0
                }
            };
            actionRow.Add(installButton);

            card.Add(actionRow);

            return card;
        }

        private void UpdateStatusLabel(Label label, DependencyStatus status)
        {
            switch (status)
            {
                case DependencyStatus.Installed:
                    label.text = "✓ Installed";
                    label.style.backgroundColor = new Color(0.2f, 0.6f, 0.2f);
                    label.style.color = Color.white;
                    break;
                case DependencyStatus.Installing:
                    label.text = "⟳ Installing...";
                    label.style.backgroundColor = new Color(0.4f, 0.4f, 0.8f);
                    label.style.color = Color.white;
                    break;
                case DependencyStatus.Error:
                    label.text = "✗ Error";
                    label.style.backgroundColor = new Color(0.8f, 0.2f, 0.2f);
                    label.style.color = Color.white;
                    break;
                default:
                    label.text = "Not Installed";
                    label.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f);
                    label.style.color = Color.white;
                    break;
            }
        }

        private void CreateFooter()
        {
            var footer = new VisualElement
            {
                style =
                {
                    paddingTop = 15,
                    paddingBottom = 15,
                    paddingLeft = 20,
                    paddingRight = 20,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f),
                    borderTopWidth = 1,
                    borderTopColor = new Color(0.12f, 0.12f, 0.12f),
                    flexDirection = FlexDirection.Row,
                    justifyContent = Justify.SpaceBetween,
                    alignItems = Align.Center
                }
            };

            _statusLabel = new Label("Ready")
            {
                style =
                {
                    fontSize = 11,
                    color = new Color(0.7f, 0.7f, 0.7f)
                }
            };
            footer.Add(_statusLabel);

            var buttonContainer = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row
                }
            };

            _refreshButton = new Button(RefreshDependencies)
            {
                text = "Refresh",
                style =
                {
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 20,
                    paddingRight = 20,
                    marginRight = 10
                }
            };
            buttonContainer.Add(_refreshButton);

            _installAllButton = new Button(InstallAllMissing)
            {
                text = "Install All",
                style =
                {
                    paddingTop = 8,
                    paddingBottom = 8,
                    paddingLeft = 20,
                    paddingRight = 20,
                    backgroundColor = new Color(0.24f, 0.48f, 0.24f),
                    color = Color.white
                }
            };
            buttonContainer.Add(_installAllButton);

            footer.Add(buttonContainer);

            _root.Add(footer);
        }

        private void RefreshDependencies()
        {
            if (_isRefreshing) return;

            _isRefreshing = true;
            _statusLabel.text = "Checking installed packages...";
            _listRequest = Client.List(true);
            EditorApplication.update += CheckListProgress;
        }

        private void CheckListProgress()
        {
            if (_listRequest == null || !_listRequest.IsCompleted)
                return;

            EditorApplication.update -= CheckListProgress;

            if (_listRequest.Status == StatusCode.Success)
            {
                var installedPackages = new HashSet<string>();
                foreach (var package in _listRequest.Result)
                {
                    installedPackages.Add(package.name);
                }

                foreach (var dependency in _dependencies)
                {
                    dependency.Status = installedPackages.Contains(dependency.PackageId)
                        ? DependencyStatus.Installed
                        : DependencyStatus.NotInstalled;
                }

                UpdateUI();

                var missingCount = _dependencies.Count(d => d.Status == DependencyStatus.NotInstalled);
                _statusLabel.text = missingCount > 0
                    ? $"{missingCount} package(s) need to be installed"
                    : "All dependencies are installed!";
            }
            else
            {
                _statusLabel.text = "Error checking packages";
                Debug.LogError($"Failed to list packages: {_listRequest.Error.message}");
            }

            _isRefreshing = false;
            _listRequest = null;
        }

        private void InstallDependency(DependencyInfo dependency)
        {
            if (dependency.Status == DependencyStatus.Installing || dependency.Status == DependencyStatus.Installed)
                return;

            dependency.Status = DependencyStatus.Installing;
            UpdateDependencyCard(dependency);

            _statusLabel.text = $"Installing {dependency.DisplayName}...";
            _addRequest = Client.Add(dependency.GitUrl);
            EditorApplication.update += () => CheckInstallProgress(dependency);
        }

        private void CheckInstallProgress(DependencyInfo dependency)
        {
            if (_addRequest == null || !_addRequest.IsCompleted)
                return;

            EditorApplication.update -= () => CheckInstallProgress(dependency);

            if (_addRequest.Status == StatusCode.Success)
            {
                dependency.Status = DependencyStatus.Installed;
                _statusLabel.text = $"{dependency.DisplayName} installed successfully!";
                Debug.Log($"Successfully installed {dependency.DisplayName}");
            }
            else
            {
                dependency.Status = DependencyStatus.Error;
                dependency.ErrorMessage = _addRequest.Error.message;
                _statusLabel.text = $"Failed to install {dependency.DisplayName}";
                Debug.LogError($"Failed to install {dependency.DisplayName}: {_addRequest.Error.message}");
            }

            UpdateDependencyCard(dependency);
            _addRequest = null;

            // Process next in queue
            if (_installQueue.Count > 0)
            {
                var next = _installQueue.Dequeue();
                InstallDependency(next);
            }
        }

        private void InstallAllMissing()
        {
            var missingDependencies = _dependencies.Where(d => d.Status == DependencyStatus.NotInstalled).ToList();

            if (missingDependencies.Count == 0)
            {
                _statusLabel.text = "All dependencies are already installed!";
                return;
            }

            _installQueue.Clear();
            foreach (var dep in missingDependencies)
            {
                _installQueue.Enqueue(dep);
            }

            if (_installQueue.Count > 0)
            {
                var first = _installQueue.Dequeue();
                InstallDependency(first);
            }
        }

        private void UpdateUI()
        {
            foreach (var dependency in _dependencies)
            {
                UpdateDependencyCard(dependency);
            }

            UpdateButtons();
        }

        private void UpdateDependencyCard(DependencyInfo dependency)
        {
            if (!_dependencyCards.TryGetValue(dependency.PackageId, out var card))
                return;

            var statusLabel = card.Q<Label>("status-label");
            if (statusLabel != null)
            {
                UpdateStatusLabel(statusLabel, dependency.Status);
            }

            var installButton = card.Q<Button>("install-button");
            if (installButton != null)
            {
                installButton.SetEnabled(dependency.Status == DependencyStatus.NotInstalled || dependency.Status == DependencyStatus.Error);

                if (dependency.Status == DependencyStatus.Installed)
                {
                    installButton.style.backgroundColor = new Color(0.3f, 0.3f, 0.3f);
                }
                else if (dependency.Status == DependencyStatus.Installing)
                {
                    installButton.style.backgroundColor = new Color(0.4f, 0.4f, 0.8f);
                }
                else
                {
                    installButton.style.backgroundColor = new Color(0.24f, 0.48f, 0.24f);
                }
            }

            // Update card border color based on status
            switch (dependency.Status)
            {
                case DependencyStatus.Installed:
                    card.style.borderLeftColor = new Color(0.2f, 0.8f, 0.2f);
                    break;
                case DependencyStatus.Installing:
                    card.style.borderLeftColor = new Color(0.4f, 0.4f, 1f);
                    break;
                case DependencyStatus.Error:
                    card.style.borderLeftColor = new Color(0.8f, 0.2f, 0.2f);
                    break;
                default:
                    card.style.borderLeftColor = new Color(0.3f, 0.3f, 0.3f);
                    break;
            }
        }

        private void UpdateButtons()
        {
            var hasUninstalled = _dependencies.Any(d => d.Status == DependencyStatus.NotInstalled);
            _installAllButton.SetEnabled(hasUninstalled && !_isRefreshing);
            _refreshButton.SetEnabled(!_isRefreshing);
        }

        private static Texture2D LoadTextureFromFile(string packageRelativePath, [CallerFilePath] string sourceFilePath = "")
        {
            try
            {
                var scriptDirectory = Path.GetDirectoryName(sourceFilePath)!;
                var ceresRootDirectory = Path.GetFullPath(Path.Combine(scriptDirectory, "..", ".."));
                var absolutePath = Path.Combine(ceresRootDirectory, packageRelativePath);

                if (!File.Exists(absolutePath))
                {
                    Debug.LogWarning($"Banner image not found at: {absolutePath}");
                    return null;
                }

                // Read file bytes
                var fileData = File.ReadAllBytes(absolutePath);

                // Create texture and load image data
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                if (texture.LoadImage(fileData))
                {
                    return texture;
                }

                Debug.LogError("Failed to load image data into texture");
                DestroyImmediate(texture);
                return null;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error loading banner texture: {ex.Message}");
                return null;
            }
        }
    }
}

