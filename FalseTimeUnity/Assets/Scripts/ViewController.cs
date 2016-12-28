﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ViewController : MonoBehaviour
{
    // References
    private Metaverse mv;
    private GameManager gm;

    // Timline scraping (keyboard)
    private float scrape_speed = 30;
    private float scrape_speed_fast = 60;
    private KeyCode scrape_fast_key = KeyCode.LeftShift;

    // Route interaction


    private void Awake()
    {
        mv = FindObjectOfType<Metaverse>();
        gm = FindObjectOfType<GameManager>();
        mv.on_created += Initialize;
    }
    private void Initialize()
    {
        // Route interaction
        for (int i = 0; i < mv.Planets.Length; ++i)
        {
            for (int j = i; j < mv.Planets.Length; ++j)
            {
                Route r = mv.Routes[i][j];
                if (r == null) continue;

                r.on_pointer_enter += OnRoutePointerEnter;
                r.on_pointer_exit += OnRoutePointerExit;
                r.on_pointer_click += OnRouteClick;
            }
        }

        // Timeline interaction
        foreach (TimelineUI tl in FindObjectsOfType<TimelineUI>())
        {
            tl.on_pointer_drag += OnTimelineDrag;
            tl.on_pointer_down += OnTimelineDown;
        }
    }
    private void Update()
    {
        if (!gm.IsGamePlaying()) return;

        UpdateKBControl();
    }

    private void UpdateKBControl()
    {
        // Scrape timeline
        float input = Input.GetAxis("Horizontal");
        if (Mathf.Abs(input) > 0.5f)
        {
            float speed = Input.GetKey(scrape_fast_key) ? scrape_speed_fast : scrape_speed;

            float time = mv.View.Time + input * UnityEngine.Time.deltaTime * speed;
            mv.SetView(time);
        }

        // Switch timeline
        if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Space))
        {
            // Switch timeline
            SwitchTimeline();
        }
    }
    private void SwitchTimeline()
    {
        int i = mv.View.Universe.UniverseID;
        i = (i + 1) % mv.Universes.Length;

        mv.SetView(mv.View.Time, mv.Universes[i]);
    }

    private void OnRoutePointerEnter(Route route)
    {
        // Highlight route
        Color c = route.IsTimeRoute() ?
            Tools.SetColorAlpha(DataManager.Instance.color_timetravel, 0.15f) :
            Tools.SetColorAlpha(Color.white, 0.05f);

        route.ShowHighlight(c);
    }
    private void OnRoutePointerExit(Route route)
    {
        route.HideHighlight();
    }
    private void OnRouteClick(Route route)
    {
        Tools.Log("here");

        if (!gm.IsGamePlaying()) return;
        if (route.IsTimeRoute())
        {
            float to_time = route.GetTimeTravelTime(mv.View.Time);
            if (to_time == mv.View.Time)
            {
                // Not at either time route time - go to second tr time
                mv.SetView(route.GetTRSecond() + 0.001f);
            }
            else
            {
                // At a time route time - go to corresponding time and universe
                mv.SetView(to_time + 0.001f,
                    mv.Universes[route.GetTimeTravelUV(mv.View.Time)]);
            }
        }
    }
    private void OnTimelineDrag(TimelineUI tl, float time)
    {
        mv.SetView(time, tl.Universe);
    }
    private void OnTimelineDown(TimelineUI tl, float time)
    {
        mv.SetView(time, tl.Universe);
    }
}
