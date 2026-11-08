using UnityEngine;

// Puts an enemy's 3D model into the world, in the hallway and in battle.
// The models come from different artists at very different sizes, and their pivots can be anywhere,
// so each one is measured, scaled to EnemyData.modelHeight, then moved so its feet are on the spot.
public static class EnemyModel
{
    // localFeet = where its feet go, relative to parent. Parent should have a scale of 1.
    public static GameObject Spawn(EnemyData data, Transform parent, Vector3 localFeet, float sizeMultiplier = 1f)
    {
        if (!data || !data.modelPrefab) return null;

        var model = Object.Instantiate(data.modelPrefab, parent);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.Euler(0f, data.modelYaw, 0f);

        // Enemies never block the player, like the old capsules.
        foreach (var col in model.GetComponentsInChildren<Collider>(true))
            Object.Destroy(col);

        if (!HasMesh(model))
        {
            Debug.LogWarning($"{data.enemyName}: the model has nothing to show. Are its mesh files missing?", data.modelPrefab);
            return model;
        }

        float height = Measure(model).size.y;
        if (height > 0.001f)
            model.transform.localScale *= data.modelHeight * sizeMultiplier / height;

        Bounds box = Measure(model);
        Vector3 feet = parent.TransformPoint(localFeet) + Vector3.up * data.modelLift;
        model.transform.position += new Vector3(feet.x - box.center.x, feet.y - box.min.y, feet.z - box.center.z);
        return model;
    }

    static bool HasMesh(GameObject model)
    {
        foreach (var r in model.GetComponentsInChildren<Renderer>())
            if (r is MeshRenderer || r is SkinnedMeshRenderer) return true;
        return false;
    }

    // The box around all the model's meshes (particles and trails don't count).
    static Bounds Measure(GameObject model)
    {
        bool first = true;
        var box = new Bounds();
        foreach (var r in model.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (first) { box = r.bounds; first = false; }
            else box.Encapsulate(r.bounds);
        }
        return box;
    }
}
