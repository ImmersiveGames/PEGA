using System;
using UnityEngine;

namespace PEGA.ObjectSystems.EnemySystems
{
    public class EnemiesTarget : ObjectTarget
    {
        [Header("Test Only")]
        public Transform forTestsObject;
        protected override void SetTarget()
        {
            base.SetTarget();
            var myMovement = GetComponent<ObjectMovement>();
            myMovement.Target = myTarget;
        }

        #region Testes

        protected override void Update()
        {
            base.Update();
            if (Input.GetKeyDown(KeyCode.K))
            {
                chaseObject = chaseObject == null ? forTestsObject : null;
            }
        }

        #endregion

        protected override void OnDestinationReached()
        {
            base.OnDestinationReached();
        }
    }
}