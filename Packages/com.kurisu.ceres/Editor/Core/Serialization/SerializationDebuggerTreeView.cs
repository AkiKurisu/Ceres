using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UObject = UnityEngine.Object;
#if UNITY_6000_0_OR_NEWER
using TreeView = UnityEditor.IMGUI.Controls.TreeView<int>;
using TreeViewItem = UnityEditor.IMGUI.Controls.TreeViewItem<int>;
using TreeViewState = UnityEditor.IMGUI.Controls.TreeViewState<int>;
#endif

namespace Ceres.Serialization.Editor
{
    public class SerializationDebuggerTreeView : TreeView
    {
        public class ViewItem : TreeViewItem
        {
            public ulong Handle { get; set; }
            public string Type { get; set; }
            public UObject Object { get; set; }
            public ViewItem(int id) : base(id)
            {

            }
        }

        private const string SortedColumnIndexStateKey = "SerializationDebuggerTreeView_sortedColumnIndex";

        public IReadOnlyList<TreeViewItem> CurrentBindingItems;

        public SerializationDebuggerTreeView()
            : this(new TreeViewState(), new MultiColumnHeader(new MultiColumnHeaderState(new[]
            {
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Handle"), width = 20},
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Type"), width = 20},
                new MultiColumnHeaderState.Column() { headerContent = new GUIContent("Object")}
            })))
        {
        }

        SerializationDebuggerTreeView(TreeViewState state, MultiColumnHeader header)
            : base(state, header)
        {
            rowHeight = 20;
            showAlternatingRowBackgrounds = true;
            showBorder = true;
            header.sortingChanged += Header_sortingChanged;

            header.ResizeToFit();
            Reload();

            header.sortedColumnIndex = SessionState.GetInt(SortedColumnIndexStateKey, 1);
        }

        public void ReloadAndSort()
        {
            var currentSelected = state.selectedIDs;
            Reload();
            Header_sortingChanged(multiColumnHeader);
            state.selectedIDs = currentSelected;
        }

        private void Header_sortingChanged(MultiColumnHeader multiColumnHeader)
        {
            SessionState.SetInt(SortedColumnIndexStateKey, multiColumnHeader.sortedColumnIndex);
            var index = multiColumnHeader.sortedColumnIndex;
            var ascending = multiColumnHeader.IsSortedAscending(multiColumnHeader.sortedColumnIndex);

            var items = rootItem.children.Cast<ViewItem>();
            IOrderedEnumerable<ViewItem> orderedEnumerable = index switch
            {
                0 => ascending ? items.OrderBy(item => item.Handle) : items.OrderByDescending(item => item.Handle),
                1 => ascending ? items.OrderBy(item => item.Type) : items.OrderByDescending(item => item.Type),
                2 => ascending ? items.OrderBy(item => item.Object) : items.OrderByDescending(item => item.Object),
                _ => throw new ArgumentOutOfRangeException(nameof(index), index, null),
            };
            CurrentBindingItems = rootItem.children = orderedEnumerable.Cast<TreeViewItem>().ToList();
            BuildRows(rootItem);
        }

        protected override TreeViewItem BuildRoot()
        {
            var root = new TreeViewItem { depth = -1 };

            var children = new List<TreeViewItem>();
            
            GlobalObjectManager.ForEach(structure =>
            {
                var uObject = structure.Object as UObject;
                children.Add(new ViewItem(structure.Handle.GetIndex())
                {
                    Handle = structure.Handle.Handle,
                    Type = (bool)uObject ? GetObjectTypeName(structure.Object.GetType()) : "Null",
                    Object = uObject
                });
            });

            CurrentBindingItems = children;
            root.children = CurrentBindingItems as List<TreeViewItem>;
            return root;
        }
        private static string GetObjectTypeName(Type type)
        {
            if (type.Name.Contains("_SerializedObjectWrapper_")) return "SerializedObjectWrapper";
            return type.Name;
        }

        protected override bool CanMultiSelect(TreeViewItem item)
        {
            return false;
        }

        protected override void RowGUI(RowGUIArgs args)
        {
            var item = (ViewItem)args.item;

            for (var visibleColumnIndex = 0; visibleColumnIndex < args.GetNumVisibleColumns(); visibleColumnIndex++)
            {
                var rect = args.GetCellRect(visibleColumnIndex);
                var columnIndex = args.GetColumn(visibleColumnIndex);

                var labelStyle = args.selected ? EditorStyles.whiteLabel : EditorStyles.label;
                labelStyle.alignment = TextAnchor.MiddleLeft;
                switch (columnIndex)
                {
                    case 0:
                        EditorGUI.LabelField(rect, item.Handle.ToString(), labelStyle);
                        break;
                    case 1:
                        EditorGUI.LabelField(rect, item.Type, labelStyle);
                        break;
                    case 2:
                        using (new EditorGUI.DisabledGroupScope(true))
                        {
                            EditorGUI.ObjectField(rect, item.Object, typeof(UObject), false);
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(columnIndex), columnIndex, null);
                }
            }
        }
    }

}
