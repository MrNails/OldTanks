namespace CoolEngine.Services;

public static class UI
{
    public static void UIInvoke(Action callback) => Application.Current.Dispatcher.Invoke(callback);
    public static T UIInvoke<T>(Func<T> callback) => Application.Current.Dispatcher.Invoke(callback);
}