using Mirror;
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "New Map Set", menuName = "Rounds/MapSet")]
public class MapSet : ScriptableObject
{
    [Scene]
    [SerializeField] private List<string> maps = new List<string>();

    public IReadOnlyCollection<string> Maps => maps.AsReadOnly();
}
