namespace Ceres.Events.Editor
{
    internal class TitleInfo : IRegisteredCallbackLine
    {
        public LineType Type => LineType.Title;
        public string Text { get; }
        public CallbackEventHandler CallbackHandler { get; }

        public TitleInfo(string text, CallbackEventHandler handler)
        {
            Text = text;
            CallbackHandler = handler;
        }
    }
}
