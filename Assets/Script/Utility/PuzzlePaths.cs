using System.IO;
using UnityEngine;

// 퍼즐 에셋과 게임 저장 파일의 경로.
public static class PuzzlePaths
{
    public const string ContentCatalogAsset = "Assets/Aset/Data/GameContent.asset";
    public const string SaveFileName = "savefile-puzzle-local.json";
    public static string SaveFile => Path.Combine(Application.persistentDataPath, SaveFileName);
}
