using UnityEngine;

/// <summary>
/// 音频管理器（单例）
/// 负责背景音乐（BGM）和音效（SFX）的播放、暂停、停止及音量控制
/// </summary>
public class AudioManager : MonoBehaviour
{
    #region 单例实现

    private static AudioManager _instance;
    /// <summary>
    /// 获取音频管理器实例（懒加载）
    /// 若实例不存在，则自动创建一个带有 AudioManager 组件的 GameObject
    /// </summary>
    public static AudioManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("AudioManager");
                _instance = go.AddComponent<AudioManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    #endregion

    #region 音频源组件

    private AudioSource bgmSource;   // 用于播放背景音乐的 AudioSource
    private AudioSource sfxSource;   // 用于播放音效的 AudioSource

    #endregion
    public AudioClip StartBGM;

    #region 音量参数

    [Range(0f, 1f)]
    public float bgmVolume = 0.8f;   // BGM 音量（0~1）

    [Range(0f, 1f)]
    public float sfxVolume = 1f;     // SFX 音量（0~1）

    #endregion

    #region Unity 生命周期

    private void Awake()
    {
        // 单例检查：如果已存在其他实例，销毁当前对象
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 创建并配置 BGM 音频源
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;          // 背景音乐循环播放
        bgmSource.playOnAwake = false;  // 不自动播放
        bgmSource.volume = bgmVolume;

        // 创建并配置 SFX 音频源
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;         // 音效不循环
        sfxSource.playOnAwake = false;  // 不自动播放
        sfxSource.volume = sfxVolume;
        AudioManager.Instance.PlayBGM(StartBGM);
    }

    private void OnDestroy()
    {
        // 清除静态引用，避免内存泄漏
        if (_instance == this)
            _instance = null;
    }

    #endregion

    #region BGM 控制方法

    /// <summary>
    /// 播放背景音乐
    /// </summary>
    /// <param name="clip">要播放的音频剪辑</param>
    /// <param name="fadeTime">淡入时间（秒），目前尚未实现，保留扩展</param>
    public void PlayBGM(AudioClip clip, float fadeTime = 0f)
    {
        if (clip == null) return;

        // 如果正在播放相同的剪辑，则不做任何操作
        if (bgmSource.clip == clip && bgmSource.isPlaying)
            return;

        // TODO: 实现淡入淡出效果（fadeTime > 0 时）
        if (fadeTime > 0f)
        {
            // 此处可添加协程实现音量渐变
        }

        bgmSource.clip = clip;
        bgmSource.Play();
    }

    /// <summary>
    /// 停止播放背景音乐
    /// </summary>
    public void StopBGM()
    {
        bgmSource.Stop();
    }

    /// <summary>
    /// 暂停播放背景音乐（保留当前位置）
    /// </summary>
    public void PauseBGM()
    {
        bgmSource.Pause();
    }

    /// <summary>
    /// 恢复播放已暂停的背景音乐
    /// </summary>
    public void ResumeBGM()
    {
        bgmSource.UnPause();
    }

    /// <summary>
    /// 设置背景音乐音量
    /// </summary>
    /// <param name="volume">音量值（0~1）</param>
    public void SetBGMVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        bgmSource.volume = bgmVolume;
    }

    #endregion

    #region SFX 控制方法

    /// <summary>
    /// 播放一次音效（通过 sfxSource 播放，不受 3D 空间影响）
    /// </summary>
    /// <param name="clip">音效剪辑</param>
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    /// <summary>
    /// 在指定世界位置播放一次音效（3D 空间音效）
    /// </summary>
    /// <param name="clip">音效剪辑</param>
    /// <param name="position">播放位置</param>
    public void PlaySFXAtPoint(AudioClip clip, Vector3 position)
    {
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, position, sfxVolume);
    }

    /// <summary>
    /// 设置音效全局音量
    /// </summary>
    /// <param name="volume">音量值（0~1）</param>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        // 注意：PlayOneShot 和 PlayClipAtPoint 使用独立的音量参数，
        // 此处无需更新 sfxSource.volume，因为 PlayOneShot 会传入 sfxVolume。
        // 若需调整实时播放中的音效音量，可扩展为直接修改 sfxSource.volume。
    }

    /// <summary>
    /// 停止所有音效播放（停止 sfxSource 上的当前播放）
    /// </summary>
    public void StopSFX()
    {
        sfxSource.Stop();
    }

    #endregion
}