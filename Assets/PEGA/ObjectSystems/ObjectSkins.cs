using System;
using ImmersiveGames.DebugSystems;
using PEGA.ObjectSystems.Interfaces;
using PEGA.ObjectSystems.ObjectsScriptables;
using PEGA.ObjectSystems.Tags;
using UnityEngine;
using UnityEngine.Serialization;

namespace PEGA.ObjectSystems
{
    public abstract class ObjectSkins : MonoBehaviour, ISkinConfigurable
    {
        public int forceSkinIndex = -1;
        public bool randomSkin;
        public SkinCollections defaultSkinList;
        protected ObjectMaster ObjectMaster;
        
        private GameObject _skin;
        #region Unity Methods

        private void Awake()
        {
            RemoveAllSkins();
            CreateSkin(defaultSkinList);
        }
        protected void OnEnable()
        {
            SetInitialReferences();
            
        }

        #endregion
        
        protected virtual void SetInitialReferences()
        {
            ObjectMaster = GetComponent<ObjectMaster>();
        }
        private void CreateSkin(SkinCollections skinCollections)
        {
            var skin = skinCollections.GetObjectSkin(forceSkinIndex, randomSkin);
            _skin = Instantiate(skin, transform);
            ApplySettings(_skin.GetComponent<SkinAttach>().skinSettings);
            _skin.transform.SetAsFirstSibling();
        }

        private void RemoveAllSkins()
        {
            var children = GetComponentInChildren<SkinAttach>();
            if (children != true) return;
            var siblingIndex = children.transform.GetSiblingIndex();
            Destroy(transform.GetChild(siblingIndex).gameObject);
        }
        
        public void ApplySettings(SkinSettings settings)
        {
            if (settings != null )
            {
                var characterController = GetComponent<CharacterController>();
                if (characterController == null) return;
                characterController.height = settings.height;
                characterController.radius = settings.radius;
                characterController.center = settings.center;
            }
            DebugManager.LogWarning<ObjectSkins>($"Nenhum Asset de Configuração foi encontrado na Skin");
        }
    }
}