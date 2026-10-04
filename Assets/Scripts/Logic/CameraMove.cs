using UnityEngine;

public class CameraMove : MonoBehaviour
{
    public Transform player;
    public float pivotHeight = 1.5f;

    public Vector3 offset = new Vector3(0.6f, 0.2f, -3f);

    public float mouseSensitivity = 3f;
    public float minPitch = -60f;
    public float maxPitch = 70f;

    private float yaw;//绕y旋转
    private float pitch;//绕x旋转

    void Start()
    {
        if (player == null)
        {
            var obj = GameObject.FindGameObjectWithTag("Player");
            if (obj != null)
                player = obj.transform;
        }

        yaw = transform.eulerAngles.y;
        pitch = Mathf.DeltaAngle(0f, transform.eulerAngles.x);//转到正负180之间
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
    }

    void LateUpdate()
    {
        if (player == null) return;
            yaw += Input.GetAxis("Mouse X") * mouseSensitivity;
            pitch -= Input.GetAxis("Mouse Y") * mouseSensitivity;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        
        //计算相机旋转
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 pivot =
            player.position + Vector3.up * pivotHeight;
        transform.position = pivot + rotation * offset;
        transform.rotation = rotation;
        
    }
}