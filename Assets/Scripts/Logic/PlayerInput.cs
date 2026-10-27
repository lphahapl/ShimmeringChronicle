using System.Collections.Generic;
using UnityEngine;

// 方向每帧更新，攻击和闪避在有效时间内等待动作消费。
public sealed class PlayerInput
{
    float flashBufferTime;
    float comboBufferTime;
    bool flashBuffered;
    float flashExpiresAt;
    Queue<float> bufferedCombo = new Queue<float>();

    public Vector2 MoveInput { get; private set; }
    public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;
    public int BufferedComboCount => bufferedCombo.Count;
    public bool BagPressed { get; private set; }
    public bool MissionsPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool ContinuePressed { get; private set; }
    public bool AttackPressed { get; private set; }
    public bool DebugHitPressed { get; private set; }
    public bool DebugDiePressed { get; private set; }
    public float ScrollValue { get; private set; }
    public Vector2 PointerPosition { get; private set; }

    public PlayerInput(float flashTime, float comboTime = 0.5f)
    {
        flashBufferTime = Mathf.Max(0f, flashTime);
        comboBufferTime = Mathf.Max(0f, comboTime);
    }

    // 所有键鼠都在这里读取，控制器只使用结果。
    public void ReadInput(float time)
    {
        Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        bool flash = Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
        BagPressed = Input.GetKeyDown(KeyCode.Tab);
        MissionsPressed = Input.GetKeyDown(KeyCode.V);
        InteractPressed = Input.GetKeyDown(KeyCode.F);
        ContinuePressed = Input.GetKeyDown(KeyCode.Space);
        AttackPressed = Input.GetMouseButtonDown(0);
        ScrollValue = Input.GetAxis("Mouse ScrollWheel");
        PointerPosition = Input.mousePosition;
        DebugHitPressed = false;
        DebugDiePressed = false;
#if UNITY_EDITOR
        DebugHitPressed = Input.GetKeyDown(KeyCode.H);
        DebugDiePressed = Input.GetKeyDown(KeyCode.L);
#endif
        RecordInput(move, flash, time);
    }

    public void RecordInput(Vector2 move, bool flashPressed, float time)
    {
        MoveInput = Vector2.ClampMagnitude(move, 1f);
        ClearExpiredCombo(time);
        if (flashPressed)
        {
            flashBuffered = true;
            flashExpiresAt = time + flashBufferTime;
        }
        if (flashBuffered && time > flashExpiresAt) flashBuffered = false;
    }

    public void RecordComboInput(float time)
    {
        ClearExpiredCombo(time);
        if (bufferedCombo.Count >= 3) return;
        bufferedCombo.Enqueue(time + comboBufferTime);
    }

    public bool TryConsumeCombo(float time)
    {
        ClearExpiredCombo(time);
        if (bufferedCombo.Count == 0) return false;
        bufferedCombo.Dequeue();
        return true;
    }

    public bool TryConsumeFlash(float time)
    {
        if (!flashBuffered) return false;
        flashBuffered = false;
        return time <= flashExpiresAt;
    }

    public void ClearExpiredCombo(float time)
    {
        while (bufferedCombo.Count > 0 && time > bufferedCombo.Peek())
            bufferedCombo.Dequeue();
    }

    public void ClearCombo()
    {
        bufferedCombo.Clear();
    }

    public void Clear()
    {
        MoveInput = Vector2.zero;
        flashBuffered = false;
        ClearCombo();
    }
}
