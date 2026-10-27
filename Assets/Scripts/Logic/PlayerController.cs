using UnityEngine;

// 只负责动作和动画。玩家数据、装备、死亡和对话由 PlayerObj 管。
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerObj), typeof(Animator), typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public CharacterController characterController;
    public Animator animator;
    public Camera playerCamera;
    public float gravity = -20f;
    public bool showDebugHUD = true;
    public bool readInput = true;

    [Header("移动和转身")]
    public float maxSpeed = 2f;
    public float acceleration = 2f;
    public float turnSmoothTime = 0.12f;
    public float standingTurnEnterAngle = 60f;
    public float movingTurnEnterAngle = 100f;
    public float turnExitAngle = 10f;

    PlayerObj player;
    PlayerInput playerInput;
    WeaponData comboWeapon;
    RuntimeAnimatorController defaultAnimatorController;
    int[] attackHashes = new int[0];
    int nextComboIndex;
    Vector3 targetDirection;
    Vector3 gravityStep;
    bool hasTarget;
    bool isTurning;
    float speed;
    float verticalVelocity;
    float inputTime;

    public bool IsDead
    {
        get { return player != null && player.IsDead; }
    }
    public bool isTalking
    {
        get { return player != null && player.IsTalking; }
    }
    public bool isCanAttack
    {
        get { return player != null && player.CanAct; }
    }
    public float dtFix
    {
        get
        {
            if (player != null) return player.dtFix;
            return Time.deltaTime;
        }
    }
    int ComboCount
    {
        get
        {
            if (player == null) return 0;
            WeaponData weapon = player.Data.GetWeaponData();
            if (weapon == null || weapon.combo == null) return 0;
            return Mathf.Min(weapon.combo.Count, attackHashes.Length);
        }
    }

    void Awake()
    {
        player = GetComponent<PlayerObj>();
        if (animator == null) animator = GetComponent<Animator>();
        if (characterController == null) characterController = GetComponent<CharacterController>();
        if (playerCamera == null) playerCamera = Camera.main;
        defaultAnimatorController = animator.runtimeAnimatorController;
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        playerInput = new PlayerInput(0.15f, 0.5f);
        ConfigureAnimator();
    }

    void OnEnable()
    {
        player.EquippedItemChanged += OnEquippedItemChanged;
        player.Staggered += OnStaggered;
        player.ActionLockChanged += OnActionLockChanged;
        this.Subscribe(GameEvents.Died, OnDied);
        RefreshItemAnimator();
    }

    void OnDisable()
    {
        if (player != null)
        {
            player.EquippedItemChanged -= OnEquippedItemChanged;
            player.Staggered -= OnStaggered;
            player.ActionLockChanged -= OnActionLockChanged;
        }
        this.UnSubscribe(GameEvents.Died, OnDied);
        ClearInput();
    }

    void ConfigureAnimator()
    {
        int comboCount = 0;
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type != AnimatorControllerParameterType.Trigger) continue;
            if (!parameter.name.StartsWith("Combo")) continue;
            int number;
            if (int.TryParse(parameter.name.Substring(5), out number))
                comboCount = Mathf.Max(comboCount, number);
        }
        attackHashes = new int[comboCount];
        for (int i = 0; i < comboCount; i++)
        {
            string stateName = "Combo" + (i + 1);
            attackHashes[i] = Animator.StringToHash(stateName);
        }
    }

    void OnEquippedItemChanged(ItemData item)
    {
        RefreshItemAnimator();
    }

    void RefreshItemAnimator()
    {
        if (animator == null || player == null) return;
        ClearInput();
        RuntimeAnimatorController controller = defaultAnimatorController;
        ItemData item = player.EquippedItem;
        // 武器覆盖动画必须基于同一套通用状态机，旧状态机不再切入。
        if (item != null && item.overrideController != null)
        {
            if (item.overrideController.runtimeAnimatorController == defaultAnimatorController)
                controller = item.overrideController;
        }
        if (animator.runtimeAnimatorController != controller)
            animator.runtimeAnimatorController = controller;
        ConfigureAnimator();
        if (!IsDead) PlayIdle();
    }

    void PlayIdle()
    {
        string stateName = "Base Layer.Locomotion.Idle_Wait_A";
        if (animator.HasState(0, Animator.StringToHash(stateName))) animator.Play(stateName, 0, 0f);
    }

    public void ApplyInput(PlayerInput input, Vector3 referenceForward, float deltaTime, float time)
    {
        playerInput = input;
        inputTime = time;
        if (animator == null || input == null) return;
        if (!player.CanAct)
        {
            ClearInput();
            return;
        }
        Vector3 direction = GetInputDirection(input.MoveInput, referenceForward);
        UpdateLocomotion(input, direction, deltaTime, time);
    }

    Vector3 GetInputDirection(Vector2 move, Vector3 referenceForward)
    {
        Vector3 forward = Vector3.ProjectOnPlane(referenceForward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
        forward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return Vector3.ClampMagnitude(forward * move.y + right * move.x, 1f);
    }

    void UpdateLocomotion(PlayerInput input, Vector3 direction, float deltaTime, float time)
    {
        if (IsState("Hit"))
        {
            ClearInput();
            return;
        }
        if (input.HasMoveInput)
        {
            targetDirection = direction.normalized;
            hasTarget = true;
        }
        float angle = 0f;
        if (hasTarget) angle = Vector3.SignedAngle(transform.forward, targetDirection, Vector3.up);
        bool dodging = IsTag("Dodge");
        if (dodging) isTurning = false;
        else if (isTurning)
        {
            if (Mathf.Abs(angle) <= turnExitAngle) isTurning = false;
        }
        else if (input.HasMoveInput)
        {
            float enterAngle = standingTurnEnterAngle;
            if (speed > 0f) enterAngle = movingTurnEnterAngle;
            if (Mathf.Abs(angle) >= enterAngle) isTurning = true;
        }
        if (isTurning)
        {
            // 转身保留下一段序号和有效缓存，只中止当前攻击。
            int current = CurrentAttackIndex();
            if (current >= 0 && ComboCount > 0) nextComboIndex = (current + 1) % ComboCount;
            ResetAttackTriggers();
            animator.SetBool("IsAttacking", false);
        }
        bool moving = input.HasMoveInput && !isTurning;
        if (!moving) speed = 0f;
        else if (dodging) speed = maxSpeed;
        else speed = Mathf.MoveTowards(speed, maxSpeed, acceleration * deltaTime);
        animator.SetBool("IsTurning", isTurning);
        animator.SetBool("IsMove", moving);
        animator.SetFloat("Speed", speed);
        animator.SetFloat("Angle", angle);
        animator.SetFloat("Turn", Mathf.Clamp(angle / 90f, -1f, 1f), turnSmoothTime, deltaTime);
        if (!dodging && input.TryConsumeFlash(time))
        {
            isTurning = false;
            hasTarget = false;
            speed = 0f;
            if (input.HasMoveInput) speed = maxSpeed;
            animator.SetBool("IsTurning", false);
            animator.SetBool("IsMove", input.HasMoveInput);
            animator.SetFloat("Speed", speed);
            float flashAngle = 0f;
            if (input.HasMoveInput)
                flashAngle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
            animator.SetFloat("Angle", flashAngle);
            animator.SetTrigger("IsFlash");
        }
        else if (!dodging) TryStartAttack();
        if (!isTurning && !input.HasMoveInput) hasTarget = false;
    }

    void Update()
    {
        if (animator == null || characterController == null) return;
        if (readInput)
        {
            playerInput.ReadInput(Time.time);
            HandleInput();
        }
        if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dtFix;
        gravityStep = Vector3.up * verticalVelocity * dtFix;
    }

    // 按键已经由 PlayerInput 读好，这里处理操作请求和界面拦截。
    void HandleInput()
    {
        UIManager manager = UIManager.Instance;
        if (playerInput.BagPressed && manager != null) manager.Toggle<BagPanel>();
        if (playerInput.MissionsPressed && manager != null) manager.Toggle<MissionsBar>();
        if (player.IsDead)
        {
            ClearInput();
            return;
        }
        if (player.IsTalking)
        {
            if (playerInput.ContinuePressed) this.Publish(GameEvents.OnPlayerContinue);
            ClearInput();
            return;
        }
        if (playerInput.InteractPressed) player.TryInteract();
        if (player.IsTalking)
        {
            ClearInput();
            return;
        }
        ReadQuickBarWheel();
        bool blocked = ItemSlot.IsAnyDragging;
        if (manager != null && manager.HasOpenPanel) blocked = true;
        if (blocked)
        {
            ClearInput();
            return;
        }
        if (playerInput.AttackPressed && !ItemSlot.IsPointerOverSlot(playerInput.PointerPosition))
            playerInput.RecordComboInput(Time.time);
#if UNITY_EDITOR
        if (playerInput.DebugHitPressed) player.TakeDamage(50f, gameObject);
        if (playerInput.DebugDiePressed) player.Die();
#endif
        Vector3 referenceForward = Vector3.forward;
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera != null) referenceForward = playerCamera.transform.forward;
        ApplyInput(playerInput, referenceForward, dtFix, Time.time);
    }

    void ReadQuickBarWheel()
    {
        UIManager manager = UIManager.Instance;
        if (manager == null || manager.BlocksQuickBarScroll) return;
        float scroll = playerInput.ScrollValue;
        if (Mathf.Approximately(scroll, 0f)) return;
        QuickBar bar = manager.Get<QuickBar>();
        if (bar == null || bar.quickSlots == null || bar.quickSlots.Count == 0) return;
        int count = bar.quickSlots.Count;
        int current = bar.quickSlots.IndexOf(player.SelectedSlot);
        int next;
        if (current < 0)
        {
            next = count - 1;
            if (scroll > 0f) next = 0;
        }
        else
        {
            int step = 1;
            if (scroll > 0f) step = -1;
            next = (current + step + count) % count;
        }
        ItemSlot slot = bar.quickSlots[next];
        if (slot != null) this.Publish(GameEvents.OnSlotSelectionRequested, slot);
    }

    void OnApplicationFocus(bool focus)
    {
        if (!focus) ClearInput();
    }

    public void Attack()
    {
        if (!player.CanAct || playerInput == null) return;
        playerInput.RecordComboInput(Time.time);
    }

    public void TryStartAttack()
    {
        if (!player.CanAct || ComboCount == 0) return;
        if (isTurning || IsTag("Dodge") || IsState("Hit")) return;
        if (animator.GetBool("IsAttacking") || playerInput == null) return;
        if (!playerInput.TryConsumeCombo(inputTime)) return;
        if (nextComboIndex >= ComboCount) nextComboIndex = 0;
        comboWeapon = player.Data.GetWeaponData();
        animator.SetTrigger(attackHashes[nextComboIndex]);
        nextComboIndex = (nextComboIndex + 1) % ComboCount;
        animator.SetBool("IsAttacking", true);
    }

    // 保留源动画事件的方法名。这个事件只检查接招，不造成伤害。
    public void TryComsumeCombo()
    {
        if (!player.CanAct || playerInput == null) return;
        if (!IsTag("Attack") || IsTag("Dodge") || isTurning || animator.IsInTransition(0)) return;
        if (nextComboIndex <= 0 || nextComboIndex >= ComboCount) return;
        if (!playerInput.TryConsumeCombo(Time.time)) return;
        animator.SetTrigger(attackHashes[nextComboIndex]);
        nextComboIndex = (nextComboIndex + 1) % ComboCount;
    }

    public void PublishPlayerCombo(AnimationEvent animationEvent)
    {
        if (!player.CanAct || comboWeapon == null) return;
        int index = AttackIndex(animationEvent.animatorStateInfo.shortNameHash);
        if (index < 0 || index >= ComboCount) return;
        player.PublishAttackHit(comboWeapon, index);
    }

    int AttackIndex(int hash)
    {
        for (int i = 0; i < attackHashes.Length; i++)
            if (attackHashes[i] == hash) return i;
        return -1;
    }

    int CurrentAttackIndex()
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (AttackIndex(next.shortNameHash) >= 0) state = next;
        }
        return AttackIndex(state.shortNameHash);
    }

    bool IsTag(string tag)
    {
        if (animator.GetCurrentAnimatorStateInfo(0).IsTag(tag)) return true;
        if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsTag(tag)) return true;
        return false;
    }

    bool IsState(string state)
    {
        if (animator.GetCurrentAnimatorStateInfo(0).IsName(state)) return true;
        if (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state)) return true;
        return false;
    }

    void ResetAttackTriggers()
    {
        if (animator == null) return;
        foreach (int hash in attackHashes) animator.ResetTrigger(hash);
    }

    public void ClearAttack()
    {
        ResetAttackTriggers();
        if (animator != null) animator.SetBool("IsAttacking", false);
        if (playerInput != null) playerInput.ClearCombo();
        nextComboIndex = 0;
        comboWeapon = null;
    }

    public void ClearInput()
    {
        if (playerInput != null) playerInput.Clear();
        hasTarget = false;
        isTurning = false;
        speed = 0f;
        ClearAttack();
        if (animator == null) return;
        animator.SetBool("IsTurning", false);
        animator.SetBool("IsMove", false);
        animator.SetFloat("Speed", 0f);
        animator.SetFloat("Turn", 0f);
        animator.ResetTrigger("IsFlash");
    }

    void OnStaggered(GameObject attacker)
    {
        ClearInput();
        if (attacker != null)
        {
            Vector3 direction = attacker.transform.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction);
        }
        animator.SetTrigger("Hit");
    }

    void OnActionLockChanged(bool locked)
    {
        if (!locked) return;
        ClearInput();
        if (!IsDead) PlayIdle();
    }

    void OnDied(GameObject who)
    {
        if (who != gameObject) return;
        ClearInput();
        animator.SetBool("IsDead", true);
    }

    public void TakeHit()
    {
        player.TakeDamage(50f, gameObject);
    }

    public void Die()
    {
        player.Die();
    }

    public void DestorySelf()
    {
        player.RemovePlayer();
    }

    protected virtual void OnAnimatorMove()
    {
        if (animator == null || characterController == null || !characterController.enabled) return;
        Vector3 movement = animator.deltaPosition;
        movement.y = 0f;
        if (!player.CanAct) movement = Vector3.zero;
        movement += gravityStep;
        characterController.Move(movement);
        if (player.CanAct && !IsState("Hit"))
            transform.rotation *= animator.deltaRotation;
    }

    public string CurrentStateName
    {
        get
        {
            if (animator == null) return "无 Animator";
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (IsDead) return "Dead";
            if (IsTag("Attack")) return "Combo" + (CurrentAttackIndex() + 1);
            if (IsTag("Dodge")) return "Dodge";
            if (IsTag("Turn")) return "Turn";
            if (IsState("Hit")) return "Hit";
            if (animator.GetBool("IsMove")) return "Move";
            if (IsState("Idle_Wait_A")) return "Idle";
            return "过渡中";
        }
    }

    void OnGUI()
    {
        if (!showDebugHUD || animator == null) return;
        GUI.Label(new Rect(10, 10, 650, 24), "当前状态: " + CurrentStateName);
        GUI.Label(new Rect(10, 32, 650, 24), "下一段: " + (nextComboIndex + 1) + " / " + ComboCount);
    }
}
