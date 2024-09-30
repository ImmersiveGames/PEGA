using System.Collections.Generic;
using UnityEngine;

namespace ImmersiveGames.PoolingSystems
{
    public class GenericObjectPool<T> where T : Component
    {
        private readonly Queue<T> _objects = new Queue<T>();
        private readonly T _prefab;
        private readonly Transform _parent;

        public GenericObjectPool(T prefab, Transform parent = null, int initialCapacity = 0)
        {
            _prefab = prefab;
            _parent = parent;

            for (var i = 0; i < initialCapacity; i++)
            {
                T obj = Object.Instantiate(_prefab, _parent);
                obj.gameObject.SetActive(false);
                _objects.Enqueue(obj);
            }
        }

        public T GetObject()
        {
            if (_objects.Count > 0)
            {
                T obj = _objects.Dequeue();
                obj.gameObject.SetActive(true);
                return obj;
            }
            else
            {
                T newObj = Object.Instantiate(_prefab, _parent);
                return newObj;
            }
        }

        public void ReleaseObject(T obj)
        {
            obj.gameObject.SetActive(false);
            _objects.Enqueue(obj);
        }
    }
}