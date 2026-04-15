using System.Text;
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
    public Vector2 beatIntervalRange = new Vector2(0.25f, 0.35f);
    public Vector2 vocalIntervalRange = new Vector2(0.25f, 0.35f);

    [Header("Spawn Y Jitter")]
    public float beatYJitter = 0f;
    public float vocalYJitter = 0f;

    [Header("Speed")]
    public float beatSpeed = 20f;
    public float vocalSpeed = 20f;

    [Header("Spawn Spacing")]
    public float beatMinSpawnSpacing = 1.2f;
    public float vocalMinSpawnSpacing = 1.6f;

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
    public Vector2Int vocalTokenLengthRange = new Vector2Int(3, 8);

    [Header("Character Scale Variation")]
    public Vector2 beatScaleRange = new Vector2(1f, 1f);
    public Vector2 vocalScaleRange = new Vector2(1f, 1f);

    private float beatTimer;
    private float vocalTimer;
    private float nextBeatTime;
    private float nextVocalTime;

    private readonly char[] beatMainChars = { '■', '█', '▮' };
    private readonly char[] beatSubChars = { '|', ':', '=', '-' };

    private readonly char[] vocalMainChars = { '■', '□', '▌', '-', '=' };
    private readonly char[] vocalSubChars = { ':', '.' };

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

    bool CanSpawnInLane(Transform lane, Transform spawnPoint, float minSpacing, bool movingLeft)
    {
        if (lane == null || spawnPoint == null) return false;

        float spawnX = spawnPoint.localPosition.x;

        foreach (Transform child in lane)
        {
            float x = child.localPosition.x;

            if (movingLeft)
            {
                // 鼓点：从右往左，检查出生点左侧最近的 token 是否离开足够距离
                if (x <= spawnX && (spawnX - x) < minSpacing)
                    return false;
            }
            else
            {
                // 人声：从左往右，检查出生点右侧最近的 token 是否离开足够距离
                if (x >= spawnX && (x - spawnX) < minSpacing)
                    return false;
            }
        }

        return true;
    }

    void SpawnBeatToken()
    {
        if (tokenPrefab == null || beatLane == null || beatSpawnPoint == null) return;

        if (!CanSpawnInLane(beatLane, beatSpawnPoint, beatMinSpawnSpacing, true))
            return;

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

        if (!CanSpawnInLane(vocalLane, vocalSpawnPoint, vocalMinSpawnSpacing, false))
            return;

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