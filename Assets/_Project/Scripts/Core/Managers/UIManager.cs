using System.Collections;
using InElementum.Combat.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InElementum.Core.Managers
{
    [DefaultExecutionOrder(-4)]
    public class UIManager : MonoBehaviour
    {
        public UIManager() {}
        public static UIManager Instance { get; private set; }

        [Header("Canvases")]
        [SerializeField] private CombatCanvas combatCanvas;

        [Header("Fader")]
        [SerializeField] private CanvasGroup fader;
        [SerializeField] private float fadeSpeed = 1f;

        #region Cache
        public CombatCanvas CombatCanvas => combatCanvas;
        #endregion

        #region Setup
        private void Awake()
        {
            if (Instance == null)
            {
                transform.parent = null;
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
        #endregion

        #region Public Methods
        public void SetCanvas(CanvasBase targetCanvas, bool open, bool reset = false)
        {
            if (reset)
                targetCanvas.ResetCanvas();

            if (open)
                targetCanvas.OpenCanvas();
            else
                targetCanvas.CloseCanvas();
        }

        public void ChangeScene(int index)
        {
            StartCoroutine(ChangeSceneRoutine(index));
        }
        #endregion

        #region Routines
        private IEnumerator ChangeSceneRoutine(int index)
        {
            SceneManager.LoadScene(index);
            yield return StartCoroutine(Fade(false));
        }

        public IEnumerator Fade(bool isIn)
        {
            var waitFrame = new WaitForEndOfFrame();
            var timer = isIn ? 0f : 1f;

            while (true)
            {
                timer += Time.deltaTime * (isIn ? fadeSpeed : -fadeSpeed);
                fader.alpha = timer;
                if (isIn ? timer >= 1f : timer <= 0f) break;
                yield return waitFrame;
            }
        }
        #endregion
    }
}
