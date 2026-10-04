using UnityEngine;

public interface ICanChangeHandingItem
{
    public ItemData GetHandingItem();
    public bool ChangeItem(ItemData itemData);
}
