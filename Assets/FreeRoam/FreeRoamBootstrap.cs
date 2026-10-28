using UnityEngine;

// Builds the whole first milestone when Play starts, so the scene needs no hand editing.
public static class FreeRoamBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        var root = new GameObject("FreeRoam").transform;
        CityBuilder.Build(root);
        var car = CarController.Create(root, new Vector3(0f, 0.6f, 20f));   // on the north-south road
        var player = PlayerController.Create(root, new Vector3(0f, 0.1f, 0f)); // at the crossroads
        player.car = car;
        var cam = player.GetComponentInChildren<Camera>();
        player.aim = cam.gameObject.AddComponent<WeaponAim>();
        player.aim.Init(cam);
    }
}
