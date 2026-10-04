using UnityEngine;

/// <summary>仅场景视图调试绘制。不会修改角色移动与接地逻辑。</summary>
[DisallowMultipleComponent, RequireComponent(typeof(CharacterController))]
public sealed class CharacterControllerDebugView : MonoBehaviour
{
    public bool show = true;
    public bool onlyWhenSelected;
    [Min(.1f)] public float probeDistance = 1.5f;
    public LayerMask groundMask = ~0;
    public bool showFootBones = true;

#if UNITY_EDITOR
    void OnDrawGizmos() { if (!onlyWhenSelected) Draw(); }
    void OnDrawGizmosSelected() { if (onlyWhenSelected) Draw(); }

    bool Probe(Vector3 origin, float distance, out RaycastHit nearest)
    {
        nearest = default;
        float best = float.PositiveInfinity;
        foreach (var hit in Physics.RaycastAll(origin, Vector3.down, distance, groundMask, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.transform.IsChildOf(transform) || hit.distance >= best) continue;
            best = hit.distance; nearest = hit;
        }
        return best < float.PositiveInfinity;
    }

    void Draw()
    {
        if (!show || !enabled) return;
        var cc = GetComponent<CharacterController>();
        var scale = transform.lossyScale;
        float radius = cc.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
        float height = Mathf.Max(2 * radius, cc.height * Mathf.Abs(scale.y));
        var center = transform.TransformPoint(cc.center);
        var axis = transform.up;
        var lower = center - axis * (height * .5f - radius);
        var upper = center + axis * (height * .5f - radius);
        var bottom = center - axis * height * .5f;
        var old = UnityEditor.Handles.color;
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawWireDisc(lower, axis, radius);
        UnityEditor.Handles.DrawWireDisc(upper, axis, radius);
        foreach (var radial in new[] { transform.right, transform.forward })
        {
            UnityEditor.Handles.DrawLine(lower + radial * radius, upper + radial * radius);
            UnityEditor.Handles.DrawLine(lower - radial * radius, upper - radial * radius);
            var normal = Vector3.Cross(axis, radial);
            UnityEditor.Handles.DrawWireArc(upper, normal, radial, 180, radius);
            UnityEditor.Handles.DrawWireArc(lower, normal, -radial, 180, radius);
        }
        UnityEditor.Handles.color = Color.yellow;
        UnityEditor.Handles.DrawWireDisc(bottom, Vector3.up, radius);
        UnityEditor.Handles.DrawWireDisc(bottom + Vector3.up * cc.stepOffset * Mathf.Abs(scale.y), Vector3.up, radius);
        UnityEditor.Handles.Label(bottom + transform.right * radius, "CC底部 / 黄色上圆环: 台阶偏移(Step Offset)");

        string ground = "探测范围内未检测到地面";
        // 中心 + 四条边缘射线，用于查看斜坡与台阶；这些仅为调试射线，不等同于CharacterController的接地检测逻辑。
        foreach (var offset in new[] { Vector3.zero, transform.right * radius * .8f, -transform.right * radius * .8f, transform.forward * radius * .8f, -transform.forward * radius * .8f })
        {
            var origin = bottom + offset + Vector3.up * .5f;
            bool found = Probe(origin, probeDistance + .5f, out var hit);
            float slope = found ? Vector3.Angle(hit.normal, Vector3.up) : 0;
            UnityEditor.Handles.color = !found ? Color.gray : slope <= cc.slopeLimit ? Color.green : Color.red;
            UnityEditor.Handles.DrawLine(origin, found ? hit.point : origin + Vector3.down * (probeDistance + .5f));
            if (found)
            {
                UnityEditor.Handles.DrawWireDisc(hit.point, hit.normal, .055f);
                UnityEditor.Handles.DrawLine(hit.point, hit.point + hit.normal * .55f);
                if (offset == Vector3.zero)
                    ground = $"地面物体: {hit.collider.name}\n斜坡角度: {slope:F1} / 最大限制 {cc.slopeLimit:F1} 度\n底部垂直间隙: {bottom.y - hit.point.y:F3} 米";
            }
        }
        UnityEditor.Handles.color = Color.white;
        string live = Application.isPlaying ? $"isGrounded(由Move更新): {cc.isGrounded}\n碰撞标记(CollisionFlags): {cc.collisionFlags}\n控制器速度(CC velocity): {cc.velocity}" : "编辑模式: isGrounded 不会被计算";
        UnityEditor.Handles.Label(upper + Vector3.up * (radius + .25f), $"{name} — CharacterController调试\n{live}\n{ground}\n高度 {height:F2}, 半径 {radius:F2}, 皮肤厚度 {cc.skinWidth:F3}\n下方射线仅用于调试，并非控制器内部接地求解逻辑。");
        if (showFootBones)
        {
            var animator = GetComponentInChildren<Animator>();
            if (animator && animator.isHuman)
                foreach (var bone in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
                {
                    var foot = animator.GetBoneTransform(bone); if (!foot) continue;
                    UnityEditor.Handles.color = Color.magenta;
                    UnityEditor.Handles.DrawWireDisc(foot.position, Vector3.up, .08f);
                    if (Probe(foot.position + Vector3.up * .3f, probeDistance + .3f, out var hit))
                    {
                        UnityEditor.Handles.DrawDottedLine(foot.position, hit.point, 3);
                        UnityEditor.Handles.Label(foot.position, $"{bone} 骨骼离地间隙: {foot.position.y - hit.point.y:F3} 米");
                    }
                }
        }
        UnityEditor.Handles.color = old;
    }
#endif
}
