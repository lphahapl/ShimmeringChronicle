using TMPro;
using Unity.VisualScripting;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class EnemyObj : MonoBehaviour,IDamageable
{
    public EnemySO SO;
    public EnemyData data;
    public Animator animator;
    public EnemyHPBar HPBar;
    public TMP_Text enemyName;
    public EnemyAI enemyAI;
    private Collider[] chcheColliders;
    public Transform handPos;
    public bool isDead;
    private GameObject handingInstance;//手上的预制体
    private WeaponSO reservedWeaponSO;
    public ItemData HandingItem=>data.HandingItem;//手上的数据
   

    public void TakeDamage(float damage, GameObject Attacker)
    {
        if(data.hp==0) return;
        data.hp= Mathf.Clamp( data.hp -= damage,0,data.maxHp);
        if (data.hp == 0)
        {
            isDead = true;
            this.Publish<GameObject>(GameEvents.Died, this.gameObject);
        }
        print($"{this.gameObject.name}收到了来自{Attacker.name}的{damage}伤害剩余{data.hp}血量");
        
        this.Publish(GameEvents.Damaged, gameObject, damage, Attacker);
        this.Publish(GameEvents.HpChanged, gameObject, data.hp, data.maxHp);
        
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        HPBar = GetComponent<EnemyHPBar>();
        
        ResetEnemyStatu(SO);
        InitAI();
        
    }

    private void OnEnable()
    {
        
        this.Subscribe(GameEvents.Damaged, OnDameged);
        this.Subscribe(GameEvents.Died, OnDie);
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
  
    private void InitHealthBar()
    {
        if (HPBar == null)
        {
            Debug.LogWarning("忘记了挂敌人血条");
            return;
        }
        HPBar.enemy = this;
        HPBar.Refresh();

    }
    /// <summary>
    /// 对象池重设敌人参数用
    /// </summary>
    public void ResetEnemyStatu(EnemySO config)
    {
        SO = config;
        data = config.CreateData();
        
        isDead = false;
        if (enemyAI != null) enemyAI.data = data;
        if (chcheColliders == null)
        {
            chcheColliders = GetComponentsInChildren<Collider>(true);
        }
        foreach (var col in chcheColliders)
        {
            col.enabled = true;
        }
        enemyName.text = data.targetName;
        InitHealthBar();
        SpawnDefaultWeapon();
        if (enemyAI != null)
        {
            enemyAI.InitAgentParameters();
            enemyAI.ResetDetectionRange();
        }
        isDead = false;
        animator.Rebind();
       
    }
    // Reset animation state for a new life without moving the spawn transform.
    

    private void OnDameged(GameObject victim ,float damage,GameObject attacker)
    {
        if (victim != this.gameObject|| isDead) return;
        Vector3 damageDir = attacker.transform.position - transform.position;
        damageDir.y = 0f; 

        if (damageDir.sqrMagnitude > 0.0001f)
        {
            transform.rotation = Quaternion.LookRotation(damageDir, Vector3.up);
        }
        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }
    }

    /// <summary>
    /// 死那一瞬间触发回调
    /// </summary>
    /// <param name="who"></param>
    private void OnDie(GameObject who)
    {
        if (who != this.gameObject) return;
       
        print($"{this.gameObject.name}死了");
        if (chcheColliders == null)
        {
            chcheColliders = GetComponentsInChildren<Collider>(true);
        }
        foreach (var col in chcheColliders)
        {
            col.enabled = false;
        }
        EnemyManager.Instance.Unregister(this);
        animator.SetTrigger("isDied");

    }
    /// <summary>
    /// 动画播完回调
    /// </summary>
    public void Died()
    {
        
        EnemyManager.Instance.RecycleEnemy(this.gameObject, SO.prefab);
    }
    private void OnDisable()
    {
        this.UnSubscribe(GameEvents.Damaged, OnDameged);
        this.UnSubscribe(GameEvents.Died, OnDie);
    }
    public void PublishEnemyCombo(int index)
    {
        if (!isActiveAndEnabled || data == null || data.hp <= 0) return;
        this.Publish(GameEvents.OnEnemyCombo, this, index);
    }

    private void SpawnDefaultWeapon()
    {
        var config = SO.defaultWeaponSO;
        if (config == null) return;
        if (handPos == null || config.prefab == null)
        {
            Debug.LogWarning("[EnemyObj] Missing weapon hand point or prefab.", this);
            return;
        }

        if (reservedWeaponSO != config || handingInstance == null)
        {
            if (handingInstance != null)
            {
                handingInstance.SetActive(false);
                Destroy(handingInstance);
            }
            handingInstance = Instantiate(config.prefab, handPos, false);
            reservedWeaponSO = config;
        }

        
        var weaponData = config.CreateData();
        data.weaponData = weaponData;
        data.HandingItem = weaponData;

        foreach (var weapon in handingInstance.GetComponentsInChildren<WeaponObj>(true))
        {
            weapon.SO = config;
            weapon.weaponData = weaponData;
            weapon.InitWeapon(gameObject);
        }
        animator.runtimeAnimatorController = weaponData.overrideController;
        this.Publish<EnemyObj, ItemData>(GameEvents.OnEnemyChangedHandingItem, this, data.HandingItem);
    }

    private void InitAI()
    {

        enemyAI = GetComponent<EnemyAI>();
        if (enemyAI == null)
        {
            Debug.LogError("没挂AI");
        }
        enemyAI.enemyObj=this;
        enemyAI.animator=this.animator;
        enemyAI.data = data;
        enemyAI.InitAgentParameters();
        enemyAI.ResetDetectionRange();
    }
}
