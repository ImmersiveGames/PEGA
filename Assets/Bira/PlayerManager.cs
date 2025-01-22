using System.Collections.Generic;
using UnityEngine;
using Cinemachine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerManager : MonoBehaviour
{
    private List<PlayerInput> players = new List<PlayerInput>();
    [SerializeField]
    private List<Transform> startingPoints;
    [SerializeField]
    private List<LayerMask> playerLayers;
    [SerializeField]
    private Image verticalFrame = null;
    [SerializeField]
    private Image horizontalFrame = null;

    private PlayerInputManager playerInputManager;

    private void Awake()
    {
        playerInputManager = FindObjectOfType<PlayerInputManager>();
    }

    private void OnEnable()
    {
        playerInputManager.onPlayerJoined += AddPlayer;
    }

    private void OnDisable()
    {
        playerInputManager.onPlayerJoined -= AddPlayer;
    }

    public void AddPlayer(PlayerInput player)
    {
        players.Add(player);

        //need to use the parent due to the structure of the prefab
        
        var playerParent = player.transform.parent;
        player.GetComponentInChildren<CharacterController>().enabled = false;
        playerParent.position = startingPoints[players.Count - 1].position;
        player.GetComponentInChildren<CharacterController>().enabled = true;

        //convert layer mask (bit) to an integer 
        var layerToAdd = (int)Mathf.Log(playerLayers[players.Count - 1].value, 2);

        //set the layer
        playerParent.GetComponentInChildren<CinemachineVirtualCamera>().gameObject.layer = layerToAdd;
        //add the layer
        playerParent.GetComponentInChildren<Camera>().cullingMask |= 1 << layerToAdd;
        //set the action in the custom cinemachine Input Handler
        //playerParent.GetComponentInChildren<InputHandler>().horizontal = player.actions.FindAction("Look");
        player.GetComponentInChildren<PlayersIndicators>().SetPlayerColor(player.playerIndex);

        if (players.Count == 2) 
        {
            verticalFrame.enabled = true;
            horizontalFrame.enabled = false;
        }
        else if (players.Count > 2) 
        {
            verticalFrame.enabled = true;
            horizontalFrame.enabled = true;
        }
        else 
        {
            verticalFrame.enabled = false;
            horizontalFrame.enabled = false;
        }

    }
}