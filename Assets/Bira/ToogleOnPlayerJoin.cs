using UnityEngine;
using UnityEngine.InputSystem;

public class ToggleOnPlayerJoin : MonoBehaviour
{
    [SerializeField] bool shouldActivate = false;
    [SerializeField] Rect viewportAdjustFor3Players;

    private PlayerInputManager playerInputManager;


    private void Awake()
    {
        playerInputManager = FindObjectOfType<PlayerInputManager>();
    }

    private void OnEnable()
    {
        playerInputManager.onPlayerJoined += ToggleThis;
   
    }

    private void OnDisable()
    {
        playerInputManager.onPlayerJoined -= ToggleThis;
    }

    void Start()
    {
        
    }   

    private void ToggleThis(PlayerInput player)
    {
        GetComponent<Camera>().enabled = shouldActivate;

        Debug.Log(playerInputManager.playerCount);

        if (playerInputManager.playerCount == 3)
        {
            GetComponent<Camera>().enabled = true;
            GetComponent<Camera>().rect = viewportAdjustFor3Players;
        }

    }
}