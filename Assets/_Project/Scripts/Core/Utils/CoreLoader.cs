using InElementum.Core.Managers;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InElementum.Core.Utils
{
    [DefaultExecutionOrder(-11)]
    public class CoreLoader : MonoBehaviour
    {
        private const string CoreSceneName = "Core";

        private void Awake()
        {
            if (!GameManager.Instance)
            {
                if (Application.CanStreamedLevelBeLoaded(CoreSceneName))
                    SceneManager.LoadScene(CoreSceneName, LoadSceneMode.Additive);
                else
                    Debug.LogError($"Add the {CoreSceneName} scene to Build Settings.");
            }

            Destroy(gameObject);
        }
    }
}
