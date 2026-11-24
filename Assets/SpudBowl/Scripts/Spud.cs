using UnityEngine;

// One spud in the bowl. Two spuds of the same tier that touch merge into the next tier.
// Merge pattern (one merging flag per body, the lower instance id performs the merge, new body at the
// midpoint) follows GeonhanLee/SputnikaGame-GameOff2023 (MIT), written fresh for this game.
public class Spud : MonoBehaviour
{
    public int tier;
    public bool merging;
    public float born;
    public int id;
    static int nextId;
    void Awake() => id = ++nextId;
    public Rigidbody2D rb;
    float pop = 1f;

    public float Radius => SpudGame.Radius[tier];

    public void Pop() { pop = 0f; Apply(0.55f); }

    void Update()
    {
        if (pop >= 1f) return;
        pop = Mathf.Min(1f, pop + Time.deltaTime / 0.2f);
        // ease out back
        float t = pop - 1f;
        float e = 1f + t * t * (2.7f * t + 1.7f);
        Apply(Mathf.LerpUnclamped(0.55f, 1f, e));
    }

    void Apply(float k) => transform.localScale = Vector3.one * Radius * k;

    void OnCollisionEnter2D(Collision2D c) => TryMerge(c);
    void OnCollisionStay2D(Collision2D c) => TryMerge(c);

    void TryMerge(Collision2D c)
    {
        if (merging) return;
        var other = c.collider.GetComponent<Spud>();
        if (other == null || other.merging || other.tier != tier) return;
        if (id > other.id) return; // the pair merges once, from one side
        SpudGame.I.Merge(this, other);
    }
}
