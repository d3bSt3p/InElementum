using System.Collections;
using System.Collections.Generic;
using InElementum.Data.Containers;
using InElementum.Data.Enums;
using UnityEngine;
using UnityEngine.Audio;

namespace InElementum.Core.Managers
{
    /// <summary>
    /// Plays the game's sounds, ported from NueDeck's AudioManager. Requests name an <see cref="AudioActionType"/>;
    /// the manager plays a random clip from that type's <see cref="SoundProfileData"/>. Profiles come from the
    /// inspector list and from every profile under <c>Resources/Audio</c>, the inspector winning a tie. A missing
    /// profile is silent, so code can ask for sounds that have no clips yet.
    /// <para>
    /// Four channels: music and ambience loop and crossfade, SFX and UI are one-shots. One-shots play on a small pool
    /// of voices so each can take its own pitch. All of it ignores <see cref="Time.timeScale"/>, so the paused menu
    /// still has sound.
    /// </para>
    /// <para>
    /// Every source routes through one mixer, <c>Resources/Audio/GameMixer</c>: groups Music, Ambience, SFX and UI,
    /// with UI under SFX. Set overall loudness per group there; the volumes below scale on top of it.
    /// </para>
    /// <para>
    /// No scene needs one: if none is placed, one is created after the first scene loads and kept across scenes.
    /// </para>
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private AudioManager() { }
        public static AudioManager Instance { get; private set; }

        /// <summary>Profiles in any folder under this Resources path are loaded at start.</summary>
        public const string ResourceFolder = "Audio";

        /// <summary>Resources path of the mixer every source routes through, used when none is assigned.</summary>
        public const string MixerResource = "Audio/GameMixer";

        [Header("Mixer (loaded from Resources/Audio/GameMixer when empty)")]
        [Tooltip("Groups named Music, Ambience, SFX and UI receive those channels. Every one-shot voice uses SFX.")]
        [SerializeField] private AudioMixer mixer;

        [Header("Sources (created when empty)")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource ambienceSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource buttonSource;

        [Header("Profiles")]
        [Tooltip("Optional. Profiles under Resources/Audio are loaded too; these win when both have a type.")]
        [SerializeField] private List<SoundProfileData> soundProfileDataList = new List<SoundProfileData>();

        [Header("Mix")]
        [SerializeField] [Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.6f;
        [SerializeField] [Range(0f, 1f)] private float ambienceVolume = 0.5f;
        [SerializeField] [Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField] [Range(0f, 1f)] private float uiVolume = 0.8f;
        [Tooltip("Seconds for music and ambience to fade in or out.")]
        [SerializeField] [Min(0f)] private float fadeDuration = 0.75f;
        [Tooltip("How many one-shots can play at once, each at its own pitch.")]
        [SerializeField] [Range(1, 32)] private int sfxVoices = 12;

        private readonly Dictionary<AudioActionType, SoundProfileData> _audioDict = new Dictionary<AudioActionType, SoundProfileData>();
        private readonly Dictionary<SoundProfileData, float> _lastPlayed = new Dictionary<SoundProfileData, float>();
        private readonly List<AudioSource> _voices = new List<AudioSource>();
        private int _nextVoice;

        private readonly Queue<(AudioClip clip, float volume)> _sequentialQueue = new Queue<(AudioClip, float)>();
        private bool _isPlayingSequential;

        private LoopChannel _music;
        private LoopChannel _ambience;

        #region Mix properties
        public float MasterVolume { get => masterVolume; set => masterVolume = Mathf.Clamp01(value); }
        public float MusicVolume { get => musicVolume; set => musicVolume = Mathf.Clamp01(value); }
        public float AmbienceVolume { get => ambienceVolume; set => ambienceVolume = Mathf.Clamp01(value); }
        public float SfxVolume { get => sfxVolume; set => sfxVolume = Mathf.Clamp01(value); }
        public float UiVolume { get => uiVolume; set => uiVolume = Mathf.Clamp01(value); }
        #endregion

        #region Setup
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>The manager, created now if no scene has made one yet. For Start methods that set the music.</summary>
        public static AudioManager Ensure()
        {
            if (Instance == null) new GameObject(nameof(AudioManager)).AddComponent<AudioManager>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            transform.parent = null;
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (musicSource == null) musicSource = CreateSource("Music", true);
            if (ambienceSource == null) ambienceSource = CreateSource("Ambience", true);
            if (sfxSource == null) sfxSource = CreateSource("SFX", false);
            if (buttonSource == null) buttonSource = CreateSource("UI", false);

            _voices.Add(sfxSource);
            for (var i = 1; i < sfxVoices; i++) _voices.Add(CreateSource($"SFX Voice {i}", false));

            RouteToMixer();

            _music = new LoopChannel(musicSource);
            _ambience = new LoopChannel(ambienceSource);
            BuildDictionary();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private AudioSource CreateSource(string sourceName, bool loop)
        {
            var child = new GameObject(sourceName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = true;
            return source;
        }

        /// <summary>Sends each source to its mixer group. Without a mixer, sources play straight to the listener.</summary>
        private void RouteToMixer()
        {
            if (mixer == null) mixer = Resources.Load<AudioMixer>(MixerResource);
            if (mixer == null) return;

            var sfx = FindGroup("SFX");
            musicSource.outputAudioMixerGroup = FindGroup("Music");
            ambienceSource.outputAudioMixerGroup = FindGroup("Ambience");
            buttonSource.outputAudioMixerGroup = FindGroup("UI") ?? sfx;
            foreach (var voice in _voices) voice.outputAudioMixerGroup = sfx;
        }

        /// <summary>The mixer group called <paramref name="groupName"/>, or the master group if there is none.</summary>
        private AudioMixerGroup FindGroup(string groupName)
        {
            AudioMixerGroup master = null;
            foreach (var group in mixer.FindMatchingGroups(string.Empty))
            {
                if (group.name == groupName) return group;
                if (master == null) master = group;
            }

            return master;
        }

        private void BuildDictionary()
        {
            _audioDict.Clear();
            if (soundProfileDataList != null)
                foreach (var profile in soundProfileDataList)
                    Register(profile);

            foreach (var profile in Resources.LoadAll<SoundProfileData>(ResourceFolder))
                Register(profile);
        }

        private void Register(SoundProfileData profile)
        {
            if (profile == null) return;
            if (_audioDict.TryGetValue(profile.AudioType, out var existing))
            {
                if (existing != profile)
                    Debug.LogWarning($"[AudioManager] '{profile.name}' and '{existing.name}' are both {profile.AudioType}; using '{existing.name}'.", profile);
                return;
            }

            _audioDict.Add(profile.AudioType, profile);
        }
        #endregion

        #region Static helpers
        /// <summary>Plays <paramref name="type"/> as a one-shot, if there is a manager. Safe to call from anywhere.</summary>
        public static void Play(AudioActionType type)
        {
            if (Instance != null) Instance.PlayOneShot(type);
        }

        /// <summary>Plays a clip from <paramref name="profile"/>, if there is a manager and a profile.</summary>
        public static void Play(SoundProfileData profile)
        {
            if (Instance != null) Instance.PlayOneShot(profile);
        }

        /// <summary>Plays <paramref name="type"/> on the UI channel, if there is a manager.</summary>
        public static void PlayUi(AudioActionType type)
        {
            if (Instance != null) Instance.PlayOneShotButton(type);
        }
        #endregion

        #region Public Methods
        /// <summary>True when <paramref name="type"/> has a profile with at least one clip.</summary>
        public bool HasSound(AudioActionType type) =>
            _audioDict.TryGetValue(type, out var profile) && profile != null && profile.RandomClipList.Count > 0;

        public SoundProfileData GetProfile(AudioActionType type) => _audioDict.TryGetValue(type, out var profile) ? profile : null;

        /// <summary>Crossfades the music channel to <paramref name="clip"/>. Null fades the music out.</summary>
        public void PlayMusic(AudioClip clip, float volume = 1f) => Request(_music, clip, volume);

        /// <summary>
        /// Crossfades the music channel to a clip of <paramref name="type"/>, or fades it out when the type has no
        /// clips. Keeps playing if a clip of the type already is.
        /// </summary>
        public void PlayMusic(AudioActionType type) => PlayLoop(_music, type);

        public void StopMusic() => Request(_music, null, 1f);

        /// <summary>Like <see cref="PlayMusic(AudioActionType)"/>, on the ambience channel.</summary>
        public void PlayAmbience(AudioActionType type) => PlayLoop(_ambience, type);

        public void StopAmbience() => Request(_ambience, null, 1f);

        /// <summary>
        /// Fades the ambience out without forgetting it, or back in. Combat hushes the room's ambience this way while
        /// the battle plays its own music.
        /// </summary>
        public void SetAmbienceSuspended(bool suspended) => _ambience.Suspended = suspended;

        public void PlayOneShot(AudioActionType type) => PlayOneShot(GetProfile(type));

        public void PlayOneShot(SoundProfileData profile) => PlayProfile(profile, false);

        public void PlayOneShot(AudioClip clip) => PlayOneShot(clip, 1f);

        public void PlayOneShot(AudioClip clip, float volume, float pitch = 1f)
        {
            if (clip == null) return;
            var voice = NextVoice();
            voice.pitch = pitch;
            voice.PlayOneShot(clip, volume * sfxVolume * masterVolume);
        }

        public void PlayOneShotButton(AudioActionType type) => PlayProfile(GetProfile(type), true);

        public void PlayOneShotButton(AudioClip clip)
        {
            if (clip == null) return;
            buttonSource.pitch = 1f;
            buttonSource.PlayOneShot(clip, uiVolume * masterVolume);
        }

        /// <summary>Queues a sound to play sequentially — each clip waits for the previous one to finish.</summary>
        public void PlayQueued(AudioActionType type)
        {
            var profile = GetProfile(type);
            var clip = profile != null ? profile.GetRandomClip() : null;
            if (clip == null) return;

            _sequentialQueue.Enqueue((clip, profile.Volume));
            if (!_isPlayingSequential) StartCoroutine(PlaySequentialQueue());
        }
        #endregion

        #region One-shots
        private void PlayProfile(SoundProfileData profile, bool ui)
        {
            if (profile == null) return;

            var now = Time.unscaledTime;
            if (_lastPlayed.TryGetValue(profile, out var last) && now - last < profile.MinInterval) return;

            var clip = profile.GetRandomClip();
            if (clip == null) return;
            _lastPlayed[profile] = now;

            if (ui)
            {
                buttonSource.pitch = profile.GetRandomPitch();
                buttonSource.PlayOneShot(clip, profile.Volume * uiVolume * masterVolume);
            }
            else
            {
                PlayOneShot(clip, profile.Volume, profile.GetRandomPitch());
            }
        }

        /// <summary>The next voice in the pool: an idle one if there is one, else the oldest.</summary>
        private AudioSource NextVoice()
        {
            for (var i = 0; i < _voices.Count; i++)
            {
                var index = (_nextVoice + i) % _voices.Count;
                if (_voices[index].isPlaying) continue;
                _nextVoice = (index + 1) % _voices.Count;
                return _voices[index];
            }

            var voice = _voices[_nextVoice];
            _nextVoice = (_nextVoice + 1) % _voices.Count;
            return voice;
        }

        private IEnumerator PlaySequentialQueue()
        {
            _isPlayingSequential = true;

            while (_sequentialQueue.Count > 0)
            {
                var (clip, volume) = _sequentialQueue.Dequeue();
                PlayOneShot(clip, volume);
                yield return new WaitForSecondsRealtime(clip.length);
            }

            _isPlayingSequential = false;
        }
        #endregion

        #region Loops
        /// <summary>
        /// A looping channel. Changing its clip fades the old one out first, then fades the new one in. Suspending
        /// fades it to silence without forgetting the clip.
        /// </summary>
        private sealed class LoopChannel
        {
            public readonly AudioSource Source;
            public AudioClip Clip;
            public float ClipVolume = 1f;
            /// <summary>0 to 1, eased toward the target each frame.</summary>
            public float Fade;
            public bool Suspended;
            public bool HasPending;
            public AudioClip Pending;
            public float PendingVolume = 1f;

            public LoopChannel(AudioSource source) => Source = source;
        }

        private void Update()
        {
            Tick(_music, musicVolume);
            Tick(_ambience, ambienceVolume);
        }

        private void Tick(LoopChannel channel, float channelVolume)
        {
            if (channel == null) return;

            if (channel.HasPending && (channel.Clip == null || channel.Fade <= 0f)) Swap(channel);

            var target = channel.Clip != null && !channel.HasPending && !channel.Suspended ? 1f : 0f;
            channel.Fade = fadeDuration > 0f
                ? Mathf.MoveTowards(channel.Fade, target, Time.unscaledDeltaTime / fadeDuration)
                : target;
            channel.Source.volume = masterVolume * channelVolume * channel.ClipVolume * channel.Fade;
        }

        private static void Swap(LoopChannel channel)
        {
            channel.Source.Stop();
            channel.Clip = channel.Pending;
            channel.ClipVolume = channel.PendingVolume;
            channel.HasPending = false;
            channel.Pending = null;
            channel.Fade = 0f;
            if (channel.Clip == null) return;

            channel.Source.clip = channel.Clip;
            channel.Source.Play();
        }

        /// <summary>Asks <paramref name="channel"/> to move to <paramref name="clip"/>; null fades it out.</summary>
        private static void Request(LoopChannel channel, AudioClip clip, float volume)
        {
            if (channel.Clip == clip)
            {
                // Already playing it: drop any change still waiting on the fade-out.
                channel.HasPending = false;
                channel.Pending = null;
                return;
            }

            channel.HasPending = true;
            channel.Pending = clip;
            channel.PendingVolume = volume;
        }

        /// <summary>Plays a clip of <paramref name="type"/>, or fades out when it has none. Keeps a clip of it already playing.</summary>
        private void PlayLoop(LoopChannel channel, AudioActionType type)
        {
            var profile = GetProfile(type);
            if (profile == null || profile.RandomClipList.Count == 0)
            {
                Request(channel, null, 1f);
                return;
            }

            var current = channel.HasPending ? channel.Pending : channel.Clip;
            if (current != null && profile.RandomClipList.Contains(current)) return;
            Request(channel, profile.GetRandomClip(), profile.Volume);
        }
        #endregion
    }
}
