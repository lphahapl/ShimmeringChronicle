using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


public class PlayerController : MonoBehaviour
{
   
    const string IdleStateName   = "Idle";
    const string WalkStateName   = "Walk";
    const string RunStateName    = "Run";
    const string JumpStateName   = "Jump";
    const string Attack1StateName = "Attack_01";
    const string Attack2StateName = "Attack_02";
    const string Attack3StateName = "Attack_03";
    const string HitStateName    = "Hit";
    const string DeadStateName   = "Dead";

    // ==================== Animator 参数 ====================
    // 预先算成哈希，省掉每帧的字符串查找
    static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
    static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
    static readonly int JumpHash      = Animator.StringToHash("Jump");
    static readonly int HitHash       = Animator.StringToHash("Hit");
    static readonly int IsDeadHash    = Animator.StringToHash("IsDead");

    // 三段连招按顺序触发：Attack_01 → Attack_02 → Attack_03
    static readonly int[] AttackHashes =
    {
        Animator.StringToHash("Attack_01"),
        Animator.StringToHash("Attack_02"),
        Animator.StringToHash("Attack_03"),
    };

    static readonly int IdleStateHash   = Animator.StringToHash(IdleStateName);
    static readonly int WalkStateHash   = Animator.StringToHash(WalkStateName);
    static readonly int RunStateHash    = Animator.StringToHash(RunStateName);
    static readonly int JumpStateHash   = Animator.StringToHash(JumpStateName);

    // 哈希 -> 名字，只给调试显示用
    static readonly Dictionary<int, string> StateNames = new Dictionary<int, string>
    {
        { IdleStateHash,    IdleStateName },
        { WalkStateHash,    WalkStateName },
        { RunStateHash,     RunStateName },
        { JumpStateHash,    JumpStateName },
        { Animator.StringToHash(Attack1StateName), Attack1StateName },
        { Animator.StringToHash(Attack2StateName), Attack2StateName },
        { Animator.StringToHash(Attack3StateName), Attack3StateName },
        { Animator.StringToHash(HitStateName),     HitStateName },
        { Animator.StringToHash(DeadStateName),    DeadStateName },
    };

    [Header("引用")]
    public CharacterController characterController;
    public Animator animator;
    private int HandingLayerIndex;
    AnimatorStateInfo stateInfo;

    public Camera playerCamera;
    private float walkSpeed = 2f;
    private float runSpeed = 5f;
    private float turnSpeed = 720f;
    [Tooltip("重力（负值），只用来把角色压在地面上")]
    public float gravity = -20f;
    PlayerObj player;              // 同一个物体上的数据持有者
    PlayerData data;               // 只在 Awake 里取一次，之后用本地副本
    public bool isCanAttack=true;
    public bool isTalking;
    public CharacterTime hitStop=>player.hitStop;
    public float dtFix=>player.dtFix;
    [Header("调试")]
    [Tooltip("在屏幕左上角显示当前状态名，验证状态机用")]
    public bool showDebugHUD = true;

    /// <summary>当前是否处于死亡状态，外部查询用</summary>
    public bool IsDead { get; private set; }

    // ---- 内部状态 ----
    Vector3 moveDir;          // 本帧想走的方向，世界空间、xz 平面、已归一化
    bool wantsRun;            // 想走还是想跑
    float verticalVelocity;   // 竖直速度，重力用
    int comboIndex;           // 连招进度：0 = 没在连招，1~3 = 已经出到第几段

  
    int pendingAttackIndex = -1;
    WeaponData comboWeapon;
    int ComboCount => Mathf.Min(data?.GetWeaponData()?.combo?.Count ?? 0, AttackHashes.Length);
    RuntimeAnimatorController defaultAnimatorController;

    void Awake()
    {
        if (!animator) animator = GetComponent<Animator>();
        if (animator) defaultAnimatorController = animator.runtimeAnimatorController;
        if (!characterController) characterController = GetComponent<CharacterController>();

        if (!animator)
            Debug.LogError("[PlayerController] 没找到 Animator，状态机驱动不了", this);
        if (!characterController)
            Debug.LogWarning("[PlayerController] 没找到 CharacterController，动画照常播但角色不会移动", this);
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        player = GetComponent<PlayerObj>();
        if (!player)
        {
            Debug.LogError("[PlayerController] 同一个物体上没有 PlayerObj，受伤/死亡事件收不到", this);
            return;
        }

        data = player.Data;
        if (data != null) InitSpeed();
        else Debug.LogError("[PlayerController] 玩家数据为空，速度保持 Inspector 上的默认值", this);


        HandingLayerIndex = animator ? animator.GetLayerIndex("HandingLayer") : -1;
    }

    // 订阅/退订用 OnEnable/OnDisable 成对写。
    // 用 Awake/OnDestroy 的话，物体被禁用期间仍然收事件；
    // 而且 Awake 只跑一次、OnEnable 每次激活都跑，很容易订阅两次。
    void OnEnable()
    {
        this.Subscribe(GameEvents.Damaged, OnDamaged);
        this.Subscribe(GameEvents.HitStopRequested, OnHitStopRequested);
        this.Subscribe(GameEvents.Died, OnDied);
        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        this.Subscribe(GameEvents.OnSelectedSlotChanged, OnSelectedSlotChanged);
        this.Subscribe(GameEvents.OnPlayerTalk, OnPlayerTalk);
        RefreshItemAnimator();
    }

    void OnDisable()
    {
        ResetCombo();
        comboWeapon = null;
        this.UnSubscribe(GameEvents.Damaged, OnDamaged);
        this.UnSubscribe(GameEvents.HitStopRequested, OnHitStopRequested);
        this.UnSubscribe(GameEvents.Died, OnDied);
        this.UnSubscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        this.UnSubscribe(GameEvents.OnSelectedSlotChanged, OnSelectedSlotChanged);
        this.UnSubscribe(GameEvents.OnPlayerTalk, OnPlayerTalk);
    }

    void OnPlayerTalk(PlayerObj talkingPlayer, bool talking)
    {
        if (talkingPlayer != player) return;
        isTalking = talking;
        isCanAttack = CheckCanAttack();
        if (isTalking) StopMove();
    }

    void OnItemsChanged(List<ItemData> changed)
    {
        if (data == null || (!ReferenceEquals(changed, data.Bag) &&
            !ReferenceEquals(changed, data.quickBar))) return;
        RefreshItemAnimator();
    }

    void OnSelectedSlotChanged(ItemSlot slot)
    {
        // Resolve our own selection, even when another player publishes this event.
        RefreshItemAnimator();
    }

    void RefreshItemAnimator()
    {
        
        if (!animator || !player) return;
        var weapon = player.isActiveAndEnabled ? player.SelectedItem : null;
        if (data != null) data.HandingItem = weapon;
        if (!ReferenceEquals(comboWeapon, data?.GetWeaponData()))
        {
            bool wasAttacking = comboWeapon != null && (comboIndex > 0 || pendingAttackIndex >= 0);
            ResetCombo();
            comboWeapon = data?.GetWeaponData();
            if (wasAttacking && !IsDead) animator.Play(IdleStateHash, 0, 0f);
        }
        RuntimeAnimatorController controller = weapon?.overrideController;
        if (!controller) controller = defaultAnimatorController;
        if (animator.runtimeAnimatorController == controller) return;

        comboIndex = 0;
        for (int i = 0; i < AttackHashes.Length; i++)
            animator.ResetTrigger(AttackHashes[i]);
        animator.runtimeAnimatorController = controller;
    }
 
    void OnHitStopRequested(GameObject victim, GameObject attacker, AttackData attack)
    {
        if (victim != gameObject && attacker != gameObject) return;
        if (!player || !hitStop || attack == null || attack.time <= 0f) return;
        hitStop.Apply(attack.priority, attack.scale, attack.time);
    }

    void OnDamaged(GameObject victim, float damage, GameObject attacker)
    {
        if (victim != gameObject) return;   // 总线上有别人的受伤事件，只认自己
        if (IsDead) return;
        Vector3 damageDir = attacker.transform.position - transform.position;
        damageDir.y = 0f; 

        if (damageDir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(damageDir, Vector3.up);
        }
        // 这里只做「反应」：播受击动画。
        //
        // 伤害是别人已经施加完的事实，这里绝不能再施加一次 ——
        // 一施加就又 Publish(Damaged)，绕回来又是这里，一路递归到爆栈。
        ResetCombo();
        comboWeapon = null;
        animator.SetTrigger(HitHash);
    }

    void OnDied(GameObject who)
    {
        if (who != gameObject) return;
        Die();
    }

    void Update()
    {
        if (!animator) return;


        stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        UpdateComboReset();
        ReadKeyboard();
        UpdateLocomotion();
    }

    
    void ReadKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.Tab) && UIManager.Instance)
            UIManager.Instance.Toggle<BagPanel>();
        if (Input.GetKeyDown(KeyCode.V) && UIManager.Instance)
            UIManager.Instance.Toggle<MissionsBar>();
        if (isTalking)
        {
            if (Input.GetKeyDown(KeyCode.Space))
                this.Publish(GameEvents.OnPlayerContinue);
            return;
        }

        float h = Input.GetAxis("Horizontal");   // A/D
        float v = Input.GetAxis("Vertical");     // W/S

        Vector3 r = h * playerCamera.transform.right;
        r.y = 0;
        Vector3 f = v*playerCamera.transform.forward;
        f.y = 0;
        Vector3 dir = r + f;



        if(!stateInfo.IsName("Base Layer.Hit"))
        {
            SetMove(dir, Input.GetKey(KeyCode.LeftShift));
            
        }
        

      
        if (Input.GetKeyDown(KeyCode.F) && !IsDead) player?.TryInteract();
        if (isTalking) return;
        ReadQuickBarWheel();

        isCanAttack = CheckCanAttack();
        if (isCanAttack)
        {
            if (Input.GetMouseButtonDown(0) && !ItemSlot.IsPointerOverSlot(Input.mousePosition)) Attack();
            if (Input.GetKeyDown(KeyCode.Space)) Jump();
            if (Input.GetKeyDown(KeyCode.H)) TakeHit();
            if (Input.GetKeyDown(KeyCode.L)) Die();
        }
    }
    private void ReadQuickBarWheel()
    {
        var manager = UIManager.Instance;
        if (player == null || manager == null) return;
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Approximately(scroll, 0f)) return;
        if (manager.BlocksQuickBarScroll) return;
        var bar = manager.Get<QuickBar>();
        if (bar == null || bar.quickSlots == null || bar.quickSlots.Count == 0) return;
        int count = bar.quickSlots.Count;
        int current = bar.quickSlots.IndexOf(player.SelectedSlot);
        int next = current < 0 ? (scroll > 0f ? 0 : count - 1)
            : (current + (scroll > 0f ? -1 : 1) + count) % count;
        var slot = bar.quickSlots[next];
        if (slot != null) this.Publish(GameEvents.OnSlotSelectionRequested, slot);
    }

    public void SetMove(Vector3 direction, bool run)
    {
        direction.y = 0f;
        moveDir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        wantsRun = run;
    }

    public void StopMove()
    {
        SetMove(Vector3.zero, false);
    }


    public void Attack()
    {
        if (IsDead || !animator || !CheckCanAttack() || ComboCount == 0) return;
        if (pendingAttackIndex >= 0 || animator.IsInTransition(0)) return;
        int current = AttackIndex(animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
        int next = current + 1;
        if (current < 0 && !IsLocomotionState()) return;
        if (next >= ComboCount) return;
        comboWeapon = data.GetWeaponData();
        pendingAttackIndex = next;
        animator.SetTrigger(AttackHashes[next]);
    }

    public void Jump()
    {
        if (IsDead || isTalking) return;
        animator.SetTrigger(JumpHash);
    }

   
    public void TakeHit()
    {
        if (IsDead) return;
        player.TakeDamage(50, this.gameObject);
    }

   
    public void Die()
    {
        if (IsDead) return;

        ResetCombo();
        comboWeapon = null;
        IsDead = true;
        StopMove();
        animator.SetBool(IsDeadHash, true);
    }

   
    public void PublishPlayerCombo(AnimationEvent animationEvent)
    {
        if (IsDead || !isActiveAndEnabled || data?.GetWeaponData() == null ||
            !ReferenceEquals(comboWeapon, data.GetWeaponData())) return;
        int index = AttackIndex(animationEvent.animatorStateInfo.shortNameHash);
        if (index < 0 || index >= ComboCount) return;
        this.Publish(GameEvents.OnPlayerCombo, index);
    }

    static int AttackIndex(int stateHash)
    {
        for (int i = 0; i < AttackHashes.Length; i++)
            if (AttackHashes[i] == stateHash) return i;
        return -1;
    }

    void ResetCombo()
    {
        comboIndex = 0;
        pendingAttackIndex = -1;
        if (!animator) return;
        foreach (int hash in AttackHashes) animator.ResetTrigger(hash);
    }

    public string CurrentStateName
    {
        get
        {
            if (!animator) return "(无 Animator)";

            if (animator.IsInTransition(0))
            {
                return StateNameOf(animator.GetCurrentAnimatorStateInfo(0).shortNameHash)
                     + " -> "
                     + StateNameOf(animator.GetNextAnimatorStateInfo(0).shortNameHash);
            }
            return StateNameOf(animator.GetCurrentAnimatorStateInfo(0).shortNameHash);
        }
    }
    void UpdateLocomotion()
    {
        float dt = dtFix;
        bool moving = !IsDead
                   && !isTalking
                   && IsLocomotionState()
                   && moveDir.sqrMagnitude > 0.0001f;
        animator.SetBool(IsWalkingHash, moving);
        animator.SetBool(IsRunningHash, moving && wantsRun);

        if (!characterController) return;

        if (moving)
        {
            Quaternion target = Quaternion.LookRotation(moveDir, Vector3.up);
            if (!stateInfo.IsName("Base Layer.Hit"))
            {
                transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, turnSpeed * dt);

            }
           
        }
        if (characterController.isGrounded && verticalVelocity < 0f) verticalVelocity = -2f;
        verticalVelocity += gravity * dt;

        float speed = (moving && wantsRun) ? runSpeed : walkSpeed;
        Vector3 velocity = (moving ? moveDir * speed : Vector3.zero)
                         + Vector3.up * verticalVelocity;

        characterController.Move(velocity * dt);
    }

    void UpdateComboReset()
    {
        var state = animator.GetCurrentAnimatorStateInfo(0);
        int current = AttackIndex(state.shortNameHash);
        if (current >= 0)
        {
            comboIndex = current + 1;
            if (pendingAttackIndex == current) pendingAttackIndex = -1;
        }
        else if (!animator.IsInTransition(0) && (pendingAttackIndex < 0 || comboIndex > 0))
        {
            ResetCombo();
        }
    }

    bool IsLocomotionState()
    {
        int h = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        return h == IdleStateHash || h == WalkStateHash
            || h == RunStateHash  || h == JumpStateHash;
    }

    static string StateNameOf(int hash)
    {
        return StateNames.TryGetValue(hash, out string name) ? name : "未知(" + hash + ")";
    }

    void OnGUI()
    {
        if (!showDebugHUD || !animator) return;

        string dead = IsDead ? "  [已死亡]" : "";
        GUI.Label(new Rect(10, 10, 600, 24), "当前状态: " + CurrentStateName + dead);
        string speed = characterController ? characterController.velocity.magnitude.ToString("F2") : "无 CC";
        GUI.Label(new Rect(10, 30, 600, 24), "连招: " + comboIndex + "/" + ComboCount + "   速度: " + speed);
    }
    private void InitSpeed()
    {
        walkSpeed = data.speed;
        runSpeed=data.runSpeed;
        turnSpeed = data.turnSpeed;
    }
    public void DestorySelf()
    {
        Destroy(this.gameObject);
    }
    private bool CheckCanAttack()
    {
        if (isTalking) return false;
        if (ItemSlot.IsAnyDragging) return false;
        var manager = UIManager.Instance;
        if (manager == null) return true;

        return !manager.HasOpenPanel;
    }
}
