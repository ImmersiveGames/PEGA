using System;
using TMPro;
using UnityEngine;

namespace Bira
{
    public class ChangeTarget: MonoBehaviour
    {
        private TMP_Text _textMeshPro;
        private DisplayDebugsManager _displayDebugsManager;

        private void OnEnable()
        {
            _textMeshPro = GetComponent<TMP_Text>();
            _displayDebugsManager = DisplayDebugsManager.instance;
            _displayDebugsManager.EventUIDebug += ChangeName;
        }

        private void OnDisable()
        {
            _displayDebugsManager.EventUIDebug -= ChangeName;
        }

        private void ChangeName(Transform obj)
        {
            var nameObj = "Player"; 
            if (obj != null) nameObj = obj.name;
            _textMeshPro.text = nameObj;
        }
    }
}