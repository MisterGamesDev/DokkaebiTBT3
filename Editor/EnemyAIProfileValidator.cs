using UnityEngine;
using UnityEditor;

public class EnemyAIProfileValidator : EditorWindow
{
    [MenuItem("Tools/Validate Enemy AI Profiles")]
    public static void ValidateProfiles()
    {
        var guids = AssetDatabase.FindAssets("t:EnemyAIProfile");
        int errorCount = 0;

        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (profile == null)
            {
                Debug.LogError($"[AI Validation] Could not load EnemyAIProfile at {path}");
                errorCount++;
                continue;
            }

            var so = new SerializedObject(profile);
            var actionsProp = so.FindProperty("AvailableActions");
            if (actionsProp == null)
            {
                Debug.LogError($"[AI Validation] Profile '{profile.name}' is missing the AvailableActions field!");
                errorCount++;
                continue;
            }
            if (actionsProp.arraySize == 0)
            {
                Debug.LogWarning($"[AI Validation] Profile '{profile.name}' has no actions assigned.");
            }
            for (int i = 0; i < actionsProp.arraySize; i++)
            {
                var actionRef = actionsProp.GetArrayElementAtIndex(i);
                if (actionRef.objectReferenceValue == null)
                {
                    Debug.LogError($"[AI Validation] Profile '{profile.name}' has a missing action at index {i}.");
                    errorCount++;
                    continue;
                }
                var actionSO = actionRef.objectReferenceValue as ScriptableObject;
                var actionSOObj = new SerializedObject(actionSO);
                var actionNameProp = actionSOObj.FindProperty("ActionName");
                if (actionNameProp != null && string.IsNullOrEmpty(actionNameProp.stringValue))
                {
                    Debug.LogError($"[AI Validation] Action '{actionSO.name}' in profile '{profile.name}' has a blank ActionName.");
                    errorCount++;
                }
                var abilityProp = actionSOObj.FindProperty("AbilityToUse");
                if (abilityProp != null && abilityProp.objectReferenceValue == null)
                {
                    Debug.LogError($"[AI Validation] Action '{actionSO.name}' in profile '{profile.name}' is missing AbilityToUse.");
                    errorCount++;
                }
            }
        }
        Debug.Log($"[AI Validation] Validation complete. Errors found: {errorCount}");
    }
}