using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Game Content Catalog", fileName = "GameContent")]
public class GameContentCatalog : ScriptableObject
{
    public const int StagesPerChunk = 50;
    public List<TextAsset> stageChunks = new List<TextAsset>();

    public TextAsset GetStageChunk(int stage)
    {
        int index = stage > 0 ? (stage - 1) / StagesPerChunk : -1;
        return index >= 0 && index < stageChunks.Count ? stageChunks[index] : null;
    }
    // mapId 1, 2, 3 순서. 씬에서 이 카탈로그를 참조하므로 빌드에도 포함된다.
    public List<GameObject> maps = new List<GameObject>();
}
