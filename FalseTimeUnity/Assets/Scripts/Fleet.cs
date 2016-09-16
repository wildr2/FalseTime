using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Fleet : MonoBehaviour
{
    public SpriteRenderer sprite_sr;
    private Text text;

    public int OwnerID { get; private set; }


    public void Initialize(int ownerID, int number_of_ships, Color player_color)
    {
        text = GetComponentInChildren<Text>();

        OwnerID = ownerID;
        sprite_sr.color = player_color;
        text.text = number_of_ships.ToString();
        transform.localScale = Vector3.one * (0.8f + number_of_ships / 50f);
    }
    public void SetPosition(Planet from, Planet to, float progress)
    {
        Vector2 dir = (to.transform.position - from.transform.position).normalized;
        Vector2 p0 = (Vector2)from.transform.position + dir * (from.Radius + 0.3f);
        Vector2 p1 = (Vector2)to.transform.position - dir * (to.Radius + 0.3f);

        transform.position = Vector3.Lerp(p0, p1, progress);
        sprite_sr.transform.rotation = LookRotation2D(dir, -90);
    }
    public void SetAlpha(float a)
    {
        text.color = Tools.SetColorAlpha(text.color, a);
        sprite_sr.color = Tools.SetColorAlpha(sprite_sr.color, a);
    }

    private Quaternion LookRotation2D(Vector2 forward, float deg_offset=0)
    {
        return Quaternion.Euler(0, 0, Mathf.Atan2(forward.y, forward.x)*Mathf.Rad2Deg + deg_offset);
    }
}
