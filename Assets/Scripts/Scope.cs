using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Scope
{
    protected GlobalScript globalScript;
    public string id; // Set in Scope()
    public string name; // Set in Scope()
    public string longName; // Setting not needed
    public Dictionary<string, int> flags; // Set in Scope()

    public Scope(string id) {
        globalScript = GameObject.Find("GlobalScript").GetComponent<GlobalScript>();
        this.id = id;
        this.name = id;
        flags = new Dictionary<string, int>() {};
    }

    public int SetFlag(string flag, int value) {
        this.flags[flag] = value;
        return value;
    }

    public int GetFlag(string flag, int defVal = 0) {
        if (flags.ContainsKey(flag)) {
            return flags[flag];
        }
        else {
            return defVal;
        }
    }

    public string GetName() {
        return this.name;
    }
    public string GetLongName() {
        if (longName == null) {
            if (globalScript.LocalisationKey(name + "_long")) {
                return name + "_long";
            }
            else {
                return name;
            }
        }
        else {
            return longName;
        }
    }
}

public class Country : Scope {
    public Color32 color;
    public List<string> tilesOwned;
    public List<string> tilesControlled;
    public List<string> allowedDecisions;
    public List<string> visibleDecisions;
    public List<string> availableDecisions;
    public Dictionary<string, int> decisionTimeouts;
    public string cosmeticFlag;
    public Country(string id) : base(id) {
        color = new Color32(255, 255, 255, 255);
        tilesOwned = new List<string>() {};
        tilesControlled = new List<string>() {};
        allowedDecisions = new List<string>() {};
        visibleDecisions = new List<string>() {};
        availableDecisions = new List<string>() {};
        decisionTimeouts = new Dictionary<string, int>() {};
    }

    public string GetCosmeticFlag() {
        if (cosmeticFlag == null) {
            return id;
        }
        else {
            return cosmeticFlag;
        }
    }
}

public class Tile : Scope
{
    public GameObject gameObject;
    public string owner;
    public string controller;

    public Tile(string id) : base(id) {
    }

    public void SetOwner(string country, int transferControl = 0) {
        string oldOwner = owner;
        string oldController = controller;
        owner = country;
        globalScript.countries[country].tilesOwned.Add(id);
        if (oldOwner != null) {
            globalScript.countries[oldOwner].tilesOwned.Remove(id);
        }
        if (transferControl == 1) { // transfer
            SetController(country);
        }
        else if (transferControl == 0) { // default
            if (oldOwner == oldController) {
                SetController(country);
            }
        }
        else if (transferControl == -1) { // no transfer

        }
        globalScript.StyleTile(id);
    }

    public void SetController(string country) {
        string oldController = controller;
        controller = country;
        globalScript.countries[country].tilesControlled.Add(id);
        if (oldController != null) {
            globalScript.countries[country].tilesControlled.Remove(id);
        }
        globalScript.StyleTile(id);
    }
}