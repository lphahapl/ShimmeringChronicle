using UnityEngine;

// 进入站立或移动时清理连招，由状态机上的挂载位置决定。
public sealed class ClearAttackOnStateEnter : StateMachineBehaviour
{
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        PlayerController controller = animator.GetComponent<PlayerController>();
        if (controller != null) controller.ClearAttack();
    }
}
