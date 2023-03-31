using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

public class SpriteImporter
{
	//Contains all the sprites themselves
	public Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
	//I do not understand this code but it works
public void Generate(string key, string fileName)
{
    // Load the file as a byte array
    byte[] fileData = File.ReadAllBytes(fileName);

    // Create a new texture and load the image data into it
    Texture2D tex = new Texture2D(4, 4, TextureFormat.DXT1, false);
    tex.LoadImage(fileData);

    // Create a new sprite from the texture
    Sprite sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), Vector2.zero, 100, 0, SpriteMeshType.FullRect);

    // Set the sprite name and add it to the dictionary
    sprite.name = fileName;
    sprites[key] = sprite;
}

	public bool Contains(string key)
	{
		return sprites.ContainsKey(key);
	}
	//Allows you to do the [] thingy
	public Sprite this[string key]
	{
		get
		{
			return sprites[key];
		}
		set
		{
			sprites[key] = value;
		}
	}
}