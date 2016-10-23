﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Planet : MonoBehaviour
{
    private GameManager gm;

    public Color neutral_color;

    private Text text;
    public Image raycast_image;
    public SpriteRenderer sprite_sr;
    public SpriteRenderer highlight_sr;
    public MeshRenderer sphere;

    public int PlanetID { get; private set; }
    public float Size { get; private set; }
    public int Pop { get; private set; }
    public int OwnerID { get; private set; }

    public float Radius { get; private set; }


    // Events
    public System.Action<Planet> on_pointer_enter;
    public System.Action<Planet> on_pointer_exit;


    public float GetPopPerSecond(int ownerID)
    {
        return ownerID == -1 ? 0 : Size * 1;
    }

    public void Initialize(int id, float size, int pop, int ownerID)
    {
        gm = FindObjectOfType<GameManager>();
        text = GetComponentInChildren<Text>();

        PlanetID = id;

        // Size
        Size = size;
        Radius = size / 2.5f;
        sphere.transform.localScale = Vector3.one * (Radius * 2f);
        sprite_sr.transform.localScale = Vector3.one * (Radius * 2f + 0.05f);
        highlight_sr.transform.localScale = Vector3.one * (Radius * 2f + 0.05f);
        raycast_image.transform.localScale = Vector3.one * Radius * 2f;
        
        // Pop
        SetPop(pop, ownerID);

        // Color
        //sphere.material.color = Color.Lerp(Color.Lerp(new Color(Random.value, Random.value, Random.value), Color.white, 0.6f), Color.black, 0f);
        //sphere.material.color = neutral_color; //HsvToRgb(Random.value * 360, 0f, 0.3f);
        //sphere.material.color = Color.Lerp(sprite_sr.color, Color.black, 0.5f);
        text.color = Color.black;

        // Rotation
        sphere.transform.rotation = Quaternion.Euler(0, Random.value * 360f, 0);
    }
    public void ShowHighlight(Color color)
    {
        highlight_sr.gameObject.SetActive(true);
        highlight_sr.color = Color.Lerp(color, Color.black, 0.5f);
    }
    public void HideHighlight()
    {
        highlight_sr.gameObject.SetActive(false);
    }
    public void SetPop(int pop)
    {
        Pop = pop;
        text.text = pop.ToString();
    }
    public void SetPop(int pop, int ownerID)
    {
        Pop = pop;
        text.text = pop.ToString();

        OwnerID = ownerID;
        sprite_sr.color = ownerID == -1 ? neutral_color : gm.player_colors[ownerID];
        //text.color = ownerID == -1 ? Color.black : sprite_sr.color;
        text.color = ownerID == -1 ? neutral_color : sprite_sr.color;
        //inner_sprite_sr.color = Color.Lerp(sprite_sr.color, Color.black, 0.8f);
        //sphere.material.color = Color.Lerp(sprite_sr.color, Color.black, 0.5f);

        //ParticleSystem ps = GetComponentInChildren<ParticleSystem>();
        //if (OwnerID == -1)
        //{
        //    ps.Stop();
        //    ps.Clear();
        //}
        //else
        //{
        //    ps.startColor = sprite_sr.color;
        //    ps.Play();
        //}
    }


    public void OnPointerEnter()
    {
        if (on_pointer_enter != null) on_pointer_enter(this);
    }
    public void OnPointerExit()
    {
        if (on_pointer_exit != null) on_pointer_exit(this);
    }


    private void Update()
    {
        //sphere.transform.Rotate(0, Time.deltaTime * 20, 0);
    }

    /// <summary>
    /// Convert HSV to RGB
    /// h is from 0-360
    /// s,v values are 0-1
    /// Based upon http://ilab.usc.edu/wiki/index.php/HSV_And_H2SV_Color_Space#HSV_Transformation_C_.2F_C.2B.2B_Code_2
    /// </summary>
    private Color HsvToRgb(float h, float S, float V)
    {
        float H = h;
        while (H < 0) { H += 360; };
        while (H >= 360) { H -= 360; };
        float R, G, B;
        if (V <= 0)
        { R = G = B = 0; }
        else if (S <= 0)
        {
            R = G = B = V;
        }
        else
        {
            float hf = H / 60f;
            int i = (int)Mathf.Floor(hf);
            float f = hf - i;
            float pv = V * (1 - S);
            float qv = V * (1 - S * f);
            float tv = V * (1 - S * (1 - f));
            switch (i)
            {

                // Red is the dominant color

                case 0:
                    R = V;
                    G = tv;
                    B = pv;
                    break;

                // Green is the dominant color

                case 1:
                    R = qv;
                    G = V;
                    B = pv;
                    break;
                case 2:
                    R = pv;
                    G = V;
                    B = tv;
                    break;

                // Blue is the dominant color

                case 3:
                    R = pv;
                    G = qv;
                    B = V;
                    break;
                case 4:
                    R = tv;
                    G = pv;
                    B = V;
                    break;

                // Red is the dominant color

                case 5:
                    R = V;
                    G = pv;
                    B = qv;
                    break;

                // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                case 6:
                    R = V;
                    G = tv;
                    B = pv;
                    break;
                case -1:
                    R = V;
                    G = pv;
                    B = qv;
                    break;

                // The color is not defined, we should throw an error.

                default:
                    //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                    R = G = B = V; // Just pretend its black/white
                    break;
            }
        }
        return new Color(R, G, B);
    }
 
}


