using UnityEngine;

public class StorageToggle : MonoBehaviour
{
    [SerializeField] private GameObject storage;
    [SerializeField] private GameObject storageContent;

    private bool isOpen = false;

    void Start()
    {
        storageContent.SetActive(true);

        storage.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            isOpen = !isOpen;

            storage.SetActive(isOpen);

        }
    }
}
