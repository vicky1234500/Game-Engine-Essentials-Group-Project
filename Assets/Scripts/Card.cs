
using UnityEngine;
using TMPro;

public class Card : MonoBehaviour
{
    public CardData cardData;

    [Header("Card Information")]
    public Transform modelHolder;
    public TMP_Text cardName;
    public TMP_Text cardType;
    public TMP_Text manaCost;
    public TMP_Text value;
    public TMP_Text description;
    public TMP_Text cardNum;

    [Header("Model")]
    public Transform models;

    public void Setup(CardData data, GameObject sharedModelPrefab, int count)
    {
        cardData = data;

        if (cardData == null)
        {
            Debug.LogError("CardData is null!");
            return;
        }

        if (cardName != null)
            cardName.text = cardData.cardName;

        if (cardType != null)
            cardType.text = cardData.type.ToString();

        if (manaCost != null)
            manaCost.text = cardData.manaCost.ToString();

        if (value != null)
            value.text = cardData.value.ToString();

        if (description != null)
            description.text = cardData.description;

        if (cardNum != null)
            cardNum.text = count.ToString();

        if (models == null)
        {
            Debug.LogError("Card Models is not assigned!");
            return;
        }


        if (sharedModelPrefab != null)
        {
            GameObject spawnedModel = Instantiate(sharedModelPrefab, models);

            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
            spawnedModel.transform.localScale = Vector3.one;
            NormalizeAndFitModel(spawnedModel);
        }
    }
    void NormalizeAndFitModel(GameObject modelObj)
    {

        Renderer[] renderers = modelObj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return;


        Bounds combinedBounds = renderers[0].bounds;
        foreach (Renderer r in renderers)
        {
            combinedBounds.Encapsulate(r.bounds);
        }

        Vector3 localCenter = modelObj.transform.InverseTransformPoint(combinedBounds.center);
        modelObj.transform.localPosition -= localCenter;

        float maxDimension = Mathf.Max(combinedBounds.size.x, Mathf.Max(combinedBounds.size.y, combinedBounds.size.z));

        float targetSize = 1.0f;

        if (maxDimension > 0f)
        {
            float scaleFactor = targetSize / maxDimension;
            modelObj.transform.localScale *= scaleFactor;
        }
    }
}
