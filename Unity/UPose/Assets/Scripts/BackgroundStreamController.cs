using System.Text;
using TMPro;
using UnityEngine;

public class BackgroundTokenStreamController : MonoBehaviour
{
    [Header("References")]
    public StreamToken tokenPrefab;

    public Transform beatLane;
    public Transform vocalLane;

    public Transform beatSpawnPoint;
    public Transform vocalSpawnPoint;

    [Header("Spawn Timing")]
    public Vector2 beatIntervalRange = new Vector2(0.08f, 0.22f);
    public Vector2 vocalIntervalRange = new Vector2(0.18f, 0.50f);

    [Header("Spawn Y Jitter")]
    public float beatYJitter = 1.8f;
    public float vocalYJitter = 1.8f;

    [Header("Speed")]
    public float beatSpeed = 7.5f;
    public float vocalSpeed = 7.5f;

    [Header("Destroy Bounds")]
    public float destroyXMin = -14f;
    public float destroyXMax = 14f;

    [Header("Color")]
    public Color normalColor = Color.white;
    public Color accentColor = new Color(0f, 1f, 0f, 1f);
    [Range(0f, 1f)] public float accentChance = 0.12f;

    [Header("Beat Token Length")]
    public Vector2Int beatTokenLengthRange = new Vector2Int(3, 8);

    [Header("Vocal Token Length")]
    public Vector2Int vocalTokenLengthRange = new Vector2Int(6, 16);

    [Header("Character Scale Variation")]
    public Vector2 beatScaleRange = new Vector2(0.9f, 1.3f);
    public Vector2 vocalScaleRange = new Vector2(0.9f, 1.4f);

    private float beatTimer;
    private float vocalTimer;
    private float nextBeatTime;
    private float nextVocalTime;

    private readonly char[] beatMainChars = { '■', '█', '▮' };
    private readonly char[] beatSubChars  = { '|', ':', '=', '-' };

    private readonly char[] vocalMainChars = { '■', '□', '▌', '-', '=' };
    private readonly char[] vocalSubChars  = { ':', '.', '~' };

    void Start()
    {
        ScheduleNextBeat();
        ScheduleNextVocal();
    }

    void Update()
    {
        beatTimer += Time.deltaTime;
        vocalTimer += Time.deltaTime;

        if (beatTimer >= nextBeatTime)
        {
            SpawnBeatToken();
            beatTimer = 0f;
            ScheduleNextBeat();
        }

        if (vocalTimer >= nextVocalTime)
        {
            SpawnVocalToken();
            vocalTimer = 0f;
            ScheduleNextVocal();
        }
    }

    void ScheduleNextBeat()
    {
        nextBeatTime = Random.Range(beatIntervalRange.x, beatIntervalRange.y);
    }

    void ScheduleNextVocal()
    {
        nextVocalTime = Random.Range(vocalIntervalRange.x, vocalIntervalRange.y);
    }

    void SpawnBeatToken()
    {
        if (tokenPrefab == null || beatLane == null || beatSpawnPoint == null) return;

        string token = BuildToken(
            beatMainChars,
            beatSubChars,
            Random.Range(beatTokenLengthRange.x, beatTokenLengthRange.y + 1),
            0.75f
        );

        StreamToken instance = Instantiate(tokenPrefab, beatLane);

        Vector3 pos = beatSpawnPoint.localPosition;
        pos.y += Random.Range(-beatYJitter, beatYJitter);
        instance.transform.localPosition = pos;

        float scale = Random.Range(beatScaleRange.x, beatScaleRange.y);
        instance.transform.localScale = Vector3.one * scale;

        Color c = Random.value < accentChance ? accentColor : normalColor;

        instance.Initialize(
            token,
            c,
            Vector3.left,
            beatSpeed,
            destroyXMin,
            destroyXMax
        );
    }

    void SpawnVocalToken()
    {
        if (tokenPrefab == null || vocalLane == null || vocalSpawnPoint == null) return;

        string token = BuildToken(
            vocalMainChars,
            vocalSubChars,
            Random.Range(vocalTokenLengthRange.x, vocalTokenLengthRange.y + 1),
            0.70f
        );

        StreamToken instance = Instantiate(tokenPrefab, vocalLane);

        Vector3 pos = vocalSpawnPoint.localPosition;
        pos.y += Random.Range(-vocalYJitter, vocalYJitter);
        instance.transform.localPosition = pos;

        float scale = Random.Range(vocalScaleRange.x, vocalScaleRange.y);
        instance.transform.localScale = Vector3.one * scale;

        Color c = Random.value < accentChance ? accentColor : normalColor;

        instance.Initialize(
            token,
            c,
            Vector3.right,
            vocalSpeed,
            destroyXMin,
            destroyXMax
        );
    }

    string BuildToken(char[] mainChars, char[] subChars, int length, float mainWeight)
    {
        StringBuilder sb = new StringBuilder();

        for (int i = 0; i < length; i++)
        {
            bool useMain = Random.value < mainWeight;
            char chosen = useMain
                ? mainChars[Random.Range(0, mainChars.Length)]
                : subChars[Random.Range(0, subChars.Length)];

            sb.Append(chosen);
        }

        return sb.ToString();
    }
}