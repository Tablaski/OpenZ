namespace UnityEngine
{
    // Only the isolated test process uses this path; actual player preferences are untouched.
    static class Application
    {
        public static string persistentDataPath => System.IO.Path.GetFullPath(System.IO.Path.Combine(System.AppContext.BaseDirectory, "../../../.private-test"));
    }
}
