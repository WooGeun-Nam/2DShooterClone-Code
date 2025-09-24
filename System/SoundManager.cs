using UnityEngine;
using UnityEngine.Audio;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    public AudioMixer masterMixer;

    private AudioSource bgmPlayer;
    private List<AudioSource> sfxSources = new List<AudioSource>();
    private Dictionary<GameObject, AudioSource> loopingSfxSources = new Dictionary<GameObject, AudioSource>();
    [Tooltip("동시에 재생할 수 있는 최대 효과음 개수")]
    [SerializeField] private int maxSfxSources = 20; // 인스펙터에서 조절 가능
    
    // 사운드 쿨다운(재생 조절)을 위한 변수들
    [Tooltip("동일한 사운드를 다시 재생하기까지 필요한 최소 시간 간격 (초)")]
    [SerializeField] private float sfxThrottleTime = 0.05f;
    private Dictionary<string, float> _sfxCooldowns = new Dictionary<string, float>();
    
    // 오디오 클립 및 분석 데이터를 관리할 Dictionary
    private Dictionary<string, AudioClip[]> audioClips;
    private Dictionary<string, float> clipPeakVolumes = new Dictionary<string, float>(); // 오디오 클립의 피크 볼륨을 캐싱

    private const float TARGET_PEAK_VOLUME = 0.7f; // 볼륨 정규화를 위한 목표 피크 값 (오디오 클리핑 방지를 위해 1.0f 보다 낮게 설정)

    // 추가된 상수 정의
    private const string GAME_BGM_PREFIX = "BGM_Game_"; // 인게임 BGM 파일을 식별하기 위한 접두사
    
    // 초기화 상태를 외부에 알릴 수 있는 변수
    public bool IsInitialized { get; private set; } = false;
    
    public float InitializationProgress { get; private set; } = 0f; // 초기화 진행률
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            bgmPlayer = gameObject.AddComponent<AudioSource>();
            bgmPlayer.outputAudioMixerGroup = masterMixer.FindMatchingGroups("BGM")[0];
            
            for (int i = 0; i < maxSfxSources; i++)
            {
                AudioSource sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.outputAudioMixerGroup = masterMixer.FindMatchingGroups("SFX")[0];
                sfxSource.loop = false;
                sfxSources.Add(sfxSource);
            }
            
            IsInitialized = false;
            StartCoroutine(LoadAndAnalyzeAudioClipsAsync());
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 볼륨 정규화 기능이 적용된 재생 함수

    // 동일한 BGM이 이미 재생 중일 경우 다시 시작하지 않도록 수정
    public void PlayBGM(string clipName, bool loop = true, float volume = 1.0f, float pitch = 1.0f)
    {
        AudioClip clip = FindClip(clipName);
        if (clip == null) return;

        // 현재 재생 중인 클립이 요청된 클립과 동일하면 아무것도 하지 않고 리턴
        if (bgmPlayer.isPlaying && bgmPlayer.clip != null && bgmPlayer.clip.name == clip.name)
        {
            return;
        }

        bgmPlayer.clip = clip;
        bgmPlayer.loop = loop;
        bgmPlayer.volume = GetAdjustedVolume(clipName, volume);
        bgmPlayer.pitch = pitch;
        bgmPlayer.Play();
    }
    
    public void PlaySFX(string clipName, float volume = 1.0f, float pitch = 1.0f)
    {
        AudioClip clip = FindClip(clipName);
        if (clip == null) return;
        
        // 이전에 이 사운드를 재생한 기록이 있는지 확인합니다.
        if (_sfxCooldowns.ContainsKey(clipName))
        {
            // 마지막 재생 시간과 현재 시간을 비교하여, 설정된 쿨다운 시간(sfxThrottleTime)이 지나지 않았다면
            // 소리를 재생하지 않고 즉시 함수를 종료합니다.
            if (Time.unscaledTime - _sfxCooldowns[clipName] < sfxThrottleTime)
            {
                return;
            }
        }

        // 오디오 소스 풀에서 비어있는 소스를 찾아옵니다.
        AudioSource sourceToPlay = GetAvailableSfxSource();
        if (sourceToPlay == null) return;
        
        // 재생 직전에, 현재 시간을 마지막 재생 시간으로 기록(갱신)합니다.
        _sfxCooldowns[clipName] = Time.unscaledTime;

        // 찾은 소스를 사용하여 효과음을 재생합니다.
        sourceToPlay.pitch = pitch;
        sourceToPlay.PlayOneShot(clip, GetAdjustedVolume(clipName, volume));
    }
    
    // 동기 함수를 비동기 코루틴으로 변경
    private IEnumerator LoadAndAnalyzeAudioClipsAsync()
    {
        audioClips = new Dictionary<string, AudioClip[]>();
        clipPeakVolumes = new Dictionary<string, float>();

        // 지정된 모든 경로의 오디오 클립을 비동기로 로드하고 분석
        yield return StartCoroutine(LoadClipsFromPathAsync("BGM"));
        yield return StartCoroutine(LoadClipsFromPathAsync("SFX"));
        IsInitialized = true; // 모든 로딩이 완료되었음을 알립니다.
    }

    // 경로에서 오디오 클립을 비동기로 로드하는 코루틴
    private IEnumerator LoadClipsFromPathAsync(string path)
    {
        // 1. 먼저 경로에 있는 모든 클립의 참조를 동기적으로 가져옵니다.
        AudioClip[] clips = Resources.LoadAll<AudioClip>("Sound/" + path);

        if (clips.Length > 0)
        {
            // 2. 클립 배열을 저장
            audioClips.Add(path, clips);
        
            // 3. 각 클립에 대해 비동기 로딩 및 분석을 진행합니다.
            foreach (AudioClip clip in clips)
            {
                if (!clipPeakVolumes.ContainsKey(clip.name))
                {
                    string fullPath = "Sound/" + path + "/" + clip.name;

                    ResourceRequest request = Resources.LoadAsync<AudioClip>(fullPath);
                
                    // 로딩이 완료될 때까지 기다립니다.
                    while (!request.isDone)
                    {
                        yield return null;
                    }
                    // 로딩이 완료된 클립을 분석합니다.
                    float peakVolume = AnalyzeClipPeakVolume((AudioClip)request.asset);
                    clipPeakVolumes.Add(clip.name, peakVolume);
                }
            }
        }
    }

    private float AnalyzeClipPeakVolume(AudioClip clip)
    {
        // 클립의 모든 오디오 샘플 데이터를 가져옴
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        float peakVolume = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            // 샘플의 절대값이 현재 피크 값보다 크면 업데이트
            float currentSample = Mathf.Abs(samples[i]);
            if (currentSample > peakVolume)
            {
                peakVolume = currentSample;
            }
        }
        return peakVolume;
    }

    private float GetAdjustedVolume(string clipName, float originalVolume)
    {
        if (clipPeakVolumes.TryGetValue(clipName, out float peakVolume))
        {
            // 피크 볼륨이 매우 작으면(거의 무음이면) 과도하게 증폭되는 것을 방지
            if (peakVolume < 0.01f)
            {
                return originalVolume;
            }
            // 목표 피크 볼륨을 기준으로 정규화된 볼륨 계산
            return originalVolume * (TARGET_PEAK_VOLUME / peakVolume);
        }
        return originalVolume; // 분석 데이터가 없는 경우 원래 볼륨 사용
    }
    
    public void StopAllSounds()
    {
        bgmPlayer.Stop();
        
        // sfxPlayer.Stop(); // 기존 코드 삭제
        foreach (var source in sfxSources)
        {
            source.Stop();
        }
    }

    public AudioClip FindClip(string clipName)
    {
        foreach (var key in audioClips.Keys)
        {
            foreach (var clip in audioClips[key])
            {
                if (clip.name.Equals(clipName))
                {
                    return clip;
                }
            }
        }
        Debug.LogWarning($"AudioClip not found: {clipName}"); // 클립을 찾지 못했을 때 경고 메시지 추가
        return null;
    }
    
    private AudioSource GetAvailableSfxSource()
    {
        // sfxSources 리스트를 처음부터 순회합니다.
        foreach (var source in sfxSources)
        {
            // 현재 재생 중이 아닌 소스를 찾으면, 그 소스를 즉시 반환합니다.
            if (!source.isPlaying)
            {
                return source;
            }
        }
        
        // 모든 소스가 사용 중이라면(위의 반복문에서 반환되지 않았다면), null을 반환합니다.
        return null;
    }
}