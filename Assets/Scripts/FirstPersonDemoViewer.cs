using UnityEngine;

public sealed class FirstPersonDemoViewer : MonoBehaviour
{
    public Camera sceneCamera;
    public Renderer floor;
    public Renderer console;

    private void Start()
    {
        if (sceneCamera == null) sceneCamera = Camera.main;
        if (sceneCamera == null) return;

        if (floor == null) floor = FindRendererById("r4");
        if (console == null) console = FindRendererById("r646");
        float ground = floor != null ? floor.bounds.max.y : 0f;
        GameObject player = new GameObject("First Person Player");
        Vector3 origin = console != null ? console.bounds.center : sceneCamera.transform.position;
        Vector3 direction = sceneCamera.transform.position - origin;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
        direction.Normalize();
        float radius = console != null
            ? Mathf.Abs(direction.x) * console.bounds.extents.x
                + Mathf.Abs(direction.z) * console.bounds.extents.z
            : 0f;
        player.transform.position = new Vector3(origin.x + direction.x * (radius + 1f),
            ground, origin.z + direction.z * (radius + 1f));
        player.transform.rotation = Quaternion.LookRotation(-direction, Vector3.up);
        FirstPersonWalker walker = player.AddComponent<FirstPersonWalker>();
        walker.viewCamera = sceneCamera;
        walker.floorHeight = ground;
        walker.lockCursorOnStart = false;
    }

    private static Renderer FindRendererById(string marker)
    {
        foreach (Renderer renderer in FindObjectsOfType<Renderer>())
        {
            string name = renderer.name;
            int index = name.IndexOf(marker, System.StringComparison.Ordinal);
            if (index >= 0 && (index == 0 || name[index - 1] == '_')
                && (index + marker.Length == name.Length || name[index + marker.Length] == '_'))
                return renderer;
        }
        return null;
    }
}
