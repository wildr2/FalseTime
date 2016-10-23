using UnityEngine;
using System.Collections;

public class Route : MonoBehaviour
{
    private LineRenderer line;
    private Planet p1, p2;
    public Color default_color;
    private Coroutine quiver_routine;
    private float quiv_t1 = -1, quiv_t2 = -1, quiv_len = 0;
    private bool crossing = false;


    public float GetTimeTravelTime(float from_time)
    {
        if (quiv_len > 0)
        {
            if (from_time > quiv_t1 && from_time < quiv_t1 + quiv_len)
            {
                return quiv_t2 + from_time - quiv_t1;
            }
            else if (from_time > quiv_t2 && from_time < quiv_t2 + quiv_len)
            {
                return quiv_t1 + from_time - quiv_t2;
            }
        }
        return from_time;
    }
    public bool IsQuivering(float time)
    {
        return GetTimeTravelTime(time) != time;
    }
    public bool IsCrossing()
    {
        return crossing;
    }


    public void Initialize(Planet p1, Planet p2)
    {
        this.p1 = p1;
        this.p2 = p2;

        float dist = Vector2.Distance(p1.transform.position, p2.transform.position);
        line.SetVertexCount(2);
        line.SetPosition(0, p1.transform.position);
        line.SetPosition(1, p2.transform.position);
        line.material.mainTextureScale = new Vector2(15f * (dist/4f), 1);
        //for (int i = 0; i < 2; ++i)
        //{
        //    line.SetPosition(i, Vector2.Lerp(p1.transform.position, p2.transform.position, (float)i / 1));
        //}

        //line.material.mainTextureOffset = new Vector2(0, 0f);
        //line.material.mainTextureScale = new Vector2(2, 1);


        //line.SetPosition(0, p1.transform.position);
        //line.SetPosition(1, p2.transform.position);

        //if (Random.value < 0.5f)
        //{
        //    quiv_len = 5;
        //    quiv_t1 = Random.Range(0, 120 - quiv_len*4f);
        //    quiv_t2 = Random.Range(quiv_t1+quiv_len, 120 - quiv_len);

        //    if (Random.value < 0.5f)
        //    {
        //        crossing = true;
        //        line.SetWidth(0.1f, 0.1f);
        //    }
        //}

    }
    public void UpdateVisuals(float time)
    {
        // Color
        //line.SetWidth(p1.OwnerID == -1 ? 0.03f : 0.08f, p2.OwnerID == -1 ? 0.03f : 0.08f);
        line.SetColors(p1.OwnerID == -1 ? default_color : p1.sprite_sr.color,
            p2.OwnerID == -1 ? default_color : p2.sprite_sr.color);

        // Quiver
        if (quiv_len > 0)
        {
            if (time > quiv_t1 && time < quiv_t1 + quiv_len)
            {
                if (quiver_routine == null) StartQuiver();
            }
            else if (time > quiv_t2 && time < quiv_t2 + quiv_len)
            {
                if (quiver_routine == null) StartQuiver();
            }
            else
            {
                if (quiver_routine != null) StopQuiver();
            }
        }
    }

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    private IEnumerator Quiver()
    {
        int n = 15;
        line.SetVertexCount(n);
        line.SetPosition(0, p1.transform.position);
        line.SetPosition(n-1, p2.transform.position);

        Vector2 dir = Vector3.Cross(p2.transform.position - p1.transform.position, Vector3.forward).normalized;
        float[] phase = new float[n - 2];
        for (int i = 0; i < n - 2; ++i)
        {
            phase[i] = Random.value * Mathf.PI * 2f; //(float)i/n * Mathf.PI * 2f; //
        }


        while (true)
        {
            for (int i = 0; i < n-2; ++i)
            {
                Vector2 pos = Vector2.Lerp(p1.transform.position, p2.transform.position, (float)(i+1) / n);
                pos += Mathf.Sin(Time.time*5f + phase[i]) * dir * 0.1f;
                line.SetPosition(i+1, pos);
            }
            yield return null;
        }   
    }
    private void StartQuiver()
    {
        quiver_routine = StartCoroutine(Quiver());
    }
    private void StopQuiver()
    {
        StopCoroutine(quiver_routine);
        quiver_routine = null;

        line.SetVertexCount(2);
        line.SetPosition(0, p1.transform.position);
        line.SetPosition(1, p2.transform.position);
    }
}
