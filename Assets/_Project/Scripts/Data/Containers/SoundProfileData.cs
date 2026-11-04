using System.Collections.Generic;
using InElementum.Data.Enums;
using UnityEngine;

namespace InElementum.Data.Containers
{
    /// <summary>
    /// A set of interchangeable clips for one <see cref="AudioActionType"/>. The <see cref="Core.Managers.AudioManager"/>
    /// loads every profile under <c>Resources/Audio</c> and plays a random clip from the one matching each request.
    /// Can also be used on its own, such as an enemy's voice.
    /// </summary>
    [CreateAssetMenu(fileName = "Sound Profile", menuName = "In Elementum/Containers/SoundProfile", order = 1)]
    public class SoundProfileData : ScriptableObject
    {
        [SerializeField] private AudioActionType audioType;
        [SerializeField] private List<AudioClip> randomClipList = new List<AudioClip>();

        [Header("Playback")]
        [SerializeField] [Range(0f, 1f)] private float volume = 1f;
        [Tooltip("Each play picks a pitch this far either side of 1, so repeats don't sound identical.")]
        [SerializeField] [Range(0f, 0.5f)] private float pitchVariance = 0.05f;
        [Tooltip("Plays closer together than this are dropped, so many hits in one frame don't stack up.")]
        [SerializeField] [Min(0f)] private float minInterval = 0.03f;

        private int _lastIndex = -1;

        public AudioActionType AudioType => audioType;
        public List<AudioClip> RandomClipList => randomClipList;
        public float Volume => volume;
        public float PitchVariance => pitchVariance;
        public float MinInterval => minInterval;

        /// <summary>A random clip, never the same one twice in a row when there is a choice. Null when empty.</summary>
        public AudioClip GetRandomClip()
        {
            if (randomClipList == null || randomClipList.Count == 0) return null;
            if (randomClipList.Count == 1) return randomClipList[0];

            var count = randomClipList.Count;
            int index;
            if (_lastIndex < 0 || _lastIndex >= count) index = Random.Range(0, count);
            else
            {
                index = Random.Range(0, count - 1);
                if (index >= _lastIndex) index++;
            }

            _lastIndex = index;
            return randomClipList[index];
        }

        /// <summary>A pitch around 1, within <see cref="PitchVariance"/>.</summary>
        public float GetRandomPitch() => 1f + Random.Range(-pitchVariance, pitchVariance);
    }
}
