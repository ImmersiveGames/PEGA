using System;
using ImmersiveGames.Utils;
using UnityEngine;

namespace Bira
{
    public class DisplayDebugsManager : Singleton<DisplayDebugsManager>
    {
        public event Action<Transform> EventUIDebug;
        

        public void OnEventUIDebug(Transform target)
        {
            EventUIDebug?.Invoke(target);
        }
    }
}
