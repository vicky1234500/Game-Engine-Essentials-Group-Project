[System.Serializable]
public class ShopSlot
{
    public ItemData item;
    public int price; // after discount
    public bool sold;

    public ShopSlot(ItemData item, int price)
    {
        this.item = item;
        this.price = price;
    }

    public void MarkSold() => sold = true;
    public void ResetSlot() => sold = false;
}