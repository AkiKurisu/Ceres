using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using UObject = UnityEngine.Object;
namespace Ceres.Annotations
{
    /// <summary>
    /// Overwrite the display text in Ceres Graph Editor
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Method
        | AttributeTargets.Parameter | AttributeTargets.ReturnValue, Inherited = false)]
    public sealed class CeresLabelAttribute : Attribute
    {
        private readonly string _label;

        public string GetLabel(bool richText = true)
        {
            if (FontSize != 0 && richText)
            {
                return $"<size={FontSize}>{_label}</size>";
            }

            return _label;
        }

        public int FontSize { get; set; }
        
        public CeresLabelAttribute(string label)
        {
            _label = label;
        }
    }
    
    public static class CeresLabel
    {
        private static readonly TextInfo TextInfo = new CultureInfo("en-US", false).TextInfo;
        
        public static string GetLabel(FieldInfo fieldInfo, bool richText = true)
        {
            var labelAttribute = fieldInfo.GetCustomAttribute<CeresLabelAttribute>();
            return labelAttribute?.GetLabel(richText) ?? GetLabel(fieldInfo.Name);
        }
        
        public static string GetLabel(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            
            var sb = new StringBuilder();
            sb.Append(name);
            sb[0] = TextInfo.ToUpper(sb[0]);
            return sb.ToString();
        }
        
        public static string GetLabel(Type type, bool richText = true)
        {
            var labelAttribute = type.GetCustomAttribute<CeresLabelAttribute>();
            if (labelAttribute != null)
            {
                return labelAttribute.GetLabel(richText);
            }
            
            if (TryGetTypeAlias(type, out var name))
            {
                return name;
            }
            
            if (type.IsGenericType)
            {
                return type.GetGenericTypeDefinition().Name.Split('`')[0] 
                       + '<' + string.Join(", ",type.GetGenericArguments().Select(x => GetLabel(x, richText)).ToArray()) + '>';
            }
            return GetLabel(type.Name);
        }

        public static string GetTypeName(Type type)
        {
            if (TryGetTypeAlias(type, out var name))
            {
                return name;
            }
            
            if (type.IsGenericType)
            {
                return type.GetGenericTypeDefinition().Name.Split('`')[0] 
                       + '<' + string.Join(", ",type.GetGenericArguments().Select(GetTypeName).ToArray()) + '>';
            }
            return GetLabel(type.Name);
        }

        private static bool TryGetTypeAlias(Type type, out string typeName)
        {
            if(type == typeof(UObject))
            {
                typeName = "UObject"; // No Object
                return true;
            }
            if(type == typeof(float))
            {
                typeName = "Float"; // No Single
                return true;
            }
            if(type == typeof(int))
            {
                typeName = "Int"; // No Int32
                return true;
            }

            typeName = null;
            return false;
        }
        
        public static string GetMethodSignature(MethodInfo method, bool includeParameterName)
        {
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            var sb = new StringBuilder();
            sb.Append(method.Name);
            sb.Append("(");

            var parameters = method.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                var param = parameters[i];
                if (includeParameterName)
                {
                    sb.Append($"{GetTypeName(param.ParameterType)} {param.Name}");
                }
                else
                {
                    sb.Append(GetTypeName(param.ParameterType));
                }
                if (i < parameters.Length - 1)
                    sb.Append(", ");
            }

            sb.Append(")");
            return sb.ToString();
        }
    }
}