using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayersIndicators : MonoBehaviour
{
    [SerializeField] Image playerColorMark;
    [SerializeField] Color[] playerColors;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SetPlayerColor(int colorindex) 
    {
        playerColorMark.color = playerColors[colorindex];
    }
}
