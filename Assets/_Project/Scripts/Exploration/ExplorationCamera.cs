using UnityEngine;
#if IE_CINEMACHINE
using Unity.Cinemachine;
#endif

namespace InElementum.Exploration
{
    /// <summary>
    /// Hooks into the room's Cinemachine follow camera so a teleported player is not panned to: the camera cuts
    /// to them instead. Without Cinemachine installed it does nothing.
    /// </summary>
    public class ExplorationCamera : MonoBehaviour
    {
        /// <summary>Cuts every follow camera in the scene to its target on the next update.</summary>
        public void Snap()
        {
#if IE_CINEMACHINE
            foreach (var camera in FindObjectsByType<CinemachineCamera>(FindObjectsInactive.Include))
            {
                if (camera.gameObject.scene == gameObject.scene) camera.PreviousStateIsValid = false;
            }
#endif
        }
    }
}
