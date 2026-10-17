using System;
using Immersive.Framework.Authoring;
using Immersive.Logging.Loggers;
using Immersive.Logging.Unity;
using UnityEngine;

namespace PEGA.Diagnostics
{
    internal static class PEGALogging
    {
        internal static ScopedLogger CreateScopedLogger<T>(string category, string context)
        {
            ImmersiveFrameworkSettingsAsset settings = Resources.Load<ImmersiveFrameworkSettingsAsset>(
                ImmersiveFrameworkSettingsAsset.ResourcesPath);
            if (settings == null)
            {
                throw new InvalidOperationException(
                    $"PEGA logging requires the project-level {nameof(ImmersiveFrameworkSettingsAsset)} at Resources/{ImmersiveFrameworkSettingsAsset.ResourcesPath}.");
            }

            return UnityLoggingFactory.CreateLogger(settings.LoggingConfig)
                .For<T>(category, context);
        }
    }
}
