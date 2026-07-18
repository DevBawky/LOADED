using UnityEngine;

public class BoardTile : MonoBehaviour
{
    [SerializeField] private GameObject warningObject;

    private void Awake()
    {
        SetWarningActive(false);
    }

    public void SetWarningActive(bool isActive)
    {
        if (warningObject != null)
        {
            warningObject.SetActive(isActive);
        }
    }
}
