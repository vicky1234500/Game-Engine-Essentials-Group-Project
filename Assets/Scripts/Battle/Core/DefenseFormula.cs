using UnityEngine;

// DF = % damage taken reduction, with diminishing returns (the more DF, the less each point gives).
//
//   DF    reduction
//    0      0%
//   20     10%      (0.5% per DF)
//   40     20%      (0.5% per DF)
//  100     40%      (~0.33% per DF)
//  200     55%      (0.15% per DF)  <- guess, change if the team wants
//  200+    55%      (cap)
//
// Between two points it's a straight line, e.g. DF 25 (Character 1) = 12.5%, DF 70 = 30%.
public static class DefenseFormula
{
    static readonly Vector2[] points =
    {
        new Vector2(0, 0),
        new Vector2(20, 10),
        new Vector2(40, 20),
        new Vector2(100, 40),
        new Vector2(200, 55),
    };

    public static float ReductionPercent(int df)
    {
        if (df <= 0) return 0f;
        for (int i = 1; i < points.Length; i++)
        {
            if (df <= points[i].x)
            {
                float t = (df - points[i - 1].x) / (points[i].x - points[i - 1].x);
                return Mathf.Lerp(points[i - 1].y, points[i].y, t);
            }
        }
        return points[points.Length - 1].y;
    }

    public static int Apply(int damage, int df)
    {
        float multiplier = 1f - ReductionPercent(df) / 100f;
        return Mathf.Max(0, Mathf.RoundToInt(damage * multiplier));
    }
}
