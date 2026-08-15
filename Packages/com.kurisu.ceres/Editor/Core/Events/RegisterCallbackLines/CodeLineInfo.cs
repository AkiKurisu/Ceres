namespace Ceres.Events.Editor
{
    internal class CodeLineInfo : IRegisteredCallbackLine
    {
        public LineType Type => LineType.CodeLine;
        public string Text { get; }
        public CallbackEventHandler CallbackHandler { get; }

        public string FileName { get; }
        public int LineNumber { get; }
        public int LineHashCode { get; }

        public bool Highlighted { get; set; }

        public CodeLineInfo(string text, CallbackEventHandler handler, string fileName, int lineNumber, int lineHashCode)
        {
            Text = text;
            CallbackHandler = handler;
            FileName = fileName;
            LineNumber = lineNumber;
            LineHashCode = lineHashCode;
        }
    }
}
