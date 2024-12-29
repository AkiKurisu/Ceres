using System;
using System.Collections.Generic;
using System.Reflection;
using Ceres.Annotations;
using UnityEngine.UIElements;
namespace Ceres.Editor.Graph
{
    [Ordered]
    public class LanguageCodeResolver : FieldResolver<DropdownField, string>
    {
        private static readonly List<string> LanguageCode = new() {
            "af", "ga", "sq", "it", "ar", "ja", "az", "kn", "eu", "ko",
            "bn", "la", "be", "lv", "bg", "lt", "ca", "mk", "zh", "ms",
            "mt", "hr", "no", "cs", "fa", "da", "pl", "nl", "pt",
            "en", "ro", "eo", "ru", "et", "sr", "tl", "sk", "fi", "sl",
            "fr", "es", "gl", "sw", "ka", "sv", "de", "ta", "el", "te",
            "gu", "th", "ht", "tr", "iw", "uk", "hi", "ur", "hu", "vi",
            "is", "cy", "id", "yi"
        };
        
        public LanguageCodeResolver(FieldInfo fieldInfo) : base(fieldInfo) { }
        
        protected override DropdownField CreateEditorField(FieldInfo fieldInfo)
        {
            DropdownField field = new(fieldInfo.Name)
            {
                choices = LanguageCode
            };
            return field;
        }
        
        public override bool IsAcceptable(Type fieldValueType, FieldInfo info)
        {
            return fieldValueType == typeof(string) && info.GetCustomAttribute<LanguageCodeAttribute>() != null;
        }
    }
}