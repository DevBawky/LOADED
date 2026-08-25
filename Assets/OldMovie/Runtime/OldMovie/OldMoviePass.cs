using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Audio;
using Random = UnityEngine.Random;

//  OldMovieFx © NullTale - https://x.com/NullTale
namespace VolFx
{
    [ShaderName("Hidden/VolFx/OldMovie")]
    public class OldMoviePass : VolFx.Pass
    {
        private static readonly int s_Vignette = Shader.PropertyToID("_Vignette");
        private static readonly int s_Grain    = Shader.PropertyToID("_Grain");
        private static readonly int s_Jolt     = Shader.PropertyToID("_Jolt");
        private static readonly int s_GrainTex = Shader.PropertyToID("_GrainTex");
        private static readonly int s_NoiseTex = Shader.PropertyToID("_NoiseTex");
        private static readonly int s_Tint     = Shader.PropertyToID("_Tint");
		
		public override string ShaderName => string.Empty;
        
        [Tooltip("Vignette power")] [Range(0f, 3f)]
        public float   _vignette    = 0.33f;
        [Tooltip("Vignette intensity range")]
        public Vector2 _flickering = new Vector2(5.5f, 10f);
        [Tooltip("Frame Jolt range, interpolated from volume profile")]
        public Vector2 _jolt = new Vector2(-0.01f, 0.07f);
        
        [Tooltip("Default frame rate of noise and vignette deviations")]
        public float     _fps = 16;
        
        [Tooltip("Audio mixer channel for effect Audio")]
        public AudioMixerGroup _audioChannel;
        
        private Clip      _noiseClip;
        private Texture2D _noiseTex;
        
        private Texture2D _grainTex;
        
        private int     _frame;
        private float   _vig;
        private Vector4 _tiling;
        private Vector4 _joltValue;

        [HideInInspector]
        public Clip[]      _clips;
        [HideInInspector]
        public Texture2D[] _grains;
        
        public AduioContent[] _audio;
        private bool _clearAudio;
        
        // =======================================================================
        [Serializable]
        public class Clip
        {
            public Texture2D[] _data;
        }
        
        [Serializable]
        public class AduioContent
        {
            public AudioClip _audio;
        }

        // =======================================================================
        public enum Audio
        {
            None,
            Soft,
            Deep,
            Heavy
        }
        
        public enum Noise
        {
            None,
            Dust,
            Lint,
            Lines,
            Tape,
            Tidy
        }
        
        // copy unity parameters, we can't use embedded post processing because of application sequence
        public enum GrainTex
        {
            Large_A,
            Large_B,
            Medium_A,
            Medium_B,
            Medium_C,
            Medium_D,
            Medium_E,
            Medium_F,
            Thin_A,
            Thin_B,
        }

        public class AudioOutput : MonoBehaviour
        {
            public static AudioOutput Instance
            {
                get
                {
                    if (s_Instance == null)
                    {
                        s_Instance = FindObjectOfType<AudioOutput>();
                        if (s_Instance == null)
                        {
                            var go = new GameObject("OldMovie Audio");
                            go.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy | HideFlags.DontSave;
                            if (Application.isPlaying)
                                DontDestroyOnLoad(go);

                            s_Instance        = go.AddComponent<AudioOutput>();
                            
                            s_Instance._audio = go.AddComponent<AudioSource>();
                            s_Instance._audio.loop = true;
                            s_Instance._audio.clip = null;
                            s_Instance._audio.outputAudioMixerGroup = null;
                            s_Instance._audio.Play();
                        }
                    }
                    
                    return s_Instance;
                }
            }
            private static AudioOutput s_Instance;
            
            public AudioSource _audio;
        }
        
        // =======================================================================
        public override void Init()
        {
            _frame = 0;
        }

        public override bool Validate(Material mat)
        {
            var settings = Stack.GetComponent<OldMovieVol>();

            if (settings.IsActive() == false)
            {
                
                if (_clearAudio)
                {
                    var audio = AudioOutput.Instance;
                    audio._audio.clip                  = null;
                    audio._audio.outputAudioMixerGroup = null;
                    _clearAudio = false;
                }
                
                return false;
            }
            
            _grainTex  = _grains[(int)settings.m_GrainTex.value];
            _noiseClip = _clips[(int)settings.m_Noise.value];

            var fps = settings.m_Fps.overrideState ? settings.m_Fps.value : _fps;
            var curFrame = Mathf.FloorToInt(Time.unscaledTime / (1f / fps));
            if (_frame != curFrame)
            {
                _frame = curFrame;
                _vig = Mathf.LerpUnclamped(_flickering.x, _flickering.y, Random.value);
                _noiseTex = _noiseClip._data[Random.Range(0, _noiseClip._data.Length)];
				if (settings.m_Noise.value == 0)
					_noiseTex = Texture2D.blackTexture;
                _tiling = new Vector4(Screen.width / (float)_grainTex.width, Screen.height / (float)_grainTex.height, Random.value, Random.value);
                _joltValue = new Vector2(Mathf.LerpUnclamped(_jolt.x, _jolt.y, Random.value), Mathf.LerpUnclamped(_jolt.x, _jolt.y, Random.value)) *  settings.m_Jolt.value;
            }
            
            var grain = settings.m_Grain.value;

            mat.SetVector(s_Vignette, new Vector4(_vig, _vignette, grain, settings.m_NoiseAlpha.value));
            mat.SetVector(s_Grain, _tiling);
            mat.SetVector(s_Jolt, _joltValue);
            mat.SetColor(s_Tint, settings.m_Vignette.value);
            mat.SetTexture(s_GrainTex, _grainTex);
            mat.SetTexture(s_NoiseTex, _noiseTex);
            
            // audio works only in play mode os skip it in editor
            if (Application.isPlaying)
            {
                var audio = AudioOutput.Instance;
                audio._audio.outputAudioMixerGroup = _audioChannel;
                var audioContent = settings.m_Audio.value switch
                {
                    Audio.None  => null,
                    Audio.Soft  => _audio[0],
                    Audio.Deep  => _audio[1],
                    Audio.Heavy => _audio[2],
                    _           => throw new ArgumentOutOfRangeException()
                };

                if (audioContent != null)
                {
                    audio._audio.clip                  = audioContent._audio;
                    audio._audio.outputAudioMixerGroup = _audioChannel;
                    audio._audio.pitch                 = settings.m_Pich.value;
                    audio._audio.volume                = settings.m_Volume.value;
                    if (audio._audio.isPlaying == false)
                        audio._audio.Play();
                    
                    _clearAudio                        = true;
                }
                else
                {
                    if (_clearAudio)
                    {
                        audio._audio.clip = null;
                        audio._audio.outputAudioMixerGroup = null;
                        _clearAudio                        = false;
                    }
                }
            }
            
            return true;
        }

        protected override bool _editorValidate => _clips == null || _clips.Length == 0 || _grains == null || _grains.Length == 0 || _grains[0] == null || _clips[1]._data[0] == null || _audio == null || _audio.Length == 0;

        protected override void _editorSetup(string folder, string asset)
        {
#if UNITY_EDITOR
            var sep = Path.DirectorySeparatorChar;
            _grains = texFrom($"{folder}\\Grain");
            _clips = new List<Clip>()
                     .Append(new Clip(){_data = new Texture2D[]{Texture2D.blackTexture}})
                     .Append(new Clip(){_data = texFrom($"{folder}{sep}Noise{sep}A")})
                     .Append(new Clip(){_data = texFrom($"{folder}{sep}Noise{sep}B")})
                     .Append(new Clip(){_data = texFrom($"{folder}{sep}Noise{sep}C")})
                     .Append(new Clip(){_data = texFrom($"{folder}{sep}Noise{sep}D")})
                     .Append(new Clip(){_data = texFrom($"{folder}{sep}Noise{sep}E")})
                     .ToArray();
            
            _audio = audionFrom($"{folder}\\Audio")
                     .Select(n => new AduioContent(){ _audio = n })
                     .ToArray();
            

            // -----------------------------------------------------------------------
            Texture2D[] texFrom(string path)
            {
                return UnityEditor.AssetDatabase.FindAssets("t:texture", new string[] {path})
                                  .Select(n => UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(UnityEditor.AssetDatabase.GUIDToAssetPath(n)))
                                  .Where(n => n != null)
                                  .ToArray();
            }
            AudioClip[] audionFrom(string path)
            {
                return UnityEditor.AssetDatabase.FindAssets("t:audioclip", new string[] {path})
                                  .Select(n => UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(UnityEditor.AssetDatabase.GUIDToAssetPath(n)))
                                  .Where(n => n != null)
                                  .ToArray();
            }
#endif
        }
    }
}