#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class DATReaderEditorTools
{
    [MenuItem("DAT Reader/Open Console")]
    public static void OpenConsole()
    {
        EditorApplication.ExecuteMenuItem("Window/General/Console");
    }
}
#endif
