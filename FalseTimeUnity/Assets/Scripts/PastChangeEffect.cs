using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PastChangeEffect : MonoBehaviour
{
    // References
    private CamController cam_controller;
    private Metaverse mv;

    // Effect
    private Coroutine effect_routine;
    private Universe flash_univ;
    private float earliest_change;


    private void Awake()
    {
        mv = FindObjectOfType<Metaverse>();
        mv.on_history_change += OnHistoryChange;
        mv.on_view_set += OnViewSet;

        cam_controller = Camera.main.GetComponent<CamController>();
        //text = graphics.GetComponentInChildren<Text>();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (effect_routine != null)
            {
                StopFlash();
            }
            effect_routine = StartCoroutine(EffectRoutine());
        }
    }

    private void OnHistoryChange(Universe uv, float earliest)
    {
        //Tools.Log("univ " + uv.UniverseID + ": " + earliest);
        earliest_change = earliest;

        // only flash if more than 0.1 seconds ahead of change
        if (mv.View.Universe == uv && mv.View.Time > earliest + 0.1f)
        {
            // Start flash
            flash_univ = uv;
            if (effect_routine != null)
            {
                StopFlash();
            }
            effect_routine = StartCoroutine(EffectRoutine());
        }
    }
    private void OnViewSet(View view)
    {
        if (effect_routine != null && 
            (view.Universe != flash_univ || view.Time < earliest_change))
        {
            StopFlash();
        }
    }

    private IEnumerator EffectRoutine()
    {
        Vector3 cam_pos = Camera.main.transform.position;
        float ortho = Camera.main.orthographicSize;
        Quaternion cam_rot = Camera.main.transform.rotation;
        cam_controller.enabled = false;

        // Flash
        for (int i = 0; i < 5; ++i)
        {
            if (Random.value < 0.3f)
            {
                Camera.main.transform.position = Camera.main.transform.position
                + (Vector3)Tools.RandomDirection2D() * Random.value * 3f;
            }
            if (Random.value < 0.3f)
            {
                Camera.main.orthographicSize += (Random.value - 0.5f) * 3f;
            }
            if (Random.value < 0.3f)
            {
                Camera.main.transform.rotation = Quaternion.Euler(0, 0, Random.Range(-45, 45));
            }

            yield return new WaitForSeconds(Random.value * 0.05f);
            Camera.main.transform.position = cam_pos;
            Camera.main.orthographicSize = ortho;
            Camera.main.transform.rotation = cam_rot;
             yield return new WaitForSeconds(0.05f + Random.value * 0.1f);
        }

        Camera.main.transform.position = cam_pos;
        Camera.main.orthographicSize = ortho;
        Camera.main.transform.rotation = cam_rot;

        StopFlash();
    }
    private void StopFlash()
    {
        effect_routine = null;
        cam_controller.enabled = true;
    }
}
