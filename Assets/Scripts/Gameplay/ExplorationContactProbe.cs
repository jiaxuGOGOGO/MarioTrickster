#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

/// <summary>Passive instrumentation. Contact is evidence of contact, never proof of successful activation.</summary>
public sealed class ExplorationContactProbe : MonoBehaviour
{
    public string mechanism;
    public static event Action<string, GameObject, GameObject> Contact;
    private ExplorationContactProbe owner;
    public bool IsMovingPart { get; private set; }
    public ExplorationContactProbe Root => IsMovingPart ? owner : this;

    private void Start() => BindMovingPart();

    // Runtime Awake may create the hammer after the editor scene was instrumented.
    // A relay is not a second mechanism instance and never adds or changes colliders.
    public void BindMovingPart()
    {
        if (IsMovingPart || GetComponent<PendulumTrap>() == null) return;
        var hammer = GetComponentInChildren<PendulumHammerTrigger>(true);
        if (hammer == null) return;
        var relay = hammer.GetComponent<ExplorationContactProbe>();
        if (relay == null) relay = hammer.gameObject.AddComponent<ExplorationContactProbe>();
        relay.owner = this;
        relay.IsMovingPart = true;
        relay.mechanism = mechanism;
    }
    private void OnCollisionEnter2D(Collision2D collision) => Record(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => Record(other);
    private void Record(Collider2D other)
    {
        if (other == null || !isActiveAndEnabled || Root == null || !Root.isActiveAndEnabled) return;
        var mario = other.GetComponentInParent<MarioController>();
        var trickster = other.GetComponentInParent<TricksterController>();
        if (mario != null) Contact?.Invoke(mechanism, gameObject, mario.gameObject);
        else if (trickster != null) Contact?.Invoke(mechanism, gameObject, trickster.gameObject);
    }
}
#endif
