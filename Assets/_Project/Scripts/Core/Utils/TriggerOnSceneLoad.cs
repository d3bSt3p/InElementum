using UnityEngine;
using UnityEngine.Events;

namespace InElementum.Core.Utils
{
    public class TriggerOnSceneLoad : MonoBehaviour
    {
        public UnityEvent onLoad;
      
        private void Start()
        {
            onLoad?.Invoke();
        }
    }
}
