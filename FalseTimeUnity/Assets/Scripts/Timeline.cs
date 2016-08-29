using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;

public class Timeline : MonoBehaviour
{
    private GameManager gm;
    private CamController cam;

    // UI
    public RectTransform cmds_parent;
    public RectTransform line, knob, marker_prefab;
    private bool pointer_down;
    public Text clock, win_text;

    // Interaction
    private bool paused = true;

    // Length of the timeline in seconds
    private float time_length = 120;

    // Time in seconds
    public float Time { get; private set; }

    // Events
    public System.Action<float> on_time_set;



    public void OnPointerDown()
    {
        pointer_down = true;
        if (cam != null) cam.EnableTranslation(false);
    }
    public void OnPointerUp()
    {
        pointer_down = false;
        if (cam != null) cam.EnableTranslation(true);
    }

    private void OnWin(int winner, float win_time)
    {
        SetTime(win_time);
        SetMarkerPosition(knob, win_time);
        UpdateClock();

        win_text.transform.parent.gameObject.SetActive(true);
        win_text.text = gm.player_names[winner].ToUpper() + " WINS";
    }

    private void Awake()
    {
        gm = FindObjectOfType<GameManager>();
        gm.on_history_change += ReMakeHistoryMarkers;
        gm.on_win += OnWin;

        cam = Camera.main.GetComponent<CamController>();

        SetMarkerPosition(knob, 0);
        UpdateClock();
    }
    private void Update()
    {
        if (!gm.IsGamePlaying()) return;

        if (pointer_down)
        {
            // Set timeline knob position
            float x = Input.mousePosition.x;
            SetMarkerPositionMouse(knob, x);

            // Set time
            SetTime(GetMarkerTime(knob));
        }

        // Play / Pause
        if (Input.GetKeyDown(KeyCode.Space))
            paused = !paused;

        if (!paused)
        {
            SetTime(Time + UnityEngine.Time.deltaTime);
            SetMarkerPosition(knob, Time);
        }
    }
    
    /// <summary>
    /// Pos from 0 to 1
    /// </summary>
    /// <param name="line_pos"></param>
    private void SetTime(float time)
    {
        Time = time;
        if (on_time_set != null)
            on_time_set(Time);

        UpdateClock();
    }
    private void ReMakeHistoryMarkers()
    {
        // Delete old markers
        Tools.DestroyChildren(cmds_parent);

        // Commands
        foreach (PlayerCmd cmd in gm.GetPlayerCmds())
        {
            RectTransform marker = Instantiate(marker_prefab);
            marker.SetParent(cmds_parent, false);
            SetMarkerPosition(marker, cmd.time);
            //marker.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, line.rect.height * );

            Color color = gm.player_colors[cmd.player_id];

            if (cmd.IsValid(gm.GetState(cmd.time)))
            {
                marker.GetComponent<Image>().color = Color.Lerp(color, Color.black, 0);
            }
            else
            {
                marker.GetComponent<Image>().color = Color.Lerp(color, Color.black, 0.5f);
            }
        }
    }

    private void UpdateClock()
    {
        clock.text = Tools.FormatTimeAsMinSec(Time);
    }
    private void SetMarkerPosition(RectTransform marker, float time)
    {
        float x = Mathf.Lerp(0, line.rect.width, time / time_length);
        marker.anchoredPosition = SetX(marker.anchoredPosition, x);
    }
    private void SetMarkerPositionMouse(RectTransform marker, float mouse_x)
    {
        marker.position = SetX(marker.position, mouse_x);

        if (marker.anchoredPosition.x < 0)
            marker.anchoredPosition = SetX(marker.anchoredPosition, 0);
        else if (marker.anchoredPosition.x > line.rect.width)
            marker.anchoredPosition = SetX(marker.anchoredPosition, line.rect.width);
    }
    private float GetMarkerTime(RectTransform marker)
    {
        return (marker.anchoredPosition.x / line.rect.width) * time_length;
    }
    private Vector3 SetX(Vector3 v, float x)
    {
        v.x = x;
        return v;
    }
}
