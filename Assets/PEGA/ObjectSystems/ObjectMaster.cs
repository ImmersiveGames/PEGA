using System;
using JetBrains.Annotations;
using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;

namespace PEGA.ObjectSystems
{
    public abstract class ObjectMaster: MonoBehaviour
    {
        public ObjectDataScriptable objectData;
        protected ObjectMaster(ObjectDataScriptable objectData)
        {
            this.objectData = objectData;
        }

        #region Action Calls
        [UsedImplicitly] public Action CreateSkin;

        public void OnCreateSkin()
        {
            CreateSkin?.Invoke();
        }

        #endregion
    }
}