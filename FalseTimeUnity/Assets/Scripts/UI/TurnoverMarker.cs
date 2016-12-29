using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TurnoverMarker : MonoBehaviour
{
    public RectTransform rt;
    private Image image;

    public Turnover Turnover { get; private set; }
    public float Size { get; private set; }


    public void Awake()
    {
        image = GetComponent<Image>();
    }
    public void Initialize(Turnover Turnover, Color color, bool alert)
    {
        this.Turnover = Turnover;
        SetSize();
        image.color = color;

        if (alert) StartCoroutine(AnimateAlert());
    }
    public void UpdateTurnover(Turnover updated_to)
    {
        Turnover = updated_to;
        SetSize();
    }
    public void Remove()
    {
        StopAllCoroutines();
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Size);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Size);
        StartCoroutine(AnimateOut());
    }


    private void SetSize()
    {
        Size = Mathf.Lerp(5, 40, Turnover.new_pop / 200f);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Size);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Size);
    }
    private IEnumerator AnimateAlert()
    {
        // Shrink
        for (float t = 0; t < 1; t += Time.deltaTime * 2f)
        {
            if (rt == null) yield break;

            float s = Mathf.Lerp(Size * 4f, Size, 1 - Mathf.Pow(1 - t, 2));
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, s);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, s);

            yield return null;
        }

        if (rt == null) yield break;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Size);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Size);

        // Flash
        for (int i = 0; i < 16; ++i)
        {
            if (rt == null) yield break;

            float s = i % 2 == 0 ? Size * 2f : Size;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, s);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, s);
            yield return new WaitForSeconds(0.25f);
        }
    }
    private IEnumerator AnimateOut()
    {
        Image img = GetComponent<Image>();
        Color color0 = img.color;

        // Flash
        for (int i = 0; i < 8; ++i)
        {
            img.color = i % 2 == 0 ? Color.clear : color0;
            yield return new WaitForSeconds(0.1f);
        }

        // Fade
        for (float t = 0; t < 1; t += UnityEngine.Time.deltaTime / 5f)
        {
            img.color = Color.Lerp(color0, Color.clear, t);
            yield return null;
        }

        // Destroy
        Destroy(gameObject);
    }

}
