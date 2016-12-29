using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TimelineUI : EventTrigger
{
    // References
    public Universe Universe { get; private set; }
    public Canvas parent_canvas;
    private CamController cam;
    private Metaverse mv;

    // General
    public int universe_id = 0;
    private bool focused = false;
    public RectTransform line, knob;
    public Text clock;

    // Markers
    public RectTransform to_markers_parent, tr_markers_parent;
    public RectTransform tr_marker_prefab;
    public TurnoverMarker to_marker_prefab;
    private List<TurnoverMarker> to_markers = new List<TurnoverMarker>();

    // Goal posts
    public FlagMarker flag_marker_prefab;

    // Change Flash
    public Image change_overlay;
    private Coroutine flash_change_routine;

    // Events
    public System.Action<TimelineUI, float> on_click; // this, time
    public System.Action<TimelineUI, float> on_pointer_down; // this, time
    public System.Action<TimelineUI, float> on_pointer_drag; // this, time


    // PUBLIC MODIFIERS

    public override void OnPointerClick(PointerEventData eventData)
    {
        if (on_click != null) on_click(this, TimeFromMousePos(Input.mousePosition.x));
        base.OnPointerClick(eventData);
    }
    public override void OnPointerDown(PointerEventData eventData)
    {
        if (cam != null) cam.EnableTranslation(false);
        if (on_pointer_down != null) on_pointer_down(this, TimeFromMousePos(Input.mousePosition.x));
        base.OnPointerDown(eventData);
    }
    public override void OnPointerUp(PointerEventData eventData)
    {
        if (cam != null) cam.EnableTranslation(true);
        base.OnPointerUp(eventData);
    }
    public override void OnDrag(PointerEventData eventData)
    {
        if (on_pointer_drag != null) on_pointer_drag(this, TimeFromMousePos(Input.mousePosition.x));
        base.OnDrag(eventData);
    }


    // PRIVATE MODIFIERS

    private void Awake()
    {
        cam = Camera.main.GetComponent<CamController>();
        mv = FindObjectOfType<Metaverse>();

        SetFocus(false);

        mv.on_created += Initialize;
    }
    private void Initialize()
    {
        Universe = mv.Universes[universe_id];
        Universe.on_history_change += OnHistoryChange;

        mv.on_view_set += OnViewSet;
        mv.on_new_flag += OnNewFlag;
        OnViewSet(mv.View); // set initial view (not ready for first mv.on_view_set)

        SetMarkerPosition(knob, 0);
        UpdateClock(0);

        if (universe_id == 0) CreateRouteMarkers();
    }
    private void SetFocus(bool focused)
    {
        this.focused = focused;
        knob.gameObject.SetActive(focused);
    }

    private void OnHistoryChange(Universe uv, float earliest)
    {
        UpdateTurnoverMarkers();

        if (flash_change_routine != null) StopCoroutine(flash_change_routine);
        flash_change_routine = StartCoroutine(FlashChangeOverlay(earliest));
    }
    private void OnViewSet(View view)
    {
        // Focus
        if (view.Universe == Universe && !focused)
            SetFocus(true);
        else if (view.Universe != Universe && focused)
            SetFocus(false);

        // Knob and clock
        SetMarkerPosition(knob, view.Time);
        UpdateClock(view.Time);
    }
    private void OnNewFlag(NewFlagEvent e)
    {
        if (e.universe_id != universe_id) return;

        FlagMarker fm = Instantiate(flag_marker_prefab);
        fm.transform.SetParent(line.transform, false);
        fm.rt.anchoredPosition = SetX(fm.rt.anchoredPosition, XPosFromTime(e.time));
        fm.Initialize(DataManager.Instance.GetPlayerColor(e.player_id));
    }

    // Interaction and UI
    private void CreateRouteMarkers()
    {
        for (int i = 0; i < mv.Planets.Length; ++i)
        {
            for (int j = i; j < mv.Planets.Length; ++j)
            {
                Route r = mv.Routes[i][j];
                if (r == null) continue;
                if (r.Wormhole != null)
                {
                    float size = Mathf.Lerp(0.5f, 2f, (r.Wormhole.TailTime - r.Wormhole.HeadTime)
                        / Universe.TimeLength);

                    // Right pointing arrow
                    RectTransform marker = Instantiate(tr_marker_prefab);
                    marker.SetParent(tr_markers_parent, false);
                    Text txt = marker.GetComponent<Text>();
                    txt.fontSize = (int)(txt.fontSize * size);
                    txt.text = ">";
                    SetMarkerPosition(marker, r.Wormhole.HeadTime);

                    // Left pointing arrow
                    marker = Instantiate(tr_marker_prefab);
                    marker.SetParent(tr_markers_parent, false);
                    txt = marker.GetComponent<Text>();
                    txt.text = "<";
                    txt.fontSize = (int)(txt.fontSize * size);
                    SetMarkerPosition(marker, r.Wormhole.TailTime);
                }
            }
        }
    }
    private void UpdateTurnoverMarkers()
    {
        List<TurnoverMarker> new_markers = new List<TurnoverMarker>();
        bool[] reuse = new bool[to_markers.Count];

        // Make new markers
        foreach (Turnover to in this.Universe.GetTurnovers())
        {
            bool new_turnover = true;
            for (int i = 0; i < to_markers.Count; ++i)
            {
                if (to_markers[i].Turnover.IsSameEventAs(to))
                {
                    // Not a new turnover - reuse old marker
                    reuse[i] = true;

                    // same turnover might have different population numbers
                    to_markers[i].UpdateTurnover(to);
                    new_markers.Add(to_markers[i]);
                    new_turnover = false;
                    break;
                }
            }
            if (!new_turnover) continue;

            // New turnover - new marker
            TurnoverMarker marker = Instantiate(to_marker_prefab);
            new_markers.Add(marker);
            marker.Initialize(to, DataManager.Instance.GetPlayerColor(to.new_owner_id), true);
            marker.transform.SetParent(to_markers_parent, false);

            // X pos
            SetMarkerPosition(marker.rt, to.time);

            // Y pos
            Vector3 p = marker.rt.localPosition;
            p.y = ((float)to.planet_id / mv.Planets.Length) * 30 - 15;
            marker.rt.localPosition = p;
        }

        // Remove not old markers that weren't reused
        for (int i = 0; i < to_markers.Count; ++i)
        {
            if (!reuse[i]) to_markers[i].Remove();
        }

        // Update current list of markers
        to_markers = new_markers;
    }
    private void UpdateClock(float time)
    {
        clock.text = ((int)time).ToString("D3");
    }
    private IEnumerator FlashChangeOverlay(float earliest)
    {
        change_overlay.gameObject.SetActive(true);
        change_overlay.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
            line.rect.width * ((Universe.TimeLength - earliest) / Universe.TimeLength));
        SetMarkerPosition(change_overlay.rectTransform, earliest);

        Color start_color = Color.white;
        start_color.a = 0.3f;

        for (float t = 0; t < 1; t += UnityEngine.Time.deltaTime)
        {
            //float f = Mathf.Lerp(0, 1, 1-Mathf.Pow(1-t, 2));
            change_overlay.color = Color.Lerp(start_color, Color.clear, t);
            yield return null;
        }

        change_overlay.gameObject.SetActive(false);
    }


    // PRIVATE HELPERS

    private void SetMarkerPosition(RectTransform marker, float time)
    {
        float x = Mathf.Lerp(0, line.rect.width, time / Universe.TimeLength);
        marker.anchoredPosition = SetX(marker.anchoredPosition, x);
    }
    private float DurationToTLDist(float duration)
    {
        return (duration / Universe.TimeLength) * line.rect.width;
    }
    private float XPosFromTime(float time, bool relative_to_line=false)
    {
        return Mathf.Lerp(0, line.rect.width, time / Universe.TimeLength)
            + (relative_to_line ? 0 : line.offsetMin.x);
    }
    private float TimeFromMousePos(float mouse_x)
    {
        mouse_x /= parent_canvas.scaleFactor;
        mouse_x -= line.offsetMin.x;
        mouse_x = Mathf.Clamp(mouse_x, 0, line.rect.width);

        return (mouse_x / line.rect.width) * Universe.TimeLength;
    }
    private Vector3 SetX(Vector3 v, float x)
    {
        v.x = x;
        return v;
    }
    private Vector3 SetY(Vector3 v, float y)
    {
        v.y = y;
        return v;
    }
}