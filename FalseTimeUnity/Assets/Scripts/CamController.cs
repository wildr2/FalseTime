using UnityEngine;
using System.Collections;


public class CamController : MonoBehaviour
{
    private Camera cam;

    // Zoom
    private float zoom, target_zoom = 1f; // 0 to 1 (0 being zoomed in)
    private const float min_ortho = 3, max_ortho = 18;
    private const float zoom_speed = 50f;
    private const float zoom_lerp_speed = 10f;

    // Rotation effect
    private const float min_rot = 2;
    private const float max_rot = 4;

    // Translation
    private bool trans_enabled = true;
    private Rect tran_limits = new Rect(-10, -10, 20, 20);
    private Vector3 tran_start_pos;
    private Vector3 target_pos;
    private Vector3 prev_mouse;
    private const float trans_speed = 1f;
    private const float trans_lerp_speed = 10f;


    public void EnableTranslation(bool enable = true)
    {
        trans_enabled = enable;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();

        target_pos = transform.position;
        prev_mouse = Input.mousePosition;

        // Initial zoom
        zoom = target_zoom;
        cam.orthographicSize = min_ortho + zoom * (max_ortho - min_ortho);
    }
    private void Update()
    {
        // Input
        float input_zoom = Input.GetAxis("Mouse ScrollWheel");
        bool input_cam_grab = (Input.GetMouseButtonDown(1)) && trans_enabled;
        bool input_cam_grabbing = (Input.GetMouseButton(1)) && trans_enabled;

        if (Input.GetKeyDown(KeyCode.C))
        {
            input_zoom = -100;
        }


        // Zoom
        if (!input_cam_grabbing)
        {
            // Modify target_zoom based on input
            target_zoom -= input_zoom * Time.deltaTime * zoom_speed;
            target_zoom = Mathf.Clamp(target_zoom, 0, 1);

            // Modify zoom based on target zoom    - only if zoom and target zoom are not ~equal
            if (Mathf.Abs(target_zoom - zoom) > 0.001f)
            {
                zoom = Mathf.Lerp(zoom, target_zoom, Time.deltaTime * zoom_lerp_speed);

                // camera scale
                float old_ortho = cam.orthographicSize;
                cam.orthographicSize = min_ortho + zoom * (max_ortho - min_ortho);

                // zoom to the mouse point (translate)
                Vector3 mwp = cam.ScreenToWorldPoint(Input.mousePosition);
                Vector3 ci = transform.position;
                Vector3 cf = -(mwp - ci) * (cam.orthographicSize / old_ortho) + mwp;
                transform.position = cf;
                target_pos = transform.position;
            }
        }
        else if (input_cam_grab)
        {
            // Fix zoom when start camera grab (to avoid translation conflict)
            target_zoom = zoom;
        }


        // Rotation effect
        float r = 1f - zoom;
        transform.rotation = Quaternion.Euler(0, 0, min_rot + r * max_rot);


        // Translation
        if (input_cam_grab)
        {
            // Start cam grab
            tran_start_pos = transform.position;
            target_pos = tran_start_pos;
        }
        if (input_cam_grabbing)
        {
            //float units_per_pixel = cam.orthographicSize * 2f / cam.pixelHeight;
            Vector3 mouse_delta = Input.mousePosition - prev_mouse;

            // Resolution independant translation distance
            target_pos -= mouse_delta * (Screen.height / 729f) * Time.deltaTime * trans_speed;
            target_pos.x = Mathf.Clamp(target_pos.x, tran_limits.xMin, tran_limits.xMax);
            target_pos.y = Mathf.Clamp(target_pos.y, tran_limits.yMin, tran_limits.yMax);
        }
        transform.position = Vector3.Lerp(transform.position, target_pos, Time.deltaTime * trans_lerp_speed);
        prev_mouse = Input.mousePosition;

    }

}