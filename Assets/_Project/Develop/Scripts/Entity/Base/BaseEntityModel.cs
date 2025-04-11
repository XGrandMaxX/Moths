using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BaseEntityModel
{
    [SerializeField] public float moveSpeed = 5f;
    [SerializeField] public float energy = 100f;
    [SerializeField] public float energyRecoveryRate = 10f;

    public override string ToString()
    {
        return "";
    }
}
