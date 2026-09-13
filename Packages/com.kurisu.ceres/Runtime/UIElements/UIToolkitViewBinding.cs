using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Ceres.UIElements
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class UIToolkitViewAttribute : Attribute
    {
        public UIToolkitViewAttribute(string prefix = "") => Prefix = prefix ?? string.Empty;

        public string Prefix { get; }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public sealed class UIToolkitQueryAttribute : Attribute
    {
        public UIToolkitQueryAttribute(string name = null) => Name = name;

        public string Name { get; }
    }

    public readonly struct UIToolkitElementDescriptor
    {
        public UIToolkitElementDescriptor(string fieldName, string elementName, Type elementType)
        {
            FieldName = fieldName;
            ElementName = elementName;
            ElementType = elementType;
        }

        public string FieldName { get; }
        public string ElementName { get; }
        public Type ElementType { get; }
    }

    public static class UIToolkitQuery
    {
        public static T Require<T>(VisualElement root, string name, Type viewType, string fieldName) where T : VisualElement
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            VisualElement element = root.Q(name);
            if (element == null)
                throw new InvalidOperationException($"{viewType.FullName}.{fieldName} requires UI Toolkit element '{name}'.");
            if (element is T typed) return typed;
            throw new InvalidOperationException(
                $"{viewType.FullName}.{fieldName} requires '{name}' to be {typeof(T).FullName}, but found {element.GetType().FullName}.");
        }
    }

    public sealed class UIToolkitBindingScope : IDisposable
    {
        private readonly List<Action> _unsubscribe = new();
        private bool _disposed;

        public void Click(Button button, Action action)
        {
            if (button == null) throw new ArgumentNullException(nameof(button));
            if (action == null) throw new ArgumentNullException(nameof(action));
            ThrowIfDisposed();
            button.clicked += action;
            _unsubscribe.Add(() => button.clicked -= action);
        }

        public void ValueChanged<TValue, TElement>(TElement element, EventCallback<ChangeEvent<TValue>> callback)
            where TElement : VisualElement, INotifyValueChanged<TValue>
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            ThrowIfDisposed();
            element.RegisterValueChangedCallback(callback);
            _unsubscribe.Add(() => element.UnregisterValueChangedCallback(callback));
        }

        public void Callback<TEvent>(VisualElement element, EventCallback<TEvent> callback,
            TrickleDown trickleDown = TrickleDown.NoTrickleDown) where TEvent : EventBase<TEvent>, new()
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            ThrowIfDisposed();
            element.RegisterCallback(callback, trickleDown);
            _unsubscribe.Add(() => element.UnregisterCallback(callback, trickleDown));
        }

        public void Register(Action unsubscribe)
        {
            if (unsubscribe == null) throw new ArgumentNullException(nameof(unsubscribe));
            ThrowIfDisposed();
            _unsubscribe.Add(unsubscribe);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            for (int i = _unsubscribe.Count - 1; i >= 0; i--) _unsubscribe[i]();
            _unsubscribe.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(UIToolkitBindingScope));
        }
    }
}
