#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(LevelUpRewardsConfig))]
public class LevelUpRewardsConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        DrawDefaultInspector();

        GUILayout.Space(12f);
        EditorGUILayout.LabelField("Tools", EditorStyles.boldLabel);

        LevelUpRewardsConfig config = (LevelUpRewardsConfig)target;

        if (GUILayout.Button("Apply Economy V2 Preset (Levels 2-9999)"))
        {
            Undo.RecordObject(config, "Apply Economy V2 Level Rewards");
            config.ApplyEconomyV2PresetUpTo9999();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        if (GUILayout.Button("Auto Generate Rewards"))
        {
            Undo.RecordObject(config, "Auto Generate Level Rewards");
            config.AutoGenerateRewards();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        if (GUILayout.Button("Sort And Deduplicate"))
        {
            Undo.RecordObject(config, "Sort And Deduplicate Level Rewards");
            config.SortAndDeduplicate();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
        }

        if (GUILayout.Button("Clear Rewards"))
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Clear rewards",
                "Delete all level rewards from this asset?",
                "Yes",
                "No");

            if (confirmed)
            {
                Undo.RecordObject(config, "Clear Level Rewards");
                config.ClearRewards();
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
