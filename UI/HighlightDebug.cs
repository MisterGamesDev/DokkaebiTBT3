using UnityEngine;
using Dokkaebi.Utilities; // Assuming SmartLogger is in this namespace

public class HighlightDebug : MonoBehaviour
{
    private MeshRenderer meshRenderer; // Add a reference to the MeshRenderer

    private void Awake()
    {
        // Get the MeshRenderer component when the object wakes up
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            SmartLogger.LogWarning($"[HighlightDebug] {gameObject.name} (ID: {GetInstanceID()}) Awake - MeshRenderer component not found!", LogCategory.UI, this);
        }
    }

    private void OnEnable()
    {
        // Log when the GameObject becomes enabled/active in the scene
        SmartLogger.Log($"[HighlightDebug] {gameObject.name} (ID: {GetInstanceID()}) OnEnable", LogCategory.UI, this);
    }

    private void OnDisable()
    {
        // Log when the GameObject becomes disabled/inactive in the scene
        SmartLogger.Log($"[HighlightDebug] {gameObject.name} (ID: {GetInstanceID()}) OnDisable", LogCategory.UI, this);
    }

    private void OnDestroy()
    {
        // Log when the GameObject is destroyed
        SmartLogger.Log($"[HighlightDebug] {gameObject.name} (ID: {GetInstanceID()}) OnDestroy", LogCategory.UI, this);
    }

    // Add Update to continuously check active state and renderer enabled state
    private void Update()
    {
        // Check if the GameObject is still valid before accessing properties (it might be null after Destroy)
        if (this.gameObject != null)
        {
            SmartLogger.Log($"[HighlightDebug] {gameObject.name} (ID: {GetInstanceID()}) Update - activeSelf: {gameObject.activeSelf}, activeInHierarchy: {gameObject.activeInHierarchy}, MeshRenderer Enabled: {(meshRenderer != null ? meshRenderer.enabled.ToString() : "N/A")}", LogCategory.UI, this);
        }
        // Note: This Update can generate a lot of logs. You might want to comment it out once debugging is complete.
    }
} 