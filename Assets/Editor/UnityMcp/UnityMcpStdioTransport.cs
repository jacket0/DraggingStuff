using MCPForUnity.Editor.Services;
using UnityEditor;

[InitializeOnLoad]
internal static class UnityMcpStdioTransport
{
    static UnityMcpStdioTransport()
    {
        var configuration = EditorConfigurationCache.Instance;
        if (!configuration.UseHttpTransport)
            return;

        configuration.SetUseHttpTransport(false);
        EditorUtility.RequestScriptReload();
    }
}
