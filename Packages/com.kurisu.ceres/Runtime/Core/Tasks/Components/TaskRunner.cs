using System.Collections.Generic;
using Ceres.Events;
using UnityEngine;
using R3;
using R3.Ceres;

namespace Ceres.Tasks
{
    internal class TaskRunner : MonoBehaviour
    {
        private class TaskCallbackEventHandler: CallbackEventHandler
        {
            public override IEventCoordinator Coordinator => EventSystem.Instance;

            public TaskCallbackEventHandler()
            {
                Parent = EventSystem.EventHandler;
            }
            
            public override void SendEvent(EventBase e, DispatchMode dispatchMode = DispatchMode.Default)
            {
                e.Target = this;
                EventSystem.Instance.Dispatch(e, dispatchMode, MonoDispatchType.Update);
            }
        }
        
        internal readonly List<TaskBase> Tasks = new();
        
        private readonly List<TaskBase> _tasksToAdd = new();
        
        private static TaskRunner _instance;
        
        private static TaskRunner GetInstance()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return null;
#endif
            if (_instance == null)
            {
                GameObject managerObject = new() { name = nameof(TaskRunner) };
                _instance = managerObject.AddComponent<TaskRunner>();
            }
            return _instance;
        }

        private TaskCallbackEventHandler _eventHandler;
        
        public static void RegisterTask(TaskBase task)
        {
            var instance = GetInstance();
            if (instance)
            {
                if (instance.Tasks.Contains(task))
                {
                    Debug.LogWarning($"[TaskRunner] Task {task.InternalGetTaskName()} has already been registered!");
                    return;
                }
                task.Acquire();
                task.Parent = instance.GetEventHandler();
                instance._tasksToAdd.Add(task);
            }
        }
        
        private void Awake()
        {
            GetEventHandler().AsObservable<TaskCompleteEvent>()
                             .SubscribeSafe(OnTaskComplete)
                             .RegisterTo(destroyCancellationToken);
        }
        
        private void Update()
        {
            UpdateAllTasks();
        }

        public CallbackEventHandler GetEventHandler()
        {
            return _eventHandler ??= new TaskCallbackEventHandler();
        }
        
        private void UpdateAllTasks()
        {
            if (_tasksToAdd.Count > 0)
            {
                Tasks.AddRange(_tasksToAdd);
                _tasksToAdd.Clear();
            }

            foreach (var task in Tasks)
            {
                if (task.GetStatus() == TaskStatus.Running)
                {
                    task.Tick();
                }
            }

            for (int i = Tasks.Count - 1; i >= 0; i--)
            {
                var status = Tasks[i].GetStatus();
                if (status is TaskStatus.Completed or TaskStatus.Stopped)
                {
                    if (status == TaskStatus.Completed)
                    {
                        Tasks[i].PostComplete();
                    }
                    Tasks[i].Dispose();
                    Tasks.RemoveAt(i);
                }
            }
        }
        
        private static void OnTaskComplete(TaskCompleteEvent evt)
        {
            foreach (var task in evt.Listeners)
            {
                if (task.ReleasePrerequisite(evt) && !task.HasPrerequisite())
                {
                    task.Run();
                }
            }
        }
    }
}
