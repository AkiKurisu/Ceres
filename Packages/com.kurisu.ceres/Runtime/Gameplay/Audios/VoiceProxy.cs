using System;
using System.Collections.Generic;
using Ceres.Collections;
using Ceres.Resource;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace Ceres.Gameplay.Audios
{
    /// <summary>
    /// Command structure of character voice
    /// </summary>
    public class VoiceCommand : IComparable<VoiceCommand>, IDisposable
    {
        private static readonly ObjectPool<VoiceCommand> Pool = new(() => new VoiceCommand(), command => command.Reset());
        
        public int Priority;
        
        public float Volume;
        
        public string Name;
        
        public SoftAssetReference<AudioClip> Reference;
        
        public AudioClip AudioClip;
        
        public bool IsLoaded => AudioClip != null;
        
        public bool IsLoading { get; private set; }
        
        // Should use Get() allocated from pool
        private VoiceCommand()
        {

        }

        private void Reset()
        {
            Name = string.Empty;
            AudioClip = null;
            Reference = new SoftAssetReference<AudioClip>();
            IsLoading = false;
        }
        
        public static VoiceCommand Get(string name, SoftAssetReference<AudioClip> audioClip, int priority = -1, float volume = 0.5f)
        {
            var cmd = Pool.Get();
            cmd.Name = name;
            cmd.Reference = audioClip;
            cmd.Priority = priority;
            cmd.Volume = volume;
            return cmd;
        }
        
        public static VoiceCommand Get(string name, AudioClip audioClip, int priority = -1, float volume = 0.5f)
        {
            var cmd = Pool.Get();
            cmd.Name = name;
            cmd.AudioClip = audioClip;
            cmd.Priority = priority;
            cmd.Volume = volume;
            return cmd;
        }
        
        public async UniTask LoadAsync(ResourceCache<AudioClip> cache)
        {
            if (IsLoaded) return;
            if (IsLoading) return;
            IsLoading = true;
            try
            {
                AudioClip = await cache.LoadAssetAsync(Reference.Address);
            }
            finally
            {
                IsLoading = false;
            }
        }

        public int CompareTo(VoiceCommand other)
        {
            return Priority.CompareTo(other.Priority);
        }

        public void Dispose()
        {
            Pool.Release(this);
            // TODO: Cancel asset loading
        }
    }
    
    /// <summary>
    /// Class for managing character voice playing order
    /// </summary>
    public class VoiceProxy : IDisposable
    {
        public enum VoiceStatus
        {
            Pending,
            Playing,
            Stopped
        }

        private readonly PriorityQueue<VoiceCommand> _commandQueue = new();
        
        private VoiceCommand _pendingCommand;
        
        private VoiceCommand _playingCommand;
        
        private readonly ResourceCache<AudioClip> _voiceCache = new();
        public VoiceStatus Status { get; private set; } = VoiceStatus.Stopped;
        
        public bool IsPlaying => Status == VoiceStatus.Playing;
        
        public bool IsStopped => Status == VoiceStatus.Stopped;
        
        public bool IsPending => Status == VoiceStatus.Pending;
        
        private readonly HashSet<string> _voiceStates = new();
        
        private readonly AudioSource _audioSource;
        
        private readonly int _maxCommandNum;
        
        public VoiceProxy(AudioSource audioSource, int maxCommandNum = -1)
        {
            _audioSource = audioSource;
            _maxCommandNum = maxCommandNum;
        }
        
        /// <summary>
        /// Enqueue a new voice command
        /// </summary>
        /// <param name="command"></param>
        public void EnqueueCommand(VoiceCommand command)
        {
            if (_maxCommandNum != -1 && Count() >= _maxCommandNum)
            {
                command.Dispose();
                return;
            }
            if (_voiceStates.Contains(command.Name))
            {
                command.Dispose();
                return;
            }
            // Pre-initialize if use soft asset reference
            command.LoadAsync(_voiceCache).Forget();
            _commandQueue.Enqueue(command);
            _voiceStates.Add(command.Name);
        }
        
        /// <summary>
        /// Get command left count
        /// </summary>
        /// <returns></returns>
        public int Count()
        {
            return _commandQueue.Count();
        }
        
        /// <summary>
        /// Tick command buffer
        /// </summary>
        public void Tick()
        {
            if (IsPlaying)
            {
                bool needStopCommand = !_audioSource.isPlaying;

                if (_commandQueue.Count() != 0)
                {
                    var command = _commandQueue.Peek();
                    needStopCommand |= command.Priority > _playingCommand.Priority;
                }

                if (needStopCommand)
                {
                    StopPlayingCommand();
                }
            }

            if (IsPending && _pendingCommand != null)
            {
                if (!_pendingCommand.IsLoaded) return;
                // Wait asset loaded
                ConsumeVoiceCommand(_pendingCommand);
                _pendingCommand = null;
            }

            if (!IsStopped) return;

            if (_commandQueue.Count() != 0)
            {
                var command = _commandQueue.Dequeue();
                if (!command.IsLoaded)
                {
                    Status = VoiceStatus.Pending;
                    _pendingCommand = command;
                    return;
                }

                ConsumeVoiceCommand(command);
            }
        }
        
        /// <summary>
        /// Clear all commands and release memeory if possible
        /// </summary>
        public void Clear()
        {
            Status = VoiceStatus.Stopped;
            _playingCommand?.Dispose();
            _playingCommand = null;
            _pendingCommand?.Dispose();
            _pendingCommand = null;
            _commandQueue.Clear();
            _voiceCache.ReleaseAssetsAndUpdateVersion();
        }
        
        private void StopPlayingCommand()
        {
            Status = VoiceStatus.Stopped;
            _voiceStates.Remove(_playingCommand.Name);
            _playingCommand.Dispose();
            _playingCommand = null;
        }
        
        private void ConsumeVoiceCommand(VoiceCommand command)
        {
            _playingCommand = command;
            _audioSource.clip = command.AudioClip;
            _audioSource.volume = command.Volume;
            _audioSource.Play();
            Status = VoiceStatus.Playing;
        }
        
        public void Dispose()
        {
            _playingCommand?.Dispose();
            _playingCommand = null;
            _pendingCommand?.Dispose();
            _pendingCommand = null;
            _commandQueue.Clear();
            _voiceCache.Dispose();
        }
    }
}
