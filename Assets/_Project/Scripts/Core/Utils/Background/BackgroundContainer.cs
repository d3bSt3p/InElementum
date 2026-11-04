using System;
using System.Collections.Generic;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Core.Utils.Background
{
    public class BackgroundContainer : MonoBehaviour
    {
        [SerializeField] private List<BackgroundRoot> backgroundRootList;
        public List<BackgroundRoot> BackgroundRootList => backgroundRootList;
        
        public void OpenBackground(BackgroundTypes type)
        {
            foreach (var backgroundRoot in BackgroundRootList)
                backgroundRoot.gameObject.SetActive(type == backgroundRoot.BackgroundType);
        }
    }
}