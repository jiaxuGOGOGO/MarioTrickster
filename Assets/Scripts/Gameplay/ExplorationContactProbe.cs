#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

/// <summary>Passive instrumentation. Contact is evidence of contact, never proof of successful activation.</summary>
public sealed class ExplorationContactProbe : MonoBehaviour
{
    public string mechanism;
    public static event Action<string, GameObject, GameObject> Contact;
    private void OnCollisionEnter2D(Collision2D collision) => Record(collision.collider);
    private void OnTriggerEnter2D(Collider2D other) => Record(other);
    private void Record(Collider2D other)
    {
        if (other == null) return;
        var mario = other.GetComponentInParent<MarioController>();
        var trickster = other.GetComponentInParent<TricksterController>();
        if (mario != null) Contact?.Invoke(mechanism, gameObject, mario.gameObject);
        else if (trickster != null) Contact?.Invoke(mechanism, gameObject, trickster.gameObject);
    }
}
#endif
