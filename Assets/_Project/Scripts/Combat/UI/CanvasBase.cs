using InElementum.Combat.Managers;using InElementum.Core.Managers;
using UnityEngine;

namespace InElementum.Combat.UI
{
    public class CanvasBase : MonoBehaviour
    {
        protected CombatManager CombatManager => CombatManager.Instance;
        protected GameManager GameManager => GameManager.Instance;
        protected UIManager UIManager => UIManager.Instance;
        public virtual void OpenCanvas()
        {
            gameObject.SetActive(true);
        }


        public virtual void CloseCanvas()
        {
            gameObject.SetActive(false);
        }

        public virtual void ResetCanvas()
        {
            
        }
    }
}