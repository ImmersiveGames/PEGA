using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace ImmersiveGames.StateSystems.Interfaces
{
    public interface IState
    {
        string StateName { get; }
        bool StateInitialized { get; set; }
        bool StateFinalization { get; }

        Task EnterAsync(IState previousState);
        void UpdateState();
        Task ExitAsync(IState nextState);
        
    }
}