using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class MinimapRTBinder : MonoBehaviour
{
    public RenderTexture rt;

    void Awake()
    {
        Apply();
    }

    void OnEnable()
    {
        Apply();
    }

    [ContextMenu("Apply RT")]
    public void Apply()
    {
        var ri = GetComponent<RawImage>();
        if (!rt)
        {
            Debug.LogError("MinimapRTBinder: RT is not assigned.");
            return;
        }

        ri.texture = rt;
        ri.color = Color.white; // fully visible
        Debug.Log("MinimapRTBinder: Bound RenderTexture to Minimap RawImage.");
    }
}
