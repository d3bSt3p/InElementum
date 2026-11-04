using UnityEngine;

namespace InElementum.Data.Settings
{
    [CreateAssetMenu(fileName = "Scene Data", menuName = "In Elementum/Settings/Scene", order = 2)]
    public class SceneData : ScriptableObject
    {
        public int mainMenuSceneIndex = 0;
        public int combatSceneIndex = 1;
    }
}
