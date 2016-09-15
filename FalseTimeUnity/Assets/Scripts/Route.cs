using UnityEngine;
using System.Collections;

public class Route : MonoBehaviour
{
    private LineRenderer line;
    private Planet p1, p2;
    private Coroutine quiver_routine;
    private float quiv_t1 = -1, quiv_t2 = -1, quiv_len = 0;


    public void Initialize(Planet p1, Planet p2)
    {
        this.p1 = p1;
        this.p2 = p2;
        line.SetPosition(0, p1.transform.position);
        line.SetPosition(1, p2.transform.position);

        if (Random.value < 0.25f)
        {
            quiv_len = 5;
            quiv_t1 = Random.Range(0, 120 - quiv_len*4f);
            quiv_t2 = Random.Range(quiv_t1+quiv_len, 120 - quiv_len);
        }
    }
    public void OnSetTime(float time)
    {
        if (quiv_len <= 0) return;

        if (time > quiv_t1 && time < quiv_t1 + quiv_len)
        {
            if (!IsQuivering()) StartQuiver();
        }
        else if (time > quiv_t2 && time < quiv_t2 + quiv_len)
        {
            if (!IsQuivering()) StartQuiver();
        }
        else
        {
            if (IsQuivering()) StopQuiver();
        }
    }
    public bool IsQuivering()
    {
        return quiver_routine != null;
    }
    public float GetFlightStartTime(float time)
    {
        if (quiv_len > 0)
        {
            if (time > quiv_t1 && time < quiv_t1 + quiv_len)
            {
                return quiv_t2 + time - quiv_t1;
            }
            else if (time > quiv_t2 && time < quiv_t2 + quiv_len)
            {
                return quiv_t1 + time - quiv_t2;
            }
        }
        return time;
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
            phase[i] = Random.value * Mathf.PI * 2f;
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
