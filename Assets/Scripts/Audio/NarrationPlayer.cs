using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>웹에서 제작한 로컬 음성을 한 번에 하나씩 재생한다. 온라인 TTS 호출은 없다.</summary>
public sealed class NarrationPlayer : MonoBehaviour
{
    public const string VolumeKey = "audio_narration_volume";
    private static NarrationPlayer _instance;
    private AudioSource _source;
    private UnityEngine.Object _owner;
    private string _currentId;
    private Coroutine _sequence;

    public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
    public static bool IsPlaying => _instance != null && _instance._source != null && _instance._source.isPlaying;
    public static string CurrentId => IsPlaying ? _instance._currentId : null;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Ensure()
    {
        if (_instance == null)
            new GameObject("NarrationPlayer").AddComponent<NarrationPlayer>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        _source = gameObject.AddComponent<AudioSource>();
        _source.playOnAwake = false;
        _source.loop = false;
        _source.spatialBlend = 0f;
        _source.volume = Volume;
        LocalizationManager.OnLanguageChanged += StopAll;
        SceneManager.activeSceneChanged += OnSceneChanged;
    }

    private void OnDestroy()
    {
        if (_instance != this) return;
        LocalizationManager.OnLanguageChanged -= StopAll;
        SceneManager.activeSceneChanged -= OnSceneChanged;
        _instance = null;
    }

    private void Update()
    {
        // Destroyed or disabled UI must not leave its narration behind.
        if (_currentId == null) return;
        if (_owner == null || (_owner is Behaviour behaviour && !behaviour.isActiveAndEnabled)
            || (_owner is GameObject go && !go.activeInHierarchy))
            StopAll();
    }

    private static void OnSceneChanged(Scene previous, Scene next) => StopAll();

    public static void SetVolume(float value)
    {
        PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
        if (_instance != null) _instance._source.volume = Volume;
    }

    public static bool Play(string id, UnityEngine.Object owner, bool restart = false)
    {
        Ensure();
        if (!restart && _instance._owner == owner && _instance._currentId == id && IsPlaying)
            return true;
        StopAll();
        if (owner == null) return false;
        var clip = NarrationCatalog.GetClip(id, LocalizationManager.Current);
        if (clip == null) return false; // Missing/unproduced language stays silent.
        _instance._owner = owner;
        _instance._currentId = id;
        _instance._source.clip = clip;
        _instance._source.volume = Volume;
        _instance._source.Play();
        return true;
    }

    public static bool PlayText(string koreanSource, UnityEngine.Object owner, bool restart = false)
        => Play(NarrationCatalog.FindId(koreanSource), owner, restart);

    public static bool PlaySequence(UnityEngine.Object owner, params string[] ids)
    {
        Ensure();
        StopAll();
        if (owner == null || ids == null) return false;
        var availableIds = new List<string>();
        var clips = new List<AudioClip>();
        foreach (string id in ids)
        {
            var clip = NarrationCatalog.GetClip(id, LocalizationManager.Current);
            if (clip == null) continue;
            availableIds.Add(id);
            clips.Add(clip);
        }
        if (clips.Count == 0) return false;
        _instance._sequence = _instance.StartCoroutine(_instance.ReadSequence(owner, availableIds, clips));
        return true;
    }

    private IEnumerator ReadSequence(UnityEngine.Object owner, List<string> ids, List<AudioClip> clips)
    {
        _owner = owner;
        for (int i = 0; i < clips.Count; i++)
        {
            _currentId = ids[i];
            _source.clip = clips[i];
            _source.volume = Volume;
            _source.Play();
            yield return new WaitWhile(() => _source.isPlaying);
        }
        _sequence = null;
        _source.clip = null;
        _currentId = null;
        _owner = null;
    }

    public static void Stop(UnityEngine.Object owner)
    {
        if (_instance != null && _instance._owner == owner) StopAll();
    }

    public static void StopAll()
    {
        if (_instance == null) return;
        if (_instance._sequence != null)
        {
            _instance.StopCoroutine(_instance._sequence);
            _instance._sequence = null;
        }
        _instance._source.Stop();
        _instance._source.clip = null;
        _instance._owner = null;
        _instance._currentId = null;
    }
}

/// <summary>화면의 원문과 음성 ID를 연결한다. 수정한 낭독 대본은 제작 이력에서 따로 관리한다.</summary>
public static class NarrationCatalog
{
    [Serializable] private sealed class Document { public Entry[] entries; }
    [Serializable] private sealed class Entry
    {
        public string id, sourceKo, koResource, enResource;
        public string[] aliases;
    }

    private static Dictionary<string, Entry> _byId;
    private static Dictionary<string, string> _byText;

    private static void Load()
    {
        if (_byId != null) return;
        _byId = new Dictionary<string, Entry>(StringComparer.Ordinal);
        _byText = new Dictionary<string, string>(StringComparer.Ordinal);
        var asset = Resources.Load<TextAsset>("Audio/Narration/Catalog");
        if (asset == null) return;
        var document = JsonUtility.FromJson<Document>(asset.text);
        if (document?.entries == null) return;
        foreach (var entry in document.entries)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.id)) continue;
            _byId[entry.id] = entry;
            if (!string.IsNullOrEmpty(entry.sourceKo)) _byText[entry.sourceKo] = entry.id;
            if (entry.aliases == null) continue;
            foreach (var alias in entry.aliases)
                if (!string.IsNullOrEmpty(alias)) _byId[alias] = entry;
        }
    }

    public static string FindId(string koreanSource)
    {
        Load();
        return koreanSource != null && _byText.TryGetValue(koreanSource, out var id) ? id : null;
    }

    public static AudioClip GetClip(string id, Language language)
    {
        Load();
        if (id == null || !_byId.TryGetValue(id, out var entry)) return null;
        string path = language == Language.Korean ? entry.koResource : entry.enResource;
        return string.IsNullOrEmpty(path) ? null : Resources.Load<AudioClip>(path);
    }
}
