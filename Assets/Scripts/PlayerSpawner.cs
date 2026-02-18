using UnityEngine;
using UnityEngine.Tilemaps;

public class PlayerSpawner : MonoBehaviour
{
    [Header("References")]
    public DungeonGenerator2D generator;   // LevelGenerator (has DungeonGenerator2D)
    public Tilemap floorMap;              // Grid/Floor
    public GameObject playerPrefab;       // Player prefab
    public CameraFollow2D cameraFollow;   // Main Camera's follow script (optional)

    [Header("Spawn")]
    public Vector3 worldOffset = new Vector3(0.5f, 0.5f, 0f); // center of tile
    public bool spawnOnStart = true;

    GameObject playerInstance;

    void Start()
    {
        if (spawnOnStart)
            Spawn();
    }

    [ContextMenu("Spawn")]
    public void Spawn()
    {
        // Find references if not assigned
        if (!generator) generator = FindFirstObjectByType<DungeonGenerator2D>();
        if (!generator)
        {
            Debug.LogError("PlayerSpawner: No DungeonGenerator2D found in scene.");
            return;
        }

        if (!floorMap) floorMap = generator.floorMap;
        if (!floorMap)
        {
            Debug.LogError("PlayerSpawner: Floor Tilemap not assigned and generator.floorMap is null.");
            return;
        }

        if (!playerPrefab)
        {
            Debug.LogError("PlayerSpawner: Player Prefab is not assigned.");
            return;
        }

        // Make sure generator actually has a spawn cell set
        Vector2Int spawnCell2D = generator.PlayerSpawnCell;
        Vector3Int spawnCell = new Vector3Int(spawnCell2D.x, spawnCell2D.y, 0);

        // Optional sanity check: does floor map have a tile at spawn?
        if (!floorMap.HasTile(spawnCell))
        {
            Debug.LogWarning($"PlayerSpawner: Spawn cell {spawnCell} has no floor tile. " +
                             "PlayerSpawnCell may not be set yet or generation order is wrong.");
        }

        Vector3 spawnWorld = floorMap.CellToWorld(spawnCell) + worldOffset;

        // Spawn/move player
        if (!playerInstance)
            playerInstance = Instantiate(playerPrefab, spawnWorld, Quaternion.identity);
        else
            playerInstance.transform.position = spawnWorld;
            
            var mini = FindFirstObjectByType<MinimapCameraFollow>();
if (mini) mini.target = playerInstance.transform;

var fog = FindFirstObjectByType<MinimapFogOfWar>();
if (fog) fog.player = playerInstance.transform;



        // Hook camera
        if (!cameraFollow && Camera.main) cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
        if (cameraFollow) cameraFollow.target = playerInstance.transform;
    }
}
