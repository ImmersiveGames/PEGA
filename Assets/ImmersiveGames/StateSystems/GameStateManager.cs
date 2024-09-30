using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImmersiveGames.DebugSystems;
using ImmersiveGames.SceneSystems;

namespace ImmersiveGames.StateSystems
{
    public class GameStateManager
    {
        private Dictionary<string, GameState> _states = new Dictionary<string, GameState>();
        private static GameState _currentState;
        private static GameState _previousState;
        
        public delegate void OnStateChangeHandler(GameState gameState);
        public event OnStateChangeHandler OnStateChanged;
        
        public void AddState(GameState state)
        {
            _states[state.StateName] = state;
        }
        public GameState GetCurrentState()
        {
            return _currentState;
        }

        public GameState GetPreviousState()
        {
            return _previousState;
        }
        public async Task ChangeStateAsync(string stateName)
        {
            if (!_states.TryGetValue(stateName, out var nextState))
            {
                DebugManager.LogError<GameStateManager>($"Estado não encontrado: {stateName}");
                return;
            }
            if (nextState == _currentState)
            {
                DebugManager.Log<GameStateManager>($"Já está no estado: {stateName}");
                return;
            }
            //Aqui eu chamo indicando que vai haver uma troca de estates ideal para chamar um audio,
            // ou algo que deve inicializar antes da saida do ultimo estado
            // Aqui você cria um TaskScheduler para garantir que o código abaixo seja executado na Main Thread
            var mainThreadScheduler = TaskScheduler.FromCurrentSynchronizationContext();

            // Aqui você garante que o OnStateChanged será executado na Main Thread
            await Task.Factory.StartNew(() =>
            {
                OnStateChanged?.Invoke(nextState);
            }, CancellationToken.None, TaskCreationOptions.None, mainThreadScheduler);
            
            if (_currentState != null)
            {
                await _currentState.ExitAsync(nextState).ConfigureAwait(false);
            }

            _previousState = _currentState;
            _currentState = nextState;
            

            if (_currentState.RequiresSceneLoad && !string.IsNullOrEmpty(_currentState.SceneName))
            {
                // Agora, esperamos que a transição de cena seja concluída antes de prosseguir
                await SceneChangeManager.StartSceneTransitionAsync(_currentState, _previousState?.SceneName, _currentState.LoadMode, _currentState.UnLoadAdditiveScene).ConfigureAwait(false);
            }

            await _currentState.EnterAsync(_previousState).ConfigureAwait(false);

            DebugManager.Log<GameStateManager>($"Mudou para o estado: {stateName}");
        }
    }
}