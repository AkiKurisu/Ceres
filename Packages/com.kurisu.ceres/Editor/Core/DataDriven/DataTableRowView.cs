using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using System.Collections.ObjectModel;
using Ceres.Serialization;
using Ceres.Serialization.Editor;

namespace Ceres.DataDriven.Editor
{
    /// <summary>
    /// GUI class for drawing DataTable rows
    /// </summary>
    public class DataTableRowView
    {
        private const int ColumSpace = 5;
        
        private ReorderableList _reorderableList;
        
        public DataTable Table { get; }
        
        private SerializedObject _serializedObject;
        
        private int _selectIndex;
        
        public bool ReadOnly { get; set; }
                
        private static readonly int[] DefaultIndices = Array.Empty<int>();
        
        public DataTableRowView(DataTable dataTable)
        {
            Table = dataTable;
        }
        
        public void DrawGUI(SerializedObject serializedObject)
        {
            _serializedObject = serializedObject;
            var rowsProp = serializedObject.FindProperty("m_rows");
            bool canEdit = true;
            EditorGUI.BeginChangeCheck();
            var rowStructType = Table.GetRowStructType();
            if (rowStructType != null)
            {
                var header = ReflectionUtility.GetSerializedFieldsName(rowStructType);
                header.Insert(0, "Row Id");
                var rows = Table.GetAllRows();
                _reorderableList ??= new ReorderableList(rows, rowStructType, true, true, true, true);
                if (_selectIndex >= 0)
                {
                    SelectRow(_selectIndex);
                    _selectIndex = -1;
                }
                _reorderableList.multiSelect = true;
                _reorderableList.elementHeightCallback = index =>
                {
                    var prop = rowsProp.GetArrayElementAtIndex(index);
                    if (ReadOnly)
                    {
                        return EditorGUIUtility.singleLineHeight;
                    }
                    return GetDataTableRowHeight(rowStructType, prop);
                };
                _reorderableList.drawHeaderCallback = rect =>
                {
                    DrawDataTableHeader(rect, header);
                };
                _reorderableList.drawElementCallback = (rect, index, selected, focused) =>
                {
                    var prop = rowsProp.GetArrayElementAtIndex(index);
                    if (ReadOnly)
                    {
                        DrawReadOnlyDataTableRow(rect, header.Count, rowStructType, prop);
                    }
                    else
                    {
                        DrawDataTableRow(rect, header.Count, rowStructType, prop);
                    }
                };
                _reorderableList.onAddCallback = list =>
                {
                    Table.AddRow(Table.NewRowId(), (IDataTableRow)Activator.CreateInstance(rowStructType));
                    canEdit = false;
                    RequestDataTableUpdate();
                    GUIUtility.ExitGUI();
                };
                _reorderableList.onRemoveCallback = list =>
                {
                    if (rows.Length == 0) return;

                    var indexsToRemove = list.selectedIndices.ToList();
                    if (indexsToRemove.Count == 0)
                    {
                        indexsToRemove.Add(rows.Length - 1);
                    }
                    Table.RemoveRow(indexsToRemove);
                    canEdit = false;
                    RequestDataTableUpdate();
                    GUIUtility.ExitGUI();
                };
                _reorderableList.onReorderCallbackWithDetails = (list, oldIndex, newIndex) =>
                {
                    Table.ReorderRow(oldIndex, newIndex);
                    canEdit = false;
                    RequestDataTableUpdate();
                };
                _reorderableList.DoLayoutList();
            }

            Event evt = Event.current;
            if (evt.type == EventType.ContextClick && GUILayoutUtility.GetLastRect().Contains(evt.mousePosition))
            {
                GenericMenu menu = new();
                menu.AddItem(new GUIContent("Clear"), false, () =>
                {
                    Table.RemoveAllRows();
                    canEdit = false;
                    RequestDataTableUpdate();
                });
                menu.ShowAsContext();
            }


            if (EditorGUI.EndChangeCheck() && canEdit)
            {
                serializedObject.ApplyModifiedProperties();
                RequestDataTableUpdate();
            }
        }
        
        /// <summary>
        /// Get row view selected indices
        /// </summary>
        /// <returns></returns>
        public ReadOnlyCollection<int> GetSelectedIndices()
        {
            if (_reorderableList == null) return new ReadOnlyCollection<int>(DefaultIndices);
            return _reorderableList.selectedIndices;
        }
        
        /// <summary>
        /// Get row view selected rows
        /// </summary>
        /// <returns></returns>
        public IDataTableRow[] GetSelectedRows()
        {
            if (_reorderableList == null) return Array.Empty<IDataTableRow>();
            var rows = Table.GetAllRows();
            return _reorderableList.selectedIndices.Select(x => rows[x]).ToArray();
        }
        
        /// <summary>
        /// Get row view selected rows in serialized property format
        /// </summary>
        /// <returns></returns>
        public SerializedProperty[] GetSelectedRowProperties(SerializedObject serializedObject)
        {
            var rowsProp = serializedObject.FindProperty("m_rows");
            return _reorderableList.selectedIndices.Select(x => rowsProp.GetArrayElementAtIndex(x)).ToArray();
        }

        private void RequestDataTableUpdate()
        {
            DataTableEditorUtils.OnDataTablePreUpdate?.Invoke(Table);
            Rebuild();
            DataTableEditorUtils.OnDataTablePostUpdate?.Invoke(Table);
        }
        
        /// <summary>
        /// Select target row in view
        /// </summary>
        /// <param name="index"></param>
        public void SelectRow(int index)
        {
            if (_reorderableList == null)
            {
                // Cache pre-select index when list view is not prepared
                _selectIndex = index;
                return;
            }
            if (_selectIndex < _reorderableList.count)
                _reorderableList.Select(index);
        }
        
        /// <summary>
        /// Rebuild rows view
        /// </summary>
        public void Rebuild()
        {
            // ensure row type is matched with row struct before drawing
            Table.InternalUpdate();
            // rebuild reorderable list next editor frame
            _reorderableList = null;
            _serializedObject?.Update();
        }

        private void DrawDataTableRow(Rect rect, int columNum, Type elementType, SerializedProperty property)
        {
            rect.width /= columNum;
            rect.width -= ColumSpace;
            var height = rect.height;
            rect.height = EditorGUIUtility.singleLineHeight;
            var rowIdProp = property.FindPropertyRelative("RowId");
            string rowId = rowIdProp.stringValue;
            EditorGUI.PropertyField(rect, rowIdProp, GUIContent.none);
            if (rowId != rowIdProp.stringValue)
            {
                DataTableEditorUtils.ValidateNewRowId(Table, rowId, rowIdProp.stringValue, out string validId);
                rowIdProp.stringValue = validId;
            }
            rect.x += rect.width + ColumSpace;
            rect.height = height;

            property = property.FindPropertyRelative("RowData");
            var jsonProp = property.FindPropertyRelative("jsonData");
            var objectHandleProp = property.FindPropertyRelative("objectHandle");
            var handle = new SoftObjectHandle(objectHandleProp.ulongValue);
            SerializedObjectWrapper wrapper = SerializedObjectWrapperManager.CreateWrapper(elementType, ref handle);
            if (objectHandleProp.ulongValue != handle.Handle)
            {
                objectHandleProp.ulongValue = handle.Handle;
                property.serializedObject.ApplyModifiedProperties();
            }
            if (!wrapper) return;

            if (!string.IsNullOrEmpty(jsonProp.stringValue))
            {
                JsonUtility.FromJsonOverwrite(jsonProp.stringValue, wrapper.Value);
            }
            EditorGUI.BeginChangeCheck();
            SerializedObjectWrapperDrawer.DrawGUIHorizontal(rect, ColumSpace, wrapper);
            if (EditorGUI.EndChangeCheck())
            {
                jsonProp.stringValue = JsonUtility.ToJson(wrapper.Value);
            }
        }
        
        private void DrawReadOnlyDataTableRow(Rect rect, int columNum, Type elementType, SerializedProperty property)
        {
            rect.width /= columNum;
            rect.width -= ColumSpace;
            var height = rect.height;
            rect.height = EditorGUIUtility.singleLineHeight;
            var rowIdProp = property.FindPropertyRelative("RowId");
            string rowId = rowIdProp.stringValue;
            EditorGUI.PropertyField(rect, rowIdProp, GUIContent.none);
            if (rowId != rowIdProp.stringValue)
            {
                DataTableEditorUtils.ValidateNewRowId(Table, rowId, rowIdProp.stringValue, out string validId);
                rowIdProp.stringValue = validId;
            }
            rect.x += rect.width + ColumSpace;
            rect.height = height;

            property = property.FindPropertyRelative("RowData");
            var jsonProp = property.FindPropertyRelative("jsonData");
            var objectHandleProp = property.FindPropertyRelative("objectHandle");
            var handle = new SoftObjectHandle(objectHandleProp.ulongValue);
            SerializedObjectWrapper wrapper = SerializedObjectWrapperManager.CreateWrapper(elementType, ref handle);
            if (objectHandleProp.ulongValue != handle.Handle)
            {
                objectHandleProp.ulongValue = handle.Handle;
                property.serializedObject.ApplyModifiedProperties();
            }
            if (!wrapper) return;

            if (!string.IsNullOrEmpty(jsonProp.stringValue))
            {
                JsonUtility.FromJsonOverwrite(jsonProp.stringValue, wrapper.Value);
            }
            SerializedObjectWrapperDrawer.DrawReadOnlyGUIHorizontal(rect, ColumSpace, wrapper);
        }
        
        private void DrawDataTableHeader(Rect rect, List<string> header)
        {
            rect.width /= header.Count;
            rect.width -= ColumSpace;
            for (int i = 0; i < header.Count; ++i)
            {
                GUI.Label(rect, header[i]);
                rect.x += rect.width + ColumSpace;
            }
        }
        
        private float GetDataTableRowHeight(Type elementType, SerializedProperty property)
        {
            property = property.FindPropertyRelative("RowData");
            var objectHandleProp = property.FindPropertyRelative("objectHandle");
            var handle = new SoftObjectHandle(objectHandleProp.ulongValue);
            ScriptableObject wrapper = SerializedObjectWrapperManager.CreateWrapper(elementType, ref handle);
            if (objectHandleProp.ulongValue != handle.Handle)
            {
                objectHandleProp.ulongValue = handle.Handle;
                property.serializedObject.ApplyModifiedProperties();
            }
            return SerializedObjectWrapperDrawer.CalculatePropertyHeightLayout(wrapper);
        }
    }
}