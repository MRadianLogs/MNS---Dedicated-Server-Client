using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public static Dictionary<int, PlayerManager> players = new Dictionary<int, PlayerManager>();

    public GameObject localPlayerPrefab;
    public GameObject playerPrefab;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.Log("Instance already exists! Destroying object!");
            Destroy(this);
        }
    }

    public void SpawnPlayer(int id, string username, Vector3 position, Quaternion rotation)
    {
        GameObject player;
        if(id == Client.instance.clientID)
        {
            player = Instantiate(localPlayerPrefab, position, rotation); 
        }
        else
        {
            player = Instantiate(playerPrefab, position, rotation);
        }

        PlayerManager playerManager = player.GetComponentInChildren<PlayerManager>();
        playerManager.id = id;
        playerManager.username = username;

        players.Add(id, playerManager);
    }
}
