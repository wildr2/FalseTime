using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEditor;
using System.Linq;
using System.Collections;
using System.Collections.Generic;


public class Route : EventTrigger
{
    // References
    public RectTransform canvas;

    // Graphics
    private LineRenderer line;
    public Color default_color;
    private float dist;
    public Image highlight;

    // General
    private Planet p1, p2;

    // Time route
    private float tr_first = -1, tr_second = -1, tr_len = 0;
    private bool crossing = false;

    private static List<Pair<float, float>> tr_times_pool;
    private static int time_routes_count = 0;

    // Events
    public System.Action<Route> on_pointer_enter;
    public System.Action<Route> on_pointer_exit;
    public System.Action<Route> on_pointer_click;


    // PUBLIC ACCESSORS

    public bool IsCrossing()
    {
        return crossing;
    }
    public bool IsTimeRoute()
    {
        return tr_len > 0;
    }
    public bool IsTimeRoute(float time)
    {
        return GetTimeTravelTime(time) != time;
    }
    public float GetTimeTravelTime(float from_time)
    {
        if (tr_len > 0)
        {
            if (from_time >= tr_first && from_time < tr_first + tr_len)
            {
                return tr_second + from_time - tr_first;
            }
            else if (from_time >= tr_second && from_time < tr_second + tr_len)
            {
                return tr_first + from_time - tr_second;
            }
        }
        return from_time;
    }
    public int GetTimeTravelUV(float from_time)
    {
        return 1;
    }
    public float GetTRFirst()
    {
        return tr_first;
    }
    public float GetTRSecond()
    {
        return tr_second;
    }


    // PUBLIC MODIFIERS

    public void Initialize(Planet p1, Planet p2)
    {
        this.p1 = p1;
        this.p2 = p2;

        dist = Vector2.Distance(p1.transform.position, p2.transform.position);
        line.numPositions = 2;
        Vector2 pos1 = new Vector2(-dist / 2f, 0);
        Vector2 pos2 = -pos1;
        line.SetPosition(0, pos1);
        line.SetPosition(1, pos2);

        Vector2 p1pos = p1.transform.position;
        Vector2 p2pos = p2.transform.position;
        transform.position = Vector2.Lerp(p1pos, p2pos, 0.5f);
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * Mathf.Atan2(p2pos.y - p1pos.y, p2pos.x - p1pos.x));
        canvas.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, dist);

        // Time route
        if (Random.value < 0.5f)
        {
            if (time_routes_count >= tr_times_pool.Count)
            {
                Debug.LogWarning("Ran out of time route times");
            }
            else
            {
                tr_len = 5;

                tr_first = tr_times_pool[time_routes_count].First;
                tr_second = tr_times_pool[time_routes_count].Second;
                ++time_routes_count;

                if (Random.value < 0.5f)
                {
                    // Crossing time route route
                    crossing = true;
                    line.material.mainTextureScale = new Vector2(50f * (dist / 4f), 1);

                    int n = 15;
                    float phase = Random.value * 360f;
                    line.numPositions = n;
                    for (int i = 0; i < n; ++i)
                    {
                        float t = (float)i / n;
                        line.SetPosition(i, Vector2.Lerp(pos1, pos2, t)
                            + new Vector2(0, Mathf.Sin(t*180f + phase) * 0.1f));
                    }
                }
                else
                {   // Non crossing time route
                    line.material.mainTextureScale = new Vector2(15f * (dist / 4f), 1);

                    int n = 15;
                    float phase = Random.value * 360f;
                    line.numPositions = n;
                    for (int i = 0; i < n; ++i)
                    {
                        float t = (float)i / n;
                        line.SetPosition(i, Vector2.Lerp(pos1, pos2, t)
                            + new Vector2(0, Mathf.Sin(t * 180f + phase) * 0.1f));
                    }
                }
            }
        }
        if (!IsTimeRoute())
        {
            // Regular route
            line.material.mainTextureScale = new Vector2(15f * (dist / 4f), 1);
        }
    }
    public void UpdateVisuals(float time)
    {
        // Color
        line.startColor = p1.OwnerID == -1 ? default_color : p1.sprite_sr.color;
        line.endColor = p2.OwnerID == -1 ? default_color : p1.sprite_sr.color;

        // Time route
        if (tr_len > 0)
        {
            if (time > tr_first && time < tr_first + tr_len)
            {
                // Active (First tr time)
                ShowActiveTimeRoute();
            }
            else if (time > tr_second && time < tr_second + tr_len)
            {
                // Active (Second tr time)
                ShowActiveTimeRoute();
            }
            else
            {
                // Inactive
                ShowInactiveTimeRoute();
            }
        }
    }

    public void ShowHighlight(Color color)
    {
        highlight.gameObject.SetActive(true);
        highlight.color = color;
    }
    public void HideHighlight()
    {
        highlight.gameObject.SetActive(false);
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
    public override void OnPointerClick(PointerEventData eventData)
    {
        if (on_pointer_click != null) on_pointer_click(this);
        Tools.Log("here");
        base.OnPointerClick(eventData);
    }


    // PRIVATE MODIFIERS

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (tr_times_pool == null) GenerateTimeRoutePool();
    }
    private void GenerateTimeRoutePool()
    {
        // Generate possible time route times
        bool[] taken = new bool[100];

        int[] tr_first_pool = Tools.ShuffleArray(Enumerable.Range(2, 68).ToArray());
        int[] tr_second_pool = Tools.ShuffleArray(Enumerable.Range(25, 68).ToArray());

        tr_times_pool = new List<Pair<float, float>>();

        for (int i = 0; i < Mathf.Min(tr_first_pool.Length, tr_second_pool.Length); ++i)
        {
            int first = tr_first_pool[i];
            int second = tr_second_pool[i];
            if (!taken[first] && !taken[second] && second - first > 15)
            {
                tr_times_pool.Add(new Pair<float, float>(first, second));
                taken[first] = true;
                taken[second] = true;
            }
        }
    }

    private void ShowActiveTimeRoute()
    {
        line.startWidth = 0.2f;
        line.endWidth = 0.2f;
    }
    private void ShowInactiveTimeRoute()
    {
        line.startWidth = 0.03f;
        line.endWidth = 0.03f;
    }
}

[CustomEditor(typeof(Route))]
public class RouteEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
    }
}
