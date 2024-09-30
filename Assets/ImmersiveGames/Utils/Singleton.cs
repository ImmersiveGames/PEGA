using UnityEngine;

namespace ImmersiveGames.Utils
{
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T _instance;
        private static readonly object _lock = new object();

        public static T instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance != null) return _instance;
                    _instance = FindObjectOfType<T>();

                    if (_instance != null) return _instance;
                    var obj = new GameObject(typeof(T).Name);
                    _instance = obj.AddComponent<T>();

                    return _instance;
                }
            }
        }

        protected virtual void Awake()
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = this as T;
                        //DontDestroyOnLoad(gameObject);
                    }
                    else
                    {
                        Destroy(gameObject);
                    }
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}