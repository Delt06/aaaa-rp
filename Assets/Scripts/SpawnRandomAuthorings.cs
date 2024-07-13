using DELTation.AAAARP;
using UnityEngine;
using Random = UnityEngine.Random;

public class SpawnRandomAuthorings : MonoBehaviour
{
    public int Seed;
    public int Count = 10;
    [Min(0.0f)]
    public float MaxDistance = 10.0f;
    public Vector2 ScaleRange = new(0.9f, 1.1f);

    public AAAARendererAuthoringBase[] AuthoringPool;

    private void Awake()
    {
        Random.State oldState = Random.state;
        Random.InitState(Seed);

        for (int i = 0; i < Count; i++)
        {
            Vector3 position = transform.position + Random.insideUnitSphere * MaxDistance;
            Quaternion rotation = Random.rotationUniform;
            Vector3 scale = Vector3.one * Random.Range(ScaleRange.x, ScaleRange.y);

            int authoringIndex = Random.Range(0, AuthoringPool.Length);
            AAAARendererAuthoringBase authoringPrefab = AuthoringPool[authoringIndex];

            AAAARendererAuthoringBase authoring = Instantiate(authoringPrefab, position, rotation);
            authoring.transform.localScale = scale;
        }

        Random.state = oldState;
    }
}