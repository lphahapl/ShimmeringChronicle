using System.Collections.Generic;
using UnityEngine;

public class WeaponObj : MonoBehaviour
{
    public WeaponData weaponData;
    public WeaponSO SO;
    public GameObject parent;
    public List<Collider> targets = new List<Collider>();
    public LayerMask targetLayer;
    readonly HashSet<EnemyObj> hitEnemies = new HashSet<EnemyObj>();
    readonly HashSet<PlayerObj> hitPlayers = new HashSet<PlayerObj>();
    private EnemyObj ownerEnemy;
    
    [Header("Debug Settings")]
    [Tooltip("是否开启攻击判定范围调试显示")]
    public bool showDebug = true;
    [Tooltip("在运行/攻击时调试线条保留的时间（秒）")]
    public float debugDuration = 1.0f;
    [Tooltip("是否仅在选中物体时绘制Scene视图Gizmos")]
    public bool onlyWhenSelected = false;
    [Tooltip("预览或调试的连招段数索引(0开始)")]
    public int debugComboIndex = 0;
    [Tooltip("检测范围线框颜色")]
    public Color debugRangeColor = new Color(0.2f, 0.8f, 1f, 0.8f);
    [Tooltip("成功命中敌人的指示线颜色")]
    public Color debugHitColor = Color.red;
    [Tooltip("在球体内但因角度未命中的指示线颜色")]
    public Color debugMissColor = new Color(1f, 0.92f, 0.016f, 0.5f);

    public void Awake()
    {
        if (SO != null) weaponData = SO.CreateData();
        else Debug.LogWarning("[WeaponObj] Missing WeaponSO.", this);
        
        targetLayer = LayerMask.GetMask("enemy");
    }

    public void InitWeapon(GameObject parent)
    {
        UnsubscribeComboEvents();
        this.parent = parent;
        ownerEnemy = parent != null ? parent.GetComponent<EnemyObj>() : null;
        if (parent == null) return;

        targetLayer = ownerEnemy != null ? LayerMask.GetMask("Player") : LayerMask.GetMask("enemy");
        if (!isActiveAndEnabled) return;

        if (ownerEnemy != null)
            this.Subscribe<EnemyObj, int>(GameEvents.OnEnemyCombo, EnemyCheckAttack);
        else if (parent.GetComponent<PlayerObj>() != null)
            this.Subscribe<int>(GameEvents.OnPlayerCombo, CheckAttack);
    }

    private void OnEnable()
    {
        if (parent != null) InitWeapon(parent);
    }

    private void UnsubscribeComboEvents()
    {
        this.UnSubscribe<int>(GameEvents.OnPlayerCombo, CheckAttack);
        this.UnSubscribe<EnemyObj, int>(GameEvents.OnEnemyCombo, EnemyCheckAttack);
    }

    /// <summary>
    /// 获取指定攻击判定的世界坐标判定中心、朝向四元数和前向向量
    /// </summary>
    public void GetAttackTransform(AttackData attack, out Vector3 judgePos, out Quaternion facing, out Vector3 forward)
    {
        // 优先使用父对象（攻击者）的朝向；若无父对象则以剑自身轴（模型局部-Z）为基准
        facing = parent != null ? parent.transform.rotation :
            transform.rotation * Quaternion.Euler(0f, 180f, 0f);
        Vector3 origin = parent != null ? parent.transform.position : transform.position;
        judgePos = origin + facing * (attack != null ? attack.judgeOffset : Vector3.zero);
        forward = facing * Vector3.forward;
    }

    // One call represents one hit. Multiple colliders on an enemy deal damage once.
    public virtual void CheckAttack(int index)
    {
        targets ??= new List<Collider>();
        targets.Clear();
        hitEnemies.Clear();

        // 运行时让调试预览自动跟随当前触发的连招索引
        debugComboIndex = index;

        if (weaponData?.combo == null || index < 0 || index >= weaponData.combo.Count)
        {
            Debug.LogWarning($"[WeaponObj] 收到越界或无效的连招索引 index = {index} (当前武器连招总数: {weaponData?.combo?.Count ?? 0})", this);
            return;
        }

        AttackData attack = weaponData.combo[index];
        if (attack == null) return;

        if (showDebug)
        {
            Debug.Log($"[WeaponObj] 触发范围检测: Combo[{index}], 半径={attack.radius}m, 角度={attack.angle}°, 伤害={attack.damagePerHit}");
        }

        GetAttackTransform(attack, out Vector3 judgePos, out Quaternion facing, out Vector3 forward);
        float angle = attack.angle;
        float minDot = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
        Collider[] hits = Physics.OverlapSphere(judgePos, attack.radius, targetLayer);

        foreach (Collider col in hits)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;
            if (parent != null && col.transform.IsChildOf(parent.transform)) continue;

            EnemyObj enemy = col.GetComponent<EnemyObj>();
            if (enemy == null) enemy = col.GetComponentInParent<EnemyObj>();
            if (enemy == null || hitEnemies.Contains(enemy)) continue;

            Vector3 direction = col.ClosestPoint(judgePos) - judgePos;
            if (angle < 360f && direction.sqrMagnitude > 0.000001f &&
                Vector3.Dot(forward, direction.normalized) < minDot) continue;

            hitEnemies.Add(enemy);
            targets.Add(col);
            enemy.TakeDamage(attack.damagePerHit, parent);
        }

        // 调用调试器函数，显示范围检测的范围及命中目标
        if (showDebug)
        {
            DrawAttackDebug(judgePos, facing, attack.radius, angle, hits, targets);
        }
    }
    public virtual void EnemyCheckAttack(EnemyObj targetEnemy,int index)
    {
        if (!isActiveAndEnabled || ownerEnemy == null || targetEnemy != ownerEnemy) return;
        targets ??= new List<Collider>();
        targets.Clear();
        hitPlayers.Clear();

        // 运行时让调试预览自动跟随当前触发的连招索引
        debugComboIndex = index;

        if (weaponData?.combo == null || index < 0 || index >= weaponData.combo.Count)
        {
            Debug.LogWarning($"[WeaponObj] 收到越界或无效的连招索引 index = {index} (当前武器连招总数: {weaponData?.combo?.Count ?? 0})", this);
            return;
        }

        AttackData attack = weaponData.combo[index];
        if (attack == null) return;

        if (showDebug)
        {
            Debug.Log($"[WeaponObj] 触发范围检测: Combo[{index}], 半径={attack.radius}m, 角度={attack.angle}°, 伤害={attack.damagePerHit}");
        }

        GetAttackTransform(attack, out Vector3 judgePos, out Quaternion facing, out Vector3 forward);
        float angle = attack.angle;
        float minDot = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
        Collider[] hits = Physics.OverlapSphere(judgePos, attack.radius, targetLayer);
        
        foreach (Collider col in hits)
        {
            
            if (col == null || col.transform.IsChildOf(transform)) continue;
            if (parent != null && col.transform.IsChildOf(parent.transform)) continue;

            PlayerObj player = col.GetComponent<PlayerObj>();
            if (player == null) player = col.GetComponentInParent<PlayerObj>();
            if (player == null || hitPlayers.Contains(player)) continue;

            Vector3 direction = col.ClosestPoint(judgePos) - judgePos;
            if (angle < 360f && direction.sqrMagnitude > 0.000001f &&
                Vector3.Dot(forward, direction.normalized) < minDot) continue;

            hitPlayers.Add(player);
            targets.Add(col);
            player.TakeDamage(attack.damagePerHit, parent);
        }

        // 调用调试器函数，显示范围检测的范围及命中目标
        if (showDebug)
        {
            DrawAttackDebug(judgePos, facing, attack.radius, angle, hits, targets);
        }
    }
    /// <summary>
    /// 调试器函数：调试绘制当前 debugComboIndex 的检测范围
    /// </summary>
    [ContextMenu("Debug Draw Current Combo Range")]
    public void DrawAttackDebug()
    {
        DrawAttackDebug(debugComboIndex);
    }

    /// <summary>
    /// 调试器函数：按连招索引调试绘制检测范围
    /// </summary>
    public void DrawAttackDebug(int index)
    {
        List<AttackData> combos = weaponData?.combo ?? SO?.combo;
        if (combos == null || combos.Count == 0) return;
        if (index < 0 || index >= combos.Count) index = 0;

        AttackData attack = combos[index];
        if (attack == null) return;

        GetAttackTransform(attack, out Vector3 judgePos, out Quaternion facing, out _);
        Collider[] hits = Application.isPlaying
            ? Physics.OverlapSphere(judgePos, attack.radius, targetLayer)
            : null;

        DrawAttackDebug(judgePos, facing, attack.radius, attack.angle, hits, null);
    }

    /// <summary>
    /// 调试器核心函数：在 Scene/Game 视图中绘制范围检测的立体扇形/球体、朝向与命中反馈
    /// </summary>
    public void DrawAttackDebug(Vector3 judgePos, Quaternion facing, float radius, float angle, Collider[] hits = null, List<Collider> successHits = null)
    {
        if (radius <= 0f) return;

        Vector3 forward = facing * Vector3.forward;
        Vector3 up = facing * Vector3.up;
        Vector3 right = facing * Vector3.right;

        // 1. 判定中心点标记
        DrawDebugMarker(judgePos, 0.1f, Color.cyan, debugDuration);

        // 2. 正前方中心朝向线
        Debug.DrawRay(judgePos, forward * radius, Color.white, debugDuration);

        // 3. 角度与立体范围线框
        if (angle >= 360f)
        {
            // 全向球体判定（绘制3个互相垂直的圆环）
            DrawDebugCircle(judgePos, up, radius, debugRangeColor, debugDuration);
            DrawDebugCircle(judgePos, right, radius, debugRangeColor, debugDuration);
            DrawDebugCircle(judgePos, forward, radius, debugRangeColor, debugDuration);
        }
        else if (angle > 0.001f)
        {
            // 水平扇形边线与圆弧
            Vector3 leftDir = Quaternion.AngleAxis(-angle * 0.5f, up) * forward;
            Vector3 rightDir = Quaternion.AngleAxis(angle * 0.5f, up) * forward;
            Debug.DrawLine(judgePos, judgePos + leftDir * radius, debugRangeColor, debugDuration);
            Debug.DrawLine(judgePos, judgePos + rightDir * radius, debugRangeColor, debugDuration);
            DrawDebugArc(judgePos, up, leftDir, angle, radius, debugRangeColor, debugDuration);

            // 垂直扇形边线与圆弧（显示3D扇形张角）
            Vector3 topDir = Quaternion.AngleAxis(-angle * 0.5f, right) * forward;
            Vector3 bottomDir = Quaternion.AngleAxis(angle * 0.5f, right) * forward;
            Debug.DrawLine(judgePos, judgePos + topDir * radius, debugRangeColor, debugDuration);
            Debug.DrawLine(judgePos, judgePos + bottomDir * radius, debugRangeColor, debugDuration);
            DrawDebugArc(judgePos, right, topDir, angle, radius, debugRangeColor, debugDuration);

            // 锥体底部开口圆环
            float halfRad = angle * 0.5f * Mathf.Deg2Rad;
            float capDist = radius * Mathf.Cos(halfRad);
            float capRadius = radius * Mathf.Sin(halfRad);
            Vector3 capCenter = judgePos + forward * capDist;
            DrawDebugCircle(capCenter, forward, capRadius, debugRangeColor, debugDuration);
        }
        else
        {
            // angle <= 0 特殊情况（直线）
            Debug.DrawRay(judgePos, forward * radius, debugRangeColor, debugDuration);
        }

        // 4. 目标碰撞体命中与排除反馈
        if (hits != null)
        {
            foreach (Collider col in hits)
            {
                if (col == null || col.transform.IsChildOf(transform)) continue;
                if (parent != null && col.transform.IsChildOf(parent.transform)) continue;

                Vector3 closest = col.ClosestPoint(judgePos);
                bool isSuccess = successHits != null && successHits.Contains(col);

                Color lineColor = isSuccess ? debugHitColor : debugMissColor;
                Debug.DrawLine(judgePos, closest, lineColor, debugDuration);
                DrawDebugMarker(closest, isSuccess ? 0.2f : 0.1f, lineColor, debugDuration);
            }
        }
    }

    private static void DrawDebugArc(Vector3 center, Vector3 normal, Vector3 from, float angle, float radius, Color color, float duration, int segments = 24)
    {
        if (radius <= 0f || angle <= 0.001f) return;
        float step = angle / segments;
        Vector3 prevPoint = center + from.normalized * radius;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(step * i, normal) * from.normalized;
            Vector3 nextPoint = center + dir * radius;
            Debug.DrawLine(prevPoint, nextPoint, color, duration);
            prevPoint = nextPoint;
        }
    }

    private static void DrawDebugCircle(Vector3 center, Vector3 normal, float radius, Color color, float duration, int segments = 28)
    {
        if (radius <= 0f) return;
        Vector3 right = Vector3.Cross(normal, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(normal, Vector3.right);
        right.Normalize();

        float step = 360f / segments;
        Vector3 prevPoint = center + right * radius;
        for (int i = 1; i <= segments; i++)
        {
            Vector3 nextPoint = center + (Quaternion.AngleAxis(step * i, normal) * right) * radius;
            Debug.DrawLine(prevPoint, nextPoint, color, duration);
            prevPoint = nextPoint;
        }
    }

    private static void DrawDebugMarker(Vector3 position, float size, Color color, float duration)
    {
        Debug.DrawLine(position - Vector3.right * size, position + Vector3.right * size, color, duration);
        Debug.DrawLine(position - Vector3.up * size, position + Vector3.up * size, color, duration);
        Debug.DrawLine(position - Vector3.forward * size, position + Vector3.forward * size, color, duration);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!onlyWhenSelected && showDebug)
        {
            DrawEditorGizmos();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (onlyWhenSelected && showDebug)
        {
            DrawEditorGizmos();
        }
    }

    private void DrawEditorGizmos()
    {
        List<AttackData> combos = weaponData?.combo ?? SO?.combo;
        if (combos == null || combos.Count == 0) return;

        int index = Mathf.Clamp(debugComboIndex, 0, combos.Count - 1);
        AttackData attack = combos[index];
        if (attack == null) return;

        GetAttackTransform(attack, out Vector3 judgePos, out Quaternion facing, out Vector3 forward);
        Vector3 up = facing * Vector3.up;
        Vector3 right = facing * Vector3.right;
        float radius = attack.radius;
        float angle = attack.angle;

        var prevColor = UnityEditor.Handles.color;

        if (angle >= 360f)
        {
            UnityEditor.Handles.color = debugRangeColor;
            UnityEditor.Handles.DrawWireDisc(judgePos, up, radius);
            UnityEditor.Handles.DrawWireDisc(judgePos, right, radius);
            UnityEditor.Handles.DrawWireDisc(judgePos, forward, radius);
        }
        else if (angle > 0.001f)
        {
            Vector3 leftDir = Quaternion.AngleAxis(-angle * 0.5f, up) * forward;
            Vector3 rightDir = Quaternion.AngleAxis(angle * 0.5f, up) * forward;

            // 半透明扇形填充面
            Color fillColor = new Color(debugRangeColor.r, debugRangeColor.g, debugRangeColor.b, 0.12f);
            UnityEditor.Handles.color = fillColor;
            UnityEditor.Handles.DrawSolidArc(judgePos, up, leftDir, angle, radius);

            // 水平扇形边线与圆弧
            UnityEditor.Handles.color = debugRangeColor;
            UnityEditor.Handles.DrawWireArc(judgePos, up, leftDir, angle, radius);
            UnityEditor.Handles.DrawLine(judgePos, judgePos + leftDir * radius);
            UnityEditor.Handles.DrawLine(judgePos, judgePos + rightDir * radius);

            // 垂直弧线
            Vector3 topDir = Quaternion.AngleAxis(-angle * 0.5f, right) * forward;
            Vector3 bottomDir = Quaternion.AngleAxis(angle * 0.5f, right) * forward;
            UnityEditor.Handles.DrawWireArc(judgePos, right, topDir, angle, radius);
            UnityEditor.Handles.DrawLine(judgePos, judgePos + topDir * radius);
            UnityEditor.Handles.DrawLine(judgePos, judgePos + bottomDir * radius);

            // 锥体底部开口圆环
            float halfRad = angle * 0.5f * Mathf.Deg2Rad;
            float capDist = radius * Mathf.Cos(halfRad);
            float capRadius = radius * Mathf.Sin(halfRad);
            Vector3 capCenter = judgePos + forward * capDist;
            UnityEditor.Handles.DrawWireDisc(capCenter, forward, capRadius);
        }
        else
        {
            UnityEditor.Handles.color = debugRangeColor;
            UnityEditor.Handles.DrawLine(judgePos, judgePos + forward * radius);
        }

        // 中心朝向线
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.DrawLine(judgePos, judgePos + forward * radius);

        // 判定中心标记
        UnityEditor.Handles.color = Color.cyan;
        UnityEditor.Handles.DrawWireDisc(judgePos, up, 0.08f);

        // 场景视图文本标签
        string labelText = $"[{gameObject.name}] Combo[{index}]: 半径={radius}m, 角度={angle}°\n偏移={attack.judgeOffset}, 伤害={attack.damagePerHit}";
        UnityEditor.Handles.Label(judgePos + Vector3.up * (radius * 0.5f + 0.2f), labelText);

        UnityEditor.Handles.color = prevColor;
    }
#endif

    private void OnDisable()
    {
        UnsubscribeComboEvents();
    }
}
