using UnityEngine;
using System.Collections;

public class StarManager2 : MonoBehaviour
{
    public SpriteRenderer star_prefab;
    private SpriteRenderer[] stars;
    public int n = 50;
    public float radius = 20f;


    private void Awake()
    {
        // Create stars
        stars = new SpriteRenderer[n];
        for (int i = 0; i < n; ++i)
        {
            SpriteRenderer star = Instantiate(star_prefab);
            star.transform.SetParent(transform);

            if (Random.value < 0.6f || i == 0)
                star.transform.localPosition = Tools.RandomPosInCircle(radius);
            else
                star.transform.localPosition = (Vector2)stars[i-1].transform.position + Tools.RandomPosInCircle(1);

            star.transform.localScale = Vector3.one * Random.Range(0.03f, 0.07f);
            star.color = Color.Lerp(Color.black, Color.white, Random.Range(0.4f, 0.7f));
            stars[i] = star;
        }
    }
}
