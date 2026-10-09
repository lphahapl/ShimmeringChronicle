using UnityEngine;

[RequireComponent(typeof(SphereCollider), typeof(Rigidbody))]
public class DropItem : MonoBehaviour
{
    public string id;
    public int count;
    public float speed = 90f;
    public ItemData data;

    void Awake()
    {
        GetComponent<SphereCollider>().isTrigger = true;
        var body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
    }

    void Update()
    {
        transform.Rotate(0, speed * Time.deltaTime, 0);
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerObj player = other.GetComponentInParent<PlayerObj>();
        if (player == null) return;

        count -= player.AddItem(id, count);
        if (data != null) data.count = count;
        if (count <= 0)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }

    public static DropItem Spawn(ItemData item, Vector3 position)
    {
        if (item == null || item.prefab == null || item.count <= 0) return null;

        GameObject obj = Instantiate(item.prefab, position, Quaternion.identity);
        DropItem drop = obj.GetComponent<DropItem>();
        if (drop == null) drop = obj.AddComponent<DropItem>();
        drop.data = item.Clone();
        drop.id = item.id;
        drop.count = item.count;
        obj.SetActive(true);
        return drop;
    }
}
