using System.Globalization;
using System.Text;
using Ceres.Editor.Graph.Flow.CodeGen;
using Ceres.Graph.Flow;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Ceres.Editor.Graph.Flow
{
    internal sealed class FlowGeneratedRuntimePanel : VisualElement
    {
        private const string GeneratedRuntimeInfoPropertyName = "generatedRuntimeInfo";

        private const int MetadataPreferredBreakColumn = 18;

        private const int MetadataWrapColumn = 34;

        private readonly IFlowGeneratedRuntimeContainer _container;

        private readonly SerializedObject _serializedObject;

        private readonly SerializedProperty _infoProperty;

        private readonly Label _statusLabel;

        private readonly VisualElement _details;

        private readonly Label _idValue;

        private readonly Label _generatorVersionValue;

        private readonly Label _graphHashValue;

        private readonly Label _generatedTypeValue;

        private readonly Label _generatedTicksValue;

        private readonly Label _functionDependenciesValue;

        public FlowGeneratedRuntimePanel(IFlowGeneratedRuntimeContainer container, SerializedObject serializedObject)
        {
            _container = container;
            _serializedObject = serializedObject;
            _infoProperty = serializedObject.FindProperty(GeneratedRuntimeInfoPropertyName);

            AddToClassList("flow-generated-runtime-panel");

            var header = new VisualElement();
            header.AddToClassList("flow-generated-runtime-header");
            header.Add(new Label("Generated C# Runtime"));
            _statusLabel = new Label();
            _statusLabel.AddToClassList("flow-generated-runtime-status");
            header.Add(_statusLabel);
            Add(header);

            if (_infoProperty == null)
            {
                var message = new Label("Generated runtime metadata is not available for this container.");
                message.AddToClassList("flow-generated-runtime-message");
                Add(message);
                return;
            }

            var enabledProperty = _infoProperty.FindPropertyRelative(nameof(FlowGeneratedProgramInfo.enabled));
            var enabledToggle = new Toggle("Enabled");
            enabledToggle.AddToClassList("flow-generated-runtime-toggle");
            enabledToggle.BindProperty(enabledProperty);
            enabledToggle.RegisterValueChangedCallback(_ =>
            {
                _serializedObject.ApplyModifiedProperties();
                SynchronizeManualRuntime();
                _serializedObject.Update();
                Refresh();
            });
            Add(enabledToggle);

            _details = new VisualElement();
            _details.AddToClassList("flow-generated-runtime-details");
            _details.Add(CreateMetadataRow(IsAssetContainer() ? "Asset GUID" : "Program ID", out _idValue));
            _details.Add(CreateMetadataRow("Generator Version", out _generatorVersionValue));
            _details.Add(CreateMetadataRow("Graph Hash", out _graphHashValue));
            _details.Add(CreateMetadataRow("Generated Type", out _generatedTypeValue));
            _details.Add(CreateMetadataRow("Generated Ticks", out _generatedTicksValue));
            _details.Add(CreateMetadataRow("Function Dependencies", out _functionDependenciesValue));
            Add(_details);

            var generateButton = new Button(GenerateRuntime)
            {
                text = "Generate C# Runtime"
            };
            generateButton.AddToClassList("flow-generated-runtime-generate-button");
            Add(generateButton);

            Refresh();
        }

        public void Refresh()
        {
            _serializedObject.Update();

            ClearStatusClasses();
            var info = _container.GeneratedRuntimeInfo;
            var status = GetStatus(info);
            _statusLabel.text = status;
            _statusLabel.AddToClassList(status switch
            {
                "Current" => "status-current",
                "Disabled" => "status-disabled",
                _ => "status-warning"
            });

            if (_infoProperty == null || info == null)
            {
                return;
            }

            var hasGeneratedInfo = !string.IsNullOrEmpty(info.graphHash) ||
                                   !string.IsNullOrEmpty(info.generatedTypeName);
            _details.style.display = info.enabled && hasGeneratedInfo ? DisplayStyle.Flex : DisplayStyle.None;

            SetMetadataValue(_idValue, IsAssetContainer() ? info.assetGuid : info.GetProgramId());
            SetMetadataValue(_generatorVersionValue, info.generatorVersion.ToString(CultureInfo.InvariantCulture),
                false);
            SetMetadataValue(_graphHashValue, info.graphHash);
            SetMetadataValue(_generatedTypeValue, info.generatedTypeName);
            SetMetadataValue(_generatedTicksValue, info.generatedUtcTicks > 0
                ? info.generatedUtcTicks.ToString(CultureInfo.InvariantCulture)
                : "-", false);
            SetMetadataValue(_functionDependenciesValue, (info.functionDependencies?.Length ?? 0)
                .ToString(CultureInfo.InvariantCulture), false);
        }

        private void GenerateRuntime()
        {
            _serializedObject.ApplyModifiedProperties();
            try
            {
                FlowCSharpRuntimeGenerator.GenerateContainer(_container);
                AssetDatabase.Refresh();
                _serializedObject.Update();
                Refresh();
            }
            catch (FlowCSharpRuntimeGenerationException e)
            {
                EditorUtility.DisplayDialog("Ceres Generated Runtime", e.Message, "OK");
            }
        }

        private void SynchronizeManualRuntime()
        {
            if (IsAssetContainer()) return;

            try
            {
                FlowCSharpRuntimeGenerator.SynchronizeManualGeneratedRuntime(_container);
            }
            catch (FlowCSharpRuntimeGenerationException e)
            {
                EditorUtility.DisplayDialog("Ceres Generated Runtime", e.Message, "OK");
            }
        }

        private string GetStatus(FlowGeneratedProgramInfo info)
        {
            if (info == null || !info.enabled) return "Disabled";
            if (info.IsCurrent(_container.GetFlowGraphData())) return "Current";
            return string.IsNullOrEmpty(info.graphHash) || string.IsNullOrEmpty(info.generatedTypeName)
                ? "Missing"
                : "Stale";
        }

        private void ClearStatusClasses()
        {
            _statusLabel.RemoveFromClassList("status-disabled");
            _statusLabel.RemoveFromClassList("status-current");
            _statusLabel.RemoveFromClassList("status-warning");
        }

        private bool IsAssetContainer()
        {
            return _container.Object is FlowGraphScriptableObjectBase;
        }

        private static VisualElement CreateMetadataRow(string label, out Label valueLabel)
        {
            var row = new VisualElement();
            row.AddToClassList("flow-generated-runtime-metadata-row");

            var nameLabel = new Label(label);
            nameLabel.AddToClassList("flow-generated-runtime-metadata-name");
            row.Add(nameLabel);

            valueLabel = new Label("-");
            valueLabel.AddToClassList("flow-generated-runtime-metadata-value");
            row.Add(valueLabel);
            return row;
        }

        private static string FormatValue(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static void SetMetadataValue(Label label, string value, bool wrapLongValue = true)
        {
            var formattedValue = FormatValue(value);
            label.text = wrapLongValue ? WrapMetadataValue(formattedValue) : formattedValue;
            label.tooltip = formattedValue == "-" ? string.Empty : formattedValue;
        }

        private static string WrapMetadataValue(string value)
        {
            if (value.Length <= MetadataWrapColumn || value == "-")
            {
                return value;
            }

            var builder = new StringBuilder(value.Length + value.Length / MetadataWrapColumn);
            var lineLength = 0;
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                builder.Append(character);
                lineLength++;
                if (i == value.Length - 1)
                {
                    continue;
                }

                if (ShouldBreakMetadataValue(character, lineLength))
                {
                    builder.Append('\n');
                    lineLength = 0;
                }
            }

            return builder.ToString();
        }

        private static bool ShouldBreakMetadataValue(char character, int lineLength)
        {
            if (lineLength >= MetadataWrapColumn)
            {
                return true;
            }

            return lineLength >= MetadataPreferredBreakColumn &&
                   (character == '.' || character == '_' || character == '-' || character == '/');
        }
    }
}
