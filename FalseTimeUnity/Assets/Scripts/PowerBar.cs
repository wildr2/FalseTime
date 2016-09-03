using UnityEngine;
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
    public void SetFillGoal(float amount)
    {
        goal_indicator.anchoredPosition = new Vector2(goal_indicator.anchoredPosition.x,
            amount * rect.rect.height);
    }
}
