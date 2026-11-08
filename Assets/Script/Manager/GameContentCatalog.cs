using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Game Content Catalog", fileName = "GameContent")]
public class GameContentCatalog : ScriptableObject
{
    public TextAsset stageData;
    // mapId 1, 2, 3 순서. 씬에서 이 카탈로그를 참조하므로 빌드에도 포함된다.
    public List<GameObject> maps = new List<GameObject>();
}
