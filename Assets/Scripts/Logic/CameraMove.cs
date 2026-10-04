using UnityEngine;

public class CameraMove : MonoBehaviour
{
    public Transform player;
    public float pivotHeight = 1.5f;

    public Vector3 offset = new Vector3(0.6f, 0.2f, -3f);
    public LayerMask barrierLayer;
    public float mouseSensitivity = 3f;
    public float minPitch = -60f;
    public float maxPitch = 70f;

    private float yaw;//绕y旋转
    private float pitch;//绕x旋转


    void Start()
    {
        barrierLayer = LayerMask.GetMask("Envirnment");
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
        Vector3 desiredPos = pivot + rotation * offset;
        Vector3 dir = desiredPos - pivot;
        if (Physics.Linecast(pivot, desiredPos, out RaycastHit hit, barrierLayer))
        {
            float safeDistance = Mathf.Max(0f, hit.distance - 0.05f);
            desiredPos = pivot + dir.normalized * safeDistance;
        }
        transform.position = desiredPos;
        float rotationT = 1f - Mathf.Exp(-50f * Time.deltaTime);
        transform.rotation = rotation;
            //Quaternion.Slerp(transform.rotation, rotation, rotationT);

    }
}