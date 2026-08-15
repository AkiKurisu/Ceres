using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;
namespace Ceres.Events
{
    internal readonly struct EventDebuggerLogCall : IDisposable
    {
#if UNITY_EDITOR
        private readonly Delegate m_Callback;
        private readonly EventBase m_Event;
        private readonly long m_Start;
        private readonly bool m_IsPropagationStopped;
        private readonly bool m_IsImmediatePropagationStopped;
        private readonly bool m_IsDefaultPrevented;
#endif
        public EventDebuggerLogCall(Delegate callback, EventBase evt)
        {
#if UNITY_EDITOR
            m_Callback = callback;
            m_Event = evt;

            m_Start = (long)(Time.realtimeSinceStartup * 1000.0f);
            m_IsPropagationStopped = evt.IsPropagationStopped;
            m_IsImmediatePropagationStopped = evt.IsImmediatePropagationStopped;
            m_IsDefaultPrevented = evt.IsDefaultPrevented;
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (m_Event != null && m_Event.Log)
            {
                m_Event.EventLogger.LogCall(GetCallbackHashCode(), GetCallbackName(), m_Event,
                    m_IsPropagationStopped != m_Event.IsPropagationStopped,
                    m_IsImmediatePropagationStopped != m_Event.IsImmediatePropagationStopped,
                    m_IsDefaultPrevented != m_Event.IsDefaultPrevented,
                    (long)(Time.realtimeSinceStartup * 1000.0f) - m_Start);
            }
#endif
        }

#if UNITY_EDITOR
        private string GetCallbackName()
        {
            if (m_Callback == null)
            {
                return "No callback";
            }

            if (m_Callback.Target != null)
            {
                return m_Callback.Target.GetType().FullName + "." + m_Callback.Method.Name;
            }

            if (m_Callback.Method.DeclaringType != null)
            {
                return m_Callback.Method.DeclaringType.FullName + "." + m_Callback.Method.Name;
            }

            return m_Callback.Method.Name;
        }

        private int GetCallbackHashCode()
        {
            return m_Callback?.GetHashCode() ?? 0;
        }

#endif
    }


    internal readonly struct EventDebuggerLogExecuteDefaultAction : IDisposable
    {
#if UNITY_EDITOR
        private readonly EventBase m_Event;
        private readonly long m_Start;
#endif
        public EventDebuggerLogExecuteDefaultAction(EventBase evt)
        {
#if UNITY_EDITOR
            m_Event = evt;
            m_Start = (long)(Time.realtimeSinceStartup * 1000.0f);
#endif
        }

        public void Dispose()
        {
#if UNITY_EDITOR
            if (m_Event != null && m_Event.Log)
            {
                m_Event.EventLogger.LogExecuteDefaultAction(m_Event, m_Event.PropagationPhase,
                    (long)(Time.realtimeSinceStartup * 1000.0f) - m_Start);
            }
#endif
        }
    }

    internal class EventDebugger
    {
        public IEventCoordinator Coordinator
        {
#if UNITY_EDITOR
            get { return CoordinatorDebug; }
            set
            {
                /* Ignore in editor */
            }
#else
            get; set;
#endif
        }

#if UNITY_EDITOR
        private IEventCoordinator m_CoordinatorDebug;
        public IEventCoordinator CoordinatorDebug
        {
            get { return m_CoordinatorDebug; }
            set
            {
                m_CoordinatorDebug = value;
                if (m_CoordinatorDebug != null)
                {
                    if (!m_EventTypeProcessedCount.ContainsKey(Coordinator))
                        m_EventTypeProcessedCount.Add(Coordinator, new Dictionary<long, int>());
                }
            }
        }
#endif

        public bool IsReplaying { get; internal set; }
        public float PlaybackSpeed { get; set; } = 1.0f;
        public bool IsPlaybackPaused { get; set; }

        public void UpdateModificationCount()
        {
            if (Coordinator == null)
                return;

            if (!m_ModificationCount.TryGetValue(Coordinator, out var count))
            {
                count = 0;
            }

            count++;
            m_ModificationCount[Coordinator] = count;
        }

        public void BeginProcessEvent(EventBase evt)
        {
            AddBeginProcessEvent(evt);
            UpdateModificationCount();
        }

        public void EndProcessEvent(EventBase evt, long duration)
        {
            AddEndProcessEvent(evt, duration);
            UpdateModificationCount();
        }

        public void LogCall(int cbHashCode, string cbName, EventBase evt, bool propagationHasStopped, bool immediatePropagationHasStopped, bool defaultHasBeenPrevented, long duration)
        {
            AddCallObject(cbHashCode, cbName, evt, propagationHasStopped, immediatePropagationHasStopped, defaultHasBeenPrevented, duration);
            UpdateModificationCount();
        }

        public void LogExecuteDefaultAction(EventBase evt, PropagationPhase phase, long duration)
        {
            AddExecuteDefaultAction(evt, phase, duration);
            UpdateModificationCount();
        }
        public static void LogPropagationPaths(EventBase evt, PropagationPaths paths)
        {
#if UNITY_EDITOR
            if (evt.Log)
            {
                evt.EventLogger.LogPropagationPathsInternal(evt, paths);
            }
#endif
        }
        public void LogPropagationPathsInternal(EventBase evt, PropagationPaths paths)
        {
            var pathsCopy = paths == null ? new PropagationPaths() : new PropagationPaths(paths);
            AddPropagationPaths(evt, pathsCopy);
            UpdateModificationCount();
        }
        public void AddPropagationPaths(EventBase evt, PropagationPaths paths)
        {
            if (Suspended)
                return;

            if (m_Log)
            {
                var pathObject = new EventDebuggerPathTrace(Coordinator, evt, paths);

                if (!m_EventPathObjects.TryGetValue(Coordinator, out var list))
                {
                    list = new List<EventDebuggerPathTrace>();
                    m_EventPathObjects.Add(Coordinator, list);
                }

                list.Add(pathObject);
            }
        }
        public List<EventDebuggerCallTrace> GetCalls(IEventCoordinator coordinator, EventDebuggerEventRecord evt = null)
        {
            if (!m_EventCalledObjects.TryGetValue(coordinator, out var list))
            {
                return null;
            }

            if ((evt != null) && (list != null))
            {
                List<EventDebuggerCallTrace> filteredList = new();
                foreach (var callObject in list)
                {
                    if (callObject.EventBase.EventId == evt.EventId)
                    {
                        filteredList.Add(callObject);
                    }
                }

                list = filteredList;
            }

            return list;
        }
        public List<EventDebuggerPathTrace> GetPropagationPaths(IEventCoordinator coordinator, EventDebuggerEventRecord evt = null)
        {
            if (!m_EventPathObjects.TryGetValue(coordinator, out var list))
            {
                return null;
            }

            if ((evt != null) && (list != null))
            {
                List<EventDebuggerPathTrace> filteredList = new();
                foreach (var pathObject in list)
                {
                    if (pathObject.EventBase.EventId == evt.EventId)
                    {
                        filteredList.Add(pathObject);
                    }
                }

                list = filteredList;
            }

            return list;
        }
        public List<EventDebuggerDefaultActionTrace> GetDefaultActions(IEventCoordinator coordinator, EventDebuggerEventRecord evt = null)
        {
            if (!m_EventDefaultActionObjects.TryGetValue(coordinator, out var list))
            {
                return null;
            }

            if ((evt != null) && (list != null))
            {
                List<EventDebuggerDefaultActionTrace> filteredList = new();
                foreach (var defaultActionObject in list)
                {
                    if (defaultActionObject.EventBase.EventId == evt.EventId)
                    {
                        filteredList.Add(defaultActionObject);
                    }
                }

                list = filteredList;
            }

            return list;
        }


        public List<EventDebuggerTrace> GetBeginEndProcessedEvents(IEventCoordinator coordinator, EventDebuggerEventRecord evt = null)
        {
            if (!m_EventProcessedEvents.TryGetValue(coordinator, out var list))
            {
                return null;
            }

            if ((evt != null) && (list != null))
            {
                List<EventDebuggerTrace> filteredList = new();
                foreach (var defaultActionObject in list)
                {
                    if (defaultActionObject.EventBase.EventId == evt.EventId)
                    {
                        filteredList.Add(defaultActionObject);
                    }
                }

                list = filteredList;
            }

            return list;
        }

        public long GetModificationCount(IEventCoordinator coordinator)
        {
            if (coordinator == null)
                return -1;

            if (!m_ModificationCount.TryGetValue(coordinator, out var modificationCount))
            {
                modificationCount = -1;
            }

            return modificationCount;
        }

        public void ClearLogs()
        {
            UpdateModificationCount();

            if (Coordinator == null)
            {
                m_EventCalledObjects.Clear();
                m_EventDefaultActionObjects.Clear();
                m_EventProcessedEvents.Clear();
                m_StackOfProcessedEvent.Clear();
                m_EventTypeProcessedCount.Clear();
                return;
            }

            m_EventCalledObjects.Remove(Coordinator);
            m_EventDefaultActionObjects.Remove(Coordinator);
            m_EventProcessedEvents.Remove(Coordinator);
            m_StackOfProcessedEvent.Remove(Coordinator);

            if (m_EventTypeProcessedCount.TryGetValue(Coordinator, out var eventTypeProcessedForCoordinator))
                eventTypeProcessedForCoordinator.Clear();
        }

        public void SaveReplaySessionFromSelection(string path, List<EventDebuggerEventRecord> eventList)
        {
            if (string.IsNullOrEmpty(path))
                return;

            var recordSave = new EventDebuggerRecordList() { eventList = eventList };
            var json = JsonUtility.ToJson(recordSave);
            File.WriteAllText(path, json);
            Debug.Log($"Saved under: {path}");
        }

        public EventDebuggerRecordList LoadReplaySession(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var fileContent = File.ReadAllText(path);
            return JsonUtility.FromJson<EventDebuggerRecordList>(fileContent);
        }

        public IEnumerator ReplayEvents(IEnumerable<EventDebuggerEventRecord> eventBases, Action<int, int> refreshList)
        {
            if (eventBases == null)
                yield break;

            IsReplaying = true;
            var doReplay = DoReplayEvents(eventBases, refreshList);
            while (doReplay.MoveNext())
            {
                yield return null;
            }
        }

        private IEnumerator DoReplayEvents(IEnumerable<EventDebuggerEventRecord> eventBases, Action<int, int> refreshList)
        {
            var sortedEvents = eventBases.OrderBy(e => e.Timestamp).ToList();
            var sortedEventsCount = sortedEvents.Count;

            IEnumerator AwaitForNextEvent(int currentIndex)
            {
                if (currentIndex == sortedEvents.Count - 1)
                    yield break;

                var deltaTimestampMs = sortedEvents[currentIndex + 1].Timestamp - sortedEvents[currentIndex].Timestamp;

                var timeMs = 0.0f;
                while (timeMs < deltaTimestampMs)
                {
                    if (IsPlaybackPaused)
                    {
                        yield return null;
                    }
                    else
                    {
                        var time = EventBase.TimeSinceStartupMs();
                        yield return null;
                        var delta = EventBase.TimeSinceStartupMs() - time;
                        timeMs += delta * PlaybackSpeed;
                    }
                }
            }

            for (var i = 0; i < sortedEventsCount; i++)
            {
                if (!IsReplaying)
                    break;

                var eventBase = sortedEvents[i];
                EventBase newEvent = null;
                try
                {
                    Type eventType = Type.GetType(eventBase.EventType);
                    var getPooledMethod = eventType.GetStaticMethodWithNoParametersInBase("GetPooled");
                    Assert.IsTrue(getPooledMethod != null);
                    newEvent = (EventBase)getPooledMethod.Invoke(null, null);
                    JsonConvert.PopulateObject(eventBase.JsonData, newEvent);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
                if (newEvent == null)
                {
                    Debug.Log("Skipped event (" + eventBase.EventBaseName + "): " + eventBase);
                    var awaitSkipped = AwaitForNextEvent(i);
                    while (awaitSkipped.MoveNext()) yield return null;
                    continue;
                }
                eventBase.Target.SendEvent(newEvent);
                newEvent.Dispose();
                refreshList?.Invoke(i, sortedEventsCount);
                Debug.Log($"Replayed event {eventBase.EventId} ({eventBase.EventBaseName}): {newEvent}");
                var await = AwaitForNextEvent(i);
                while (await.MoveNext()) yield return null;
            }

            IsReplaying = false;
        }

        public void StopPlayback()
        {
            IsReplaying = false;
            IsPlaybackPaused = false;
        }

        internal struct HistogramRecord
        {
            public long count;
            public long duration;
        }

        public Dictionary<string, HistogramRecord> ComputeHistogram(List<EventDebuggerEventRecord> eventBases)
        {
            if (Coordinator == null || !m_EventProcessedEvents.TryGetValue(Coordinator, out var list))
                return null;

            if (list == null)
                return null;

            Dictionary<string, HistogramRecord> histogram = new();
            foreach (var callObject in list)
            {
                if (eventBases == null || eventBases.Count == 0 || eventBases.Contains(callObject.EventBase))
                {
                    var key = callObject.EventBase.EventBaseName;
                    var totalDuration = callObject.Duration;
                    long totalCount = 1;
                    if (histogram.TryGetValue(key, out var currentHistogramRecord))
                    {
                        totalDuration += currentHistogramRecord.duration;
                        totalCount += currentHistogramRecord.count;
                    }

                    histogram[key] = new HistogramRecord { count = totalCount, duration = totalDuration };
                }
            }

            return histogram;
        }

        // Call Object
        private readonly Dictionary<IEventCoordinator, List<EventDebuggerCallTrace>> m_EventCalledObjects;
        private readonly Dictionary<IEventCoordinator, List<EventDebuggerDefaultActionTrace>> m_EventDefaultActionObjects;
        private readonly Dictionary<IEventCoordinator, List<EventDebuggerPathTrace>> m_EventPathObjects;
        private readonly Dictionary<IEventCoordinator, List<EventDebuggerTrace>> m_EventProcessedEvents;
        private readonly Dictionary<IEventCoordinator, Stack<EventDebuggerTrace>> m_StackOfProcessedEvent;
        private readonly Dictionary<IEventCoordinator, Dictionary<long, int>> m_EventTypeProcessedCount;

        public Dictionary<long, int> EventTypeProcessedCount => m_EventTypeProcessedCount.TryGetValue(Coordinator, out var eventTypeProcessedCountForCoordinator) ? eventTypeProcessedCountForCoordinator : null;

        private readonly Dictionary<IEventCoordinator, long> m_ModificationCount;
        private readonly bool m_Log;

        public bool Suspended { get; set; }

        // Methods
        public EventDebugger()
        {
            m_EventCalledObjects = new Dictionary<IEventCoordinator, List<EventDebuggerCallTrace>>();
            m_EventDefaultActionObjects = new Dictionary<IEventCoordinator, List<EventDebuggerDefaultActionTrace>>();
            m_StackOfProcessedEvent = new Dictionary<IEventCoordinator, Stack<EventDebuggerTrace>>();
            m_EventProcessedEvents = new Dictionary<IEventCoordinator, List<EventDebuggerTrace>>();
            m_EventTypeProcessedCount = new Dictionary<IEventCoordinator, Dictionary<long, int>>();
            m_ModificationCount = new Dictionary<IEventCoordinator, long>();
            m_EventPathObjects = new Dictionary<IEventCoordinator, List<EventDebuggerPathTrace>>();
            m_Log = true;
        }

        private void AddCallObject(int cbHashCode, string cbName, EventBase evt, bool propagationHasStopped, bool immediatePropagationHasStopped, bool defaultHasBeenPrevented, long duration)
        {
            if (Suspended)
                return;

            if (m_Log)
            {
                var callObject = new EventDebuggerCallTrace(Coordinator, evt, cbHashCode, cbName, propagationHasStopped, immediatePropagationHasStopped, defaultHasBeenPrevented, duration);

                if (!m_EventCalledObjects.TryGetValue(Coordinator, out var list))
                {
                    list = new List<EventDebuggerCallTrace>();
                    m_EventCalledObjects.Add(Coordinator, list);
                }

                list.Add(callObject);
            }
        }

        private void AddExecuteDefaultAction(EventBase evt, PropagationPhase phase, long duration)
        {
            if (Suspended)
                return;

            if (m_Log)
            {
                var defaultActionObject = new EventDebuggerDefaultActionTrace(Coordinator, evt, phase, duration);

                if (!m_EventDefaultActionObjects.TryGetValue(Coordinator, out var list))
                {
                    list = new List<EventDebuggerDefaultActionTrace>();
                    m_EventDefaultActionObjects.Add(Coordinator, list);
                }

                list.Add(defaultActionObject);
            }
        }



        private void AddBeginProcessEvent(EventBase evt)
        {
            if (Suspended)
                return;

            var dbgObject = new EventDebuggerTrace(Coordinator, evt, -1);

            if (!m_StackOfProcessedEvent.TryGetValue(Coordinator, out var stack))
            {
                stack = new Stack<EventDebuggerTrace>();
                m_StackOfProcessedEvent.Add(Coordinator, stack);
            }

            if (!m_EventProcessedEvents.TryGetValue(Coordinator, out var list))
            {
                list = new List<EventDebuggerTrace>();
                m_EventProcessedEvents.Add(Coordinator, list);
            }

            list.Add(dbgObject);
            stack.Push(dbgObject);

            if (!m_EventTypeProcessedCount.TryGetValue(Coordinator, out var eventTypeProcessedCountForCoordinator))
                return;

            if (!eventTypeProcessedCountForCoordinator.TryGetValue(dbgObject.EventBase.EventTypeId, out var count))
                count = 0;

            eventTypeProcessedCountForCoordinator[dbgObject.EventBase.EventTypeId] = count + 1;
        }

        private void AddEndProcessEvent(EventBase evt, long duration)
        {
            if (Suspended)
                return;

            bool evtHandled = false;
            if (m_StackOfProcessedEvent.TryGetValue(Coordinator, out var stack))
            {
                if (stack.Count > 0)
                {
                    var dbgObject = stack.Peek();
                    if (dbgObject.EventBase.EventId == evt.EventId)
                    {
                        stack.Pop();
                        dbgObject.Duration = duration;

                        // Update the target if it was unknown in AddBeginProcessEvent.
                        if (dbgObject.EventBase.Target == null)
                        {
                            dbgObject.EventBase.Target = evt.Target;
                        }

                        evtHandled = true;
                    }
                }
            }

            if (!evtHandled)
            {
                var dbgObject = new EventDebuggerTrace(Coordinator, evt, duration);
                if (!m_EventProcessedEvents.TryGetValue(Coordinator, out var list))
                {
                    list = new List<EventDebuggerTrace>();
                    m_EventProcessedEvents.Add(Coordinator, list);
                }

                list.Add(dbgObject);

                if (!m_EventTypeProcessedCount.TryGetValue(Coordinator, out var eventTypeProcessedForCoordinator))
                    return;

                if (!eventTypeProcessedForCoordinator.TryGetValue(dbgObject.EventBase.EventTypeId, out var count))
                    count = 0;

                eventTypeProcessedForCoordinator[dbgObject.EventBase.EventTypeId] = count + 1;
            }
        }

        public static string GetObjectDisplayName(object obj, bool withHashCode = true)
        {
            if (obj == null) return string.Empty;

            var type = obj.GetType();
            var objectName = GetTypeDisplayName(type);
            // Two kinds
            // MonoBehaviour implements IEventHandler
            if (obj is Behaviour behaviour)
            {
                objectName += "#" + behaviour.gameObject.name;
                if (withHashCode)
                {
                    //Prefer to use instanceID at runtime
                    objectName += " (" + behaviour.GetInstanceID().ToString("x8") + ")";
                }
            }
            // EventHandler attached to a MonoBehaviour
            else if (obj is IBehaviourScope bs)
            {
                objectName += "#" + bs.Behaviour.gameObject.name;
                if (withHashCode)
                {
                    //Prefer to use instanceID at runtime
                    objectName += " (" + bs.Behaviour.GetInstanceID().ToString("x8") + ")";
                }
            }

            else if (withHashCode)
            {
                objectName += " (" + obj.GetHashCode().ToString("x8") + ")";
            }

            return objectName;
        }

        public static string GetTypeDisplayName(Type type)
        {
            return type.IsGenericType ? $"{type.Name.TrimEnd('`', '1')}<{type.GetGenericArguments()[0].Name}>" : type.Name;
        }
    }
}
