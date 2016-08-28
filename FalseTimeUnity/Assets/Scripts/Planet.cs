using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class Planet : MonoBehaviour
{
    public Color neutral_color;
    public Color[] player_colors;

    private Text text;
    public SpriteRenderer sprite_sr;
    public SpriteRenderer highlight_sr;

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
        text = GetComponentInChildren<Text>();

        PlanetID = id;

        // Size
        Size = size;
        Radius = size / 2.5f;
        sprite_sr.transform.localScale = Vector3.one * Radius * 2f;
        highlight_sr.transform.localScale = Vector3.one * (Radius * 2f + 0.05f);

        // Pop
        SetPop(pop, ownerID);
    }
    public void ShowHighlight(Color color)
    {
        highlight_sr.gameObject.SetActive(true);
        highlight_sr.color = color;
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
        sprite_sr.color = ownerID == -1 ? neutral_color : player_colors[ownerID];
    }


    public void OnPointerEnter()
    {
        if (on_pointer_enter != null) on_pointer_enter(this);
    }
    public void OnPointerExit()
    {
        if (on_pointer_exit != null) on_pointer_exit(this);
    }
}
