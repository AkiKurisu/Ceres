using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
namespace Ceres.Events
{
    [Serializable]
    internal class EventDebuggerRecordList
    {
        public List<EventDebuggerEventRecord> eventList;
    }
    
    [Serializable]
    internal class EventDebuggerEventRecord
    {
        [field: SerializeField]
        public string EventBaseName { get; private set; }
        
        [field: SerializeField]
        public long EventTypeId { get; private set; }
        
        [field: SerializeField]
        public string EventType { get; private set; }
        
        [field: SerializeField]
        public ulong EventId { get; private set; }
        
        [field: SerializeField]
        internal long Timestamp { get; private set; }
        
        public IEventHandler Target { get; set; }
        
        public PropagationPhase PropagationPhase { get; private set; }
        
        public string JsonData { get; set; }
        
        public void Init(EventBase evt)
        {
            var type = evt.GetType();
            EventBaseName = EventDebugger.GetTypeDisplayName(type);
            EventType = type.AssemblyQualifiedName;
            EventTypeId = evt.EventTypeId;
            EventId = evt.EventId;
            Timestamp = evt.Timestamp;
            Target = evt.Target;
            PropagationPhase = evt.PropagationPhase;
            JsonData = JsonConvert.SerializeObject(evt);
        }

        public EventDebuggerEventRecord(EventBase evt)
        {
            Init(evt);
        }

        public string TimestampString()
        {
            long ticks = (long)(Timestamp / 1000f * TimeSpan.TicksPerSecond);
            return new DateTime(ticks).ToString("HH:mm:ss.ffffff");
        }
    }
}