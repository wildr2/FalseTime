using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PowerBar : MonoBehaviour
{
    private RectTransform rect;
    public RectTransform fill, goal_indicator;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
    }

    public void SetFill(float amount)
    {
        fill.offsetMax = new Vector2(fill.offsetMax.x, (1-amount) * -rect.rect.height);
    }
    public void SetFillGoal(float amount, Color color)
    {
        goal_indicator.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, amount * rect.rect.height);
        goal_indicator.GetComponent<Image>().color = color;
        //goal_indicator.anchoredPosition = new Vector2(goal_indicator.anchoredPosition.x,
        //amount * rect.rect.height);
    }
}
