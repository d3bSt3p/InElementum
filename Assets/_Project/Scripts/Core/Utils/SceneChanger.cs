using System;
using System.Collections;
using InElementum.Core.Managers;
using InElementum.Core.Tooltip;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InElementum.Core.Utils
{
    public class SceneChanger : MonoBehaviour
    {
        private GameManager GameManager => GameManager.Instance;
        private UIManager UIManager => UIManager.Instance;

        private enum SceneType
        {
            MainMenu,
            Combat
        }

        public void OpenMainMenuScene()
        {
            StartCoroutine(DelaySceneChange(SceneType.MainMenu));
        }

        public void OpenCombatScene()
        {
            StartCoroutine(DelaySceneChange(SceneType.Combat));
        }

        public void ChangeScene(int sceneId)
        {
            if (sceneId == GameManager.SceneData.mainMenuSceneIndex)
                OpenMainMenuScene();
            else if (sceneId == GameManager.SceneData.combatSceneIndex)
                OpenCombatScene();
            else
                SceneManager.LoadScene(sceneId);

            TooltipManager.Instance.HideTooltip();
        }

        public void ExitApp()
        {
            GameManager.OnExitApp();
            Application.Quit();
        }

        private IEnumerator DelaySceneChange(SceneType type)
        {
            yield return StartCoroutine(UIManager.Fade(true));

            switch (type)
            {
                case SceneType.MainMenu:
                    UIManager.ChangeScene(GameManager.SceneData.mainMenuSceneIndex);
                    UIManager.SetCanvas(UIManager.CombatCanvas, false, true);
                    GameManager.InitGameplayData();
                    break;
                case SceneType.Combat:
                    UIManager.ChangeScene(GameManager.SceneData.combatSceneIndex);
                    UIManager.SetCanvas(UIManager.CombatCanvas, false, true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }
}
