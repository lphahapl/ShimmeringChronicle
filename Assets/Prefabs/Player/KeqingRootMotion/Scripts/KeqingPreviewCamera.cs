using UnityEngine;

/// <summary>Camera for the separate player preview scene.</summary>
public class KeqingPreviewCamera : MonoBehaviour
{
    public Transform target;
    public float distance = 4.2f;
    public float yaw = 25f;
    public float pitch = 16f;
    Vector3 focus;

    void Start() { if (target != null) focus = target.position + Vector3.up * .95f; }
    void LateUpdate()
    {
        if (target == null) return;
        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * 3f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2f, 5f, 55f);
        }
        distance = Mathf.Clamp(distance - Input.GetAxis("Mouse ScrollWheel") * 3f, 2.4f, 7f);
        focus = Vector3.Lerp(focus, target.position + Vector3.up * .95f, 1f - Mathf.Exp(-12f * Time.deltaTime));
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = focus - transform.forward * distance;
    }
    void OnGUI()
    {
        GUI.Box(new Rect(16, 16, 530, 76), "刻晴 · 根运动动作预览\nWASD 移动 / Shift 跑步 / 左键三连击 / 右键旋转镜头\n独立预览玩家，原玩家未替换");
    }
}
