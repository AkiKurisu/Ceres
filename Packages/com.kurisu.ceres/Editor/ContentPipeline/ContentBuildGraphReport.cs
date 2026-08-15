using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Ceres.ContentPipeline
{
    public static class ContentBuildGraphReport
    {
        public static string ToJson(ContentBuildGraph graph, bool prettyPrint = true)
        {
            if (graph == null) throw new ArgumentNullException(nameof(graph));

            var writer = new DeterministicJsonWriter(prettyPrint);
            writer.BeginObject();
            writer.Property("fingerprint", graph.Fingerprint);
            writer.Property("isBuildable", graph.IsBuildable);
            writer.PropertyName("scopes");
            writer.BeginArray();
            for (var i = 0; i < graph.Scopes.Count; i++)
            {
                var scope = graph.Scopes[i];
                writer.BeginObject();
                writer.Property("id", scope.Id);
                writer.Property("displayName", scope.DisplayName);
                writer.Property("version", scope.Version);
                writer.Property("enabled", scope.Enabled);
                writer.Property("defaultLocation", scope.DefaultLocation.ToString());
                writer.PropertyName("properties");
                writer.BeginObject();
                foreach (var property in scope.Properties)
                {
                    writer.Property(property.Key, property.Value);
                }

                writer.EndObject();
                writer.EndObject();
            }

            writer.EndArray();
            writer.PropertyName("assets");
            writer.BeginArray();
            for (var i = 0; i < graph.Assets.Count; i++)
            {
                var asset = graph.Assets[i];
                writer.BeginObject();
                writer.Property("assetId", asset.AssetId);
                writer.Property("assetPath", asset.AssetPath);
                writer.Property("address", asset.Address);
                writer.Property("typeName", asset.TypeName);
                writer.Property("explicit", asset.IsExplicit);
                writer.Property("location", asset.Location.ToString());
                writer.Property("ownership", asset.Ownership.ToString());
                writer.Property("ownerScopeId", asset.OwnerScopeId);
                writer.Property("partitionId", asset.PartitionId);
                writer.Property("sharedCandidate", asset.IsSharedCandidate);
                writer.StringArray("labels", asset.Labels);
                writer.StringArray("explicitScopeIds", asset.ExplicitScopeIds);
                writer.StringArray("usageScopeIds", asset.UsageScopeIds);
                writer.StringArray("packingHints", asset.PackingHints);
                writer.EndObject();
            }

            writer.EndArray();
            writer.PropertyName("edges");
            writer.BeginArray();
            for (var i = 0; i < graph.Edges.Count; i++)
            {
                var edge = graph.Edges[i];
                writer.BeginObject();
                writer.Property("sourceAssetId", edge.SourceAssetId);
                writer.Property("dependencyAssetId", edge.DependencyAssetId);
                writer.EndObject();
            }

            writer.EndArray();
            writer.PropertyName("diagnostics");
            writer.BeginArray();
            for (var i = 0; i < graph.Diagnostics.Count; i++)
            {
                var diagnostic = graph.Diagnostics[i];
                writer.BeginObject();
                writer.Property("code", diagnostic.Code);
                writer.Property("severity", diagnostic.Severity.ToString());
                writer.Property("message", diagnostic.Message);
                writer.Property("scopeId", diagnostic.ScopeId);
                writer.Property("assetId", diagnostic.AssetId);
                writer.Property("suggestion", diagnostic.Suggestion);
                writer.EndObject();
            }

            writer.EndArray();
            writer.EndObject();
            return writer.ToString();
        }
    }

    internal static class ContentBuildGraphFingerprint
    {
        public static string Compute(
            IReadOnlyList<ContentScopeDefinition> scopes,
            IReadOnlyList<ContentAssetNode> assets,
            IReadOnlyList<ContentDependencyEdge> edges,
            IReadOnlyList<ContentBuildDiagnostic> diagnostics)
        {
            var canonical = new StringBuilder();
            foreach (var scope in scopes)
            {
                Append(canonical, "scope", scope.Id, scope.DisplayName, scope.Version,
                    scope.Enabled.ToString(CultureInfo.InvariantCulture), scope.DefaultLocation.ToString());
                foreach (var property in scope.Properties)
                {
                    Append(canonical, "property", scope.Id, property.Key, property.Value);
                }
            }

            foreach (var asset in assets)
            {
                Append(canonical, "asset", asset.AssetId, asset.AssetPath, asset.Address, asset.TypeName,
                    asset.IsExplicit.ToString(CultureInfo.InvariantCulture), asset.Location.ToString(),
                    asset.Ownership.ToString(), asset.OwnerScopeId, asset.PartitionId,
                    asset.IsSharedCandidate.ToString(CultureInfo.InvariantCulture));
                AppendList(canonical, "labels", asset.AssetId, asset.Labels);
                AppendList(canonical, "explicitScopes", asset.AssetId, asset.ExplicitScopeIds);
                AppendList(canonical, "usageScopes", asset.AssetId, asset.UsageScopeIds);
                AppendList(canonical, "packingHints", asset.AssetId, asset.PackingHints);
            }

            foreach (var edge in edges)
            {
                Append(canonical, "edge", edge.SourceAssetId, edge.DependencyAssetId);
            }

            foreach (var diagnostic in diagnostics)
            {
                Append(canonical, "diagnostic", diagnostic.Code, diagnostic.Severity.ToString(),
                    diagnostic.ScopeId, diagnostic.AssetId, diagnostic.Message, diagnostic.Suggestion);
            }

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
            return string.Concat(hash.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));
        }

        private static void AppendList(
            StringBuilder builder,
            string kind,
            string owner,
            IEnumerable<string> values)
        {
            foreach (var value in values)
            {
                Append(builder, kind, owner, value);
            }
        }

        private static void Append(StringBuilder builder, params string[] values)
        {
            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i] ?? string.Empty;
                builder.Append(value.Length)
                    .Append(':')
                    .Append(value)
                    .Append('|');
            }

            builder.Append('\n');
        }
    }

    internal sealed class DeterministicJsonWriter
    {
        private readonly StringBuilder _builder = new();
        private readonly Stack<ContainerState> _containers = new();
        private readonly bool _prettyPrint;
        private int _indent;
        private bool _propertyPending;

        public DeterministicJsonWriter(bool prettyPrint)
        {
            _prettyPrint = prettyPrint;
        }

        public void BeginObject()
        {
            BeforeValue();
            _builder.Append('{');
            _containers.Push(new ContainerState(true));
            _indent++;
        }

        public void EndObject()
        {
            var state = _containers.Pop();
            _indent--;
            if (_prettyPrint && !state.IsFirst)
            {
                NewLine();
            }

            _builder.Append('}');
            CompleteValue();
        }

        public void BeginArray()
        {
            BeforeValue();
            _builder.Append('[');
            _containers.Push(new ContainerState(false));
            _indent++;
        }

        public void EndArray()
        {
            var state = _containers.Pop();
            _indent--;
            if (_prettyPrint && !state.IsFirst)
            {
                NewLine();
            }

            _builder.Append(']');
            CompleteValue();
        }

        public void PropertyName(string name)
        {
            BeforeElement();
            AppendString(name);
            _builder.Append(_prettyPrint ? ": " : ":");
            _propertyPending = true;
        }

        public void Property(string name, string value)
        {
            PropertyName(name);
            AppendString(value ?? string.Empty);
            CompleteValue();
        }

        public void Property(string name, bool value)
        {
            PropertyName(name);
            _builder.Append(value ? "true" : "false");
            CompleteValue();
        }

        public void StringArray(string name, IReadOnlyList<string> values)
        {
            PropertyName(name);
            BeginArray();
            for (var i = 0; i < values.Count; i++)
            {
                BeforeValue();
                AppendString(values[i]);
                CompleteValue();
            }

            EndArray();
        }

        public override string ToString()
        {
            return _builder.ToString();
        }

        private void BeforeValue()
        {
            if (_propertyPending)
            {
                _propertyPending = false;
                return;
            }

            if (_containers.Count > 0 && !_containers.Peek().IsObject)
            {
                BeforeElement();
            }
        }

        private void BeforeElement()
        {
            var state = _containers.Pop();
            if (!state.IsFirst)
            {
                _builder.Append(',');
            }

            state.IsFirst = false;
            _containers.Push(state);
            if (_prettyPrint)
            {
                NewLine();
            }
        }

        private void CompleteValue()
        {
            _propertyPending = false;
        }

        private void NewLine()
        {
            _builder.Append('\n');
            _builder.Append(' ', _indent * 2);
        }

        private void AppendString(string value)
        {
            _builder.Append('"');
            for (var i = 0; i < value.Length; i++)
            {
                var character = value[i];
                switch (character)
                {
                    case '"':
                        _builder.Append("\\\"");
                        break;
                    case '\\':
                        _builder.Append("\\\\");
                        break;
                    case '\b':
                        _builder.Append("\\b");
                        break;
                    case '\f':
                        _builder.Append("\\f");
                        break;
                    case '\n':
                        _builder.Append("\\n");
                        break;
                    case '\r':
                        _builder.Append("\\r");
                        break;
                    case '\t':
                        _builder.Append("\\t");
                        break;
                    default:
                        if (character < ' ')
                        {
                            _builder.Append("\\u")
                                .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _builder.Append(character);
                        }

                        break;
                }
            }

            _builder.Append('"');
        }

        private struct ContainerState
        {
            public ContainerState(bool isObject)
            {
                IsObject = isObject;
                IsFirst = true;
            }

            public bool IsObject { get; }

            public bool IsFirst { get; set; }
        }
    }
}
