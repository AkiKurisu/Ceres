using System;
using UnityEngine.UIElements;
using System.Collections.Generic;
namespace Ceres.Editor.Graph
{
    public class ListField<T> : BaseField<List<T>>
    {
        private readonly ListView _listView;
        
        protected readonly Func<VisualElement> ElementCreator;

        private readonly Func<object> _valueCreator;

        private const int DefaultListItemHeight = 20;

        private ListField(string label, Func<VisualElement> elementCreator, Func<object> valueCreator, VisualElement listContainer) : base(label, listContainer)
        {
            value ??= new List<T>();
            ElementCreator = elementCreator;
            _valueCreator = valueCreator;
            listContainer.Add(_listView = CreateListView());
        }
        
        public ListField(string label, Func<VisualElement> elementCreator, Func<object> valueCreator) : this(label, elementCreator, valueCreator, new VisualElement())
        {
            
        }

        private ListView CreateListView()
        {
            var view = new ListView(value, GetListItemHeight(), OnMakeListItem, OnBindListItem)
            {
                selectionType = SelectionType.Multiple,
                reorderable = true,
                showAddRemoveFooter = true
            };
            view.Q<Button>("unity-list-view__add-button").clickable = new Clickable(OnRequestAddListItem);
            return view;
        }

        protected virtual int GetListItemHeight()
        {
            return DefaultListItemHeight;
        }
        
        protected virtual void OnBindListItem(VisualElement e, int i)
        {
            ((BaseField<T>)e).value = value[i];
            ((BaseField<T>)e).RegisterValueChangedCallback((x) => value[i] = x.newValue);
        }

        protected virtual VisualElement OnMakeListItem()
        {
            var field = ElementCreator.Invoke();
            if (field is BaseField<T> baseField) baseField.label = string.Empty;
            return field;
        }
        
        protected virtual void OnRequestAddListItem()
        {
            value.Add((T)_valueCreator.Invoke());
            _listView.RefreshItems();
        }
        
        public sealed override List<T> value
        {
            get => base.value; 
            set
            {
                base.value = value != null ? new List<T>(value) : new List<T>();
                UpdateValue();
            }
        }
        
        private void UpdateValue()
        {
            if (_listView == null) return;
            _listView.itemsSource = value; _listView.RefreshItems();
        }
    }
}