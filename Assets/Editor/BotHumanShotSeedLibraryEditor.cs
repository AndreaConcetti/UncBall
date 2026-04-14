using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BotHumanShotSeedLibrary))]
public sealed class BotHumanShotSeedLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GUILayout.Space(12f);

        BotHumanShotSeedLibrary library = (BotHumanShotSeedLibrary)target;

        GUI.backgroundColor = new Color(0.85f, 1f, 0.85f);
        if (GUILayout.Button("Overwrite Seeds With Default Test Set", GUILayout.Height(34f)))
        {
            Undo.RecordObject(library, "Overwrite Bot Human Shot Seeds");
            library.EditorOverwriteSeedsWithDefaultTestSet();
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BotHumanShotSeedLibraryEditor] Default test seed set written into existing asset.", library);
        }
        GUI.backgroundColor = Color.white;
    }
}