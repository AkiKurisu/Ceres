namespace Ceres.Events.Editor
{
    class EventLogLine
    {
        public int LineNumber { get; }
        public string Timestamp { get; }
        public string EventName { get; }
        public string Target { get; }
        public EventDebuggerEventRecord EventBase { get; }

        public EventLogLine(int lineNumber, string timestamp = "", string eventName = "", string target = "", EventDebuggerEventRecord eventBase = null)
        {
            LineNumber = lineNumber;
            Timestamp = timestamp;
            EventName = eventName;
            Target = target;
            EventBase = eventBase;
        }
    }
}
