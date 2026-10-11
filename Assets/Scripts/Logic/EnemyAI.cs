using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;
using static UnityEditor.Progress;

public class EnemyAI : MonoBehaviour
{
    public Animator animator;
    public EnemyData data;
    public GameObject Player;
    public NavMeshAgent self;
    public EnemyObj enemyObj;
    private float dtFix => enemyObj.dtFix;
    AnimatorStateInfo stateInfo;
    public float distance;
    public float stopDistance;
    public bool isAttacking;
    public int maxComboIndex;
    public int comboIndex;
    public bool isInAttackBreak=>data.weaponData.attackBreak>0f;
    public bool isHit=>data.hitStun>0f;
    static readonly int[] AttackHashes =
   {
        Animator.StringToHash("Attack1"),
        Animator.StringToHash("Attack2"),
        Animator.StringToHash("Attack3"),
       
    };
    /// <summary>
    /// 是否找到玩家
    /// </summary>
    public bool isActive;
    public BoxCollider triggerCollider;
    private Coroutine detectionRoutine;
    private void Awake()
    {
        if (!enemyObj) enemyObj = GetComponent<EnemyObj>();
        self=GetComponent<NavMeshAgent>();
        triggerCollider = GetComponent<BoxCollider>();
        triggerCollider.isTrigger = true;
        this.Subscribe<EnemyObj, ItemData>(GameEvents.OnEnemyChangedHandingItem, OnCHnagedItem);
        
    }
    void Start()
    {
        InitAgentParameters();

    }

    private void OnEnable()
    {
        if (data != null) ResetDetectionRange();
    }

    public void ResetDetectionRange()
    {
        if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider>();
        if (detectionRoutine != null)
        {
            StopCoroutine(detectionRoutine);
            detectionRoutine = null;
        }

        Player = null;
        distance = 0f;
        StopAI();
        triggerCollider.size = Vector3.one;
        if (!isActiveAndEnabled) return;

        Vector3 targetSize = new Vector3(data.chaseDistance, 8f, data.chaseDistance);
        detectionRoutine = StartCoroutine(LerpTrrigerSize(targetSize));
    }

    protected virtual void Update()
    {
        if (data.hitStun > 0)
        {
            data.hitStun-=dtFix;
        }
        else
        {
            data.hitStun = 0;
        }
        if (data.weaponData.attackBreak > 0)
        {
            data.weaponData.attackBreak -= dtFix;
        }
        else
        {
            data.weaponData.attackBreak = 0;
        }
        stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        if (enemyObj.isDead || Player == null)
        {
            StopAI();
            return;
        }
        
        if (stateInfo.IsName("Base Layer.Idle")|| stateInfo.IsName("Base Layer.Hit"))
        {
            if (stateInfo.IsName("Base Layer.Hit"))
            {
                data.hitStun = enemyObj.SO.hitStun;
            }
            ResetCombo();
        }
        

        if (isHit)
        {
            StopAI();
            return;
        }

        distance = Vector3.Distance(transform.position, Player.transform.position);
        if (distance <= stopDistance)
        {
            StopAI();
            if (maxComboIndex >= 0 && !animator.IsInTransition(0) && !isAttacking&& !isHit)
            {
                if (comboIndex == 0 && isInAttackBreak) return;
                
                TryAttack();
            }
                
        }
            
        else
            StartAI();
    }

    private IEnumerator LerpTrrigerSize(Vector3 targetVector3)
    {
        Vector3 origin= triggerCollider.size;
        float t=0f;
        float duration = 0.5f;
       
        while(t<duration)
        {
            t += dtFix;
            float T = Mathf.Clamp01(t / duration);
            triggerCollider.size = Vector3.Lerp(origin,targetVector3, T);
            yield return null;
        }
        triggerCollider.size = targetVector3;
        detectionRoutine = null;
    }
    private void OnDisable()
    {
        if (detectionRoutine != null)
        {
            StopCoroutine(detectionRoutine);
            detectionRoutine = null;
        }
        Player = null;
        distance = 0f;
        StopAI();
        triggerCollider.size = Vector3.one;
    }
   

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<PlayerObj>();
        if (player == null || enemyObj.isDead ) return;
        if (Player != null && Player != player.gameObject) return;

        Player = player.gameObject;
        StartAI();
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponentInParent<PlayerObj>();
        if (player == null || player.gameObject != Player) return;

       

        Player = null;
        StopAI();
    }

    private void OnCHnagedItem(EnemyObj enemyObj, ItemData itemData)
    {
        if (enemyObj != this.enemyObj) return;
        InitAgentParameters();
    }

    public virtual void InitAgentParameters()
    {
        if (self == null) self = GetComponent<NavMeshAgent>();
        self.updatePosition = false;
        self.updateRotation = true;
        animator.applyRootMotion = true;
        comboIndex = 0;
        isAttacking = false;
        maxComboIndex = -1;
        var tmp = data.HandingItem as WeaponData;
        if (tmp != null)
        {
            maxComboIndex = Mathf.Min(tmp.combo.Count, AttackHashes.Length) - 1;
            stopDistance = tmp.stopDistance;
        }
        else
        {
            stopDistance = 2;
        }
        
        
        self.speed = data.chaseSpeed;//暂时废止
        self.angularSpeed = data.turnSpeed;
        self.stoppingDistance = stopDistance;
    }

    public virtual void StopAI()
    {
        isActive = false;
        animator.SetBool("isWalking", false);
        animator.SetBool("isRunning", false);
        if (self.isActiveAndEnabled && self.isOnNavMesh)
            self.isStopped = true;
    }

    public virtual void StartAI()
    {
        if (isHit)
        {
            StopAI();
            return;
        }
        if (Player == null || enemyObj.isDead || !self.isActiveAndEnabled || !self.isOnNavMesh)
            return;
        animator.SetBool("isWalking", true);
        animator.SetBool("isRunning", true);
        isActive = true;
        self.SetDestination(Player.transform.position);
        self.isStopped = false;
    }
    protected  virtual void OnAnimatorMove()
    {
        if (!self.isActiveAndEnabled || !self.isOnNavMesh) return;

        Vector3 position = animator.rootPosition;

        // 普通地面移动：高度跟随导航表面。
        position.y = self.nextPosition.y;

        // 将动画位置反馈给 Agent，再使用导航约束后的结果。
        self.nextPosition = position;
        transform.position = self.nextPosition;
    }
    protected virtual void TryAttack()
    {
        if (Player == null || maxComboIndex < 0 || isAttacking) return;
        if (comboIndex < 0 || comboIndex > maxComboIndex || comboIndex >= AttackHashes.Length)
            comboIndex = 0;

        FacePlayer();
        self.updateRotation = false;
        isAttacking = true;
        animator.SetTrigger(AttackHashes[comboIndex]);
        comboIndex = (comboIndex + 1) % (maxComboIndex + 1);

    }
    protected virtual void FacePlayer()
    {
        Vector3 direction = Player.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion q = Quaternion.LookRotation(direction, Vector3.up);
            Quaternion source = this.transform.rotation;
            this.transform.rotation = Quaternion.RotateTowards(source, q, data.turnSpeed);
        }
            
    }

    public void ResetCombo()
    {
        self.updateRotation = true;
        foreach (var item in AttackHashes)
        {
            animator.ResetTrigger(item);
            comboIndex = 0;
            isAttacking = false;
        }
        
    }
    /// <summary>
    /// 每个攻击动画绑个
    /// </summary>
    private void ChangeAttackStatu()
    {
        if (isAttacking && comboIndex == 0)
            data.weaponData.attackBreak = data.WeaponSO.attackBreak;

        isAttacking = false;
        self.updateRotation = true;
    }
}
