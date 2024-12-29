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
            if(type == typeof(UObject))
            {
                return "UObject";
            }
            if(type == typeof(float))
            {
                return "Float";
            }
            if (type.IsGenericType)
            {
                return type.GetGenericTypeDefinition().Name.Split('`')[0] 
                       + '<' + string.Join(", ",type.GetGenericArguments().Select(x => GetLabel(x, richText)).ToArray()) + '>';
            }
            return type.Name;
        }
    }
}