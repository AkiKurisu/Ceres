using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using Toolbar = UnityEditor.UIElements.Toolbar;
using Object = UnityEngine.Object;
namespace Ceres.Events.Editor
{
    internal interface ICoordinatorChoice
    {
        MonoEventCoordinator Coordinator { get; }
    }

    internal class CoordinatorChoice : ICoordinatorChoice
    {
        public MonoEventCoordinator Coordinator { get; }

        public CoordinatorChoice(MonoEventCoordinator p)
        {
            Coordinator = p;
        }

        public override string ToString()
        {
            return Coordinator.gameObject.name;
        }
    }

    [Serializable]
    internal class CoordinatorDebugger : ICoordinatorDebugger
    {
        [SerializeField]
        private string m_LastVisualTreeName;

        protected EditorWindow m_DebuggerWindow;
        private ICoordinatorChoice m_SelectedCoordinator;
        protected VisualElement m_Toolbar;
        protected ToolbarMenu m_CoordinatorSelect;
        private List<ICoordinatorChoice> m_CoordinatorChoices;
        private IVisualElementScheduledItem m_ConnectWindowScheduledItem;
        private IVisualElementScheduledItem m_RestoreSelectionScheduledItem;
        public MonoEventCoordinator CoordinatorDebug { get; set; }
        public void Initialize(EditorWindow debuggerWindow)
        {
            m_DebuggerWindow = debuggerWindow;

            m_Toolbar ??= new Toolbar();

            // Register panel choice refresh on the toolbar so the event
            // is received before the ToolbarPopup clickable handle it.
            m_Toolbar.RegisterCallback<MouseDownEvent>((e) =>
            {
                if (e.target == m_CoordinatorSelect)
                    RefreshCoordinatorChoices();
            }, UnityEngine.UIElements.TrickleDown.TrickleDown);

            m_CoordinatorChoices = new List<ICoordinatorChoice>();
            m_CoordinatorSelect = new ToolbarMenu
            {
                name = "coordinatorSelectPopup",
                variant = ToolbarMenu.Variant.Popup,
                text = "Select a coordinator"
            };

            m_Toolbar.Insert(0, m_CoordinatorSelect);

            if (!string.IsNullOrEmpty(m_LastVisualTreeName))
                m_RestoreSelectionScheduledItem = m_Toolbar.schedule.Execute(RestoreCoordinatorSelection).Every(500);
        }

        public void OnDisable()
        {
            var lastTreeName = m_LastVisualTreeName;
            SelectCoordinatorChoice(null);
            if (CoordinatorDebug) CoordinatorDebug.DetachDebugger(this);
            m_LastVisualTreeName = lastTreeName;
        }

        public void Disconnect()
        {
            var lastTreeName = m_LastVisualTreeName;
            m_SelectedCoordinator = null;
            SelectCoordinatorChoice(null);

            m_LastVisualTreeName = lastTreeName;
        }

        public void ScheduleWindowToDebug(EditorWindow window)
        {
            if (window != null)
            {
                Disconnect();
                m_ConnectWindowScheduledItem = m_Toolbar.schedule.Execute(TrySelectWindow).Every(500);
            }
        }

        private void TrySelectWindow()
        {
            MonoEventCoordinator monoEventCoordinator = Object.FindAnyObjectByType<MonoEventCoordinator>();
            SelectCoordinatorToDebug(monoEventCoordinator);

            if (m_SelectedCoordinator != null)
            {
                m_ConnectWindowScheduledItem.Pause();
            }
        }

        public virtual void Refresh()
        { }

        protected virtual bool ValidateDebuggerConnection(IEventCoordinator connection)
        {
            return true;
        }

        protected virtual void OnSelectCoordinateDebug(IEventCoordinator pdbg) { }
        protected virtual void OnRestoreCoordinatorSelection() { }

        protected virtual void PopulateCoordinatorChoices(List<ICoordinatorChoice> coordinatorChoices)
        {
            MonoEventCoordinator[] monoEventCoordinators = Object.FindObjectsByType<MonoEventCoordinator>(FindObjectsSortMode.InstanceID);
            coordinatorChoices.AddRange(monoEventCoordinators.Select(x => new CoordinatorChoice(x)));
        }

        private void RefreshCoordinatorChoices()
        {
            m_CoordinatorChoices.Clear();
            PopulateCoordinatorChoices(m_CoordinatorChoices);

            var menu = m_CoordinatorSelect.menu;
            menu.ClearItems();

            foreach (var coordinatorChoice in m_CoordinatorChoices)
            {
                menu.AppendAction(coordinatorChoice.ToString(), OnSelectCoordinator, DropdownMenuAction.AlwaysEnabled, coordinatorChoice);
            }
        }

        private void OnSelectCoordinator(DropdownMenuAction action)
        {
            if (m_RestoreSelectionScheduledItem != null && m_RestoreSelectionScheduledItem.isActive)
                m_RestoreSelectionScheduledItem.Pause();

            SelectCoordinatorChoice(action.userData as ICoordinatorChoice);
        }

        private void RestoreCoordinatorSelection()
        {
            RefreshCoordinatorChoices();
            if (m_CoordinatorChoices.Count > 0)
            {
                if (!string.IsNullOrEmpty(m_LastVisualTreeName))
                {
                    // Try to retrieve last selected VisualTree
                    for (int i = 0; i < m_CoordinatorChoices.Count; i++)
                    {
                        var vt = m_CoordinatorChoices[i];
                        if (vt.ToString() == m_LastVisualTreeName)
                        {
                            SelectCoordinatorChoice(vt);
                            break;
                        }
                    }
                }

                if (m_SelectedCoordinator != null)
                    OnRestoreCoordinatorSelection();
                else
                    SelectCoordinatorChoice(null);

                m_RestoreSelectionScheduledItem.Pause();
            }
        }

        protected virtual void SelectCoordinatorChoice(ICoordinatorChoice cc)
        {
            // Detach debugger from current panel
            if (CoordinatorDebug != null)
                CoordinatorDebug.DetachDebugger(this);
            string menuText;

            if (cc != null && ValidateDebuggerConnection(cc.Coordinator))
            {
                cc.Coordinator.AttachDebugger(this);
                m_SelectedCoordinator = cc;
                m_LastVisualTreeName = cc.ToString();

                OnSelectCoordinateDebug(CoordinatorDebug);
                menuText = cc.ToString();
            }
            else
            {
                // No tree selected
                m_SelectedCoordinator = null;
                m_LastVisualTreeName = null;

                OnSelectCoordinateDebug(null);
                menuText = "Select a coordinator";
            }

            m_CoordinatorSelect.text = menuText;
        }

        protected void SelectCoordinatorToDebug(MonoEventCoordinator coordinator)
        {
            // Select new tree
            if (m_SelectedCoordinator?.Coordinator != coordinator)
            {
                SelectCoordinatorChoice(null);
                RefreshCoordinatorChoices();
                for (int i = 0; i < m_CoordinatorChoices.Count; i++)
                {
                    var pc = m_CoordinatorChoices[i];
                    if (pc.Coordinator == coordinator)
                    {
                        SelectCoordinatorChoice(pc);
                        break;
                    }
                }
            }
        }

        public virtual bool InterceptEvent(EventBase ev)
        {
            return false;
        }

        public virtual void PostProcessEvent(EventBase ev)
        { }
    }
}
