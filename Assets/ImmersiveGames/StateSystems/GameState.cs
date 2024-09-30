using System.Threading.Tasks;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.SceneSystems.Interfaces;
using ImmersiveGames.StateSystems.Interfaces;
using ImmersiveGames.TransitionSystems.Interfaces;
using UnityEngine.SceneManagement;

namespace ImmersiveGames.StateSystems
{
    public abstract class GameState: IState,ILoadScene
    {
        private readonly ITransition _transition;
        protected GameState(string currentStateName, string sceneName, ITransition transition, LoadSceneMode loadMode, bool unLoadAdditiveScene)
        {
            // ReSharper disable once VirtualMemberCallInConstructor
            StateName = currentStateName;
            SceneName = sceneName;
            StateInitialized = false;
            StateFinalization = false;
            _transition = transition;
            LoadMode = loadMode;
            UnLoadAdditiveScene = unLoadAdditiveScene;
        }
        public string StateName { get; }
        public bool StateInitialized { get; set; }
        public bool StateFinalization { get; private set; }

        public async Task EnterAsync(IState previousState)
        {
            if (!StateInitialized)
            {
                StateInitialized = true;
            }
            DebugManager.Log<GameState>($"O {previousState} requer transição? {RequiresSceneLoad}");
            if (previousState == null || RequiresSceneLoad)
            {
                if (_transition != null)
                {
                    DebugManager.Log<GameState>($"O {previousState} ira transitar usando: {_transition}");
                    await _transition.OutTransitionAsync().ConfigureAwait(false);
                }
            }
            
            await OnEnter(previousState).ConfigureAwait(false);
            StateFinalization = true;
        }

        public void UpdateState()
        {
            throw new System.NotImplementedException();
        }

        public Task ExitAsync(IState nextState)
        {
            throw new System.NotImplementedException();
        }

        #region ILoadScene

        public bool RequiresSceneLoad => false;
        public string SceneName { get; }

        public LoadSceneMode LoadMode { get; }
        public bool UnLoadAdditiveScene { get; }

        #endregion
        
        
        #region Enter and Exit Methods

        protected virtual async Task OnEnter(IState previousState)
        {
            // Custom logic to be executed on entering the state
            
            await Task.Yield();
        }

        protected virtual async Task OnExit()
        {
            // Custom logic to be executed on exiting the state
            await Task.Yield();
        }

        #endregion
    }
}