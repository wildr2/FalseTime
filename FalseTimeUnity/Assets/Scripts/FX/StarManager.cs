using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class StarManager : MonoBehaviour
{
    public SpriteRenderer star_prefab, shootingstar_prefab;
    private SpriteRenderer[] stars;
    private List<ShootingStar> shooting_stars;

    public int n = 50;
    public float radius = 20f;
    public float shooting_star_chance = 0.05f;

    private void Awake()
    {
        Metaverse mv = FindObjectOfType<Metaverse>();
        mv.on_view_set += OnViewSet;

        // Create stars
        stars = new SpriteRenderer[n];
        shooting_stars = new List<ShootingStar>();

        for (int i = 0; i < n; ++i)
        {
            bool shooting = Random.value < shooting_star_chance;

            SpriteRenderer star = Instantiate(shooting ? shootingstar_prefab : star_prefab);
            star.transform.SetParent(transform);

            if (Random.value < 0.6f || i == 0)
                star.transform.localPosition = Tools.RandomPosInCircle(radius);
            else
                star.transform.localPosition = (Vector2)stars[i-1].transform.position + Tools.RandomPosInCircle(1);

            if (shooting)
            {
                ShootingStar ss = new ShootingStar(star.transform);
                shooting_stars.Add(ss);
            }

            star.transform.localScale = Vector3.one * Random.Range(0.03f, 0.07f);
            star.color = Color.Lerp(Color.black, Color.white, Random.Range(0.4f, 0.7f));
            stars[i] = star;
        }
    }
    private void OnViewSet(View view)
    {
        float t = view.Time / Universe.TimeLength;

        foreach (ShootingStar ss in shooting_stars)
        {
            ss.UpdatePos(t);
        }
           
    }
}

public class ShootingStar
{
    private Transform star;
    private Vector2 arc_center;
    private float arc_r;
    private float angle0, angle1;

    public ShootingStar(Transform star)
    {
        this.star = star;

        arc_r = Random.Range(10, 25);
        arc_center = (Vector2)star.localPosition + Tools.RandomDirection2D() * arc_r;

        angle0 = Mathf.Atan2(arc_center.y - star.localPosition.y, arc_center.x - star.localPosition.x);
        angle1 = angle0 + (Random.value < 0.5f ? 1 : -1) * Random.Range(Mathf.PI/6f, Mathf.PI/2f);
    }
    public void UpdatePos(float t)
    {
        float a = Mathf.Lerp(angle0, angle1, t);
        star.localPosition = arc_center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * arc_r;
    }


}
