using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using UnityEngine;

namespace ImmersiveGames.Utils
{
    public class MainThreadDispatcher : MonoBehaviour
    {
        private static MainThreadDispatcher _instance;
        private static readonly ConcurrentQueue<Action> ActionsQueue = new ConcurrentQueue<Action>();

        private void Awake()
        {
            if (_instance != null)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Update()
        {
            while (ActionsQueue.TryDequeue(out var action))
            {
                action?.Invoke();
            }
        }

        public static MainThreadDispatcher Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var go = new GameObject("MainThreadDispatcher");
                _instance = go.AddComponent<MainThreadDispatcher>();
                return _instance;
            }
        }

        public static void Enqueue(Action action)
        {
            ActionsQueue.Enqueue(action);
        }

        public static Task EnqueueAsync(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));

            var tcs = new TaskCompletionSource<bool>();
            Enqueue(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            return tcs.Task;
        }

        public static async Task EnqueueAsync(Func<Task> action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            
            var tcs = new TaskCompletionSource<bool>();

            Enqueue(Action);

            await tcs.Task.ConfigureAwait(false);
            return;

            async void Action()
            {
                try
                {
                    await action.Invoke().ConfigureAwait(false);
                    tcs.SetResult(true);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }
        }
    }
}
