
using UnityEngine;
using UnityEngine.UI;
using TMPro;
public class Itemcard : MonoBehaviour
{

    public ItemData itemData;

    public GameObject cardPrefab;
    public GameObject modelPrefab;

    public Transform cardsParent;
    // public Button itemButton;
    public TMP_Text cardName;

    public TMP_Text totalcards;
    private bool isOpen;
    private bool generated;
    public Transform models;
    void Start()
    {
        // SetModelLightIntensity();
        if (cardName != null)
            cardName.text = itemData.itemName;

        if (totalcards != null)
            totalcards.text = itemData.TotalCards.ToString();

        if (modelPrefab == null)
        {
            Debug.LogError("Model Prefab is missing!");
            return;
        }

        if (models == null)
        {
            Debug.LogError("Models Transform is missing!");
            return;
        }

        GameObject spawnedModel = Instantiate(modelPrefab, models);

        spawnedModel.transform.localPosition = Vector3.zero;
        spawnedModel.transform.localRotation = Quaternion.identity;
        spawnedModel.transform.localScale = Vector3.one;

        Light[] lights = spawnedModel.GetComponentsInChildren<Light>(true);

        // foreach (Light light in lights)
        // {
        //     light.intensity = itemData.lightIntensity;
        // }

    }
    void Awake()
    {


        if (cardsParent != null)
            cardsParent.gameObject.SetActive(false);
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("Mouse Clicked!");

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 1000f))
            {
                Debug.Log("Hit Object: " + hit.transform.name);

                if (hit.transform == transform ||
                    hit.transform.IsChildOf(transform))
                {
                    Debug.Log("ItemCard Clicked!");
                    ToggleCards();
                }
            }
            else
            {
                Debug.Log("Nothing Hit!");
            }
        }
    }

    public void ToggleCards()
    {
        if (itemData == null)
        {
            Debug.LogError("ItemData is not assigned!");
            return;
        }

        if (!generated)
        {
            GenerateCards();
            generated = true;
        }

        isOpen = !isOpen;

        if (cardsParent != null)
            cardsParent.gameObject.SetActive(isOpen);
    }
    void GenerateCards()
    {
        if (cardPrefab == null || cardsParent == null)
        {
            Debug.LogError("Card Prefab or Cards Parent is not assigned!");
            return;
        }

        int generatedCount = 0;

        foreach (CardEntry entry in itemData.cards)
        {
            if (entry == null || entry.card == null)
                continue;

            GameObject newCard = Instantiate(cardPrefab, cardsParent, false);
            Card card = newCard.GetComponent<Card>();

            if (card == null)
            {
                Debug.LogError("Card component is missing!");
                Destroy(newCard);
                continue;
            }

            card.Setup(entry.card, modelPrefab, entry.count);

            generatedCount++;

            Debug.Log(
                "Generated: " + entry.card.cardName +
                " | Count: " + entry.count
            );
        }

        Debug.Log("Total Card Types Generated: " + generatedCount);
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
