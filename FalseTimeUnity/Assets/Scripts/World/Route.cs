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
    public Color default_color;
    public Image highlight;

    private LineRenderer line;
    private Vector2 line_start, line_end;
    private float line_dist;

    // General
    private Planet planet1, planet2;

    // Wormholes
    public Wormhole Wormhole { get; private set; }

    private static List<Pair<float, float>> wh_times_pool;
    private static int num_wormholes = 0;

    // Events
    public System.Action<Route> on_pointer_enter;
    public System.Action<Route> on_pointer_exit;
    public System.Action<Route> on_pointer_click;


    // PUBLIC ACCESSORS


    // PUBLIC MODIFIERS

    public void Initialize(Planet planet1, Planet planet2)
    {
        this.planet1 = planet1;
        this.planet2 = planet2;

        // Line
        line_dist = Vector2.Distance(planet1.transform.position, planet2.transform.position);
        line_start = new Vector2(-line_dist / 2f, 0);
        line_end = -line_start;

        line.numPositions = 2;
        line.SetPosition(0, line_start);
        line.SetPosition(1, line_end);

        // Positioning
        Vector2 p1pos = planet1.transform.position;
        Vector2 p2pos = planet2.transform.position;

        transform.position = Vector2.Lerp(p1pos, p2pos, 0.5f);
        transform.rotation = Quaternion.Euler(0, 0, Mathf.Rad2Deg * Mathf.Atan2(p2pos.y - p1pos.y, p2pos.x - p1pos.x));
        canvas.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, line_dist);


        // Wormhole
        if (Random.value < 0.5f) MakeWormhole();
       
        if (Wormhole == null)
        {
            // Regular route
            line.material.mainTextureScale = new Vector2(15f * (line_dist / 4f), 1);
        }
        else
        {
            // Route with wormhole - curved route
            CreateCurvedLine();

            if (Wormhole.CrossUniverse())
            {
                line.material.mainTextureScale = new Vector2(50f * (line_dist / 4f), 1);
            }
            else
            {
                line.material.mainTextureScale = new Vector2(15f * (line_dist / 4f), 1);
            }
        }
    }
    private void CreateCurvedLine()
    {
        int n = 15;
        float phase = Random.value * 360f;
        line.numPositions = n;
        for (int i = 0; i < n; ++i)
        {
            float t = (float)i / n;
            line.SetPosition(i, Vector2.Lerp(line_start, line_end, t)
                + new Vector2(0, Mathf.Sin(t * 180f + phase) * 0.1f));
        }
    }

    public void UpdateVisuals(View view)
    {
        // Color
        line.startColor = planet1.OwnerID == -1 ? default_color : planet1.sprite_sr.color;
        line.endColor = planet2.OwnerID == -1 ? default_color : planet2.sprite_sr.color;

        // Wormhole
        if (Wormhole != null)
        {
            ShowWormholeOpen(view, Wormhole.IsOpen(view.Time));
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
        base.OnPointerClick(eventData);
    }


    // PRIVATE MODIFIERS

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (wh_times_pool == null) GenWormholeTimePool();
    }
    private void GenWormholeTimePool()
    {
        // Generate possible time route times
        bool[] taken = new bool[100];

        int[] wh_head_pool = Tools.ShuffleArray(Enumerable.Range(2, 68).ToArray());
        int[] wh_tail_pool = Tools.ShuffleArray(Enumerable.Range(25, 68).ToArray());

        wh_times_pool = new List<Pair<float, float>>();

        for (int i = 0; i < Mathf.Min(wh_head_pool.Length, wh_tail_pool.Length); ++i)
        {
            int first = wh_head_pool[i];
            int second = wh_tail_pool[i];
            if (!taken[first] && !taken[second] && second - first > 15)
            {
                wh_times_pool.Add(new Pair<float, float>(first, second));
                taken[first] = true;
                taken[second] = true;
            }
        }
    }
    private bool MakeWormhole()
    {
        if (num_wormholes >= wh_times_pool.Count)
        {
            Debug.LogWarning("Ran out of time route times");
            return false;
        }
        else
        {
            float head_time = wh_times_pool[num_wormholes].First;
            float tail_time = wh_times_pool[num_wormholes].Second;
            bool cross_uv = Random.Range(0f, 1f) < 0.5f;
            
            Wormhole = new Wormhole(head_time, tail_time, cross_uv);
            ++num_wormholes;

            return true;
        }
    }

    private void ShowWormholeOpen(View view, bool open)
    {
        if (open)
        {
            Keyframe[] keys = new Keyframe[60];
            for (int i = 0; i < keys.Length; ++i)
            {
                float t = (float)i / keys.Length;
                float w = (Mathf.Sin(t * Mathf.PI * 8f + view.Time * 2f) / 2f + 0.5f) * 0.25f + 0.03f;
                keys[i] = new Keyframe(t, w);
            }

            line.widthCurve = new AnimationCurve(keys);
            //line.startWidth = 0.2f;
            //line.endWidth = 0.2f;
        }
        else
        {
            line.widthCurve = AnimationCurve.Linear(0, 0.03f, 1, 0.03f);
            //line.startWidth = 0.03f;
            //line.endWidth = 0.03f;
        }
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


public class Wormhole
{
    public const float OpenDuration = 5;
    public float HeadTime { get; private set; }
    public float TailTime { get; private set; }
    private bool cross_universe = false;
    
    public Wormhole(float head_time, float tail_time, bool cross_universe)
    {
        HeadTime = head_time;
        TailTime = tail_time;
        this.cross_universe = cross_universe;
    }

    public bool CrossUniverse()
    {
        return cross_universe;
    }
    public bool CrossTime()
    {
        return HeadTime != TailTime;
    }
    public bool IsHeadOpen(float time)
    {
        return time >= HeadTime && time < HeadTime + OpenDuration;
    }
    public bool IsTailOpen(float time)
    {
        return time >= TailTime && time < TailTime + OpenDuration;
    }
    public bool IsOpen(float time)
    {
        return IsHeadOpen(time) || IsTailOpen(time);
    }
    public UnivTime GetExit(UnivTime ut)
    {
        return GetExit(ut.universe, ut.time);
    }
    public UnivTime GetExit(int entry_uv, float entry_time)
    {
        // --- relies on a 2 universe system
        int exit_uv = cross_universe ? 1 - entry_uv : entry_uv;

        if (IsHeadOpen(entry_time))
            return new UnivTime(exit_uv, TailTime + entry_time - HeadTime);

        else if (IsTailOpen(entry_time))
            return new UnivTime(exit_uv, HeadTime + entry_time - TailTime);

        return new UnivTime(entry_uv, entry_time);
    }
}

