using System;
using System.Collections.Generic;
using UnityEngine;
namespace Ceres.Graph
{
    [Serializable]
    public class NodeGroup
    {
        public List<string> childNodes = new();
        
        public Vector2 position;
        
        public string title = "Node Group";
    }
}
