﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Fleet : MonoBehaviour
{
    public SpriteRenderer sprite_sr;
    private Text text;

    public int OwnerID { get; private set; }


    public void Initialize(int ownerID, int number_of_ships, Color player_color)
    {
        OwnerID = ownerID;
        text = GetComponentInChildren<Text>();
       
        // Ghost fleet
        if (number_of_ships == 0)
        {
            Vector3 scale = Vector3.one;
            scale.z = 1;
            transform.localScale = scale;
            text.color = player_color;
        }
        // Regular fleet
        else
        {
            Vector3 scale = Vector3.one * (0.8f + number_of_ships / 50f);
            scale.z = 1;
            transform.localScale = scale;
            text.text = number_of_ships.ToString();
            sprite_sr.color = player_color;
        }
    }
    public void SetPosition(Planet from, Planet to, float progress)
    {
        Vector2 dir = (to.transform.position - from.transform.position).normalized;
        Vector2 p0 = (Vector2)from.transform.position + dir * (from.Radius + 0.3f);
        Vector2 p1 = (Vector2)to.transform.position - dir * (to.Radius + 0.3f);

        Vector3 pos = Vector2.Lerp(p0, p1, progress);

        transform.position = pos;
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
