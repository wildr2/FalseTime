using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class TimeChangeScreen : MonoBehaviour
{
    private CamController cam_controller;
    public Transform graphics;
    private Text text;
    private GameManager gm;
    private Coroutine flash_routine;
    private Timeline flash_line;

    private void Awake()
    {
        gm = FindObjectOfType<GameManager>();
        gm.on_history_change += OnHistoryChange;
        gm.on_time_set += OnTimeSet;

        cam_controller = Camera.main.GetComponent<CamController>();
        //text = graphics.GetComponentInChildren<Text>();
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            if (flash_routine != null)
            {
                StopFlash();
            }
            flash_routine = StartCoroutine(FlashTimeChangeScreen());
        }
    }

    private void OnHistoryChange(Timeline line)
    {
        if (gm.CurrentTimeline == line && line.Time >= line.GetLatestCmdTime())
        {
            // Start flash
            flash_line = line;
            if (flash_routine != null)
            {
                StopFlash();
            }
            flash_routine = StartCoroutine(FlashTimeChangeScreen());
        }
    }
    private void OnTimeSet(Timeline line)
    {
        if (flash_routine != null && (line != flash_line || line.Time < flash_line.GetLatestCmdTime()))
        {
            StopFlash();
        }
    }

    private IEnumerator FlashTimeChangeScreen()
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
        flash_routine = null;
        graphics.gameObject.SetActive(false);
        cam_controller.enabled = true;
    }
}
