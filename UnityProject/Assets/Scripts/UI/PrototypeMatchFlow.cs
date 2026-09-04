using UnityEngine;
using UnityEngine.EventSystems;
using PersiaWar.Unity2D5D;

/// <summary>
/// Presentation-only front end for the prototype: compact hero carousel and
/// clickable spawn-map. It does not alter PlayerController movement/combat code.
/// </summary>
public sealed class PrototypeMatchFlow : MonoBehaviour
{
    private enum ScreenState { HeroSelect, SpawnMap, Playing }

    [SerializeField] private ScreenState initialState = ScreenState.HeroSelect;
    private ScreenState state;
    private PlayerController player;
    private int selectedHero;
    private Vector2 mapOrigin;
    private Vector2 mapSize;

    private readonly string[] heroNames = { "پهلوان پارسی", "بانوی پارسی" };

    private void Start()
    {
        state = initialState;
        player = FindFirstObjectByType<PlayerController>();
        if (player != null)
            ApplyHero();
    }

    private void Update()
    {
        if (state != ScreenState.SpawnMap || player == null) return;
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            TryMapSpawn(Input.GetTouch(0).position);
        }
    }

    private void ApplyHero()
    {
        if (player == null) return;
        PrototypeCharacterVisual visual = player.GetComponent<PrototypeCharacterVisual>();
        if (visual == null) visual = player.gameObject.AddComponent<PrototypeCharacterVisual>();
        visual.SetHero(selectedHero);
    }

    private void TryMapSpawn(Vector2 screenPosition)
    {
        if (!new Rect(mapOrigin, mapSize).Contains(screenPosition)) return;
        float nx = Mathf.InverseLerp(mapOrigin.x, mapOrigin.x + mapSize.x, screenPosition.x);
        float nz = Mathf.InverseLerp(mapOrigin.y, mapOrigin.y + mapSize.y, screenPosition.y);
        float worldSize = 160f;
        Vector3 spawn = new Vector3(Mathf.Lerp(-worldSize * 0.42f, worldSize * 0.42f, nx), 0f,
            Mathf.Lerp(-worldSize * 0.42f, worldSize * 0.42f, nz));
        player.transform.position = spawn;
        state = ScreenState.Playing;
    }

    private void OnGUI()
    {
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) { }

        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
            new Vector3(Screen.width / 1280f, Screen.height / 720f, 1f));

        if (state == ScreenState.Playing) return;
        if (state == ScreenState.HeroSelect) DrawHeroSelect();
        else DrawSpawnMap();
    }

    private void DrawHeroSelect()
    {
        GUI.Box(new Rect(0, 0, 1280, 720), "");
        GUI.Label(new Rect(58, 34, 500, 70), "قهرمانان", HeaderStyle());
        GUI.Label(new Rect(58, 94, 620, 36), "یک قهرمان ایرانی برای شروع نمونه اولیه انتخاب کن", BodyStyle());

        if (GUI.Button(new Rect(70, 300, 74, 74), "‹", BigButtonStyle()))
        {
            selectedHero = (selectedHero + heroNames.Length - 1) % heroNames.Length;
            ApplyHero();
        }
        if (GUI.Button(new Rect(1136, 300, 74, 74), "›", BigButtonStyle()))
        {
            selectedHero = (selectedHero + 1) % heroNames.Length;
            ApplyHero();
        }

        DrawHeroCard(210, selectedHero, true);
        int other = (selectedHero + 1) % heroNames.Length;
        DrawHeroCard(800, other, false);

        GUI.Label(new Rect(470, 530, 340, 55), heroNames[selectedHero], SelectedStyle());
        if (GUI.Button(new Rect(470, 600, 340, 66), "انتخاب و رفتن به نقشه", ActionStyle()))
        {
            ApplyHero();
            state = ScreenState.SpawnMap;
        }
    }

    private void DrawHeroCard(float x, int hero, bool selected)
    {
        GUI.Box(new Rect(x, 150, 270, 300), "");
        GUI.Label(new Rect(x + 25, 180, 220, 220), hero == 0 ? "♛\n\nپهلوان" : "✦\n\nبانوی جنگجو", HeroArtStyle(selected));
        GUI.Label(new Rect(x + 25, 405, 220, 35), heroNames[hero], BodyStyle());
    }

    private void DrawSpawnMap()
    {
        GUI.Box(new Rect(0, 0, 1280, 720), "");
        GUI.Label(new Rect(58, 32, 500, 62), "محل ورود", HeaderStyle());
        GUI.Label(new Rect(58, 92, 520, 38), "روی هر نقطه از نقشه بزن تا همان‌جا ظاهر شوی", BodyStyle());

        mapOrigin = new Vector2(500, 125);
        mapSize = new Vector2(700, 510);
        DrawMiniMap(mapOrigin, mapSize);

        GUI.Box(new Rect(55, 160, 355, 330), "");
        GUI.Label(new Rect(85, 190, 290, 50), "CLASSIC BATTLE ROYALE", SelectedStyle());
        GUI.Label(new Rect(85, 255, 290, 110), "شهر سبز و شهری\nخیابان‌ها و بلوک‌های ساختمانی\nبخش تخریب‌شده\nموانع و فضای آزاد برای درگیری", BodyStyle());
        GUI.Label(new Rect(85, 395, 290, 50), "قهرمان: " + heroNames[selectedHero], BodyStyle());
        if (GUI.Button(new Rect(85, 455, 290, 58), "ظاهر شدن در مرکز", ActionStyle()))
        {
            player.transform.position = Vector3.zero;
            state = ScreenState.Playing;
        }
    }

    private void DrawMiniMap(Vector2 origin, Vector2 size)
    {
        GUI.Box(new Rect(origin.x - 8, origin.y - 8, size.x + 16, size.y + 16), "");
        GUI.DrawTexture(new Rect(origin.x, origin.y, size.x, size.y), Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
            new Color(0.32f, 0.58f, 0.22f), 0f, 0f);

        GUI.color = new Color(0.18f, 0.20f, 0.22f, 1f);
        GUI.DrawTexture(new Rect(origin.x + size.x * 0.44f, origin.y, size.x * 0.10f, size.y), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(origin.x, origin.y + size.y * 0.44f, size.x, size.y * 0.10f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        for (int gx = 0; gx < 5; gx++)
        {
            for (int gz = 0; gz < 4; gz++)
            {
                float x = origin.x + 22f + gx * 130f;
                float y = origin.y + 22f + gz * 120f;
                if (gx == 3 && gz < 3)
                {
                    GUI.color = new Color(0.28f, 0.29f, 0.30f, 1f);
                    GUI.DrawTexture(new Rect(x, y, 85, 68), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
                else
                {
                    GUI.color = new Color(0.86f, 0.75f, 0.44f, 1f);
                    GUI.DrawTexture(new Rect(x, y, 62, 48), Texture2D.whiteTexture);
                    GUI.color = Color.white;
                }
            }
        }

        GUI.Label(new Rect(origin.x + 20, origin.y + size.y - 50, 240, 32), "●  محل ورود را انتخاب کن", BodyStyle());
    }

    private GUIStyle HeaderStyle() => new GUIStyle(GUI.skin.label) { fontSize = 42, fontStyle = FontStyle.Bold };
    private GUIStyle BodyStyle() => new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true };
    private GUIStyle SelectedStyle() => new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    private GUIStyle BigButtonStyle() => new GUIStyle(GUI.skin.button) { fontSize = 48, fontStyle = FontStyle.Bold };
    private GUIStyle ActionStyle() => new GUIStyle(GUI.skin.button) { fontSize = 24, fontStyle = FontStyle.Bold };
    private GUIStyle HeroArtStyle(bool selected) => new GUIStyle(GUI.skin.label) { fontSize = selected ? 58 : 46, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
}
