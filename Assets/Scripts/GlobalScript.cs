using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Linq;
using UnityEditor;
using Newtonsoft.Json;
using MoonSharp.Interpreter;

public class GlobalScript : MonoBehaviour
{
    public const string ContentDirectory = @"C:\Users\tombr\Documents\Coding\CoI\MainContent";
    public Context DefaultContext;
    public SpriteImporter spriteImporter = new SpriteImporter();

    private string playerTag;
    public int turns;
    private Dictionary<string, string> _localisation;
    public Dictionary<string, Tile> tiles;
    public Dictionary<string, Country> countries;
    public Dictionary<string, Decision> decisions;
    public Dictionary<string, EventGame> events;
    ScriptLogic scriptLogic;
    Script luaScript;

    void Start()
    {
        DefaultContext = new Context();
        scriptLogic = new ScriptLogic();
        luaScript = new Script();
        UserData.RegisterType<ScriptLogic>();
        luaScript.Globals["CoI"] = UserData.Create(scriptLogic);
        luaScript.DoString(File.ReadAllText(Path.Combine(ContentDirectory, "core.lua")));

        // Sprite Generation
        Dictionary<string, string> imgs = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(ContentDirectory, "gfx.json")));
        foreach(KeyValuePair<string, string> entry in imgs) {
            spriteImporter.Generate(entry.Key, Path.Combine(ContentDirectory, entry.Value));
        }

        // Game Config
        Dictionary<string, string> _configJSON = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(ContentDirectory, "config.json")));
        _localisation = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(ContentDirectory, "localisation.json")));

        tiles = new Dictionary<string, Tile>() {};
        countries = new Dictionary<string, Country>() {};

        // Generating Map
        MapData mapData = JsonConvert.DeserializeObject<MapData>(File.ReadAllText(Path.Combine(ContentDirectory, "map/map.json")));
        foreach(KeyValuePair<string, List<float>> entry in mapData.tiles ) {
            // Tile creation
            string tile = entry.Key;
            tiles[tile] = new Tile(tile);

            // GameObject
            GameObject tileObject = new GameObject();
            tileObject.transform.SetParent(GameObject.Find("UI-Map-Tiles").transform);
            tileObject.AddComponent<RectTransform>();
            tileObject.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0);
            tileObject.GetComponent<RectTransform>().anchorMax = new Vector2(1, 1);
            tileObject.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            tileObject.GetComponent<RectTransform>().localScale = new Vector2(1, 1);
            tileObject.GetComponent<RectTransform>().offsetMin = new Vector2(0, 0);
            tileObject.GetComponent<RectTransform>().offsetMax = new Vector2(0, 0);
            spriteImporter.Generate("tile_"+tile, Path.Combine(ContentDirectory, "map/tiles/"+tile+".png"));
            tileObject.AddComponent<Image>();
            tileObject.GetComponent<Image>().sprite = spriteImporter["tile_"+tile];
            tileObject.GetComponent<Image>().mainTexture.filterMode = FilterMode.Point;
            tileObject.AddComponent<Button>();
            tileObject.AddComponent<MapTile>();
            tileObject.GetComponent<MapTile>().Create(tile);
            tiles[tile].gameObject = tileObject;

            GameObject star = new GameObject();
            star.transform.SetParent(GameObject.Find("UI-Map-Stars").transform);
            star.AddComponent<RectTransform>();
            star.GetComponent<RectTransform>().anchorMin = new Vector2( 0, 1 );
            star.GetComponent<RectTransform>().anchorMax = new Vector2( 0, 1 );
            star.GetComponent<RectTransform>().localScale = new Vector2( 1, 1 );
            star.GetComponent<RectTransform>().sizeDelta = new Vector2( 16, 16 );
            star.GetComponent<RectTransform>().anchoredPosition = new Vector2( ( entry.Value[0] / mapData.size[0] ) * 1920, (entry.Value[1] / mapData.size[1] ) * -1080 );
            star.AddComponent<Image>();
            star.GetComponent<Image>().sprite = spriteImporter["Map-Star"];
            star.GetComponent<Image>().color = new Color32(255, 0, 0, 255);
            star.name = "Map_Star-"+tile;
        }

        // Generating Countries
        Dictionary<string, string> countryData = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(Path.Combine(ContentDirectory, "countries.json")));
        foreach (KeyValuePair<string, string> entry in countryData) {
            countries[entry.Key] = new Country(entry.Key);
            spriteImporter.Generate("Flag-"+entry.Key, Path.Combine(ContentDirectory, $"gfx/flags/{entry.Key}.png"));
        }

        foreach (KeyValuePair<string, string> entry in countryData) {
            luaScript.Globals["scopes"] = new List<string>() { entry.Key };
            luaScript.DoString(File.ReadAllText(Path.Combine(ContentDirectory, entry.Value)));
        }

        // Generating Decisions
        decisions = JsonConvert.DeserializeObject<Dictionary<string, Decision>>(File.ReadAllText(Path.Combine(ContentDirectory, "decisions.json")));

        foreach(KeyValuePair<string, Decision> entry in decisions ) {
            foreach(string country in countries.Keys) {
                if (EvaluateScript(entry.Value.allowed, new Context(new List<string>() {country}, where: "decision:"+entry.Key+"/allowed")).output) {
                    countries[country].allowedDecisions.Add(entry.Key);
                }
            }
        }

        // Generating Events
        events = JsonConvert.DeserializeObject<Dictionary<string, EventGame>>(File.ReadAllText(Path.Combine(ContentDirectory, "events.json")));

        // foreach(KeyValuePair<string, EventGame> entry in events ) {
        // }

        turns = -1;
        GameObject.Find("UI-Next-Turn-Button").GetComponent<Button>().onClick.AddListener(NextTurnButton);

        SetPlayerTag(_configJSON["starting_tag"]);
        //GenerateEvent("RUS_Reform_USSR_event");
        NextTurn(); // First turn = 0, -1 is the pre-game
    }

    void Update()
    {
    }

    public void GenerateEvent(string eventId) {
        Context context = new Context(new List<string>() {playerTag}, where: "event:"+eventId);
        GameObject eventObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI-Event-Prefab.prefab"));
        eventObject.transform.SetParent(GameObject.Find("UI-Events").transform);
        eventObject.GetComponent<RectTransform>().localScale = new Vector2( 1, 1 );
        eventObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);
        eventObject.name = "UI-Event-"+eventId;
        if (events[eventId].title == null) {
            events[eventId].title = eventId+".title";
        }
        if (events[eventId].desc == null) {
            events[eventId].desc = eventId+".desc";
        }
        eventObject.transform.Find("UI-Event-Title").GetComponent<TMP_Text>().text = Localisation(events[eventId].title, context);
        eventObject.transform.Find("UI-Event-Desc").GetComponent<TMP_Text>().text = Localisation(events[eventId].desc, context);
        eventObject.transform.Find("UI-Event-Image").GetComponent<Image>().sprite = spriteImporter[events[eventId].image];
        int i = 0;
        foreach (EventOption option in Enumerable.Reverse(events[eventId].options).ToList() ) {
            i++;
            GameObject optionObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI-Event-Option-Prefab.prefab"));
            optionObject.name = "UI-Event-"+eventId+"-Options-"+option.title;
            optionObject.transform.SetParent(eventObject.transform.Find("UI-Event-Options"));
            optionObject.GetComponent<RectTransform>().localScale = new Vector2( 1, 1 );
            optionObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, (i*64)-24);
            optionObject.transform.Find("Text (TMP)").GetComponent<TMP_Text>().text = Localisation(option.title, context);
        }
    }

    public void GenerateDecisions() {
        GameObject[] oldDecisions = GameObject.FindGameObjectsWithTag("Decision");
        foreach(GameObject old in oldDecisions) {
            GameObject.Destroy(old);
        }
        int i = 0;
        List<string> totalDecisions = countries[playerTag].visibleDecisions.Concat(countries[playerTag].decisionTimeouts.Keys.ToList()).ToList();
        foreach(string entry in totalDecisions ) {
            i++;
            GameObject decisionObject = Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/UI-Decision-Prefab.prefab"));
            decisionObject.transform.SetParent(GameObject.Find("UI-Decisions-Content").transform);
            decisionObject.name = "UI-Decision-"+entry;
            decisionObject.tag = "Decision";
            decisionObject.transform.Find("UI-Decision-Text").GetComponent<TMP_Text>().text = Localisation(entry, DefaultContext);
            decisionObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(decisionObject.GetComponent<RectTransform>().anchoredPosition.x, i * -64);
            decisionObject.GetComponent<RectTransform>().localScale = new Vector2( 1, 1 );
            GameObject.Find("UI-Decisions-Content").GetComponent<RectTransform>().sizeDelta = new Vector2(GameObject.Find("UI-Decisions-Content").GetComponent<RectTransform>().sizeDelta.x, i*64);
            
            if (countries[playerTag].availableDecisions.Contains(entry)) {
                decisionObject.transform.Find("UI-Decision-Button").GetComponent<Image>().color = new Color32(64, 128, 64, 255);
                decisionObject.transform.Find("UI-Decision-Button").GetComponent<Button>().onClick.AddListener(delegate { DoDecision(entry, playerTag); });
            }
            else if (countries[playerTag].decisionTimeouts.ContainsKey(entry)) {
                decisionObject.GetComponent<Image>().color = new Color32(64, 64, 64, 255);
                decisionObject.transform.Find("UI-Decision-Button").GetComponent<Image>().color = new Color32(64, 64, 64, 255);
                decisionObject.transform.Find("UI-Decision-Button/Text (TMP)").GetComponent<TMP_Text>().text = $"{countries[playerTag].decisionTimeouts[entry]}";
            }
        }
    }

    public void DoDecision(string decision, string tag) {
        if (countries[tag].availableDecisions.Contains(decision)) {
            RunScript(decisions[decision].effects, new Context(new List<string>() {tag}, where: "decision:"+decision+"/effects"));
            if (decisions[decision].fire_only_once) {
                countries[tag].allowedDecisions.Remove(decision);
            }
            else {
                if (decisions[decision].timeout > 0) {
                    countries[tag].decisionTimeouts[decision] = decisions[decision].timeout;
                }
            }
        }
        GenerateContent();
    }

    public void CheckDecisions(string country) {
        countries[country].visibleDecisions = new List<string>() {};
        countries[country].availableDecisions = new List<string>() {};
        foreach(string decision in countries[country].allowedDecisions ) {
            if (EvaluateScript(decisions[decision].visible, new Context(new List<string>() {country}, where: "decision:"+decision+"/visible")).output && !countries[country].decisionTimeouts.ContainsKey(decision)) {
                countries[country].visibleDecisions.Add(decision);
                if (EvaluateScript(decisions[decision].available, new Context(new List<string>() {country}, where: "decision:"+decision+"/available")).output) {
                    countries[country].availableDecisions.Add(decision);
                }
            }
        }
    }

    public void NextTurnButton() {
        NextTurn();
    }

    private void GenerateContent() {
        CheckDecisions(playerTag);
        GenerateDecisions();
    }

    private void NextTurn() {
        turns++;
        GameObject.Find("UI-Turns-Text").GetComponent<TMP_Text>().text = $"{turns} Turns";

        foreach(string country in countries.Keys) {
            List<string> decs = new List<string>();
            //foreach(string decision in countries[country].decisionTimeouts.Keys) {
            for (int i = 0; i < countries[country].decisionTimeouts.Count; i++ ) {
                string decision = countries[country].decisionTimeouts.Keys.ToList()[i];
                countries[country].decisionTimeouts[decision] -= 1;
                if (countries[country].decisionTimeouts[decision] <= 0) {
                    decs.Add(decision);
                }
            }
            foreach (string decision in decs) {
                countries[country].decisionTimeouts.Remove(decision);
            }
            CheckDecisions(country);
        }

        GenerateContent();
        StyleAllTiles();
    }

    public string Localisation(string key, Context context) {
        if (_localisation.ContainsKey(key)) {
            return _localisation[key];
        }
        else {
            return key;
        }
    }

    public bool LocalisationKey(string key) {
        return _localisation.ContainsKey(key);
    }

    public void SetPlayerTag(string newTag) {
        playerTag = newTag;

        GameObject.Find("UI-PlayerName").GetComponent<TMP_Text>().text = Localisation(countries[playerTag].GetLongName(), DefaultContext);
        GameObject.Find("UI-PlayerFlag").GetComponent<Image>().sprite = spriteImporter["Flag-"+countries[playerTag].GetCosmeticFlag()];
    }

    public void StyleAllTiles() {
        foreach(string tile in tiles.Keys) {
            StyleTile(tile);
        }
    }

    public void StyleTile(string tile) {
        if (tiles[tile].controller != null) {
            GameObject.Find("Map_Tile-"+tile).GetComponent<Image>().color = countries[tiles[tile].controller].color;
        }
        else {
            GameObject.Find("Map_Tile-"+tile).GetComponent<Image>().color = new Color32(192, 192, 192, 255);
        }
    }

    public void ScriptError(string text) {
        Debug.Log($"<i><color=red>{text}</color></i>");
    }

    public string RunScript(List<Effect> script, Context context, bool doEffect = true) {
        string tooltip = "";
        foreach(Effect effect in script) {
            string scope = context.scopes.Last();
            string scopeType = DetermineScope(scope, context);
            switch (effect.type) {
                case "if":
                    TriggerBlock newBlock = effect.limit;
                    bool eval = EvaluateScript(newBlock, context).output;
                    if (eval) {
                        RunScript(effect.effects, context, doEffect);
                    }
                    break;
                case "tooltip":
                    tooltip += Localisation(effect.text, context);
                    break;
                case "set_color":
                    if (doEffect) {
                        Color newCol;
                        if (ColorUtility.TryParseHtmlString(Tokenise(effect.color, context), out newCol)) {
                            countries[scope].color = newCol;
                        }
                        StyleAllTiles();
                    }
                    break;
                
                case "set_owner":
                    if (scopeType == "tile") {
                        if (DetermineScope(Tokenise(effect.owner, context), context ) == "country") {
                            if (doEffect) {
                                tiles[scope].SetOwner(Tokenise(effect.owner, context));
                            }
                        }
                        else {
                            ScriptError($"{context.where}: set_owner: {effect.owner} is not a country");
                        }
                    }
                    if (scopeType == "country") {
                        if (DetermineScope(Tokenise(effect.tile, context), context ) == "tile") {
                            if (doEffect) {
                                tiles[Tokenise(effect.tile, context)].SetOwner(scope);
                            }
                        }
                        else {
                            ScriptError($"{context.where}: set_owner: {effect.tile} is not a tile");
                        }
                    }
                    else {
                        ScriptError($"{context.where}: set_owner: Scope ({scope}) must be tile or country");
                    }
                    break;
                case "clear_decision_timeout":
                    countries[scope].decisionTimeouts.Remove(Tokenise(effect.decision, context));
                    break;
            }
        }
        return tooltip;
    }

    public (bool output, string tooltip) EvaluateScript(TriggerBlock block, Context context) {
        bool evaluation = true;
        List<bool> evals = new List<bool>() {};
        string tooltip = "";
        string blockType = block.type;

        foreach (Trigger trigger in block.triggers) {
            string scope = context.scopes.Last();
            string scopeType = DetermineScope(scope, context);
            bool eval = true;

            switch (trigger.type) {
                case "and":
                case "or":
                    TriggerBlock newBlock = new TriggerBlock();
                    newBlock.type = trigger.type;
                    newBlock.triggers = trigger.triggers;
                    eval = EvaluateScript(newBlock, context).output;
                    break;
                case "tooltip":
                    tooltip += Localisation(trigger.text, context);
                    break;
                case "owns_tile":
                    if (scopeType == "tile") {
                        if (DetermineScope(Tokenise(trigger.owner, context), context ) == "country") {
                            eval = tiles[scope].owner == Tokenise(trigger.owner, context);
                        }
                        else {
                            ScriptError($"{context.where}: owns_tile: {trigger.owner} is not a country");
                        }
                    }
                    if (scopeType == "country") {
                        if (DetermineScope(Tokenise(trigger.tile, context), context ) == "tile") {
                            eval = tiles[Tokenise(trigger.tile, context)].owner == scope;
                        }
                        else {
                            ScriptError($"{context.where}: owns_tile: {trigger.tile} is not a tile");
                        }
                    }
                    else {
                        ScriptError($"{context.where}: owns_tile: Scope ({scope}) must be tile or country");
                    }
                    break;

                case "always":
                    eval = true;
                    break;
            }

            // Debug.Log($"<b>{scope}  {context.where}  {eval}</b>\n{trigger}");

            if (trigger.not) {
                eval = !eval;
            }

            evals.Add(eval);
        }

        switch (block.type) {
            case "and":
                evaluation = !evals.Contains(false);
                break;
            case "or":
                evaluation = evals.Contains(true);
                break;
        }

        Debug.Log($"<b>{context.scopes.Last()}  {context.where}  {evaluation}</b>");

        if (block.not == true) {
            return (!evaluation, tooltip);
        }
        else {
            return (evaluation, tooltip);
        }
    }

    public string Tokenise(string token, Context context) {
        return token;
    }

    public string DetermineScope(string token, Context context) {
        if (tiles.ContainsKey(token)) {
            return "tile";
        }
        else if (countries.ContainsKey(token)) {
            return "country";
        }
        else {
            return "unknown";
        }
    }
}

[MoonSharpUserData]
public class ScriptLogic {
    GlobalScript globalScript;

    public ScriptLogic() {
        globalScript = GameObject.Find("GlobalScript").GetComponent<GlobalScript>();
    }

    public void SetColor(string country, string color) {
        Color newCol;
        if (ColorUtility.TryParseHtmlString(color, out newCol)) {
            globalScript.countries[country].color = newCol;
        }
        globalScript.StyleAllTiles();
    }

    public void SetOwner(string country, string tile) {
        globalScript.tiles[tile].SetOwner(country);
    }

    public void SetController(string country, string tile) {
        globalScript.tiles[tile].SetController(country);
    }

    public void ClearDecisionTimeout(string country, string decision) {
        globalScript.countries[country].decisionTimeouts.Remove(decision);
    }

    public bool OwnsTile(string country, string tile) {
        return (globalScript.tiles[tile].owner == country);
    }

    public bool ControlsTile(string country, string tile) {
        return (globalScript.tiles[tile].controller == country);
    }
}
public class Context {
    public List<string> scopes;
    public Dictionary<string, string> vars;
    public string where;

    public Context(List<string> a = null, Dictionary<string, string> b = null, string where = "Other") {
        if (a == null) {
            a = new List<string>() {};
        }
        if (b == null) {
            b = new Dictionary<string, string>() {};
        }
        scopes = a;
        vars = b;
        this.where = where;
    }
}
public class MapData {
    public List<int> size;
    public Dictionary<string, List<float>> tiles;
}
public class Decision {
    public TriggerBlock allowed = new TriggerBlock();
    public TriggerBlock visible = new TriggerBlock();
    public TriggerBlock available = new TriggerBlock();
    public List<Effect> effects = new List<Effect>();
    public bool fire_only_once = false;
    public int timeout = 1;
}
public class Effect {
    public string type; // set_color, set_owner, if, tooltip, clear_decision_timeout
    public string color; // set_color
    public string owner; // set_owner (tile)
    public string tile; // set_owner (country)
    public TriggerBlock limit; // if
    public List<Effect> effects; // if
    public string text; // tooltip
    public string decision; // clear_decision_timeout
}
public class TriggerBlock {
    public string type = "and";
    public bool not = false;
    public List<Trigger> triggers = new List<Trigger>();
}
public class Trigger {
    public string type; // owns_tile, and, or, tooltip
    public bool not = false;
    public string owner; // owns_tile (tile)
    public string tile; // owns_tile (country)
    public List<Trigger> triggers; // and, or
    public string text; // tooltip

    override public string ToString() {
        string outt = $"{type}\nnot: {not}\n";
        if (owner != null) {
            outt += $"owner: {owner}\n";
        }
        if (tile != null) {
            outt += $"tile: {tile}\n";
        }
        if (triggers != null) {
            outt += $"triggers: {triggers}\n";
        }
        return outt;
    }
}
public class EventGame {
    public string image = "Event-Default";
    public string title;
    public string desc;
    public List<EventOption> options;
}
public class EventOption {
    public string title;
    public List<Effect> effects;
}