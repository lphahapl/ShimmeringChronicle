using UnityEngine;

// The copied Warrior prefab uses this controller; the original player keeps PlayerController.
public class WarriorRootMotionController : PlayerController
{
    [System.Serializable]
    public struct ControllerMapping
    {
        public RuntimeAnimatorController source;
        public RuntimeAnimatorController rootMotion;
    }

    [Header("根运动控制器副本")]
    public ControllerMapping[] controllers;
    Vector3 gravityStep;

    protected override RuntimeAnimatorController ResolveAnimatorController(RuntimeAnimatorController controller)
    {
        if (controllers != null)
            foreach (var mapping in controllers)
                if (mapping.source == controller && mapping.rootMotion != null)
                    return mapping.rootMotion;
        return controller;
    }

    protected override void MoveCharacter(Vector3 velocity, float dt)
    {
        // Horizontal displacement comes from the animation; gravity still uses player time.
        gravityStep = Vector3.up * velocity.y * dt;
    }

    void OnAnimatorMove()
    {
        if (!animator || !characterController || !characterController.enabled) return;
        Vector3 movement = animator.deltaPosition;
        bool jumping = animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Jump") ||
            (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName("Base Layer.Jump"));
        if (!jumping) movement.y = 0f;
        if (IsDead || isTalking) movement = Vector3.zero;
        characterController.Move(movement + (jumping && !IsDead && !isTalking ? Vector3.zero : gravityStep));
    }
}
