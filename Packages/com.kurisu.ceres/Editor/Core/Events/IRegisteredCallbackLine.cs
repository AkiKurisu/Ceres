namespace Ceres.Events.Editor
{
    enum LineType
    {
        Title,
        Callback,
        CodeLine
    }

    interface IRegisteredCallbackLine
    {
        LineType Type { get; }
        string Text { get; }
        CallbackEventHandler CallbackHandler { get; }
    }
}
