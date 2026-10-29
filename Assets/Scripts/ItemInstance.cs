using UnityEngine;

// One item sitting in the backpack (the ScriptableObject is the template, this is the copy you own).
public class ItemInstance
{
    public ItemData data;
    public Vector2Int position;   // top-left slot
    public bool rotated;
    public ItemView view;

    public ItemInstance(ItemData data) { this.data = data; }

    public Vector2Int Size => rotated ? new Vector2Int(data.size.y, data.size.x) : data.size;
}
