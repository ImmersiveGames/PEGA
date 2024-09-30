using UnityEngine.SceneManagement;

namespace ImmersiveGames.SceneSystems.Interfaces
{
    public interface ILoadScene
    {
        bool RequiresSceneLoad { get; }
        string SceneName { get; }
        LoadSceneMode LoadMode { get; }
        bool UnLoadAdditiveScene { get; }
    }
}