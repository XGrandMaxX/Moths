using UnityEngine;
using Pathfinding; // Пространство имен A* Pathfinding Project

public abstract class BaseEntity : MonoBehaviour
{
    private IState currentState;
    public IState CurrentState => currentState;

    public BaseEntityModel Model;

    // Компонент A* для движения
    protected AIPath aiPath;

    // Состояния
    public IState idleState;
    public IState moveToPointState;
    public IState randomMoveState;
    public IState moveToTargetState;
    public IState workState;
    public IState recoverEnergyState;
    public IState waitForPaymentState;

    protected virtual void Awake()
    {
        Model = new BaseEntityModel();
        aiPath = GetComponent<AIPath>();
        if (aiPath == null)
        {
            Debug.LogError("AIPath component is missing on " + name);
        }
        else
        {
            aiPath.maxSpeed = Model.moveSpeed;
        }
    }

    protected virtual void Start()
    {
        idleState = new IdleState();
        moveToPointState = new MoveToPointState(aiPath);
        randomMoveState = new RandomMoveState(aiPath);
        moveToTargetState = new MoveToTargetState(aiPath);
        workState = GetWorkState();
        recoverEnergyState = new RecoverEnergyState(Model.energyRecoveryRate);
        waitForPaymentState = new WaitForPaymentState();

        ChangeState(GetInitialState());
    }

    protected virtual void Update()
    {
        currentState?.Update();
    }

    protected virtual void FixedUpdate()
    {
        currentState?.FixedUpdate();
    }

    public void ChangeState(IState newState)
    {
        currentState?.Exit();
        currentState = newState;
        currentState?.Enter(this);
    }

    protected abstract IState GetInitialState();
    protected abstract IState GetWorkState();

    public void SetMoveTarget(Vector3 target)
    {
        if (moveToPointState is MoveToPointState moveState)
        {
            moveState.SetTarget(target);
            ChangeState(moveToPointState);
        }
    }

    public void SetMoveToTarget(Transform target)
    {
        if (moveToTargetState is MoveToTargetState moveState)
        {
            moveState.SetTarget(target);
            ChangeState(moveToTargetState);
        }
    }
}

public interface IState
{
    void Enter(BaseEntity entity);
    void Update();
    void FixedUpdate();
    void Exit();
    float StateTime { get; } // Время в состоянии
}

public abstract class BaseState : IState
{
    protected BaseEntity entity;
    private float stateTime;

    public float StateTime => stateTime;

    public virtual void Enter(BaseEntity entity)
    {
        this.entity = entity;
        stateTime = 0f; // Сбрасываем время при входе
    }

    public virtual void Update()
    {
        stateTime += Time.deltaTime; // Увеличиваем время каждый кадр
    }

    public virtual void FixedUpdate() { }
    public virtual void Exit() { }
}

// Состояние: Ожидание (Idle)
public class IdleState : BaseState
{
    public override void Update()
    {
        base.Update();
        Debug.Log($"{entity.name} is idle for {StateTime:F2} seconds");
    }
}

// Состояние: Движение к точке
public class MoveToPointState : BaseState
{
    private AIPath aiPath;
    private Vector3 target;
    private bool hasTarget;

    public MoveToPointState(AIPath aiPath)
    {
        this.aiPath = aiPath;
    }

    public void SetTarget(Vector3 target)
    {
        this.target = target;
        hasTarget = true;
    }

    public override void Enter(BaseEntity entity)
    {
        base.Enter(entity);
        if (hasTarget && aiPath != null)
        {
            aiPath.destination = target;
            aiPath.isStopped = false;
        }
    }

    public override void Update()
    {
        base.Update();
        if (!hasTarget || aiPath == null) return;

        if (aiPath.reachedEndOfPath || Vector3.Distance(entity.transform.position, target) < aiPath.endReachedDistance)
        {
            entity.ChangeState(entity.idleState);
            hasTarget = false;
        }
        Debug.Log($"{entity.name} moving to point for {StateTime:F2} seconds");
    }

    public override void Exit()
    {
        if (aiPath != null) aiPath.isStopped = true;
    }
}

// Состояние: Беспорядочное движение
public class RandomMoveState : BaseState
{
    private AIPath aiPath;
    private Vector3 randomTarget;
    private float changeDirectionTime = 2f;
    private float timer;

    public RandomMoveState(AIPath aiPath)
    {
        this.aiPath = aiPath;
    }

    public override void Enter(BaseEntity entity)
    {
        base.Enter(entity);
        SetNewRandomTarget();
        if (aiPath != null)
        {
            aiPath.destination = randomTarget;
            aiPath.isStopped = false;
        }
    }

    public override void Update()
    {
        base.Update();
        if (aiPath == null) return;

        timer += Time.deltaTime;
        if (timer >= changeDirectionTime || aiPath.reachedEndOfPath)
        {
            SetNewRandomTarget();
            aiPath.destination = randomTarget;
            timer = 0f;
        }
        Debug.Log($"{entity.name} moving randomly for {StateTime:F2} seconds");
    }

    public override void Exit()
    {
        if (aiPath != null) aiPath.isStopped = true;
    }

    private void SetNewRandomTarget()
    {
        randomTarget = entity.transform.position + Random.insideUnitSphere * 5f;
        randomTarget.y = entity.transform.position.y; // Оставляем высоту постоянной
    }
}

// Состояние: Движение к цели (Transform)
public class MoveToTargetState : BaseState
{
    private AIPath aiPath;
    private Transform target;

    public MoveToTargetState(AIPath aiPath)
    {
        this.aiPath = aiPath;
    }

    public void SetTarget(Transform target)
    {
        this.target = target;
    }

    public override void Enter(BaseEntity entity)
    {
        base.Enter(entity);
        if (target != null && aiPath != null)
        {
            aiPath.destination = target.position;
            aiPath.isStopped = false;
        }
    }

    public override void Update()
    {
        base.Update();
        if (target == null || aiPath == null) return;

        aiPath.destination = target.position; // Обновляем позицию цели каждый кадр
        if (aiPath.reachedEndOfPath || Vector3.Distance(entity.transform.position, target.position) < aiPath.endReachedDistance)
        {
            entity.ChangeState(entity.idleState);
        }
        Debug.Log($"{entity.name} moving to target for {StateTime:F2} seconds");
    }

    public override void Exit()
    {
        if (aiPath != null) aiPath.isStopped = true;
    }
}

// Состояние: Восстановление энергии
public class RecoverEnergyState : BaseState
{
    private float recoveryRate;

    public RecoverEnergyState(float recoveryRate)
    {
        this.recoveryRate = recoveryRate;
    }

    public override void Update()
    {
        base.Update();
        entity.Model.energy += recoveryRate * Time.deltaTime;
        entity.Model.energy = Mathf.Clamp(entity.Model.energy, 0f, 100f);

        if (entity.Model.energy >= 100f)
        {
            entity.ChangeState(entity.idleState);
        }
        Debug.Log($"{entity.name} recovering energy for {StateTime:F2} seconds");
    }
}

// Состояние: Ожидание платы
public class WaitForPaymentState : BaseState
{
    private float waitTime = 5f;

    public override void Update()
    {
        base.Update();
        if (StateTime >= waitTime)
        {
            entity.ChangeState(entity.idleState);
        }
        Debug.Log($"{entity.name} waiting for payment for {StateTime:F2} seconds");
    }
}