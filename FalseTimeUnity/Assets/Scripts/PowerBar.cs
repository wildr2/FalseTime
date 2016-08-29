using UnityEngine;
using System.Collections;

public class PowerBar : MonoBehaviour
{
    public Transform fill;

    public void SetFilled(bool filled)
    {
        fill.gameObject.SetActive(filled);
    }
}
