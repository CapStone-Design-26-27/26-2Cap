using UnityEngine;

[ExecuteAlways, RequireComponent(typeof(SpriteRenderer))]
public sealed class PlaySkinBackground : MonoBehaviour
{
    private void LateUpdate()
    {
        var camera = GetComponentInParent<Camera>();
        var sprite = GetComponent<SpriteRenderer>().sprite;
        if (camera == null || sprite == null || !camera.orthographic) return;
        var size = sprite.bounds.size;
        float height = 2f * camera.orthographicSize;
        float scale = Mathf.Max(height * camera.aspect / size.x, height / size.y);
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
