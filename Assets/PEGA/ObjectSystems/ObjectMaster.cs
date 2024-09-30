using PEGA.ObjectSystems.ObjectsScriptables;
using UnityEngine;
using UnityEngine.Serialization;

namespace PEGA.ObjectSystems
{
    public abstract class ObjectMaster: MonoBehaviour
    {
        public ObjectDataScriptable objectData;

        protected ObjectMaster(ObjectDataScriptable objectData)
        {
            this.objectData = objectData;
        }
    }
}