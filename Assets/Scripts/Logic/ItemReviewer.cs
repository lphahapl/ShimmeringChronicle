using System.Collections.Generic;
using UnityEngine;

public class ItemReviewer : IItemProvider
{
    readonly List<ItemData> items;
    public ItemReviewer(List<ItemData> items)
    {
        this.items = items ?? new List<ItemData>();
    }
    public bool ContainsItem(string id) => ItemRules.FindItem(items, id) >= 0;


    public List<ItemData> GetItemList()
    {
       return items;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
