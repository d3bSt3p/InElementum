using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Core.Utils.Background
{
    public class BackgroundRoot : MonoBehaviour
    {
        [SerializeField] private BackgroundTypes backgroundType;

        public BackgroundTypes BackgroundType => backgroundType;
    }
}