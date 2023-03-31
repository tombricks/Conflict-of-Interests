using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class MapTile : MonoBehaviour
{
    GlobalScript globalScript;
	public Button mapButton;
    public string id;
    
    public void Create(string id) {
        globalScript = GameObject.Find("GlobalScript").GetComponent<GlobalScript>();
		globalScript.tiles[id].gameObject = gameObject;
        this.id = id;
        mapButton = this.GetComponent<Button>();
		mapButton.GetComponent<Image>().alphaHitTestMinimumThreshold = 0.5F;
		mapButton.onClick.AddListener(OnClick);
        this.name = "Map_Tile-"+id;
    }

    void OnClick() {

    }
}
