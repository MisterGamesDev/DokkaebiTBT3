using UnityEngine;
using Dokkaebi.Utilities; // Assuming SmartLogger is in this namespace
using System; // Needed for System.Exception

public class GlobalExceptionHandler : MonoBehaviour
{
    void Awake()
    {
        // Subscribe to Unity's log message event
        Application.logMessageReceived += HandleLogMessage;
        SmartLogger.Log("[GlobalExceptionHandler] Subscribed to Application.logMessageReceived.", LogCategory.General, this);
    }

    void OnDestroy()
    {
        // Unsubscribe when the GameObject is destroyed
        Application.logMessageReceived -= HandleLogMessage;
        SmartLogger.Log("[GlobalExceptionHandler] Unsubscribed from Application.logMessageReceived.", LogCategory.General, this);
    }

    private void HandleLogMessage(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
        {
            // Log errors and exceptions using SmartLogger
            // Include stack trace if available
            string logMessage = $"[GLOBAL HANDLER] Type: {type}\nMessage: {logString}";
            if (!string.IsNullOrEmpty(stackTrace))
            {
                logMessage += $"\nStack Trace:\n{stackTrace}";
            }
            SmartLogger.LogError(logMessage, LogCategory.General, null);
        }
        // Uncomment below if you want to log all messages Unity receives
        // else
        // {
        //     SmartLogger.Log($"[GLOBAL LOG] Type: {type}, Message: {logString}", LogCategory.General, null);
        // }
    }
} 