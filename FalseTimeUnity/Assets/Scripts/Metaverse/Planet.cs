using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;


public class Planet : EventTrigger
{
    // General
    public int PlanetID { get; private set; }
    public float Radius { get; private set; }

    public float Size { get; private set; }
    public int Pop { get; private set; }
    public int OwnerID { get; private set; }
    public bool Ready { get; private set; }

    public Image raycast_image;

    // Graphics
    public Color neutral_color;
    private Text text;
    public SpriteRenderer sprite_sr;
    public SpriteRenderer highlight_sr;
    public MeshRenderer sphere;

    // Flags
    public Transform flags_parent;
    public Image flag_prefab;
    private Image[][] flags; // universe_id, player_id

    // Events
    public System.Action<Planet> on_pointer_enter;
    public System.Action<Planet> on_pointer_exit;


    // PUBLIC ACCESSORS

    public float GetPopPerSecond(int ownerID)
    {
        return ownerID == -1 ? 0 : Size * 1.2f;
    }


    // PUBLIC MODIFIERS

    public void Initialize(int id, float size, int pop, int ownerID)
    {
        DataManager dm = DataManager.Instance;

        text = GetComponentInChildren<Text>();

        PlanetID = id;

        // Size
        SetSize(size);

        // Pop
        SetPop(pop, ownerID);

        // Color
        //sphere.material.color = Color.Lerp(Color.Lerp(new Color(Random.value, Random.value, Random.value), Color.white, 0.6f), Color.black, 0f);
        //sphere.material.color = neutral_color; //HsvToRgb(Random.value * 360, 0f, 0.3f);
        //sphere.material.color = Color.Lerp(sprite_sr.color, Color.black, 0.5f);

        // Rotation
        sphere.transform.rotation = Quaternion.Euler(0, Random.value * 360f, 0);

        // Flags
        flags = new Image[dm.num_universes][];
        for (int i = 0; i < dm.num_universes; ++i)
        {
            flags[i] = new Image[dm.GetNumPlayers()];
            for (int j = 0; j < flags[i].Length; ++j)
            {
                flags[i][j] = Instantiate(flag_prefab);
                flags[i][j].transform.SetParent(flags_parent, false);
                flags[i][j].color = dm.GetPlayerColor(j);
                flags[i][j].gameObject.SetActive(false);
            }
        }
    }
    public void SetSize(float size)
    {
        Size = size;
        Radius = size / 2.5f;
        sphere.transform.localScale = Vector3.one * (Radius * 2f);
        sprite_sr.transform.localScale = Vector3.one * (Radius * 2f + 0.05f);
        highlight_sr.transform.localScale = Vector3.one * (Radius * 2f + 0.05f);
        raycast_image.transform.localScale = Vector3.one * Radius * 2f;
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

    public void SetReady(bool ready)
    {
        Ready = ready;
        text.color = ready ? sprite_sr.color : neutral_color;
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
        sprite_sr.color = ownerID == -1 ? neutral_color : DataManager.Instance.GetPlayerColor(ownerID);
        text.color = (ownerID == -1 || !Ready) ? neutral_color : sprite_sr.color;
    }
    public void ShowFlag(int player_id, int universe_id, bool show = true)
    {
        flags[universe_id][player_id].gameObject.SetActive(show);
    }
    public void FlashFlag(int player_id, int universe_id)
    {
        StartCoroutine(FlashFlag(flags[universe_id][player_id]));
    }

    public override void OnPointerEnter(PointerEventData eventData)
    {
        if (on_pointer_enter != null) on_pointer_enter(this);
        base.OnPointerEnter(eventData);
    }
    public override void OnPointerExit(PointerEventData eventData)
    {
        if (on_pointer_exit != null) on_pointer_exit(this);
        base.OnPointerExit(eventData);
    }


    // PRIVATE MODIFIERS

    private IEnumerator FlashFlag(Image flag)
    {
        // Wait for flag to be shown
        while (!flag.gameObject.activeInHierarchy)
            yield return null;

        // Shrink
        Vector3 scale = flag.rectTransform.localScale;
        for (float t = 0; t < 1; t += UnityEngine.Time.deltaTime * 2f)
        {
            float s = Mathf.Lerp(10, 1, 1 - Mathf.Pow(1 - t, 2));
            flag.rectTransform.localScale = scale * s;

            yield return null;
        }
        flag.rectTransform.localScale = scale;

        // Flash
        for (int i = 0; i < 16; ++i)
        {
            float s = i % 2 == 0 ? 2f : 1;
            flag.rectTransform.localScale = scale * s;

            yield return new WaitForSeconds(0.25f);
        }
    }


    // HELPERS

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


