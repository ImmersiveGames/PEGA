using System;
using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGames.DebugSystems
{
    public static class DebugManager
    {
        private static bool _globalDebugEnabled = true; // Inicialmente definido como ativo
        private static readonly Dictionary<Type, DebugLevels> ScriptDebugLevels = new Dictionary<Type, DebugLevels>();

        public enum DebugLevels
        {
            None,
            Logs,
            LogsAndWarnings,
            All
        }

        public static void SetGlobalDebugState(bool enabled)
        {
            _globalDebugEnabled = enabled;
        }

        public static void SetScriptDebugLevel<T>(DebugLevels debugLevel)
        {
            var scriptType = typeof(T);
            ScriptDebugLevels[scriptType] = debugLevel;
        }

        private static bool ShouldLog(DebugLevels localDebugLevel)
        {
            return _globalDebugEnabled && localDebugLevel != DebugLevels.None;
        }

        public static void Log<T>(string message)
        {
            var scriptType = typeof(T);
            if (ScriptDebugLevels.TryGetValue(scriptType, out DebugLevels scriptDebugLevel) && ShouldLog(scriptDebugLevel))
            {
                Debug.Log($"[{scriptType.Name}] {message}");
            }
        }

        public static void LogWarning<T>(string message)
        {
            var scriptType = typeof(T);
            if (ScriptDebugLevels.TryGetValue(scriptType, out DebugLevels scriptDebugLevel) && ShouldLog(scriptDebugLevel))
            {
                Debug.LogWarning($"[{scriptType.Name}] {message}");
            }
        }

        public static void LogError<T>(string message)
        {
            var scriptType = typeof(T);
            if (ScriptDebugLevels.TryGetValue(scriptType, out DebugLevels scriptDebugLevel) && ShouldLog(scriptDebugLevel))
            {
                Debug.LogError($"[{scriptType.Name}] {message}");
            }
        }
        public static void LogError<T>(Exception message)
        {
            var scriptType = typeof(T);
            if (ScriptDebugLevels.TryGetValue(scriptType, out DebugLevels scriptDebugLevel) && ShouldLog(scriptDebugLevel))
            {
                Debug.LogError($"[{scriptType.Name}] {message}");
            }
        }
    }
}