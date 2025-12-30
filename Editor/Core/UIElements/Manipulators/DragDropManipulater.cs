using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UObject = UnityEngine.Object;

namespace Ceres.Editor.Graph
{
    public class DragDropManipulator : PointerManipulator
    {
        private UObject _droppedObject;
        
        private readonly Action<UObject, Vector2> _dragObjectPerformEvent;
        
        private readonly Action<List<ISelectable>, GraphElement, Vector2> _dragElementPerformEvent;
        
        public DragDropManipulator(GraphView root, Action<UObject, Vector2> onDragObjectPerformEvent, 
            Action<List<ISelectable>, GraphElement, Vector2> dragElementPerformEvent)
        {
            target = root;
            _dragObjectPerformEvent = onDragObjectPerformEvent;
            _dragElementPerformEvent = dragElementPerformEvent;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            // Register a callback when the user presses the pointer down.
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            // Register callbacks for various stages in the drag process.
            target.RegisterCallback<DragLeaveEvent>(OnDragLeave);
            target.RegisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.RegisterCallback<DragPerformEvent>(OnDragPerform);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            // Unregister all callbacks that you registered in RegisterCallbacksOnTarget().
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<DragLeaveEvent>(OnDragLeave);
            target.UnregisterCallback<DragUpdatedEvent>(OnDragUpdate);
            target.UnregisterCallback<DragPerformEvent>(OnDragPerform);
        }

        // This method runs when a user presses a pointer down on the drop area.
        private void OnPointerDown(PointerDownEvent _)
        {
            // Only do something if the window currently has a reference to an asset object.
            if (!_droppedObject) return;
            // Clear existing data in DragAndDrop class.
            DragAndDrop.PrepareStartDrag();

            // Store reference to object and path to object in DragAndDrop static fields.
            DragAndDrop.objectReferences = new[] { _droppedObject };
            // Start a drag.
            DragAndDrop.StartDrag(string.Empty);
        }



        // This method runs if a user makes the pointer leave the bounds of the target while a drag is in progress.
        private void OnDragLeave(DragLeaveEvent _)
        {
            _droppedObject = null;
        }

        // This method runs every frame while a drag is in progress.
        private static void OnDragUpdate(DragUpdatedEvent _)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Generic;
        }

        // This method runs when a user drops a dragged object onto the target.
        private void OnDragPerform(DragPerformEvent evt)
        {
            // Set droppedObject and draggedName fields to refer to dragged object.
            if (DragAndDrop.GetGenericData("DragSelection") is List<ISelectable> selectables)
            {
                _dragElementPerformEvent?.Invoke(selectables, evt.target as GraphElement, evt.mousePosition);
                return;
            }
            if (DragAndDrop.objectReferences.Length == 0) return;
            _droppedObject = DragAndDrop.objectReferences[0];
            _dragObjectPerformEvent?.Invoke(_droppedObject, evt.mousePosition);
            _droppedObject = null;
        }

    }
}
