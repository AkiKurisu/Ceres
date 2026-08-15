using System;
using System.IO;
using System.Threading;
using Ceres;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine.Networking;

namespace Ceres.Gameplay.Mod
{
    public class ModDownloader : IDisposable, IProgress<float>
    {
        private readonly Subject<float> _onProgress = new();

        private readonly Subject<Result> _onComplete = new();

        public Observable<float> OnProgress => _onProgress;

        public Observable<Result> OnComplete => _onComplete;

        private readonly CancellationToken _cancellationToken;
        
        public ModDownloader(CancellationToken cancellationToken = default)
        {
            _cancellationToken = cancellationToken;
        }
        
        public async UniTask DownloadAsync(string url, string downloadPath)
        {
            Result result = new();
            using UnityWebRequest request = UnityWebRequest.Get(new Uri(url).AbsoluteUri);
            request.downloadHandler = new DownloadHandlerFile(downloadPath);
            await request.SendWebRequest().ToUniTask(this, cancellationToken: _cancellationToken);
            string unzipFolder = Path.GetDirectoryName(downloadPath);
            if (!ZipWrapper.UnzipFile(downloadPath, unzipFolder))
            {
                result.ErrorInfo = $"Can't unzip mod: {downloadPath}!";
                File.Delete(downloadPath);
                result.DownloadPath = downloadPath[..4];
                _onComplete.OnNext(result);
                return;
            }
            result.Succeed = true;
            _onComplete.OnNext(result);
        }
        
        void IProgress<float>.Report(float value)
        {
            _onProgress.OnNext(value);
        }
        
        public void Dispose()
        {
            _onProgress.Dispose();
            _onComplete.Dispose();
        }
        
        public struct Result
        {
            public string ErrorInfo;
            
            public bool Succeed;
            
            public string DownloadPath;
        }
    }
}
