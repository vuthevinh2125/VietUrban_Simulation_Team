using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Dựng lại bộ chuyển động cho người (khách, nhân viên, bảo vệ): Idle - Walking - Running.
///  - Speed (float, m/s): đứng yên dưới 0.1, trên 0.1 thì đi bộ.
///  - Run (bool): chỉ kẻ trộm đang chạy trốn và bảo vệ đang đuổi bật lên -> chạy (nhân viên đi nhanh vẫn là đi bộ).
/// Sửa NPC_Animator (khách, nhân viên dùng chung) và GuardController (trước chỉ có Idle -> Running, không có đi bộ
/// và không quay lại Idle được). Tự chạy một lần khi mở project nếu NPC_Animator chưa có tham số Run.
/// Menu: Tools > Store Setup > Update Character Animations
/// </summary>
[InitializeOnLoad]
public static class CharacterAnimationTool
{
    private const string NpcController = "Assets/Assets_ThirdParty/Materials/NPC_Animator.controller";
    private const string GuardControllerPath = "Assets/Project/Prefabs/Characters/GuardController.controller";
    private const string RunningClip = "Assets/Models/Running.fbx";

    static CharacterAnimationTool()
    {
        EditorApplication.delayCall += () =>
        {
            var c = AssetDatabase.LoadAssetAtPath<AnimatorController>(NpcController);
            if (c == null) return;
            foreach (AnimatorControllerParameter p in c.parameters) if (p.name == "Run") return;
            Run();
        };
    }

    [MenuItem("Tools/Store Setup/Update Character Animations")]
    public static void Run()
    {
        var log = new List<string>();
        AnimationClip run = LoadClip(RunningClip);
        var npc = AssetDatabase.LoadAssetAtPath<AnimatorController>(NpcController);
        if (npc == null || run == null) { Debug.LogError($"[Animations] Missing {NpcController} or {RunningClip}."); return; }

        Motion idle = FindMotion(npc, "Idle"), walk = FindMotion(npc, "Walking");
        Build(npc, idle, walk, run);
        log.Add($"NPC_Animator: Idle / Walking / Running ({run.name})");

        var guard = AssetDatabase.LoadAssetAtPath<AnimatorController>(GuardControllerPath);
        if (guard != null) { Build(guard, idle, walk, run); log.Add("GuardController: Idle / Walking / Running"); }

        AssetDatabase.SaveAssets();
        Debug.Log("[Animations] " + string.Join("; ", log));
    }

    private static void Build(AnimatorController c, Motion idle, Motion walk, Motion run)
    {
        EnsureParam(c, "Speed", AnimatorControllerParameterType.Float);
        EnsureParam(c, "Run", AnimatorControllerParameterType.Bool);
        AnimatorStateMachine sm = c.layers[0].stateMachine;
        foreach (ChildAnimatorState s in sm.states) sm.RemoveState(s.state);
        foreach (AnimatorStateTransition t in sm.anyStateTransitions) sm.RemoveAnyStateTransition(t);

        AnimatorState sIdle = sm.AddState("Idle", new Vector3(300, 0, 0));
        AnimatorState sWalk = sm.AddState("Walking", new Vector3(300, 120, 0));
        AnimatorState sRun = sm.AddState("Running", new Vector3(560, 60, 0));
        sIdle.motion = idle; sWalk.motion = walk; sRun.motion = run;
        sm.defaultState = sIdle;

        Link(sIdle, sWalk, (AnimatorConditionMode.Greater, "Speed", 0.1f));
        Link(sWalk, sIdle, (AnimatorConditionMode.Less, "Speed", 0.1f));
        Link(sWalk, sRun, (AnimatorConditionMode.If, "Run", 0f));
        Link(sIdle, sRun, (AnimatorConditionMode.If, "Run", 0f), (AnimatorConditionMode.Greater, "Speed", 0.1f));
        Link(sRun, sWalk, (AnimatorConditionMode.IfNot, "Run", 0f));
        Link(sRun, sIdle, (AnimatorConditionMode.Less, "Speed", 0.1f));
        EditorUtility.SetDirty(c);
    }

    private static void Link(AnimatorState from, AnimatorState to, params (AnimatorConditionMode mode, string param, float value)[] conditions)
    {
        AnimatorStateTransition t = from.AddTransition(to);
        t.hasExitTime = false;
        t.duration = 0.15f;
        foreach (var c in conditions) t.AddCondition(c.mode, c.value, c.param);
    }

    private static void EnsureParam(AnimatorController c, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter p in c.parameters) if (p.name == name) return;
        c.AddParameter(name, type);
    }

    private static Motion FindMotion(AnimatorController c, string stateName)
    {
        foreach (ChildAnimatorState s in c.layers[0].stateMachine.states)
            if (s.state.name == stateName) return s.state.motion;
        return null;
    }

    private static AnimationClip LoadClip(string path)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip clip && !clip.name.StartsWith("__preview__")) return clip;
        return null;
    }
}
