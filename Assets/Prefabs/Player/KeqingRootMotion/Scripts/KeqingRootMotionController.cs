using System;
using UnityEngine;

/// <summary>Independent animation-driven player prototype. Does not change the existing player.</summary>
[RequireComponent(typeof(Animator), typeof(CharacterController))]
public class KeqingRootMotionController : MonoBehaviour
{
    public bool readInput = true;
    public Transform viewCamera;
    public float turnSpeed = 540f;
    public float gravity = -22f;
    public event Action<int> AttackHit;
    public event Action<int> Footstep;

    public Animator Animator { get; private set; }
    public int AttackIndex { get; private set; } = -1;
    public string Motion { get; private set; } = "Idle";
    public Vector3 LastRootDelta { get; private set; }

    CharacterController capsule;
    Vector3 direction;
    bool running, queuedAttack;
    float verticalSpeed;
    int motionHash;
    Transform leftFoot, rightFoot;

    void Awake()
    {
        Animator = GetComponent<Animator>();
        capsule = GetComponent<CharacterController>();
        Animator.applyRootMotion = true;
        Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        motionHash = UnityEngine.Animator.StringToHash(Motion);
        foreach (var bone in GetComponentsInChildren<Transform>())
        {
            if (bone.name.EndsWith("!joint_LeftFoot")) leftFoot = bone;
            if (bone.name.EndsWith("!joint_RightFoot")) rightFoot = bone;
        }
    }

    public void SetMove(Vector3 worldDirection, bool run)
    {
        worldDirection.y = 0;
        direction = Vector3.ClampMagnitude(worldDirection, 1f);
        running = run;
    }

    public void RequestAttack()
    {
        if (AttackIndex >= 0)
        {
            if (AttackIndex < 2) queuedAttack = true;
            return;
        }
        queuedAttack = false;
        AttackIndex = 0;
        Play("Attack1", .055f);
    }

    void Play(string state, float blend = .1f, float phase = 0f)
    {
        if (Motion == state) return;
        Motion = state;
        motionHash = UnityEngine.Animator.StringToHash(state);
        float offset = 0f;
        if (phase > 0f)
            foreach (var clip in Animator.runtimeAnimatorController.animationClips)
                if (clip.name == state) { offset = phase * clip.length; break; }
        Animator.CrossFadeInFixedTime(motionHash, blend, 0, offset);
    }

    void Update()
    {
        if (readInput)
        {
            var cameraTransform = viewCamera != null ? viewCamera : Camera.main != null ? Camera.main.transform : null;
            var forward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
            var right = cameraTransform != null ? cameraTransform.right : Vector3.right;
            forward.y = right.y = 0;
            SetMove(forward.normalized * Input.GetAxisRaw("Vertical")
                + right.normalized * Input.GetAxisRaw("Horizontal"), Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            if (Input.GetMouseButtonDown(0)) RequestAttack();
        }

        var state = Animator.GetCurrentAnimatorStateInfo(0);
        bool settled = !Animator.IsInTransition(0) && state.shortNameHash == motionHash;
        float phase = settled ? state.normalizedTime : 0f;
        if (AttackIndex >= 0)
        {
            if (settled && queuedAttack && AttackIndex < 2 && phase >= .88f)
            {
                queuedAttack = false;
                AttackIndex++;
                Play("Attack" + (AttackIndex + 1), .09f);
            }
            else if (settled && phase >= .99f)
            {
                AttackIndex = -1;
                queuedAttack = false;
                Play(direction.sqrMagnitude > .01f ? running ? "RunStart" : "WalkStart" : "Idle");
            }
        }
        else
        {
            bool moving = direction.sqrMagnitude > .01f;
            bool starting = Motion == "WalkStart" || Motion == "RunStart";
            bool stopping = Motion.Contains("Stop");
            bool turning = Motion == "TurnLeft" || Motion == "TurnRight";
            if (turning)
            {
                if (settled && phase >= .99f) Play(moving ? running ? "RunStart" : "WalkStart" : "Idle");
            }
            else if (moving)
            {
                float angle = Vector3.SignedAngle(transform.forward, direction, Vector3.up);
                if (Motion == "Idle" && Mathf.Abs(angle) > 70f)
                    Play(angle > 0 ? "TurnRight" : "TurnLeft", .07f);
                else
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(direction), turnSpeed * Time.deltaTime);
                    if (Motion == "Idle" || stopping) Play(running ? "RunStart" : "WalkStart", .08f);
                    else if (starting && settled && phase >= .96f) Play(running ? "Run" : "Walk", .08f);
                    else if (!starting) Play(running ? "Run" : "Walk", .14f, Mathf.Repeat(state.normalizedTime, 1f));
                }
            }
            else
            {
                if (!stopping && Motion != "Idle")
                {
                    bool rightLead = leftFoot != null && rightFoot != null
                        && Vector3.Dot(rightFoot.position - leftFoot.position, transform.forward) > 0f;
                    Play((Motion.StartsWith("Run") ? "RunStop" : "WalkStop") + (rightLead ? "Right" : ""), .12f);
                }
                else if (stopping && settled && phase >= .98f) Play("Idle", .1f);
            }
        }

        if (capsule.isGrounded && verticalSpeed < 0) verticalSpeed = -2f;
        verticalSpeed += gravity * Time.deltaTime;
        capsule.Move(Vector3.up * verticalSpeed * Time.deltaTime);
    }

    void OnAnimatorMove()
    {
        if (Animator == null || capsule == null) return;
        var delta = Animator.deltaPosition;
        delta.y = 0;
        LastRootDelta = delta;
        capsule.Move(delta);
        transform.rotation *= Animator.deltaRotation;
    }

    // Events are authored at the strike/contact frames, ready for later gameplay integration.
    public void OnAttackHit(int index) => AttackHit?.Invoke(index);
    public void OnFootstep(int foot) => Footstep?.Invoke(foot);

    void OnDisable()
    {
        direction = Vector3.zero;
        queuedAttack = false;
        AttackIndex = -1;
        verticalSpeed = 0;
    }
}
