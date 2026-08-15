namespace Ceres.Events.Editor
{
    internal class CallbackInfo : IRegisteredCallbackLine
    {
        public LineType Type => LineType.Callback;
        public string Text { get; }
        public CallbackEventHandler CallbackHandler { get; }

        public CallbackInfo(string text, CallbackEventHandler handler)
        {
            Text = text;
            CallbackHandler = handler;
        }
    }
}
