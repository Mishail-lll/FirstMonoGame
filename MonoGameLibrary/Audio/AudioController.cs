using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MonoGameLibrary.Audio;

public class AudioController : IDisposable
{
    // Tracks sound effect instances created so they can be paused, unpaused, and/or disposed.
    private readonly List<SoundEffectInstance> _activeSoundEffectInstances;

    // Tracks the volume for song playback when muting and unmuting.
    private float _previousSongVolume;

    // Tracks the volume for sound effect playback when muting and unmuting.
    private float _previousSoundEffectVolume;

    /// <summary>
    /// Gets a value that indicates if audio is muted.
    /// </summary>
    public bool IsMuted { get; private set; }

    private Dictionary<string, SoundEffect> _sounds;

    /// <summary>
    /// Gets or Sets the global volume of songs.
    /// </summary>
    /// <remarks>
    /// If IsMuted is true, the getter will always return back 0.0f and the
    /// setter will ignore setting the volume.
    /// </remarks>
    public float SongVolume
    {
        get
        {
            if (IsMuted)
            {
                return 0.0f;
            }

            return MediaPlayer.Volume;
        }
        set
        {
            if (IsMuted)
            {
                return;
            }

            MediaPlayer.Volume = Math.Clamp(value, 0.0f, 1.0f);
        }
    }

    /// <summary>
    /// Gets or Sets the global volume of sound effects.
    /// </summary>
    /// <remarks>
    /// If IsMuted is true, the getter will always return back 0.0f and the
    /// setter will ignore setting the volume.
    /// </remarks>
    public float SoundEffectVolume
    {
        get
        {
            if (IsMuted)
            {
                return 0.0f;
            }

            return SoundEffect.MasterVolume;
        }
        set
        {
            if (IsMuted)
            {
                return;
            }

            SoundEffect.MasterVolume = Math.Clamp(value, 0.0f, 1.0f);
        }
    }

    /// <summary>
    /// Gets a value that indicates if this audio controller has been disposed.
    /// </summary>
    public bool IsDisposed { get; private set; }

    /// <summary>
    /// Creates a new audio controller instance.
    /// </summary>
    public AudioController()
    {
        _activeSoundEffectInstances = new List<SoundEffectInstance>();
        _sounds = new Dictionary<string, SoundEffect>();
    }

    // Finalizer called when object is collected by the garbage collector.
    ~AudioController() => Dispose(false);

    /// <summary>
    /// Updates this audio controller.
    /// </summary>
    public void Update()
    {
        for (int i = _activeSoundEffectInstances.Count - 1; i >= 0; i--)
        {
            SoundEffectInstance instance = _activeSoundEffectInstances[i];

            if (instance.State == SoundState.Stopped)
            {
                if (!instance.IsDisposed)
                {
                    instance.Dispose();
                }
                _activeSoundEffectInstances.RemoveAt(i);
            }
        }
    }

    public void Load(string key, string val)
    {
        // загрузи нужные звуки сюда или делай lazy load в Play
        _sounds = new Dictionary<string, SoundEffect>(StringComparer.OrdinalIgnoreCase);
        // пример:
        _sounds[key] = Core.Content.Load<SoundEffect>(val);
    }

    private SoundEffect GetOrReload(string key, SoundEffect known)
    {
        // если уже есть объект и он, видимо, валиден — вернуть
        if (known != null) return known;

        if (Core.Content == null) return null;
        try
        {
            var s = Core.Content.Load<SoundEffect>(key);
            _sounds[key] = s;
            return s;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioController: reload failed for {key}: {ex}");
            return null;
        }
    }
    public void PlaySoundEffectByKey(string key, float volume = 1f, float pitch = 0f, float pan = 0f, bool loop = false)
    {
        if (string.IsNullOrEmpty(key)) return;
        SoundEffect sfx = null;
        _sounds?.TryGetValue(key, out sfx);

        // Попытка 1: если есть sfx — создать инстанс и play
        if (sfx != null)
        {
            TryPlayInstance(sfx, volume, pitch, pan, loop, key);
            return;
        }

        // Попытка 2: lazy reload
        //sfx = GetOrReload(key, null);
        //if (sfx != null)
        //{
        //    TryPlayInstance(sfx, volume, pitch, pan, loop, key);
        //    return;
        //}

        // Попытка 3: ничего не получилось — лог
        Debug.WriteLine($"AudioController: PlaySoundEffectByKey failed - no resource for key '{key}'");
    }

    // TryPlayInstance: единственный метод, оборачивающий ошибки
    private void TryPlayInstance(SoundEffect sfx, float volume, float pitch, float pan, bool loop, string keyForReload = null)
    {
        if (sfx == null) return;

        SoundEffectInstance inst = null;
        try
        {
            inst = sfx.CreateInstance();
            inst.Volume = volume;
            inst.Pitch = pitch;
            inst.Pan = pan;
            inst.IsLooped = loop;
            inst.Play();
            Debug.WriteLine($"volume - {inst.Volume}");
            if (loop)
                _activeSoundEffectInstances.Add(inst); // only store looped ones
            else
                // For non-looped you may dispose later or rely on platform to finish
            return;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioController: instance.Play() failed for key='{keyForReload}' ex={ex.GetType().Name}:{ex.Message}");
            //try { inst?.Dispose(); } catch { }

            //// Если у нас есть ключ — попробуем перезагрузить asset и попытаться ещё раз
            //if (!string.IsNullOrEmpty(keyForReload) && Core.Content != null)
            //{
            //    try
            //    {
            //        var reloaded = Core.Content.Load<SoundEffect>(keyForReload);
            //        _sounds[keyForReload] = reloaded;
            //        var retry = reloaded.CreateInstance();
            //        retry.Volume = volume; retry.Pitch = pitch; retry.Pan = pan; retry.IsLooped = loop;
            //        retry.Play();
            //        Debug.WriteLine($"volume - {retry.Volume}");
            //        if (loop) _activeSoundEffectInstances.Add(retry);
            //        return;
            //    }
            //    catch (Exception rex)
            //    {
            //        Debug.WriteLine($"AudioController: retry after reload failed: {rex}");
            //    }
            //}
        }
    }

    /// <summary>
    /// Plays the given sound effect.
    /// </summary>
    /// <param name="soundEffect">The sound effect to play.</param>
    /// <returns>The sound effect instance created by this method.</returns>
    public SoundEffectInstance PlaySoundEffect(SoundEffect soundEffect)
    {
        return PlaySoundEffect(soundEffect, 1.0f, 0.0f, 0.0f, false);
    }

    /// <summary>
    /// Plays the given sound effect with the specified properties.
    /// </summary>
    /// <param name="soundEffect">The sound effect to play.</param>
    /// <param name="volume">The volume, ranging from 0.0 (silence) to 1.0 (full volume).</param>
    /// <param name="pitch">The pitch adjustment, ranging from -1.0 (down an octave) to 0.0 (no change) to 1.0 (up an octave).</param>
    /// <param name="pan">The panning, ranging from -1.0 (left speaker) to 0.0 (centered), 1.0 (right speaker).</param>
    /// <param name="isLooped">Whether the the sound effect should loop after playback.</param>
    /// <returns>The sound effect instance created by playing the sound effect.</returns>
    /// <returns>The sound effect instance created by this method.</returns>
    public SoundEffectInstance PlaySoundEffect(SoundEffect soundEffect, float volume, float pitch, float pan, bool isLooped)
    {
        // Create an instance from the sound effect given.
        SoundEffectInstance soundEffectInstance = soundEffect.CreateInstance();

        // Apply the volume, pitch, pan, and loop values specified.
        soundEffectInstance.Volume = volume;
        soundEffectInstance.Pitch = pitch;
        soundEffectInstance.Pan = pan;
        soundEffectInstance.IsLooped = isLooped;

        // Tell the instance to play
        soundEffectInstance.Play();

        // Add it to the active instances for tracking
        _activeSoundEffectInstances.Add(soundEffectInstance);

        return soundEffectInstance;
    }

    /// <summary>
    /// Plays the given song.
    /// </summary>
    /// <param name="song">The song to play.</param>
    /// <param name="isRepeating">Optionally specify if the song should repeat.  Default is true.</param>
    public void PlaySong(Song song, bool isRepeating = true)
    {
        // Check if the media player is already playing, if so, stop it.
        // If we do not stop it, this could cause issues on some platforms
        if (MediaPlayer.State == MediaState.Playing)
        {
            MediaPlayer.Stop();
        }

        MediaPlayer.Play(song);
        MediaPlayer.IsRepeating = isRepeating;
    }

    /// <summary>
    /// Pauses all audio.
    /// </summary>
    public void PauseAudio()
    {
        // Pause any active songs playing.
        MediaPlayer.Pause();

        // Pause any active sound effects.
        foreach (SoundEffectInstance soundEffectInstance in _activeSoundEffectInstances)
        {
            soundEffectInstance.Pause();
        }
    }

    /// <summary>
    /// Resumes play of all previous paused audio.
    /// </summary>
    public void ResumeAudio()
    {
        // Resume paused music
        MediaPlayer.Resume();

        // Resume any active sound effects.
        foreach (SoundEffectInstance soundEffectInstance in _activeSoundEffectInstances)
        {
            soundEffectInstance.Resume();
        }
    }

    /// <summary>
    /// Mutes all audio.
    /// </summary>
    public void MuteAudio()
    {
        // Store the volume so they can be restored during ResumeAudio
        _previousSongVolume = MediaPlayer.Volume;
        _previousSoundEffectVolume = SoundEffect.MasterVolume;

        // Set all volumes to 0
        MediaPlayer.Volume = 0.0f;
        SoundEffect.MasterVolume = 0.0f;

        IsMuted = true;
    }

    /// <summary>
    /// Unmutes all audio to the volume level prior to muting.
    /// </summary>
    public void UnmuteAudio()
    {
        // Restore the previous volume values.
        MediaPlayer.Volume = _previousSongVolume;
        SoundEffect.MasterVolume = _previousSoundEffectVolume;

        IsMuted = false;
    }

    /// <summary>
    /// Toggles the current audio mute state.
    /// </summary>
    public void ToggleMute()
    {
        if (IsMuted)
        {
            UnmuteAudio();
        }
        else
        {
            MuteAudio();
        }
    }

    /// <summary>
    /// Disposes of this audio controller and cleans up resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes this audio controller and cleans up resources.
    /// </summary>
    /// <param name="disposing">Indicates whether managed resources should be disposed.</param>
    protected void Dispose(bool disposing)
    {
        if (IsDisposed)
        {
            return;
        }

        if (disposing)
        {
            foreach (SoundEffectInstance soundEffectInstance in _activeSoundEffectInstances)
            {
                soundEffectInstance.Dispose();
            }
            _activeSoundEffectInstances.Clear();
        }

        IsDisposed = true;
    }

}
