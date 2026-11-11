#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Makes a Humanoid Avatar for a glb / glTF character (Ready Player Me skeleton),
// so Mixamo / humanoid animations can play on it.
// Put this file in Assets/Editor. Then: select the character in the scene > Tools > Make Humanoid Avatar.
public static class HumanoidAvatarMaker
{
    // Unity humanoid bone name  ->  Ready Player Me / Mixamo bone name (lowercase)
    static readonly Dictionary<string, string> boneMap = new Dictionary<string, string>
    {
        { "Hips", "hips" },
        { "Spine", "spine" },
        { "Chest", "spine1" },
        { "UpperChest", "spine2" },
        { "Neck", "neck" },
        { "Head", "head" },
        { "LeftEye", "lefteye" },
        { "RightEye", "righteye" },

        { "LeftShoulder", "leftshoulder" },
        { "LeftUpperArm", "leftarm" },
        { "LeftLowerArm", "leftforearm" },
        { "LeftHand", "lefthand" },
        { "RightShoulder", "rightshoulder" },
        { "RightUpperArm", "rightarm" },
        { "RightLowerArm", "rightforearm" },
        { "RightHand", "righthand" },

        { "LeftUpperLeg", "leftupleg" },
        { "LeftLowerLeg", "leftleg" },
        { "LeftFoot", "leftfoot" },
        { "LeftToes", "lefttoebase" },
        { "RightUpperLeg", "rightupleg" },
        { "RightLowerLeg", "rightleg" },
        { "RightFoot", "rightfoot" },
        { "RightToes", "righttoebase" },

        // fingers (optional, makes the hands move too)
        { "Left Thumb Proximal", "lefthandthumb1" },   { "Left Thumb Intermediate", "lefthandthumb2" },   { "Left Thumb Distal", "lefthandthumb3" },
        { "Left Index Proximal", "lefthandindex1" },   { "Left Index Intermediate", "lefthandindex2" },   { "Left Index Distal", "lefthandindex3" },
        { "Left Middle Proximal", "lefthandmiddle1" }, { "Left Middle Intermediate", "lefthandmiddle2" }, { "Left Middle Distal", "lefthandmiddle3" },
        { "Left Ring Proximal", "lefthandring1" },     { "Left Ring Intermediate", "lefthandring2" },     { "Left Ring Distal", "lefthandring3" },
        { "Left Little Proximal", "lefthandpinky1" },  { "Left Little Intermediate", "lefthandpinky2" },  { "Left Little Distal", "lefthandpinky3" },
        { "Right Thumb Proximal", "righthandthumb1" },   { "Right Thumb Intermediate", "righthandthumb2" },   { "Right Thumb Distal", "righthandthumb3" },
        { "Right Index Proximal", "righthandindex1" },   { "Right Index Intermediate", "righthandindex2" },   { "Right Index Distal", "righthandindex3" },
        { "Right Middle Proximal", "righthandmiddle1" }, { "Right Middle Intermediate", "righthandmiddle2" }, { "Right Middle Distal", "righthandmiddle3" },
        { "Right Ring Proximal", "righthandring1" },     { "Right Ring Intermediate", "righthandring2" },     { "Right Ring Distal", "righthandring3" },
        { "Right Little Proximal", "righthandpinky1" },  { "Right Little Intermediate", "righthandpinky2" },  { "Right Little Distal", "righthandpinky3" },
    };

    [MenuItem("Tools/Make Humanoid Avatar (glb character)")]
    static void Make()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("Make Humanoid Avatar", "Select the character's root object in the Hierarchy first (e.g. Shopkeeper).", "OK");
            return;
        }

        // 1. Find every bone by its name ("Hips_3", "mixamorig:Hips" and "Hips" all become "hips")
        var allTransforms = root.GetComponentsInChildren<Transform>(true);
        var lookup = new Dictionary<string, Transform>();
        foreach (var t in allTransforms)
        {
            string key = Normalize(t.name);
            if (!lookup.ContainsKey(key)) lookup[key] = t;
        }

        // 2. Match them to Unity's humanoid bones
        var human = new List<HumanBone>();
        var missing = new List<string>();
        foreach (var pair in boneMap)
        {
            if (lookup.TryGetValue(pair.Value, out Transform bone))
            {
                var hb = new HumanBone { humanName = pair.Key, boneName = bone.name };
                hb.limit.useDefaultValues = true;
                human.Add(hb);
            }
            else missing.Add(pair.Key);
        }

        // 3. The skeleton = the character's current pose (it should be standing in a T-pose)
        var skeleton = new List<SkeletonBone>();
        var usedNames = new HashSet<string>();
        foreach (var t in allTransforms)
        {
            if (!usedNames.Add(t.name)) continue;   // avatar bone names must be unique
            skeleton.Add(new SkeletonBone
            {
                name = t.name,
                position = t.localPosition,
                rotation = t.localRotation,
                scale = t.localScale
            });
        }

        var description = new HumanDescription
        {
            human = human.ToArray(),
            skeleton = skeleton.ToArray(),
            upperArmTwist = 0.5f,
            lowerArmTwist = 0.5f,
            upperLegTwist = 0.5f,
            lowerLegTwist = 0.5f,
            armStretch = 0.05f,
            legStretch = 0.05f,
            feetSpacing = 0f,
            hasTranslationDoF = false
        };

        // 4. Build the avatar
        Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);
        if (avatar == null || !avatar.isValid || !avatar.isHuman)
        {
            Debug.LogError("Couldn't build a humanoid avatar. Missing bones: " + string.Join(", ", missing));
            return;
        }
        if (missing.Count > 0) Debug.Log("Optional bones not found (that's OK): " + string.Join(", ", missing));

        // 5. Save it and put it on an Animator
        avatar.name = root.name + "Avatar";
        string path = EditorUtility.SaveFilePanelInProject("Save Humanoid Avatar", avatar.name, "asset", "Where should the avatar be saved?");
        if (string.IsNullOrEmpty(path)) return;
        AssetDatabase.CreateAsset(avatar, path);
        AssetDatabase.SaveAssets();

        Animator animator = root.GetComponent<Animator>();
        if (animator == null) animator = Undo.AddComponent<Animator>(root);
        Undo.RecordObject(animator, "Set Humanoid Avatar");
        animator.avatar = avatar;
        animator.applyRootMotion = false;

        Debug.Log($"Humanoid avatar saved to {path} and added to {root.name}'s Animator. Now assign an Animator Controller.");
    }

    static string Normalize(string name)
    {
        string n = name.ToLowerInvariant().Replace("mixamorig:", "").Replace("mixamorig", "");
        n = Regex.Replace(n, @"([_\.]\d+)+$", "");   // remove "_3", ".001" etc. at the end
        return n;
    }
}
#endif
