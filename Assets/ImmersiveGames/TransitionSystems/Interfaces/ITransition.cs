using System.Threading.Tasks;

namespace ImmersiveGames.TransitionSystems.Interfaces
{
    public interface ITransition
    {
        Task InTransitionAsync();
        Task OutTransitionAsync();
    }
}