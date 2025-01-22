using System;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.InputSystems;
using PEGA.ObjectSystems;
using PEGA.ObjectSystems.PlayerSystems;
using UnityEngine;

namespace PEGA.GameTests
{
    public class TestInGame : MonoBehaviour
    {
        private void Awake()
        {
            //Aqui vão todos os Scripts que devem ter seus debugs ativos

            //PlayerManager
            DebugManager.SetScriptDebugLevel<PlayerManager>(DebugManager.DebugLevels.All);
            
            //Actions
            DebugManager.SetScriptDebugLevel<ActionManager>(DebugManager.DebugLevels.All);
            DebugManager.SetScriptDebugLevel<ObjectJump>(DebugManager.DebugLevels.All);
            DebugManager.SetScriptDebugLevel<PlayerMovement>(DebugManager.DebugLevels.All);
            
        }
    }
}