
namespace UnityEngine
{
    public static class GameViewUtils
    {
        public static Vector2 GetSizeOfMainGameView()
        {
#if UNITY_EDITOR
            return UnityEditor.GameView.GetSizeOfMainGameView();
#else
            return new Vector2(Screen.width, Screen.height);
#endif
        }
    }
}
