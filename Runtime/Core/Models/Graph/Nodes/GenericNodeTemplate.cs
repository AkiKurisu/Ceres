using System;
using System.Collections.Generic;
using System.Linq;
using Ceres.Utilities;
namespace Ceres.Graph
{
    /// <summary>
    /// Interface for generic node template
    /// </summary>
    public interface IGenericNodeTemplate
    {
        /// <summary>
        /// Get arguments to construct generic node type
        /// </summary>
        /// <param name="portValueType">Port value type, null if no port dropped</param>
        /// <param name="selectArgumentType"></param>
        /// <returns></returns>
        Type[] GetGenericArguments(Type portValueType, Type selectArgumentType);

        /// <summary>
        /// Whether input port value type is allowed for this template
        /// </summary>
        /// <param name="portValueType">Port value type, null if no port dropped</param>
        /// <returns></returns>
        bool CanFilterPort(Type portValueType);
        
        /// <summary>
        /// Get available argument types based on port value type
        /// </summary>
        /// <param name="portValueType">Port value type, null if no port dropped</param>
        /// <returns></returns>
        Type[] GetAvailableArgumentTypes(Type portValueType);

        /// <summary>
        /// Make generic node view editor name
        /// </summary>
        /// <param name="label"></param>
        /// <param name="selectArgumentType"></param>
        /// <returns></returns>
        string GetGenericNodeEntryName(string label,  Type selectArgumentType);
        
        /// <summary>
        /// Make generic node view editor name
        /// </summary>
        /// <param name="label"></param>
        /// <param name="argumentTypes"></param>
        /// <returns></returns>
        string GetGenericNodeName(string label, Type[] argumentTypes);

        /// <summary>
        /// Whether this template require to know port value type
        /// </summary>
        /// <returns></returns>
        bool RequirePort();
    }

    /// <summary>
    /// Base class for generic node template, class name should match '{node name}_Template'
    /// </summary>
    public abstract class GenericNodeTemplate: IGenericNodeTemplate
    {
        public abstract Type[] GetGenericArguments(Type portValueType, Type selectArgumentType);
        
        public virtual bool CanFilterPort(Type portValueType)
        {
            return !RequirePort() || portValueType != null;
        }

        public abstract Type[] GetAvailableArgumentTypes(Type portValueType);
        
        public virtual string GetGenericNodeEntryName(string label, Type selectArgumentType)
        {
            return string.Format(label, selectArgumentType.Name);
        }

        public virtual string GetGenericNodeName(string label, Type[] argumentTypes)
        {
            label = GetGenericNodeBaseName(label, argumentTypes);
            if (RequirePort())
            {
                label += CeresNode.GetTargetSubtitle(argumentTypes[0]);
            }
            return label;
        }
        
        protected virtual string GetGenericNodeBaseName(string label, Type[] argumentTypes)
        {
            // ReSharper disable once CoVariantArrayConversion
            return string.Format(label, argumentTypes.Where(x=>x != null).Select(x => x.Name).ToArray());
        }

        public virtual bool RequirePort()
        {
            return false;
        }
    }
    
    public static class GenericNodeTemplateRegistry
    {
        private static readonly Dictionary<Type, IGenericNodeTemplate> TemplateDict = new();

        private static Type[] _templateTypes;
        
        public static IGenericNodeTemplate GetTemplate(Type nodeType)
        {
            if (TemplateDict.TryGetValue(nodeType, out var template))
            {
                return template;
            }

            var templateType = FindTemplateType(nodeType);
            if (templateType == null) return null;

            var instance = (IGenericNodeTemplate)Activator.CreateInstance(templateType);
            TemplateDict.Add(nodeType, instance);
            return instance;
        }
        
        private static Type FindTemplateType(Type type)
        {
            try
            {
                var className = type.Name.Split('`')[0] + "_Template";
                _templateTypes ??= SubClassSearchUtility.FindSubClassTypes(typeof(IGenericNodeTemplate)).ToArray(); 
                return _templateTypes.FirstOrDefault(x=>x.Name == className);
            }
            catch
            {
                return null;
            }
        }
    }
}