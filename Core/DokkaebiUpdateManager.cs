using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Dokkaebi.Utilities;
using Dokkaebi.Interfaces;

namespace Dokkaebi.Core
{
    /// <summary>
    /// Central update manager for all Dokkaebi systems to reduce Update() overhead
    /// </summary>
    public class DokkaebiUpdateManager : MonoBehaviour, ICoreUpdateService
    {
        private static DokkaebiUpdateManager instance;
        public static DokkaebiUpdateManager Instance 
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<DokkaebiUpdateManager>();
                    if (instance == null)
                    {
                        GameObject obj = new GameObject("DokkaebiUpdateManager");
                        instance = obj.AddComponent<DokkaebiUpdateManager>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return instance;
            }
        }

        // Observer interfaces
        public interface IFixedUpdateObserver { void CustomFixedUpdate(float deltaTime); }
        public interface ILateUpdateObserver { void CustomLateUpdate(float deltaTime); }

        // Observer collections
        private readonly List<IUpdateObserver> updateObservers = new List<IUpdateObserver>();
        private readonly List<IFixedUpdateObserver> fixedUpdateObservers = new List<IFixedUpdateObserver>();
        private readonly List<ILateUpdateObserver> lateUpdateObservers = new List<ILateUpdateObserver>();
        
        // Buffered lists to handle modifications during iteration
        private readonly List<IUpdateObserver> pendingAddUpdateObservers = new List<IUpdateObserver>();
        private readonly List<IUpdateObserver> pendingRemoveUpdateObservers = new List<IUpdateObserver>();
        private readonly List<IFixedUpdateObserver> pendingAddFixedObservers = new List<IFixedUpdateObserver>();
        private readonly List<IFixedUpdateObserver> pendingRemoveFixedObservers = new List<IFixedUpdateObserver>();
        private readonly List<ILateUpdateObserver> pendingAddLateObservers = new List<ILateUpdateObserver>();
        private readonly List<ILateUpdateObserver> pendingRemoveLateObservers = new List<ILateUpdateObserver>();
        
        private bool isUpdating = false;
        private bool isFixedUpdating = false;
        private bool isLateUpdating = false;

        private void Awake()
        {
            // SmartLogger.Log($"[UpdateManager] Awake() called on GameObject: {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})", LogCategory.Performance, this);

            if (instance != null && instance != this)
            {
                SmartLogger.LogError($"[UpdateManager] DUPLICATE INSTANCE DETECTED on {gameObject.name} (InstanceID: {gameObject.GetInstanceID()})! Destroying this duplicate. The original singleton is on {instance.gameObject.name}", LogCategory.Performance, this);
                Destroy(gameObject);
                return;
            }

            instance = this;
            // SmartLogger.Log($"[UpdateManager] Setting static instance to {gameObject.name} (InstanceID: {gameObject.GetInstanceID()}). Applying DontDestroyOnLoad.", LogCategory.Performance, this);

            // Try to apply DontDestroyOnLoad
            Transform parent = transform.parent;
            if (parent == null)
            {
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                SmartLogger.LogWarning($"[UpdateManager] {gameObject.name} is not a root object. DontDestroyOnLoad was NOT applied. Ensure the UpdateManager is on a root GameObject.", LogCategory.Performance, this);
            }
        }

        private void Update()
        {
            //SmartLogger.Log($"[UpdateManager.Update] Frame {Time.frameCount}. Running ProcessPendingObserverChanges...", LogCategory.Performance, this);
            ProcessPendingObserverChanges();

            if (updateObservers.Count == 0)
            {
                using (var sb = new StringBuilderScope(out StringBuilder builder))
                {
                    builder.AppendLine("[UpdateManager.Update] Observer list details:");
                    builder.AppendLine($"- Main list count: {updateObservers.Count}");
                    builder.AppendLine($"- Pending adds: {pendingAddUpdateObservers.Count}");
                    builder.AppendLine($"- Pending removes: {pendingRemoveUpdateObservers.Count}");
                    SmartLogger.LogWithBuilder(builder, LogCategory.Debug, this);
                }
                SmartLogger.Log($"[UpdateManager.Update] Observer list is EMPTY.", LogCategory.Debug, this);
                return;
            }

            isUpdating = true;
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < updateObservers.Count; i++)
            {
                if (updateObservers[i] == null)
                {
                    SmartLogger.LogWarning($"[UpdateManager.Update] Observer at index {i} was null!", LogCategory.Performance, this);
                    continue;
                }

                string observerName = updateObservers[i].ToString() ?? "Unknown";
                int instanceId = updateObservers[i].GetHashCode();

                try
                {
                    updateObservers[i].CustomUpdate(deltaTime);
                }
                catch (System.Exception e)
                {
                    SmartLogger.LogError($"[UpdateManager.Update] Error in CustomUpdate for '{observerName}' (InstanceID: {instanceId}): {SmartLogger.FormatException(e)}", LogCategory.Performance, this);
                }
            }

            isUpdating = false;
        }
        
        private void FixedUpdate()
        {
            isFixedUpdating = true;
            float deltaTime = Time.fixedDeltaTime;

            for (int i = 0; i < fixedUpdateObservers.Count; i++)
            {
                try
                {
                    fixedUpdateObservers[i].CustomFixedUpdate(deltaTime);
                }
                catch (System.Exception e)
                {
                    SmartLogger.LogError($"Error in CustomFixedUpdate for {fixedUpdateObservers[i]}: {SmartLogger.FormatException(e)}", LogCategory.Performance, this);
                }
            }

            isFixedUpdating = false;
        }
        
        private void LateUpdate()
        {
            isLateUpdating = true;
            float deltaTime = Time.deltaTime;

            for (int i = 0; i < lateUpdateObservers.Count; i++)
            {
                try
                {
                    lateUpdateObservers[i].CustomLateUpdate(deltaTime);
                }
                catch (System.Exception e)
                {
                    SmartLogger.LogError($"Error in CustomLateUpdate for {lateUpdateObservers[i]}: {SmartLogger.FormatException(e)}", LogCategory.Performance, this);
                }
            }

            isLateUpdating = false;
        }

        private void ProcessPendingObserverChanges()
        {
            if (pendingAddUpdateObservers.Count == 0 && pendingRemoveUpdateObservers.Count == 0)
                return;

            SmartLogger.Log($"[UpdateManager.ProcessPending] Running. Pending Adds: {pendingAddUpdateObservers.Count}, Pending Removes: {pendingRemoveUpdateObservers.Count}. Current Main Count: {updateObservers.Count}", LogCategory.Debug, this);

            int initialCount = updateObservers.Count;

            // Process additions
            if (pendingAddUpdateObservers.Count > 0)
            {
                updateObservers.AddRange(pendingAddUpdateObservers);
                SmartLogger.Log($"[UpdateManager.ProcessPending] Added {pendingAddUpdateObservers.Count} observers from pending list.", LogCategory.Debug, this);
                pendingAddUpdateObservers.Clear();
            }

            // Process removals
            if (pendingRemoveUpdateObservers.Count > 0)
            {
                int removedCount = 0;
                for (int i = updateObservers.Count - 1; i >= 0; i--)
                {
                    if (pendingRemoveUpdateObservers.Contains(updateObservers[i]))
                    {
                        updateObservers.RemoveAt(i);
                        removedCount++;
                    }
                }
                SmartLogger.Log($"[UpdateManager.ProcessPending] Removed {removedCount} observers from main list based on pending remove list.", LogCategory.Debug, this);
                pendingRemoveUpdateObservers.Clear();
            }

            SmartLogger.Log($"[UpdateManager.ProcessPending] COMPLETE. Initial Count: {initialCount}, Final Count: {updateObservers.Count}", LogCategory.Debug, this);
        }

        private void ProcessPendingFixedObserverChanges()
        {
            // Process additions
            if (pendingAddFixedObservers.Count > 0)
            {
                fixedUpdateObservers.AddRange(pendingAddFixedObservers);
                pendingAddFixedObservers.Clear();
            }

            // Process removals
            if (pendingRemoveFixedObservers.Count > 0)
            {
                for (int i = fixedUpdateObservers.Count - 1; i >= 0; i--)
                {
                    if (pendingRemoveFixedObservers.Contains(fixedUpdateObservers[i]))
                    {
                        fixedUpdateObservers.RemoveAt(i);
                    }
                }
                pendingRemoveFixedObservers.Clear();
            }
        }

        private void ProcessPendingLateObserverChanges()
        {
            // Process additions
            if (pendingAddLateObservers.Count > 0)
            {
                lateUpdateObservers.AddRange(pendingAddLateObservers);
                pendingAddLateObservers.Clear();
            }

            // Process removals
            if (pendingRemoveLateObservers.Count > 0)
            {
                for (int i = lateUpdateObservers.Count - 1; i >= 0; i--)
                {
                    if (pendingRemoveLateObservers.Contains(lateUpdateObservers[i]))
                    {
                        lateUpdateObservers.RemoveAt(i);
                    }
                }
                pendingRemoveLateObservers.Clear();
            }
        }

        public void RegisterUpdateObserver(IUpdateObserver observer)
        {
            if (observer == null)
            {
                SmartLogger.LogWarning("[UpdateManager.Register] Attempted to register NULL observer!", LogCategory.Performance, this);
                return;
            }

            if (isUpdating)
            {
                if (!updateObservers.Contains(observer) && !pendingAddUpdateObservers.Contains(observer))
                {
                    pendingAddUpdateObservers.Add(observer);
                    SmartLogger.Log($"[UpdateManager.Register] Added '{observer.GetType().Name}' to PENDING ADD list.", LogCategory.Performance, this);
                }
            }
            else
            {
                if (!updateObservers.Contains(observer))
                {
                    updateObservers.Add(observer);
                    SmartLogger.Log($"[UpdateManager.Register] Added '{observer.GetType().Name}' directly to MAIN list.", LogCategory.Performance, this);
                }
            }
        }

        public void UnregisterUpdateObserver(IUpdateObserver observer)
        {
            if (observer == null) return;

            if (isUpdating)
            {
                if (updateObservers.Contains(observer) && !pendingRemoveUpdateObservers.Contains(observer))
                {
                    pendingRemoveUpdateObservers.Add(observer);
                    SmartLogger.Log($"[UpdateManager.Unregister] Added '{observer.GetType().Name}' to PENDING REMOVE list.", LogCategory.Performance, this);
                }
            }
            else
            {
                bool removed = updateObservers.Remove(observer);
                SmartLogger.Log($"[UpdateManager.Unregister] Removed '{observer.GetType().Name}' directly from MAIN list. Result: {removed}", LogCategory.Performance, this);
            }
        }

        public void RegisterFixedUpdateObserver(IFixedUpdateObserver observer)
        {
            if (observer == null) return;

            if (isFixedUpdating)
            {
                if (!fixedUpdateObservers.Contains(observer) && !pendingAddFixedObservers.Contains(observer))
                {
                    pendingAddFixedObservers.Add(observer);
                }
            }
            else
            {
                if (!fixedUpdateObservers.Contains(observer))
                {
                    fixedUpdateObservers.Add(observer);
                }
            }
        }

        public void UnregisterFixedUpdateObserver(IFixedUpdateObserver observer)
        {
            if (observer == null) return;

            if (isFixedUpdating)
            {
                if (fixedUpdateObservers.Contains(observer) && !pendingRemoveFixedObservers.Contains(observer))
                {
                    pendingRemoveFixedObservers.Add(observer);
                }
            }
            else
            {
                fixedUpdateObservers.Remove(observer);
            }
        }

        public void RegisterLateUpdateObserver(ILateUpdateObserver observer)
        {
            if (observer == null) return;

            if (isLateUpdating)
            {
                if (!lateUpdateObservers.Contains(observer) && !pendingAddLateObservers.Contains(observer))
                {
                    pendingAddLateObservers.Add(observer);
                }
            }
            else
            {
                if (!lateUpdateObservers.Contains(observer))
                {
                    lateUpdateObservers.Add(observer);
                }
            }
        }

        public void UnregisterLateUpdateObserver(ILateUpdateObserver observer)
        {
            if (observer == null) return;

            if (isLateUpdating)
            {
                if (lateUpdateObservers.Contains(observer) && !pendingRemoveLateObservers.Contains(observer))
                {
                    pendingRemoveLateObservers.Add(observer);
                }
            }
            else
            {
                lateUpdateObservers.Remove(observer);
            }
        }

        // ICoreUpdateService implementation
        void ICoreUpdateService.RegisterUpdateObserver(IUpdateObserver observer)
        {
            RegisterUpdateObserver(observer);
        }

        void ICoreUpdateService.UnregisterUpdateObserver(IUpdateObserver observer)
        {
            UnregisterUpdateObserver(observer);
        }
    }
}
