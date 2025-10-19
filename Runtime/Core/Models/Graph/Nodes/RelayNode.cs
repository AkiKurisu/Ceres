using System;
using Chris.Serialization;
using UnityEngine;

namespace Ceres.Graph
{
    /// <summary>
    /// Relay node connection data
    /// </summary>
    [Serializable]
    public class RelayConnection
    {
        /// <summary>
        /// Connection target type
        /// </summary>
        public enum ConnectionType
        {
            /// <summary>
            /// Connected to an ExecutableNode port
            /// </summary>
            ExecutableNode,

            /// <summary>
            /// Connected to another RelayNode
            /// </summary>
            RelayNode
        }

        /// <summary>
        /// Type of connection target
        /// </summary>
        public ConnectionType connectionType;

        /// <summary>
        /// Target node GUID (ExecutableNode or RelayNode)
        /// </summary>
        public string nodeId;

        /// <summary>
        /// Target port property name (only used when connectionType is ExecutableNode)
        /// </summary>
        public string portId;

        /// <summary>
        /// Target port array index (only used when connectionType is ExecutableNode)
        /// </summary>
        public int portIndex;
    }

    /// <summary>
    /// Metadata for editor relay node
    /// </summary>
    [Serializable]
    public class RelayNode
    {
        /// <summary>
        /// Unique identifier for relay node
        /// </summary>
        public string guid;

        /// <summary>
        /// Display position in graph editor
        /// </summary>
        public Rect graphPosition = new(0, 0, 40, 20);

        /// <summary>
        /// Port type string (serialized using SerializedType)
        /// </summary>
        public string portType;

        /// <summary>
        /// Input connections (nodes connecting TO this relay)
        /// </summary>
        public RelayConnection[] inputs = Array.Empty<RelayConnection>();

        /// <summary>
        /// Output connections (nodes connecting FROM this relay)
        /// </summary>
        public RelayConnection[] outputs = Array.Empty<RelayConnection>();

        /// <summary>
        /// Get port value type
        /// </summary>
        public Type GetPortType()
        {
            return string.IsNullOrEmpty(portType) ? typeof(object) : SerializedType.FromString(portType);
        }

        /// <summary>
        /// Set port value type
        /// </summary>
        public void SetPortType(Type type)
        {
            portType = SerializedType.ToString(type);
        }
    }
}

