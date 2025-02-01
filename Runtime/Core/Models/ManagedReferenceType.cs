using System;
using System.Reflection;

namespace Ceres
{
    /// <summary>
    /// Serialized type in managed reference format
    /// </summary>
    [Serializable]
    public struct ManagedReferenceType: IEquatable<ManagedReferenceType>
    {
        // ReSharper disable once InconsistentNaming
        public string _class;

        // ReSharper disable once InconsistentNaming
        public string _ns;
            
        // ReSharper disable once InconsistentNaming
        public string _asm;
            
        public ManagedReferenceType(string inClass, string inNamespace, string inAssembly)
        {
            _class = inClass;
            _ns = inNamespace;
            _asm = inAssembly;
        }
            
        public ManagedReferenceType(Type type)
        {
            _class = type.Name;
            _ns = type.Namespace;
            _asm = type.Assembly.GetName().Name;
        }
            
        public readonly Type ToType()
        {
            return Type.GetType(Assembly.CreateQualifiedName(_asm, $"{_ns}.{_class}"));
        }

        public bool Equals(ManagedReferenceType other)
        {
            return _class == other._class && _ns == other._ns && _asm == other._asm;
        }

        public readonly override string ToString()
        {
            return $"class: {_class} ns: {_ns} asm: {_asm}";
        }
    }
}