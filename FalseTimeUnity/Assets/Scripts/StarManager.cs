using UnityEngine;
using System.Collections;

public class StarManager : MonoBehaviour
{
    public SpriteRenderer star_prefab;
    private SpriteRenderer[] stars;
    public int n = 50;
    public float rotations = 1;
    public float radius = 20f;


    private void Awake()
    {
        GameManager gm = FindObjectOfType<GameManager>();
        gm.on_time_set += OnTimeSet;

        transform.position += new Vector3((Random.value - 0.5f), (Random.value - 0.5f)) * 8f;

        // Create stars
        stars = new SpriteRenderer[n];
        for (int i = 0; i < n; ++i)
        {
            SpriteRenderer star = Instantiate(star_prefab);
            star.transform.SetParent(transform);
            star.transform.localPosition = Tools.RandomPosInCircle(radius);
            stars[i] = star;
        }
    }

    private void OnTimeSet(Timeline line)
    {
        float t = -line.Time / line.GetEndTime();
        transform.rotation = Quaternion.Euler(0, 0, t * 360f * rotations);
    }
}
