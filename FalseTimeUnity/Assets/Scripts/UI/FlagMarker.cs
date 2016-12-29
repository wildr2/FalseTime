using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class FlagMarker : MonoBehaviour
{
    [System.NonSerialized] public RectTransform rt;
    [System.NonSerialized] public Image image;

    public void Initialize(Color color)
    {
        image.color = color;
        StartCoroutine(Animate());
    }

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        image = GetComponent<Image>();
    }
    private IEnumerator Animate()
    {
        for (float t = 0; t < 1; t += Time.deltaTime * 1f)
        {
            float t2 = Mathf.Pow(t, 4);

            float y = Mathf.Lerp(0, 100, t2);
            Vector3 pos = rt.anchoredPosition;
            pos.y = y;
            rt.anchoredPosition = pos;

            float alpha = Mathf.Lerp(1, 0, t2);
            image.color = Tools.SetColorAlpha(image.color, alpha);

            yield return null;
        }

        Destroy(gameObject);
    }
}
