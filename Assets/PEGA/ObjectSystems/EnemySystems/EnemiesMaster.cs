using PEGA.ObjectSystems.ObjectsScriptables;

namespace PEGA.ObjectSystems.EnemySystems
{
    public class EnemiesMaster : ObjectMaster
    {
        private EnemiesData _enemiesData;
        public EnemiesMaster(EnemiesData objectData) : base(objectData)
        {
            _enemiesData = objectData;
        }

        
    }
}