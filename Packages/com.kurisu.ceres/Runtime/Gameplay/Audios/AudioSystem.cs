using Cysharp.Threading.Tasks;
using UnityEngine;
using R3;
using R3.Ceres;
using System;
using UnityEngine.Assertions;
using System.Collections.Generic;
using System.Linq;
using Ceres.Collections;
using Ceres.Pool;
using Ceres.Resource;
using Ceres.Schedulers;
using UnityEngine.Scripting;

namespace Ceres.Gameplay.Audios
{
    /// <summary>
    /// A versioned-index handle that identifies a pooled audio source without holding a managed reference.
    /// The handle becomes invalid automatically once the underlying source is disposed or returned to pool.
    /// </summary>
    public readonly struct AudioSourceHandle : IDisposable
    {
        /// <summary>
        /// Packed value: bits 0-23 = slot index, bits 24-63 = per-slot serial number.
        /// </summary>
        public readonly ulong Handle;

        public const int IndexBits = 24;
                
        public const int SerialNumberBits = 40;

        public const int MaxIndex = 1 << IndexBits;
        
        public const ulong MaxSerialNumber = (ulong)1 << SerialNumberBits;

        public AudioSourceHandle(ulong serial, int index)
        {
            Assert.IsTrue(index >= 0 && index < MaxIndex);
            Assert.IsTrue(serial < MaxSerialNumber);
#pragma warning disable CS0675
            Handle = (serial << IndexBits) | (ulong)index;
#pragma warning restore CS0675
        }

        public int GetIndex() => (int)(Handle & MaxIndex - 1);

        public ulong GetSerialNumber() => Handle >> IndexBits;

        /// <summary>
        /// Returns true if the handle is non-default and the underlying audio source is still alive.
        /// </summary>
        public bool IsValid() => Handle != 0 && AudioHandleRegistry.IsAlive(this);

        /// <summary>
        /// Returns true if the underlying audio source is currently playing.
        /// </summary>
        public bool IsPlaying() => AudioHandleRegistry.Resolve(this)?.Component.isPlaying ?? false;

        /// <summary>
        /// Stops and releases the underlying audio source back to the pool.
        /// Safe to call multiple times or on an already-invalid handle — always a no-op in that case.
        /// </summary>
        public void Stop() => AudioHandleRegistry.Resolve(this)?.Dispose();

        /// <summary>
        /// Returns the underlying audio source
        /// </summary>
        public AudioSource AudioSource => AudioHandleRegistry.Resolve(this)?.Component;
        
        /// <summary>
        /// Returns the underlying pooled audio source
        /// </summary>
        public PooledAudioSource PooledAudioSource => AudioHandleRegistry.Resolve(this);

        public void Dispose() => Stop();

        public static bool operator ==(AudioSourceHandle left, AudioSourceHandle right) => left.Handle == right.Handle;

        public static bool operator !=(AudioSourceHandle left, AudioSourceHandle right) => left.Handle != right.Handle;

        public override bool Equals(object obj) => obj is AudioSourceHandle h && h.Handle == Handle;

        public override int GetHashCode() => Handle.GetHashCode();

        public static implicit operator PooledAudioSource(AudioSourceHandle handle)
        {
            return AudioHandleRegistry.Resolve(handle);
        }
    }
    
    internal static class AudioHandleRegistry
    {
        private const int InitialLength = 16;

        private const int MaxCapacity = 1024;

        private static readonly SparseArray<PooledAudioSource> Slots = new(InitialLength, MaxCapacity);

        // Global serial, incremented on every Alloc. Serial 0 is never issued, so
        // default(AudioSourceHandle) is always invalid.
        private static ulong _serialNum = 1;

        /// <summary>
        /// Allocates a registry slot for <paramref name="source"/>, stores the resulting handle back
        /// on the source, and returns the handle to the caller.
        /// </summary>
        internal static AudioSourceHandle Alloc(PooledAudioSource source)
        {
            var index = Slots.Add(source);
            var handle = new AudioSourceHandle(_serialNum++, index);
            source.AudioHandle = handle;
            return handle;
        }

        /// <summary>
        /// Frees the slot identified by <paramref name="handle"/>. No-op if the handle is stale.
        /// Reads the serial from the source stored in the slot, mirroring SchedulerRunner.FindItem.
        /// </summary>
        internal static void Free(AudioSourceHandle handle)
        {
            if (handle.Handle == 0) return;
            var index = handle.GetIndex();
            if (!Slots.IsAllocated(index)) return;
            if (Slots[index].AudioHandle.GetSerialNumber() != handle.GetSerialNumber()) return;
            Slots.RemoveAt(index);
        }

        /// <summary>
        /// Returns the live source for <paramref name="handle"/>, or null if the handle is stale.
        /// </summary>
        internal static PooledAudioSource Resolve(AudioSourceHandle handle)
        {
            if (handle.Handle == 0) return null;
            var index = handle.GetIndex();
            if (!Slots.IsAllocated(index)) return null;
            var source = Slots[index];
            if (source.AudioHandle.GetSerialNumber() != handle.GetSerialNumber()) return null;
            return source;
        }

        internal static bool IsAlive(AudioSourceHandle handle) => Resolve(handle) != null;

        /// <summary>
        /// Clears all slots. Called during system shutdown to handle any sources not registered
        /// in AudioCache (e.g. PlayDuration sources).
        /// </summary>
        internal static void Clear()
        {
            Slots.Clear();
        }
    }

    [Preserve]
    public static class AudioSystem
    {
        private static Transform _hookRoot;
        
        private static Transform GetRoot()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) return null;
#endif
            if (_hookRoot) return _hookRoot;
            _hookRoot = GameObjectPoolManager.Instance.transform;
            Disposable.Create(ReleaseAll).AddTo(_hookRoot);
            return _hookRoot;
        }
        
        internal readonly struct AudioKey: IEquatable<AudioKey>
        {
            private readonly string _address;

            private readonly int _instanceId;

            public AudioKey(int instanceId)
            {
                _instanceId = instanceId;
                _address = null;
            }
            
            public AudioKey(string address)
            {
                _instanceId = 0;
                _address = address;
            }

            public bool Equals(AudioKey other)
            {
                return other._instanceId == _instanceId && other._address == _address;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_address, _instanceId);
            }
            
            public class Comparer : IEqualityComparer<AudioKey>
            {
                public bool Equals(AudioKey x, AudioKey y)
                {
                    return x.Equals(y);
                }

                public int GetHashCode(AudioKey key)
                {
                    return key.GetHashCode();
                }
            }
        }
        
        private static readonly Dictionary<AudioKey, PooledAudioSource> AudioCache = new(new AudioKey.Comparer());
        
        internal static void Register(AudioKey key, PooledAudioSource audioSource)
        {
            Assert.IsNotNull(audioSource);
            if (AudioCache.TryGetValue(key, out var latestHandle)) latestHandle?.Dispose();
            AudioCache[key] = audioSource;
        }
        
        internal static void Unregister(AudioKey key, PooledAudioSource audioSource)
        {
            Assert.IsNotNull(audioSource);
            if (!AudioCache.TryGetValue(key, out var latestHandle)) return;
            if (Equals(latestHandle, audioSource))
            {
                AudioCache.Remove(key);
            }
        }
        
        /// <summary>
        /// Stop an addressable audio source
        /// </summary>
        /// <param name="address"></param>
        public static void StopAudio(string address)
        {
            var key = new AudioKey(address);
            if (!AudioCache.TryGetValue(key, out var latestHandle)) return;
            latestHandle?.Dispose();
            AudioCache.Remove(key);
        }
        
        /// <summary>
        /// Stop an addressable audio source from audio <see cref="AudioClip"/>
        /// </summary>
        /// <param name="audioClip"></param>
        public static void StopAudio(AudioClip audioClip)
        {
            var key = new AudioKey(audioClip.GetInstanceID());
            if (!AudioCache.TryGetValue(key, out var latestHandle)) return;
            latestHandle?.Dispose();
            AudioCache.Remove(key);
        }
        
        /// <summary>
        /// Find a looping or scheduled audio source by address and return its handle.
        /// Returns a default (invalid) handle if no such source is active.
        /// </summary>
        /// <param name="address"></param>
        public static AudioSourceHandle FindAudio(string address)
        {
            var key = new AudioKey(address);
            var source = AudioCache.GetValueOrDefault(key);
            return source?.AudioHandle ?? default;
        }
        
        /// <summary>
        /// Find a looping or scheduled audio source by <see cref="AudioClip"/> and return its handle.
        /// Returns a default (invalid) handle if no such source is active.
        /// </summary>
        /// <param name="audioClip"></param>
        public static AudioSourceHandle FindAudio(AudioClip audioClip)
        {
            var key = new AudioKey(audioClip.GetInstanceID());
            var source = AudioCache.GetValueOrDefault(key);
            return source?.AudioHandle ?? default;
        }
        
        private static void ReleaseAll()
        {
            foreach (var source in AudioCache.Values.ToArray())
            {
                source?.Dispose();
            }
            AudioCache.Clear();
            // Clear any handles for sources that were not registered in AudioCache (e.g. PlayDuration)
            AudioHandleRegistry.Clear();
        }

        /// <summary>
        /// Play audioClip from address at point
        /// </summary>
        /// <param name="audioClipAddress"></param>
        /// <param name="position"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        public static void PlayClipAtPoint(string audioClipAddress, Vector3 position, float volume = 1f, float spatialBlend = 1f, float minDistance = 10f)
        {
            PlayClipAtPointAsync(audioClipAddress, position, volume, spatialBlend, minDistance).Forget();
        }

        /// <summary>
        /// Play audioClip at point, optimized version of <see cref="AudioSource.PlayClipAtPoint(AudioClip, Vector3,float)"/> 
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="position"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        public static AudioSourceHandle PlayClipAtPoint(AudioClip audioClip, Vector3 position, float volume = 1f, float spatialBlend = 1f, float minDistance = 10f)
        {
            var audioObject = PooledAudioSource.Get(new AudioKey(audioClip.GetInstanceID()), GetRoot(), volume, spatialBlend, minDistance);
            return PlayClipAtPoint(audioClip, audioObject, position);
        }
        
        /// <summary>
        /// Play audioClip async from address at point
        /// </summary>
        /// <param name="audioClipAddress"></param>
        /// <param name="position"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        /// <returns></returns>
        public static async UniTask<AudioSourceHandle> PlayClipAtPointAsync(string audioClipAddress, Vector3 position, float volume = 1f, 
            float spatialBlend = 1f, float minDistance = 10f)
        {
            var audioObject = PooledAudioSource.Get(new AudioKey(audioClipAddress), GetRoot(), volume, spatialBlend, minDistance);
            var handle = ResourceSystem.LoadAssetAsync<AudioClip>(audioClipAddress).AddTo(audioObject);
            var audioClip = await handle;
            return PlayClipAtPoint(audioClip, audioObject, position);
        }
        
        /// <summary>
        /// Play loop audioClip from address at point, stop it using <see cref="StopAudio(string)"/>. 
        /// </summary>
        /// <param name="audioClipAddress"></param>
        /// <param name="position"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        public static void PlayLoopClipAtPoint(string audioClipAddress, Vector3 position, float volume = 1f, float spatialBlend = 1f, float minDistance = 10f)
        {
            PlayLoopClipAtPointAsync(audioClipAddress, position, volume, spatialBlend, minDistance).Forget();
        }

        /// <summary>
        /// Play loop audioClip at point, stop it using <see cref="StopAudio(UnityEngine.AudioClip)"/> or <see cref="AudioSourceHandle.Stop"/>. 
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="position"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        public static AudioSourceHandle PlayLoopClipAtPoint(AudioClip audioClip, Vector3 position, float volume = 1f, float spatialBlend = 1f, float minDistance = 10f)
        {
            var audioObject = PooledAudioSource.Get(new AudioKey(audioClip.GetInstanceID()), GetRoot(), volume, spatialBlend, minDistance);
            return PlayLoopClipAtPoint(audioClip, audioObject, position);
        }
        
        /// <summary>
        /// Play loop audioClip from address at point async, stop it using <see cref="StopAudio(string)"/>. 
        /// </summary>
        /// <param name="audioClipAddress"></param>
        /// <param name="position"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        /// <returns></returns>
        public static async UniTask<AudioSourceHandle> PlayLoopClipAtPointAsync(string audioClipAddress, Vector3 position, float volume = 1f, 
            float spatialBlend = 1f, float minDistance = 10f)
        {
            var audioObject = PooledAudioSource.Get(new AudioKey(audioClipAddress), GetRoot(), volume, spatialBlend, minDistance);
            var handle = ResourceSystem.LoadAssetAsync<AudioClip>(audioClipAddress).AddTo(audioObject);
            var audioClip = await handle;
            return PlayLoopClipAtPoint(audioClip, audioObject, position);
        }

        /// <summary>
        /// Schedule audioClip from address at point, stop it using <see cref="StopAudio(string)"/>. 
        /// </summary>
        /// <param name="audioClipAddress"></param>
        /// <param name="position"></param>
        /// <param name="scheduleTime"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        public static void ScheduleClipAtPoint(string audioClipAddress, Vector3 position, float scheduleTime, float volume = 1f, 
            float spatialBlend = 1f, float minDistance = 10f)
        {
            ScheduleClipAtPointAsync(audioClipAddress, position, scheduleTime, volume, spatialBlend, minDistance).Forget();
        }

        /// <summary>
        /// Schedule audioClip at point, stop it using <see cref="StopAudio(UnityEngine.AudioClip)"/> or <see cref="AudioSourceHandle.Stop"/>. 
        /// </summary>
        /// <param name="audioClip"></param>
        /// <param name="position"></param>
        /// <param name="scheduleTime"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        public static AudioSourceHandle ScheduleClipAtPoint(AudioClip audioClip, Vector3 position, float scheduleTime, float volume = 1f, 
            float spatialBlend = 1f, float minDistance = 10f)
        {
            var audioObject = PooledAudioSource.Get(new AudioKey(audioClip.name), GetRoot(), volume, spatialBlend, minDistance);
            return ScheduleClipAtPoint(audioClip, audioObject, position, scheduleTime);
        }
        
        /// <summary>
        /// Schedule audioClip async from address at point, stop it using <see cref="StopAudio(string)"/>. 
        /// </summary>
        /// <param name="audioClipAddress"></param>
        /// <param name="position"></param>
        /// <param name="scheduleTime"></param>
        /// <param name="volume"></param>
        /// <param name="spatialBlend"></param>
        /// <param name="minDistance"></param>
        /// <returns></returns>
        public static async UniTask<AudioSourceHandle> ScheduleClipAtPointAsync(string audioClipAddress, Vector3 position, float scheduleTime, float volume, 
            float spatialBlend = 1f, float minDistance = 10f)
        {
            var audioObject = PooledAudioSource.Get(new AudioKey(audioClipAddress), GetRoot(), volume, spatialBlend, minDistance);
            var handle = ResourceSystem.LoadAssetAsync<AudioClip>(audioClipAddress).AddTo(audioObject);
            var audioClip = await handle;
            return ScheduleClipAtPoint(audioClip, audioObject, position, scheduleTime);
        }

        private static float GetDuration(AudioClip clip)
        {
            return clip.length * (Time.timeScale < 0.01f ? 0.01f : Time.timeScale);
        }
        
        private static AudioSourceHandle PlayClipAtPoint(AudioClip clip, PooledAudioSource audioObject, Vector3 position)
        {
            audioObject.GameObject.transform.position = position;
            audioObject.Component.clip = clip;
            audioObject.PlayDuration(GetDuration(clip));
            return AudioHandleRegistry.Alloc(audioObject);
        }
                
        private static AudioSourceHandle PlayLoopClipAtPoint(AudioClip clip, PooledAudioSource audioObject, Vector3 position)
        {
            audioObject.GameObject.transform.position = position;
            audioObject.Component.clip = clip;
            audioObject.PlayLoop();
            return AudioHandleRegistry.Alloc(audioObject);
        }
        
        private static AudioSourceHandle ScheduleClipAtPoint(AudioClip clip, PooledAudioSource audioObject, Vector3 position, float scheduleTime)
        {
            audioObject.GameObject.transform.position = position;
            audioObject.Component.clip = clip;
            scheduleTime += GetDuration(clip);
            audioObject.SchedulePlay(scheduleTime);
            return AudioHandleRegistry.Alloc(audioObject);
        }
    }
    
    public sealed class PooledAudioSource : PooledComponent<PooledAudioSource, AudioSource>
    {
        private AudioSystem.AudioKey _audioKey;

        /// <summary>
        /// The handle that identifies this source in the AudioHandleRegistry.
        /// Written by AudioHandleRegistry.Alloc; read by OnDispose to free the slot.
        /// </summary>
        internal AudioSourceHandle AudioHandle;
        
        internal static PooledAudioSource Get(AudioSystem.AudioKey key, Transform parent, float volume, float spatialBlend, float minDistance)
        {
            var pooledAudioSource = Get(parent);
            pooledAudioSource._audioKey = key;
            pooledAudioSource.Component.loop = false;
            pooledAudioSource.Component.spatialBlend = 1f;
            pooledAudioSource.Component.volume = volume;
            pooledAudioSource.Component.spatialBlend = spatialBlend;
            pooledAudioSource.Component.minDistance = minDistance;
            return pooledAudioSource;
        }
        
        internal unsafe void SchedulePlay(float scheduleTime)
        {
            var handle = Scheduler.DelayUnsafe(scheduleTime, new SchedulerUnsafeBinding(this, &Play_Imp), isLooped: true);
            Add(handle);
            AudioSystem.Register(_audioKey, this);
            Component.Play();
        }
                    
        public void PlayLoop()
        {
            AudioSystem.Register(_audioKey, this);
            Component.loop = true;
            Component.Play();
        }

        public void PlayDuration(float duration)
        {
            Destroy(duration);
            Component.Play();
        }
        
        private static void Play_Imp(object @object)
        {
            ((PooledAudioSource)@object).Component.Play();
        }

        protected override void OnDispose()
        {
            Component.Stop();
            Component.clip = null;
            AudioSystem.Unregister(_audioKey, this);
            AudioHandleRegistry.Free(AudioHandle);
            AudioHandle = default;
            base.OnDispose();
        }
    }
}
